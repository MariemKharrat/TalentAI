using System.Net.Http.Headers;
using System.Text.Json;
using CareerApp.Core.Models;
using CareerApp.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace CareerApp.Infrastructure.Services;

public sealed class ContentUnderstandingCvParser
{
    private readonly AzureAIOptions _options;
    private readonly HttpClient _httpClient;

    public ContentUnderstandingCvParser(IOptions<AzureAIOptions> options, HttpClient httpClient)
    {
        _options = options.Value;
        _httpClient = httpClient;
    }

    public async Task<Candidate> ParseCvAsync(Stream document, string fileName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        using var memoryStream = new MemoryStream();
        await document.CopyToAsync(memoryStream, cancellationToken).ConfigureAwait(false);
        var documentBytes = memoryStream.ToArray();

        var extractedData = await AnalyzeWithContentUnderstandingAsync(documentBytes, fileName, cancellationToken).ConfigureAwait(false);

        return MapToCandidate(extractedData, fileName);
    }

    private async Task<ContentUnderstandingResult> AnalyzeWithContentUnderstandingAsync(byte[] documentBytes, string fileName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ContentUnderstandingEndpoint))
        {
            return new ContentUnderstandingResult
            {
                FullName = Path.GetFileNameWithoutExtension(fileName).Replace('_', ' ').Replace('-', ' '),
                Summary = "Content Understanding endpoint not configured. Please set up Azure AI Content Understanding.",
                Skills = [],
                Experience = [],
                Education = []
            };
        }

        var endpoint = _options.ContentUnderstandingEndpoint.TrimEnd('/');
        var analyzerId = string.IsNullOrWhiteSpace(_options.ContentUnderstandingAnalyzerId)
            ? "cv-analyzer"
            : _options.ContentUnderstandingAnalyzerId;

        var analyzeUrl = $"{endpoint}/contentunderstanding/analyzers/{analyzerId}:analyze?api-version=2025-05-01-preview";

        using var content = new ByteArrayContent(documentBytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(GetContentType(fileName));

        using var request = new HttpRequestMessage(HttpMethod.Post, analyzeUrl);
        request.Content = content;
        request.Headers.Add("Ocp-Apim-Subscription-Key", _options.ContentUnderstandingKey);

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException($"Content Understanding API failed ({response.StatusCode}): {errorBody}");
        }

        if (response.Headers.TryGetValues("Operation-Location", out var operationLocations))
        {
            var operationUrl = operationLocations.First();
            return await PollForResultAsync(operationUrl, cancellationToken).ConfigureAwait(false);
        }

        var resultJson = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return ParseResult(resultJson);
    }

    private async Task<ContentUnderstandingResult> PollForResultAsync(string operationUrl, CancellationToken cancellationToken)
    {
        const int maxAttempts = 30;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);

            using var pollRequest = new HttpRequestMessage(HttpMethod.Get, operationUrl);
            pollRequest.Headers.Add("Ocp-Apim-Subscription-Key", _options.ContentUnderstandingKey);

            using var pollResponse = await _httpClient.SendAsync(pollRequest, cancellationToken).ConfigureAwait(false);
            var pollJson = await pollResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!pollResponse.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Content Understanding polling failed ({pollResponse.StatusCode}): {pollJson}");
            }

            using var doc = JsonDocument.Parse(pollJson);
            var status = doc.RootElement.GetProperty("status").GetString();

            if (string.Equals(status, "succeeded", StringComparison.OrdinalIgnoreCase))
            {
                return ParseResult(pollJson);
            }

            if (string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Content Understanding analysis failed: {pollJson}");
            }
        }

        throw new TimeoutException("Content Understanding analysis timed out after 60 seconds.");
    }

    private static ContentUnderstandingResult ParseResult(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var result = new ContentUnderstandingResult();

        if (root.TryGetProperty("result", out var resultElement))
        {
            root = resultElement;
        }

        if (root.TryGetProperty("contents", out var contents) && contents.ValueKind == JsonValueKind.Array && contents.GetArrayLength() > 0)
        {
            var firstContent = contents.EnumerateArray().First();
            if (firstContent.TryGetProperty("fields", out var fields))
            {
                result.FullName = GetFieldValue(fields, "fullName") ?? GetFieldValue(fields, "name") ?? string.Empty;
                result.Email = GetFieldValue(fields, "email") ?? string.Empty;
                result.Phone = GetFieldValue(fields, "phone") ?? GetFieldValue(fields, "phoneNumber") ?? string.Empty;
                result.Summary = GetFieldValue(fields, "summary") ?? GetFieldValue(fields, "professionalSummary") ?? string.Empty;

                if (fields.TryGetProperty("skills", out var skillsField))
                {
                    result.Skills = ExtractArrayField(skillsField);
                }

                if (fields.TryGetProperty("experience", out var experienceField))
                {
                    result.Experience = ExtractExperienceField(experienceField);
                }

                if (fields.TryGetProperty("education", out var educationField))
                {
                    result.Education = ExtractEducationField(educationField);
                }
            }
        }

        if (string.IsNullOrWhiteSpace(result.FullName) && root.TryGetProperty("content", out var contentText))
        {
            var text = contentText.GetString() ?? string.Empty;
            var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            result.FullName = lines.FirstOrDefault() ?? string.Empty;
            result.Summary = string.Join(' ', lines.Take(5));
        }

        return result;
    }

    private static string? GetFieldValue(JsonElement fields, string fieldName)
    {
        if (!fields.TryGetProperty(fieldName, out var field))
        {
            return null;
        }

        if (field.TryGetProperty("valueString", out var valueString))
        {
            return valueString.GetString();
        }

        if (field.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        return field.ValueKind == JsonValueKind.String ? field.GetString() : null;
    }

    private static List<string> ExtractArrayField(JsonElement field)
    {
        var items = new List<string>();

        if (field.TryGetProperty("valueArray", out var array))
        {
            foreach (var item in array.EnumerateArray())
            {
                var value = item.TryGetProperty("valueString", out var valueString)
                    ? valueString.GetString()
                    : item.ValueKind == JsonValueKind.String
                        ? item.GetString()
                        : null;

                if (!string.IsNullOrWhiteSpace(value))
                {
                    items.Add(value);
                }
            }
        }
        else if (field.TryGetProperty("value", out var valueStr) && valueStr.ValueKind == JsonValueKind.String)
        {
            var csv = valueStr.GetString() ?? string.Empty;
            items.AddRange(csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        return items;
    }

    private static List<ContentUnderstandingExperience> ExtractExperienceField(JsonElement field)
    {
        var experiences = new List<ContentUnderstandingExperience>();

        if (!field.TryGetProperty("valueArray", out var array))
        {
            return experiences;
        }

        foreach (var item in array.EnumerateArray())
        {
            var experienceObject = item.TryGetProperty("valueObject", out var valueObject) ? valueObject : item;
            if (experienceObject.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            experiences.Add(new ContentUnderstandingExperience
            {
                Company = GetFieldValue(experienceObject, "company") ?? GetFieldValue(experienceObject, "organization") ?? string.Empty,
                Title = GetFieldValue(experienceObject, "title") ?? GetFieldValue(experienceObject, "role") ?? string.Empty,
                Description = GetFieldValue(experienceObject, "description") ?? string.Empty
            });
        }

        return experiences;
    }

    private static List<ContentUnderstandingEducation> ExtractEducationField(JsonElement field)
    {
        var educations = new List<ContentUnderstandingEducation>();

        if (!field.TryGetProperty("valueArray", out var array))
        {
            return educations;
        }

        foreach (var item in array.EnumerateArray())
        {
            var educationObject = item.TryGetProperty("valueObject", out var valueObject) ? valueObject : item;
            if (educationObject.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            educations.Add(new ContentUnderstandingEducation
            {
                Institution = GetFieldValue(educationObject, "institution") ?? GetFieldValue(educationObject, "school") ?? string.Empty,
                Degree = GetFieldValue(educationObject, "degree") ?? string.Empty,
                FieldOfStudy = GetFieldValue(educationObject, "fieldOfStudy") ?? GetFieldValue(educationObject, "major") ?? string.Empty
            });
        }

        return educations;
    }

    private static string GetContentType(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".pdf" => "application/pdf",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".doc" => "application/msword",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            _ => "application/octet-stream"
        };
    }

    private static Candidate MapToCandidate(ContentUnderstandingResult result, string fileName)
    {
        return new Candidate
        {
            Id = Guid.NewGuid(),
            FullName = result.FullName,
            Email = result.Email,
            Phone = result.Phone,
            Skills = string.Join(", ", result.Skills),
            Summary = result.Summary,
            CvFileName = fileName,
            CvContent = $"[Parsed via Content Understanding] {result.Summary}",
            ParsingMethod = nameof(CvParsingMethod.ContentUnderstanding),
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}

internal sealed class ContentUnderstandingResult
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public List<string> Skills { get; set; } = [];
    public List<ContentUnderstandingExperience> Experience { get; set; } = [];
    public List<ContentUnderstandingEducation> Education { get; set; } = [];
}

internal sealed class ContentUnderstandingExperience
{
    public string Company { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

internal sealed class ContentUnderstandingEducation
{
    public string Institution { get; set; } = string.Empty;
    public string Degree { get; set; } = string.Empty;
    public string FieldOfStudy { get; set; } = string.Empty;
}

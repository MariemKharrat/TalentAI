using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Azure.AI.OpenAI;
using CareerApp.Core.Models;
using CareerApp.Infrastructure.Configuration;
using DocumentFormat.OpenXml.Packaging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

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

        ContentUnderstandingResult extractedData;
        try
        {
            extractedData = await AnalyzeWithContentUnderstandingAsync(documentBytes, fileName, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Fallback: extract text from document, then use OpenAI for structured parsing
            var text = ExtractTextFromDocument(documentBytes, fileName);
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new InvalidDataException($"Unable to extract text from '{fileName}'. Supported formats: PDF, DOCX, DOC, TXT.");
            }

            extractedData = await ParseWithOpenAIAsync(text, fileName, cancellationToken).ConfigureAwait(false);
        }

        return MapToCandidate(extractedData, fileName);
    }

    /// <summary>
    /// Extracts plain text from DOCX, DOC, PDF, or text files.
    /// </summary>
    private static string ExtractTextFromDocument(byte[] documentBytes, string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        return extension switch
        {
            ".docx" => ExtractTextFromDocx(documentBytes),
            ".doc" => ExtractTextFromDoc(documentBytes),
            ".pdf" => ExtractTextFromPdf(documentBytes),
            _ => Encoding.UTF8.GetString(documentBytes) // txt, md, etc.
        };
    }

    private static string ExtractTextFromDocx(byte[] documentBytes)
    {
        using var stream = new MemoryStream(documentBytes);
        using var wordDoc = WordprocessingDocument.Open(stream, false);
        var body = wordDoc.MainDocumentPart?.Document?.Body;
        if (body is null) return string.Empty;

        var sb = new StringBuilder();
        foreach (var paragraph in body.Descendants<DocumentFormat.OpenXml.Wordprocessing.Paragraph>())
        {
            var text = paragraph.InnerText;
            if (!string.IsNullOrWhiteSpace(text))
            {
                sb.AppendLine(text);
            }
        }
        return sb.ToString();
    }

    private static string ExtractTextFromDoc(byte[] documentBytes)
    {
        // .doc is a legacy binary format — extract readable ASCII/Unicode text
        var sb = new StringBuilder();
        var text = Encoding.UTF8.GetString(documentBytes);
        
        // Filter out binary garbage, keep readable text segments
        foreach (var line in text.Split('\n'))
        {
            var cleaned = new string(line.Where(c => !char.IsControl(c) || c == '\t').ToArray()).Trim();
            if (cleaned.Length > 3 && cleaned.Count(char.IsLetter) > cleaned.Length / 2)
            {
                sb.AppendLine(cleaned);
            }
        }

        // If UTF8 extraction yielded little, try Unicode (UTF-16)
        if (sb.Length < 50 && documentBytes.Length > 100)
        {
            sb.Clear();
            text = Encoding.Unicode.GetString(documentBytes);
            foreach (var line in text.Split('\n'))
            {
                var cleaned = new string(line.Where(c => !char.IsControl(c) || c == '\t').ToArray()).Trim();
                if (cleaned.Length > 3 && cleaned.Count(char.IsLetter) > cleaned.Length / 2)
                {
                    sb.AppendLine(cleaned);
                }
            }
        }

        return sb.ToString();
    }

    private static string ExtractTextFromPdf(byte[] documentBytes)
    {
        // For PDF, return a marker — the Document Intelligence path handles PDFs better
        // This fallback just extracts embedded text strings
        var text = Encoding.UTF8.GetString(documentBytes);
        var sb = new StringBuilder();
        
        // Extract text between BT and ET operators (basic PDF text extraction)
        var matches = Regex.Matches(text, @"\(([^)]+)\)", RegexOptions.Compiled);
        foreach (Match match in matches)
        {
            var segment = match.Groups[1].Value;
            if (segment.Length > 2 && segment.Any(char.IsLetter))
            {
                sb.Append(segment).Append(' ');
            }
        }

        return sb.Length > 50 ? sb.ToString() : text;
    }

    /// <summary>
    /// Uses Azure OpenAI to extract structured CV data from plain text.
    /// </summary>
    private async Task<ContentUnderstandingResult> ParseWithOpenAIAsync(string cvText, string fileName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.OpenAIEndpoint) || 
            _options.OpenAIEndpoint.Contains("your-resource", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(_options.OpenAIKey))
        {
            // No OpenAI available — use regex fallback
            return FallbackRegexParsing(cvText, fileName);
        }

        try
        {
            var endpoint = _options.OpenAIEndpoint.TrimEnd('/');
            // Remove /openai/v1 suffix if present since the SDK adds its own path
            endpoint = Regex.Replace(endpoint, @"/openai(/v\d+)?$", "", RegexOptions.IgnoreCase);
            
            var client = new AzureOpenAIClient(new Uri(endpoint), new System.ClientModel.ApiKeyCredential(_options.OpenAIKey));
            var chatClient = client.GetChatClient(_options.OpenAIDeploymentName ?? "gpt-4o");

            var systemPrompt = @"You are a CV/Resume parser. Extract structured information from the CV text provided.
Return ONLY a valid JSON object with this exact schema (no markdown, no explanation):
{
  ""fullName"": ""string"",
  ""email"": ""string"",
  ""phone"": ""string"",
  ""summary"": ""A 2-3 sentence professional summary"",
  ""skills"": [""skill1"", ""skill2""],
  ""experience"": [{""company"": ""string"", ""title"": ""string"", ""description"": ""brief description""}],
  ""education"": [{""institution"": ""string"", ""degree"": ""string"", ""fieldOfStudy"": ""string""}]
}";

            var trimmedCv = cvText.Length > 8000 ? cvText[..8000] : cvText;

            var response = await chatClient.CompleteChatAsync(
                [
                    new SystemChatMessage(systemPrompt),
                    new UserChatMessage($"Parse this CV:\n\n{trimmedCv}")
                ],
                new ChatCompletionOptions { Temperature = 0.1f },
                cancellationToken).ConfigureAwait(false);

            var responseText = response.Value.Content[0].Text ?? "{}";
            // Strip markdown code fences if present
            responseText = Regex.Replace(responseText, @"^```(?:json)?\s*", "", RegexOptions.Multiline);
            responseText = Regex.Replace(responseText, @"\s*```$", "", RegexOptions.Multiline);

            return ParseOpenAIResponse(responseText);
        }
        catch
        {
            return FallbackRegexParsing(cvText, fileName);
        }
    }

    private static ContentUnderstandingResult ParseOpenAIResponse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var result = new ContentUnderstandingResult
        {
            FullName = root.TryGetProperty("fullName", out var fn) ? fn.GetString() ?? "" : "",
            Email = root.TryGetProperty("email", out var em) ? em.GetString() ?? "" : "",
            Phone = root.TryGetProperty("phone", out var ph) ? ph.GetString() ?? "" : "",
            Summary = root.TryGetProperty("summary", out var su) ? su.GetString() ?? "" : ""
        };

        if (root.TryGetProperty("skills", out var skills) && skills.ValueKind == JsonValueKind.Array)
        {
            result.Skills = skills.EnumerateArray()
                .Select(s => s.GetString())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s!)
                .ToList();
        }

        if (root.TryGetProperty("experience", out var exp) && exp.ValueKind == JsonValueKind.Array)
        {
            result.Experience = exp.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.Object)
                .Select(e => new ContentUnderstandingExperience
                {
                    Company = e.TryGetProperty("company", out var c) ? c.GetString() ?? "" : "",
                    Title = e.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "",
                    Description = e.TryGetProperty("description", out var d) ? d.GetString() ?? "" : ""
                }).ToList();
        }

        if (root.TryGetProperty("education", out var edu) && edu.ValueKind == JsonValueKind.Array)
        {
            result.Education = edu.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.Object)
                .Select(e => new ContentUnderstandingEducation
                {
                    Institution = e.TryGetProperty("institution", out var i) ? i.GetString() ?? "" : "",
                    Degree = e.TryGetProperty("degree", out var dg) ? dg.GetString() ?? "" : "",
                    FieldOfStudy = e.TryGetProperty("fieldOfStudy", out var f) ? f.GetString() ?? "" : ""
                }).ToList();
        }

        return result;
    }

    private static ContentUnderstandingResult FallbackRegexParsing(string text, string fileName)
    {
        return new ContentUnderstandingResult
        {
            FullName = InferNameFromText(text, fileName),
            Email = ExtractPattern(text, @"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}"),
            Phone = ExtractPattern(text, @"(?:\+?\d[\d().\-\s]{7,}\d)"),
            Summary = text.Length > 500 ? text[..500] : text,
            Skills = ExtractSkillsFromText(text),
            Experience = [],
            Education = []
        };
    }

    private static string InferNameFromText(string text, string fileName)
    {
        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var firstLine = lines.FirstOrDefault(l => !l.Contains('@') && !Regex.IsMatch(l, @"\d{3}"));
        return !string.IsNullOrWhiteSpace(firstLine) && firstLine.Length < 60 
            ? firstLine 
            : Path.GetFileNameWithoutExtension(fileName).Replace('_', ' ').Replace('-', ' ');
    }

    private static string ExtractPattern(string text, string pattern)
    {
        var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
        return match.Success ? match.Value : string.Empty;
    }

    private static List<string> ExtractSkillsFromText(string text)
    {
        string[] knownSkills = ["C#", ".NET", "ASP.NET", "Azure", "SQL", "Python", "Java", "JavaScript", "TypeScript",
            "React", "Angular", "Node.js", "Docker", "Kubernetes", "Terraform", "Power BI", "AI", "Machine Learning",
            "AWS", "GCP", "MongoDB", "PostgreSQL", "Redis", "Git", "CI/CD", "Agile", "Scrum", "REST", "GraphQL",
            "HTML", "CSS", "Sass", "Vue.js", "Next.js", "Spring Boot", "Django", "Flask", "Go", "Rust", "Swift"];
        return knownSkills.Where(s => text.Contains(s, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private async Task<ContentUnderstandingResult> AnalyzeWithContentUnderstandingAsync(byte[] documentBytes, string fileName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ContentUnderstandingEndpoint)
            || _options.ContentUnderstandingEndpoint.Contains("your-resource", StringComparison.OrdinalIgnoreCase))
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

using System.Text;
using System.Text.RegularExpressions;
using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using Azure.AI.OpenAI;
using Azure.Identity;
using CareerApp.Core.Interfaces;
using CareerApp.Core.Models;
using CareerApp.Infrastructure.Configuration;
using DocumentFormat.OpenXml.Packaging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace CareerApp.Infrastructure.Services;

public class CvParsingService : ICvParsingService
{
    private static readonly string[] KnownSkills =
    [
        "c#", "dotnet", ".net", "asp.net", "azure", "sql", "python", "java", "javascript", "typescript",
        "react", "angular", "node.js", "docker", "kubernetes", "terraform", "power bi", "ai", "machine learning",
        "aws", "gcp", "mongodb", "postgresql", "redis", "git", "ci/cd", "agile", "html", "css", "vue.js"
    ];

    private readonly AzureAIOptions _options;
    private readonly ContentUnderstandingCvParser _contentUnderstandingParser;

    public CvParsingService(IOptions<AzureAIOptions> options, ContentUnderstandingCvParser contentUnderstandingParser)
    {
        _options = options.Value;
        _contentUnderstandingParser = contentUnderstandingParser;
    }

    public async Task<Candidate> ParseCvAsync(Stream document, string fileName, CvParsingMethod method = CvParsingMethod.ContentUnderstanding, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("A file name is required.", nameof(fileName));
        }

        if (method == CvParsingMethod.ContentUnderstanding)
        {
            return await _contentUnderstandingParser.ParseCvAsync(document, fileName, cancellationToken).ConfigureAwait(false);
        }

        await using var workingCopy = new MemoryStream();
        await document.CopyToAsync(workingCopy, cancellationToken).ConfigureAwait(false);
        workingCopy.Position = 0;

        var extractedText = await ExtractTextWithDocumentIntelligenceAsync(workingCopy, fileName, cancellationToken).ConfigureAwait(false);
        
        // Use OpenAI for structured extraction (experience, education, etc.)
        var candidate = await ParseTextWithOpenAIAsync(extractedText, fileName, cancellationToken).ConfigureAwait(false);
        candidate.ParsingMethod = nameof(CvParsingMethod.DocumentIntelligence);
        candidate.CvContent = extractedText;
        return candidate;
    }

    private async Task<Candidate> ParseTextWithOpenAIAsync(string extractedText, string fileName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.OpenAIEndpoint) ||
            _options.OpenAIEndpoint.Contains("your-resource", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(_options.OpenAIKey))
        {
            return MapCandidateRegex(extractedText, fileName);
        }

        try
        {
            var endpoint = _options.OpenAIEndpoint.TrimEnd('/');
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
  ""experience"": [{""company"": ""string"", ""title"": ""string"", ""startDate"": ""YYYY-MM"", ""endDate"": ""YYYY-MM or null"", ""description"": ""brief description""}],
  ""education"": [{""institution"": ""string"", ""degree"": ""string"", ""fieldOfStudy"": ""string"", ""startDate"": ""YYYY"", ""endDate"": ""YYYY or null""}]
}";

            var trimmedCv = extractedText.Length > 8000 ? extractedText[..8000] : extractedText;

            var response = await chatClient.CompleteChatAsync(
                [
                    new SystemChatMessage(systemPrompt),
                    new UserChatMessage($"Parse this CV:\n\n{trimmedCv}")
                ],
                new ChatCompletionOptions { Temperature = 0.1f },
                cancellationToken).ConfigureAwait(false);

            var responseText = response.Value.Content[0].Text ?? "{}";
            responseText = Regex.Replace(responseText, @"^```(?:json)?\s*", "", RegexOptions.Multiline);
            responseText = Regex.Replace(responseText, @"\s*```$", "", RegexOptions.Multiline);

            return ParseOpenAIResponseToCandidate(responseText, fileName);
        }
        catch
        {
            return MapCandidateRegex(extractedText, fileName);
        }
    }

    private static Candidate ParseOpenAIResponseToCandidate(string json, string fileName)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;

        var candidate = new Candidate
        {
            Id = Guid.NewGuid(),
            FullName = root.TryGetProperty("fullName", out var fn) ? fn.GetString() ?? "" : "",
            Email = root.TryGetProperty("email", out var em) ? em.GetString() ?? "" : "",
            Phone = root.TryGetProperty("phone", out var ph) ? ph.GetString() ?? "" : "",
            Summary = root.TryGetProperty("summary", out var su) ? su.GetString() ?? "" : "",
            CvFileName = fileName,
            CreatedAtUtc = DateTime.UtcNow
        };

        // Skills
        if (root.TryGetProperty("skills", out var skills) && skills.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            candidate.Skills = string.Join(", ", skills.EnumerateArray()
                .Select(s => s.GetString())
                .Where(s => !string.IsNullOrWhiteSpace(s)));
        }

        // Experience
        if (root.TryGetProperty("experience", out var exp) && exp.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            candidate.Experience = exp.EnumerateArray()
                .Where(e => e.ValueKind == System.Text.Json.JsonValueKind.Object)
                .Select(e => new WorkExperience
                {
                    Id = Guid.NewGuid(),
                    Company = e.TryGetProperty("company", out var c) ? c.GetString() ?? "" : "",
                    Title = e.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "",
                    StartDate = e.TryGetProperty("startDate", out var sd) ? sd.GetString() : null,
                    EndDate = e.TryGetProperty("endDate", out var ed) && ed.ValueKind != System.Text.Json.JsonValueKind.Null ? ed.GetString() : null,
                    Description = e.TryGetProperty("description", out var d) ? d.GetString() ?? "" : ""
                }).ToList();
        }

        // Education
        if (root.TryGetProperty("education", out var edu) && edu.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            candidate.Education = edu.EnumerateArray()
                .Where(e => e.ValueKind == System.Text.Json.JsonValueKind.Object)
                .Select(e => new Education
                {
                    Id = Guid.NewGuid(),
                    Institution = e.TryGetProperty("institution", out var i) ? i.GetString() ?? "" : "",
                    Degree = e.TryGetProperty("degree", out var dg) ? dg.GetString() ?? "" : "",
                    FieldOfStudy = e.TryGetProperty("fieldOfStudy", out var f) ? f.GetString() ?? "" : "",
                    StartDate = e.TryGetProperty("startDate", out var sd) ? sd.GetString() : null,
                    EndDate = e.TryGetProperty("endDate", out var ed) && ed.ValueKind != System.Text.Json.JsonValueKind.Null ? ed.GetString() : null
                }).ToList();
        }

        return candidate;
    }

    private async Task<string> ExtractTextWithDocumentIntelligenceAsync(MemoryStream document, string fileName, CancellationToken cancellationToken)
    {
        if (CanUseDocumentIntelligence())
        {
            try
            {
                document.Position = 0;
                var client = CreateDocumentAnalysisClient();
                var operation = await client.AnalyzeDocumentAsync(
                    WaitUntil.Completed,
                    "prebuilt-document",
                    document,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                if (!string.IsNullOrWhiteSpace(operation.Value.Content))
                {
                    return operation.Value.Content;
                }
            }
            catch (RequestFailedException)
            {
                // Fallback to local text extraction
            }
        }

        // Local text extraction for DOCX/DOC/TXT
        document.Position = 0;
        return ExtractTextLocally(document.ToArray(), fileName);
    }

    private static string ExtractTextLocally(byte[] documentBytes, string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".docx" => ExtractTextFromDocx(documentBytes),
            ".doc" => ExtractTextFromDoc(documentBytes),
            _ => Encoding.UTF8.GetString(documentBytes)
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
        var sb = new StringBuilder();
        var rawText = Encoding.UTF8.GetString(documentBytes);
        foreach (var line in rawText.Split('\n'))
        {
            var cleaned = new string(line.Where(c => !char.IsControl(c) || c == '\t').ToArray()).Trim();
            if (cleaned.Length > 3 && cleaned.Count(char.IsLetter) > cleaned.Length / 2)
            {
                sb.AppendLine(cleaned);
            }
        }

        if (sb.Length < 50 && documentBytes.Length > 100)
        {
            sb.Clear();
            rawText = Encoding.Unicode.GetString(documentBytes);
            foreach (var line in rawText.Split('\n'))
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

    private static Candidate MapCandidateRegex(string extractedText, string fileName)
    {
        return new Candidate
        {
            Id = Guid.NewGuid(),
            FullName = InferFullName(extractedText, fileName),
            Email = ExtractFirstMatch(extractedText, "[A-Z0-9._%+-]+@[A-Z0-9.-]+\\.[A-Z]{2,}"),
            Phone = ExtractFirstMatch(extractedText, @"(?:\+?\d[\d().\-\s]{7,}\d)"),
            Skills = ExtractSkills(extractedText),
            Summary = BuildSummary(extractedText),
            CvFileName = fileName,
            CvContent = extractedText,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    private DocumentAnalysisClient CreateDocumentAnalysisClient()
    {
        var endpoint = new Uri(_options.DocumentIntelligenceEndpoint, UriKind.Absolute);
        return string.IsNullOrWhiteSpace(_options.DocumentIntelligenceKey)
            ? new DocumentAnalysisClient(endpoint, new DefaultAzureCredential())
            : new DocumentAnalysisClient(endpoint, new AzureKeyCredential(_options.DocumentIntelligenceKey));
    }

    private bool CanUseDocumentIntelligence()
    {
        return !string.IsNullOrWhiteSpace(_options.DocumentIntelligenceEndpoint)
            && !_options.DocumentIntelligenceEndpoint.Contains("your-resource", StringComparison.OrdinalIgnoreCase);
    }

    private static string InferFullName(string extractedText, string fileName)
    {
        var firstContentLine = extractedText
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(line => !line.Contains('@') && !Regex.IsMatch(line, @"\d{3}"));

        if (!string.IsNullOrWhiteSpace(firstContentLine) && firstContentLine.Length < 60)
        {
            return firstContentLine;
        }

        return Path.GetFileNameWithoutExtension(fileName).Replace('_', ' ').Replace('-', ' ');
    }

    private static string ExtractFirstMatch(string input, string pattern)
    {
        var match = Regex.Match(input, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return match.Success ? match.Value : string.Empty;
    }

    private static string ExtractSkills(string extractedText)
    {
        var skills = KnownSkills
            .Where(skill => extractedText.Contains(skill, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(skill => skill)
            .ToArray();

        return skills.Length == 0 ? string.Empty : string.Join(", ", skills);
    }

    private static string BuildSummary(string extractedText)
    {
        if (string.IsNullOrWhiteSpace(extractedText))
        {
            return "Candidate profile extracted with placeholder parsing logic.";
        }

        var lines = extractedText
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => line.Length > 20)
            .Take(3);

        var summary = string.Join(' ', lines);
        return summary.Length <= 600 ? summary : summary[..600];
    }
}

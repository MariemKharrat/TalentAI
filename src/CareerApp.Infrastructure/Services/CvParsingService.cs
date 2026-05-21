using System.Text;
using System.Text.RegularExpressions;
using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using Azure.Identity;
using CareerApp.Core.Interfaces;
using CareerApp.Core.Models;
using CareerApp.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace CareerApp.Infrastructure.Services;

public class CvParsingService : ICvParsingService
{
    private static readonly string[] KnownSkills =
    [
        "c#", "dotnet", "asp.net", "azure", "sql", "python", "java", "javascript", "typescript",
        "react", "angular", "node", "docker", "kubernetes", "terraform", "power bi", "ai", "machine learning"
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

        var extractedText = await ExtractTextWithDocumentIntelligenceAsync(workingCopy, cancellationToken).ConfigureAwait(false);
        var candidate = MapCandidate(extractedText, fileName);
        candidate.ParsingMethod = nameof(CvParsingMethod.DocumentIntelligence);
        return candidate;
    }

    private async Task<string> ExtractTextWithDocumentIntelligenceAsync(MemoryStream document, CancellationToken cancellationToken)
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
                // Fallback to text extraction.
            }
        }

        document.Position = 0;
        return await ReadAsTextAsync(document).ConfigureAwait(false);
    }

    private static Candidate MapCandidate(string extractedText, string fileName)
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
        return !string.IsNullOrWhiteSpace(_options.DocumentIntelligenceEndpoint);
    }

    private static async Task<string> ReadAsTextAsync(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        return await reader.ReadToEndAsync().ConfigureAwait(false);
    }

    private static string InferFullName(string extractedText, string fileName)
    {
        var firstContentLine = extractedText
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(line => !line.Contains('@') && !Regex.IsMatch(line, @"\d{3}"));

        if (!string.IsNullOrWhiteSpace(firstContentLine))
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
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => line.Length > 20)
            .Take(3);

        var summary = string.Join(' ', lines);
        return summary.Length <= 600 ? summary : summary[..600];
    }
}

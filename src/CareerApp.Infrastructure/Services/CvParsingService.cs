using System.Text;
using System.Text.RegularExpressions;
using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using Azure.Identity;
using CareerApp.Core.Interfaces;
using CareerApp.Core.Models;
using CareerApp.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;

namespace CareerApp.Infrastructure.Services;

public class CvParsingService : ICvParsingService
{
    private static readonly string[] KnownSkills =
    [
        "c#", "dotnet", "asp.net", "azure", "sql", "python", "java", "javascript", "typescript",
        "react", "angular", "node", "docker", "kubernetes", "terraform", "power bi", "ai", "machine learning"
    ];

    private readonly AzureAIOptions _options;

    public CvParsingService(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        _options = LoadOptions(configuration);
    }

    public async Task<Candidate> ParseCvAsync(Stream document, string fileName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("A file name is required.", nameof(fileName));
        }

        await using var workingCopy = new MemoryStream();
        await document.CopyToAsync(workingCopy, cancellationToken).ConfigureAwait(false);
        workingCopy.Position = 0;

        var extractedText = await ExtractTextAsync(workingCopy, cancellationToken).ConfigureAwait(false);
        return MapCandidate(extractedText, fileName);
    }

    private async Task<string> ExtractTextAsync(MemoryStream document, CancellationToken cancellationToken)
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
                // TODO: Wire the final Azure AI Foundry Document Intelligence endpoint and production retry strategy.
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

        // TODO: Replace this heuristic summarization with a structured Azure AI Foundry extraction workflow.
        return summary.Length <= 600 ? summary : summary[..600];
    }

    private static AzureAIOptions LoadOptions(IConfiguration configuration)
    {
        return new AzureAIOptions
        {
            DocumentIntelligenceEndpoint = configuration["AzureAI:DocumentIntelligenceEndpoint"] ?? string.Empty,
            DocumentIntelligenceKey = configuration["AzureAI:DocumentIntelligenceKey"] ?? string.Empty,
            OpenAIEndpoint = configuration["AzureAI:OpenAIEndpoint"] ?? string.Empty,
            OpenAIKey = configuration["AzureAI:OpenAIKey"] ?? string.Empty,
            OpenAIDeploymentName = configuration["AzureAI:OpenAIDeploymentName"] ?? string.Empty
        };
    }
}

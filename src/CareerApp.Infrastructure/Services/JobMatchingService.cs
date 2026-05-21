using System.ClientModel;
using System.Text.RegularExpressions;
using Azure.AI.OpenAI;
using Azure.Identity;
using CareerApp.Core.Interfaces;
using CareerApp.Core.Models;
using CareerApp.Infrastructure.Configuration;
using CareerApp.Infrastructure.Data;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace CareerApp.Infrastructure.Services;

public class JobMatchingService : IJobMatchingService
{
    private static readonly HashSet<string> StopWords =
    [
        "and", "the", "for", "with", "you", "your", "our", "are", "this", "that", "will", "from", "into", "have", "has"
    ];

    private readonly ICandidateRepository _candidateRepository;
    private readonly IJobRepository _jobRepository;
    private readonly CosmosDbService _cosmosDb;
    private readonly AzureAIOptions _options;

    public JobMatchingService(
        ICandidateRepository candidateRepository,
        IJobRepository jobRepository,
        CosmosDbService cosmosDb,
        IConfiguration configuration)
    {
        _candidateRepository = candidateRepository;
        _jobRepository = jobRepository;
        _cosmosDb = cosmosDb;
        _options = LoadOptions(configuration);
    }

    public async Task<MatchResult> MatchCandidateToJobAsync(Guid candidateId, Guid jobId, CancellationToken cancellationToken = default)
    {
        var candidate = await _candidateRepository.GetByIdAsync(candidateId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Candidate '{candidateId}' was not found.");

        var job = await _jobRepository.GetByIdAsync(jobId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Job '{jobId}' was not found.");

        return await BuildAndPersistMatchResultAsync(candidate, job, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyCollection<MatchResult>> MatchCandidateToAllJobsAsync(Guid candidateId, CancellationToken cancellationToken = default)
    {
        var candidate = await _candidateRepository.GetByIdAsync(candidateId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Candidate '{candidateId}' was not found.");

        var jobs = await _jobRepository.GetAllActiveAsync(cancellationToken).ConfigureAwait(false);
        var results = new List<MatchResult>(jobs.Count);

        foreach (var job in jobs)
        {
            results.Add(await BuildAndPersistMatchResultAsync(candidate, job, cancellationToken).ConfigureAwait(false));
        }

        return results.OrderByDescending(result => result.Score).ToList();
    }

    public async Task<IReadOnlyCollection<MatchResult>> MatchJobToCandidatesAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = await _jobRepository.GetByIdAsync(jobId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Job '{jobId}' was not found.");

        var candidates = await _candidateRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var results = new List<MatchResult>(candidates.Count);

        foreach (var candidate in candidates)
        {
            results.Add(await BuildAndPersistMatchResultAsync(candidate, job, cancellationToken).ConfigureAwait(false));
        }

        return results.OrderByDescending(result => result.Score).ToList();
    }

    public Task<IReadOnlyCollection<MatchResult>> GetMatchesForCandidateAsync(Guid candidateId, CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.candidateId = @candidateId ORDER BY c.createdAt DESC")
            .WithParameter("@candidateId", candidateId.ToString());

        return QueryMatchResultsAsync(query, new QueryRequestOptions
        {
            PartitionKey = new PartitionKey(candidateId.ToString())
        }, cancellationToken);
    }

    public Task<IReadOnlyCollection<MatchResult>> GetMatchesForJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.jobId = @jobId ORDER BY c.createdAt DESC")
            .WithParameter("@jobId", jobId.ToString());

        return QueryMatchResultsAsync(query, requestOptions: null, cancellationToken);
    }

    private async Task<MatchResult> BuildAndPersistMatchResultAsync(Candidate candidate, Job job, CancellationToken cancellationToken)
    {
        var prompt = BuildPrompt(candidate, job);

        if (CanUseOpenAi())
        {
            var chatClient = CreateChatClient();

            // TODO: Call the Azure AI Foundry deployment and parse its structured JSON response here.
            _ = chatClient;
            _ = prompt;
            await Task.Yield();
        }

        var evaluation = EvaluateMatch(candidate, job);

        var matchResult = new MatchResult
        {
            Id = Guid.NewGuid(),
            CandidateId = candidate.Id,
            JobId = job.Id,
            Score = evaluation.Score,
            MatchLevel = DetermineMatchLevel(evaluation.Score),
            SkillMatches = evaluation.SkillMatches,
            SkillGaps = evaluation.SkillGaps,
            Explanation = evaluation.Explanation,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _cosmosDb.MatchResults.CreateItemAsync(
            matchResult,
            new PartitionKey(matchResult.CandidateId.ToString()),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return matchResult;
    }

    private async Task<IReadOnlyCollection<MatchResult>> QueryMatchResultsAsync(
        QueryDefinition query,
        QueryRequestOptions? requestOptions,
        CancellationToken cancellationToken)
    {
        var iterator = _cosmosDb.MatchResults.GetItemQueryIterator<MatchResult>(query, requestOptions: requestOptions);
        var results = new List<MatchResult>();

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
            results.AddRange(response);
        }

        return results;
    }

    private static MatchEvaluation EvaluateMatch(Candidate candidate, Job job)
    {
        var candidateTerms = Tokenize($"{candidate.Skills} {candidate.Summary} {candidate.CvContent}");
        var jobTerms = Tokenize($"{job.Title} {job.Department} {job.Description} {job.Requirements}");

        if (jobTerms.Count == 0)
        {
            return new MatchEvaluation(0m, [], [], "The job does not contain enough detail for automated matching.");
        }

        var skillMatches = candidateTerms
            .Intersect(jobTerms, StringComparer.OrdinalIgnoreCase)
            .OrderBy(term => term)
            .Take(12)
            .ToList();

        var skillGaps = jobTerms
            .Except(candidateTerms, StringComparer.OrdinalIgnoreCase)
            .OrderBy(term => term)
            .Take(12)
            .ToList();

        var score = Math.Round(Math.Clamp((decimal)skillMatches.Count / jobTerms.Count * 100m, 0m, 100m), 2);
        var explanation = skillMatches.Count > 0
            ? $"Matched on {skillMatches.Count} key terms: {string.Join(", ", skillMatches)}. Remaining gaps include {string.Join(", ", skillGaps.Take(5))}."
            : "No strong overlap was detected between the candidate profile and job requirements using the placeholder matcher.";

        return new MatchEvaluation(score, skillMatches, skillGaps, explanation);
    }

    private static HashSet<string> Tokenize(string? text)
    {
        return Regex.Matches(text ?? string.Empty, @"[A-Za-z][A-Za-z0-9+#.]{2,}")
            .Select(match => match.Value.Trim().ToLowerInvariant())
            .Where(token => !StopWords.Contains(token))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string BuildPrompt(Candidate candidate, Job job)
    {
        return $"""
        Evaluate the candidate against the job and return JSON with score and explanation.
        Candidate name: {candidate.FullName}
        Candidate skills: {candidate.Skills}
        Candidate summary: {candidate.Summary}
        Candidate CV text: {candidate.CvContent}
        Job title: {job.Title}
        Job department: {job.Department}
        Job description: {job.Description}
        Job requirements: {job.Requirements}
        """;
    }

    private ChatClient CreateChatClient()
    {
        var endpoint = new Uri(_options.OpenAIEndpoint, UriKind.Absolute);
        var client = string.IsNullOrWhiteSpace(_options.OpenAIKey)
            ? new AzureOpenAIClient(endpoint, new DefaultAzureCredential())
            : new AzureOpenAIClient(endpoint, new ApiKeyCredential(_options.OpenAIKey));

        return client.GetChatClient(_options.OpenAIDeploymentName);
    }

    private bool CanUseOpenAi()
    {
        return !string.IsNullOrWhiteSpace(_options.OpenAIEndpoint)
            && !string.IsNullOrWhiteSpace(_options.OpenAIDeploymentName);
    }

    private static MatchLevel DetermineMatchLevel(decimal score)
    {
        return score >= 75m
            ? MatchLevel.High
            : score >= 50m
                ? MatchLevel.Medium
                : MatchLevel.Low;
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

    private sealed record MatchEvaluation(decimal Score, List<string> SkillMatches, List<string> SkillGaps, string Explanation);
}

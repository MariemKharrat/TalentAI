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
        "and", "the", "for", "with", "you", "your", "our", "are", "this", "that", "will", "from", "into", "have", "has",
        "about", "all", "any", "can", "could", "would", "should", "must", "may", "might", "been", "being", "was", "were",
        "not", "but", "also", "more", "most", "some", "such", "than", "then", "them", "they", "their", "there", "these",
        "those", "what", "when", "where", "which", "while", "who", "whom", "how", "its", "each", "every", "both",
        "other", "another", "new", "old", "first", "last", "long", "great", "just", "only", "own", "same", "well",
        "back", "use", "used", "using", "make", "made", "work", "time", "year", "years", "day", "way", "end",
        "part", "full", "type", "set", "get", "good", "best", "need", "take", "come", "know", "see", "look",
        "find", "give", "tell", "think", "say", "help", "show", "try", "ask", "turn", "start", "run", "move",
        "play", "live", "believe", "bring", "happen", "write", "provide", "sit", "stand", "lose", "pay", "meet",
        "include", "continue", "learn", "change", "lead", "understand", "watch", "follow", "stop", "create",
        "speak", "read", "allow", "add", "spend", "grow", "open", "walk", "win", "offer", "remember", "love",
        "consider", "appear", "buy", "wait", "serve", "die", "send", "expect", "build", "stay", "fall", "cut",
        "reach", "kill", "remain", "able", "working", "role", "position", "team", "join", "employer",
        "applicants", "consideration", "color", "compliant", "connect", "disability", "description",
        "descriptions", "equal", "opportunity", "status", "protected", "national", "origin", "religion",
        "race", "gender", "age", "sexual", "orientation", "veteran", "qualified", "receive", "without",
        "regard", "employment", "experience", "looking", "responsible", "required", "preferred"
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

    public async Task DeleteMatchesForCandidateAsync(Guid candidateId, CancellationToken cancellationToken = default)
    {
        var matches = await GetMatchesForCandidateAsync(candidateId, cancellationToken);
        foreach (var match in matches)
        {
            try
            {
                await _cosmosDb.MatchResults.DeleteItemAsync<MatchResult>(
                    match.Id.ToString(),
                    new PartitionKey(match.CandidateId.ToString()),
                    cancellationToken: cancellationToken);
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Already deleted
            }
        }
    }

    public async Task DeleteMatchesForJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var matches = await GetMatchesForJobAsync(jobId, cancellationToken);
        foreach (var match in matches)
        {
            try
            {
                await _cosmosDb.MatchResults.DeleteItemAsync<MatchResult>(
                    match.Id.ToString(),
                    new PartitionKey(match.CandidateId.ToString()),
                    cancellationToken: cancellationToken);
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Already deleted
            }
        }
    }

    private async Task<MatchResult> BuildAndPersistMatchResultAsync(Candidate candidate, Job job, CancellationToken cancellationToken)
    {
        MatchEvaluation evaluation;

        if (CanUseOpenAi())
        {
            evaluation = await EvaluateWithOpenAiAsync(candidate, job, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            evaluation = EvaluateMatch(candidate, job);
        }

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

        // Try to persist to Cosmos, but don't fail the match if storage is unavailable
        try
        {
            await _cosmosDb.MatchResults.CreateItemAsync(
                matchResult,
                new PartitionKey(matchResult.CandidateId.ToString()),
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Storage unavailable — still return the match result to the caller
        }

        return matchResult;
    }

    private async Task<MatchEvaluation> EvaluateWithOpenAiAsync(Candidate candidate, Job job, CancellationToken cancellationToken)
    {
        try
        {
            var chatClient = CreateChatClient();
            var systemPrompt = """
                You are a recruitment matching AI. Given a candidate profile and a job description,
                evaluate how well the candidate matches the job. Return a JSON object with:
                {
                  "score": <number 0-100>,
                  "skillMatches": [<list of matching skills/keywords>],
                  "skillGaps": [<list of required skills the candidate lacks>],
                  "explanation": "<2-3 sentence explanation of the match quality>"
                }
                Be accurate and fair. Consider skills, experience level, and domain relevance.
                Return ONLY the JSON object, no markdown fences.
                """;

            var userPrompt = BuildPrompt(candidate, job);

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(userPrompt)
            };

            var response = await chatClient.CompleteChatAsync(messages, cancellationToken: cancellationToken).ConfigureAwait(false);
            var content = response.Value.Content[0].Text ?? string.Empty;

            // Strip markdown fences if present
            content = Regex.Replace(content, @"^```(?:json)?\s*", "", RegexOptions.Multiline);
            content = Regex.Replace(content, @"\s*```$", "", RegexOptions.Multiline);

            var json = System.Text.Json.JsonDocument.Parse(content);
            var root = json.RootElement;

            var score = root.TryGetProperty("score", out var scoreProp) ? (decimal)scoreProp.GetDouble() : 0m;
            var skillMatches = root.TryGetProperty("skillMatches", out var smProp)
                ? smProp.EnumerateArray().Select(e => e.GetString() ?? "").Where(s => s.Length > 0).ToList()
                : new List<string>();
            var skillGaps = root.TryGetProperty("skillGaps", out var sgProp)
                ? sgProp.EnumerateArray().Select(e => e.GetString() ?? "").Where(s => s.Length > 0).ToList()
                : new List<string>();
            var explanation = root.TryGetProperty("explanation", out var expProp) ? expProp.GetString() ?? "" : "";

            return new MatchEvaluation(Math.Clamp(score, 0m, 100m), skillMatches, skillGaps, explanation);
        }
        catch (Exception)
        {
            // Fallback to keyword matching if OpenAI fails
            return EvaluateMatch(candidate, job);
        }
    }

    private async Task<IReadOnlyCollection<MatchResult>> QueryMatchResultsAsync(
        QueryDefinition query,
        QueryRequestOptions? requestOptions,
        CancellationToken cancellationToken)
    {
        try
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
        catch (Exception)
        {
            // Cosmos unavailable — return empty list
            return [];
        }
    }

    private static MatchEvaluation EvaluateMatch(Candidate candidate, Job job)
    {
        var candidateTerms = Tokenize($"{candidate.Skills} {candidate.Summary} {candidate.CvContent}");

        // Build job terms from structured fields for better accuracy
        var jobSkillsText = string.Join(" ", job.RequiredSkills ?? []) + " " + string.Join(" ", job.PreferredSkills ?? []);
        var jobTerms = Tokenize($"{job.Title} {jobSkillsText} {job.Description} {job.Requirements}");

        if (jobTerms.Count == 0)
        {
            return new MatchEvaluation(0m, [], [], "The job does not contain enough detail for automated matching.");
        }

        // Focus on required skills for scoring
        var requiredSkillTerms = Tokenize(string.Join(" ", job.RequiredSkills ?? []));
        var allJobSkillTerms = Tokenize(jobSkillsText);

        var skillMatches = candidateTerms
            .Intersect(allJobSkillTerms.Count > 0 ? allJobSkillTerms : jobTerms, StringComparer.OrdinalIgnoreCase)
            .OrderBy(term => term)
            .Take(15)
            .ToList();

        var skillGaps = (allJobSkillTerms.Count > 0 ? allJobSkillTerms : jobTerms)
            .Except(candidateTerms, StringComparer.OrdinalIgnoreCase)
            .OrderBy(term => term)
            .Take(10)
            .ToList();

        var denominator = allJobSkillTerms.Count > 0 ? allJobSkillTerms.Count : jobTerms.Count;
        var score = Math.Round(Math.Clamp((decimal)skillMatches.Count / denominator * 100m, 0m, 100m), 2);
        var explanation = skillMatches.Count > 0
            ? $"Matched {skillMatches.Count}/{denominator} key skills: {string.Join(", ", skillMatches.Take(8))}. Gaps: {string.Join(", ", skillGaps.Take(5))}."
            : "No strong skill overlap detected between the candidate and job requirements.";

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
        Evaluate how well this candidate matches the job. Consider skills, experience, and domain relevance.
        Return JSON with: score (0-100), skillMatches (list), skillGaps (list), explanation (2-3 sentences).

        CANDIDATE:
        Name: {candidate.FullName}
        Skills: {candidate.Skills}
        Summary: {candidate.Summary}
        CV excerpt: {(candidate.CvContent?.Length > 2000 ? candidate.CvContent[..2000] : candidate.CvContent)}

        JOB:
        Title: {job.Title}
        Department: {job.Department}
        Required Skills: {string.Join(", ", job.RequiredSkills ?? [])}
        Preferred Skills: {string.Join(", ", job.PreferredSkills ?? [])}
        Description: {job.Description}
        Requirements: {job.Requirements}
        Experience Level: {job.ExperienceLevel}
        """;
    }

    private ChatClient CreateChatClient()
    {
        var rawEndpoint = _options.OpenAIEndpoint;
        rawEndpoint = Regex.Replace(rawEndpoint, @"/openai(/v\d+)?/?$", "", RegexOptions.IgnoreCase);
        var endpoint = new Uri(rawEndpoint, UriKind.Absolute);
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

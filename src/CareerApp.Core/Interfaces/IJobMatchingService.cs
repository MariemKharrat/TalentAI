using CareerApp.Core.Models;

namespace CareerApp.Core.Interfaces;

public interface IJobMatchingService
{
    Task<MatchResult> MatchCandidateToJobAsync(Guid candidateId, Guid jobId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<MatchResult>> MatchCandidateToAllJobsAsync(Guid candidateId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<MatchResult>> MatchJobToCandidatesAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<MatchResult>> GetMatchesForCandidateAsync(Guid candidateId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<MatchResult>> GetMatchesForJobAsync(Guid jobId, CancellationToken cancellationToken = default);
}

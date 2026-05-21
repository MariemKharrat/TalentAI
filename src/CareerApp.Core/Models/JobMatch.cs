namespace CareerApp.Core.Models;

public sealed class JobMatch
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CandidateId { get; set; }
    public Guid JobId { get; set; }
    public double MatchScore { get; set; }
    public string? Reasoning { get; set; }
    public DateTime MatchedAtUtc { get; set; } = DateTime.UtcNow;
    public Candidate? Candidate { get; set; }
    public Job? Job { get; set; }
}

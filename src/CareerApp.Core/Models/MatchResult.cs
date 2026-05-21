namespace CareerApp.Core.Models;

public class MatchResult
{
    public Guid Id { get; set; }
    public Guid CandidateId { get; set; }
    public Guid JobId { get; set; }
    public decimal Score { get; set; }
    public MatchLevel MatchLevel { get; set; }
    public List<string> SkillMatches { get; set; } = [];
    public List<string> SkillGaps { get; set; } = [];
    public string Explanation { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}

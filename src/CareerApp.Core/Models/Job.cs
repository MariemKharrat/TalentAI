namespace CareerApp.Core.Models;

public sealed class Job
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string? Department { get; set; }
    public string? Description { get; set; }
    public string? Requirements { get; set; }
    public List<string> RequiredSkills { get; set; } = [];
    public List<string> PreferredSkills { get; set; } = [];
    public string? Location { get; set; }
    public string? ExperienceLevel { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public ICollection<JobMatch> CandidateMatches { get; set; } = new List<JobMatch>();
}

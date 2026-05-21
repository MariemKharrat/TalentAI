namespace CareerApp.Core.Models;

public sealed class Candidate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Skills { get; set; }
    public string? Summary { get; set; }
    public string? CvFileName { get; set; }
    public string? CvContent { get; set; }
    public string ParsingMethod { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public ICollection<JobMatch> JobMatches { get; set; } = new List<JobMatch>();
}

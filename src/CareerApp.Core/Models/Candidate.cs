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
    public string? CvBlobUrl { get; set; }
    public string? CvContent { get; set; }
    public string ParsingMethod { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public List<WorkExperience> Experience { get; set; } = [];
    public List<Education> Education { get; set; } = [];
    public ICollection<JobMatch> JobMatches { get; set; } = new List<JobMatch>();
}

public sealed class WorkExperience
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Company { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
    public string Description { get; set; } = string.Empty;
}

public sealed class Education
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Institution { get; set; } = string.Empty;
    public string Degree { get; set; } = string.Empty;
    public string FieldOfStudy { get; set; } = string.Empty;
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
}

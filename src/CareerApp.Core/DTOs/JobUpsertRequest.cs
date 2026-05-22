namespace CareerApp.Core.DTOs;

public sealed class JobUpsertRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Department { get; set; }
    public string? Description { get; set; }
    public string? Requirements { get; set; }
    public List<string>? RequiredSkills { get; set; }
    public List<string>? PreferredSkills { get; set; }
    public string? Location { get; set; }
    public string? ExperienceLevel { get; set; }
    public bool IsActive { get; set; } = true;
}

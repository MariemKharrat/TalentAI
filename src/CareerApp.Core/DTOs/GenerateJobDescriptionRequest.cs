namespace CareerApp.Core.DTOs;

public sealed class GenerateJobDescriptionRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Department { get; set; }
    public string? Location { get; set; }
    public string? ExperienceLevel { get; set; }
    public string? EmploymentType { get; set; }
    public List<string>? RequiredSkills { get; set; }
    public List<string>? PreferredSkills { get; set; }
    public string? Summary { get; set; }
    public string? Responsibilities { get; set; }
    public string? Requirements { get; set; }
    public string? TeamSize { get; set; }
    public string? ReportingTo { get; set; }
    public string? SalaryRange { get; set; }
    public string? Benefits { get; set; }
    public string? PolicyContext { get; set; }
    public string? Tone { get; set; }
}

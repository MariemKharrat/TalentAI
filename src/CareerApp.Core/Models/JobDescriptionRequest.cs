namespace CareerApp.Core.Models;

public class JobDescriptionRequest
{
    public string Title { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string ExperienceLevel { get; set; } = string.Empty;
    public string EmploymentType { get; set; } = "Full-time";
    public List<string> RequiredSkills { get; set; } = [];
    public List<string> PreferredSkills { get; set; } = [];
    public string Responsibilities { get; set; } = string.Empty;
    public string Requirements { get; set; } = string.Empty;
    public string TeamSize { get; set; } = string.Empty;
    public string ReportingTo { get; set; } = string.Empty;
    public string SalaryRange { get; set; } = string.Empty;
    public string Benefits { get; set; } = string.Empty;
    public string PolicyContext { get; set; } = string.Empty;
    public string Tone { get; set; } = "Professional and inclusive";
}

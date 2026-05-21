namespace CareerApp.Core.DTOs;

public class JobDescriptionRequest
{
    public string Title { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public List<string> RequiredSkills { get; set; } = [];
    public string ExperienceLevel { get; set; } = string.Empty;
    public string PolicyContext { get; set; } = string.Empty;
}

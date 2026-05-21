namespace CareerApp.Core.Models;

public class WorkExperience
{
    public Guid Id { get; set; }
    public string Company { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTimeOffset StartDate { get; set; }
    public DateTimeOffset? EndDate { get; set; }
    public string Description { get; set; } = string.Empty;
}

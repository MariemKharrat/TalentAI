namespace CareerApp.Core.Models;

public class JobDescriptionRequest
{
    public string Title { get; set; } = string.Empty;

    public string Department { get; set; } = string.Empty;

    public string Responsibilities { get; set; } = string.Empty;

    public string Requirements { get; set; } = string.Empty;

    public string PolicyContext { get; set; } = string.Empty;
}

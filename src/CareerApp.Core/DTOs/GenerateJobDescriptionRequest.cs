namespace CareerApp.Core.DTOs;

public sealed class GenerateJobDescriptionRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Department { get; set; }
    public string? Summary { get; set; }
    public string? Responsibilities { get; set; }
    public string? Requirements { get; set; }
    public string? PolicyContext { get; set; }
}

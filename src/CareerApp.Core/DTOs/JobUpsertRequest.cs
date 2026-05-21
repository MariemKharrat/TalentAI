namespace CareerApp.Core.DTOs;

public sealed class JobUpsertRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Department { get; set; }
    public string? Description { get; set; }
    public string? Requirements { get; set; }
    public bool IsActive { get; set; } = true;
}

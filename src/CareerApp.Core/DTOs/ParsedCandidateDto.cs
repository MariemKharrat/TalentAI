namespace CareerApp.Core.DTOs;

public sealed class ParsedCandidateDto
{
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Skills { get; set; }
    public string? Summary { get; set; }
    public string? RawText { get; set; }
}

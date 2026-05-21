namespace CareerApp.Infrastructure.Configuration;

public sealed class AzureAIOptions
{
    public const string SectionName = "AzureAI";

    public string DocumentIntelligenceEndpoint { get; set; } = string.Empty;
    public string DocumentIntelligenceKey { get; set; } = string.Empty;
    public string OpenAIEndpoint { get; set; } = string.Empty;
    public string OpenAIKey { get; set; } = string.Empty;
    public string OpenAIDeploymentName { get; set; } = string.Empty;
    public string ContentUnderstandingEndpoint { get; set; } = string.Empty;
    public string ContentUnderstandingKey { get; set; } = string.Empty;
    public string ContentUnderstandingAnalyzerId { get; set; } = string.Empty;
}

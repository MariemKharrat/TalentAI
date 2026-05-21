using System.ClientModel;
using System.Text;
using Azure.AI.OpenAI;
using Azure.Identity;
using CareerApp.Core.Interfaces;
using CareerApp.Core.Models;
using CareerApp.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace CareerApp.Infrastructure.Services;

public class JobDescriptionGeneratorService : IJobDescriptionGenerator
{
    private readonly AzureAIOptions _options;

    public JobDescriptionGeneratorService(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        _options = LoadOptions(configuration);
    }

    public async Task<string> GenerateJobDescriptionAsync(JobDescriptionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var prompt = BuildPrompt(request);

        if (CanUseOpenAi())
        {
            var chatClient = CreateChatClient();

            // TODO: Invoke the Azure AI Foundry job-description deployment with the grounded prompt and compliance schema.
            _ = chatClient;
            _ = prompt;
            await Task.Yield();
        }

        return BuildFallbackDescription(request);
    }

    private static string BuildPrompt(JobDescriptionRequest request)
    {
        return $"""
        Generate a compliant, inclusive job description.
        Policy context for grounding:
        {request.PolicyContext}

        Role title: {request.Title}
        Department: {request.Department}
        Responsibilities: {request.Responsibilities}
        Requirements: {request.Requirements}
        Return a polished job description suitable for publishing.
        """;
    }

    private static string BuildFallbackDescription(JobDescriptionRequest request)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Title: {request.Title}");
        builder.AppendLine($"Department: {request.Department}");
        builder.AppendLine();
        builder.AppendLine("Overview");
        builder.AppendLine($"We are looking for a {request.Title} to join our {request.Department} team. This placeholder description is structured to be replaced by Azure AI Foundry-generated content once the final deployment is wired in.");
        builder.AppendLine();
        builder.AppendLine("Key Responsibilities");
        builder.AppendLine(request.Responsibilities);
        builder.AppendLine();
        builder.AppendLine("Core Requirements");
        builder.AppendLine(request.Requirements);

        if (!string.IsNullOrWhiteSpace(request.PolicyContext))
        {
            builder.AppendLine();
            builder.AppendLine("Compliance and Policy Grounding");
            builder.AppendLine(request.PolicyContext);
        }

        return builder.ToString().Trim();
    }

    private ChatClient CreateChatClient()
    {
        var endpoint = new Uri(_options.OpenAIEndpoint, UriKind.Absolute);
        var client = string.IsNullOrWhiteSpace(_options.OpenAIKey)
            ? new AzureOpenAIClient(endpoint, new DefaultAzureCredential())
            : new AzureOpenAIClient(endpoint, new ApiKeyCredential(_options.OpenAIKey));

        return client.GetChatClient(_options.OpenAIDeploymentName);
    }

    private bool CanUseOpenAi()
    {
        return !string.IsNullOrWhiteSpace(_options.OpenAIEndpoint)
            && !string.IsNullOrWhiteSpace(_options.OpenAIDeploymentName);
    }

    private static AzureAIOptions LoadOptions(IConfiguration configuration)
    {
        return new AzureAIOptions
        {
            DocumentIntelligenceEndpoint = configuration["AzureAI:DocumentIntelligenceEndpoint"] ?? string.Empty,
            DocumentIntelligenceKey = configuration["AzureAI:DocumentIntelligenceKey"] ?? string.Empty,
            OpenAIEndpoint = configuration["AzureAI:OpenAIEndpoint"] ?? string.Empty,
            OpenAIKey = configuration["AzureAI:OpenAIKey"] ?? string.Empty,
            OpenAIDeploymentName = configuration["AzureAI:OpenAIDeploymentName"] ?? string.Empty
        };
    }
}

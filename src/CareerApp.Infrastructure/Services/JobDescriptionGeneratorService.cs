using System.ClientModel;
using System.Text;
using Azure.AI.OpenAI;
using Azure.Identity;
using CareerApp.Core.Interfaces;
using CareerApp.Core.Models;
using CareerApp.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace CareerApp.Infrastructure.Services;

public class JobDescriptionGeneratorService : IJobDescriptionGenerator
{
    private readonly AzureAIOptions _options;

    public JobDescriptionGeneratorService(IOptions<AzureAIOptions> options)
    {
        _options = options.Value;
    }

    public async Task<string> GenerateJobDescriptionAsync(JobDescriptionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (CanUseOpenAi())
        {
            try
            {
                return await GenerateWithAIAsync(request);
            }
            catch (Exception)
            {
                // Fallback to template if AI fails
            }
        }

        return BuildFallbackDescription(request);
    }

    private async Task<string> GenerateWithAIAsync(JobDescriptionRequest request)
    {
        var chatClient = CreateChatClient();

        var systemPrompt = """
            You are an expert technical recruiter and HR job description writer.
            You have deep knowledge of current industry standards, market rates, and role expectations.
            
            Generate a comprehensive, professional job description with EXACTLY these sections:
            
            ## About the Role
            A compelling 2-3 paragraph overview of the position, team, and impact.
            
            ## Key Responsibilities
            - 8-12 specific, action-oriented bullet points
            
            ## Requirements
            - 5-8 mandatory qualifications (education, years of experience, certifications, technical skills)
            
            ## Preferred Qualifications
            - 4-6 nice-to-have skills or experiences
            
            Rules:
            - Use inclusive language (avoid gendered terms, age-biased language)
            - Be specific and realistic about responsibilities and qualifications
            - Base requirements on current industry standards for the role level
            - Make descriptions engaging while remaining professional
            - Include an equal opportunity statement at the end
            - Do NOT include a Benefits section
            - Format using markdown
            """;

        var userPrompt = BuildDetailedPrompt(request);

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(userPrompt)
        };

        var options = new ChatCompletionOptions
        {
            Temperature = 0.7f,
            MaxOutputTokenCount = 4000
        };

        var response = await chatClient.CompleteChatAsync(messages, options);
        return response.Value.Content[0].Text;
    }

    private static string BuildDetailedPrompt(JobDescriptionRequest request)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Generate a comprehensive job description based on the following inputs. Use your knowledge of current industry standards to fill in realistic details where the user has not provided them.");
        sb.AppendLine();
        sb.AppendLine($"**Job Title:** {request.Title}");
        sb.AppendLine($"**Department:** {request.Department}");

        if (!string.IsNullOrWhiteSpace(request.Location))
            sb.AppendLine($"**Location:** {request.Location}");

        if (!string.IsNullOrWhiteSpace(request.ExperienceLevel))
            sb.AppendLine($"**Experience Level:** {request.ExperienceLevel}");

        if (!string.IsNullOrWhiteSpace(request.EmploymentType))
            sb.AppendLine($"**Employment Type:** {request.EmploymentType}");

        if (!string.IsNullOrWhiteSpace(request.ReportingTo))
            sb.AppendLine($"**Reports To:** {request.ReportingTo}");

        if (!string.IsNullOrWhiteSpace(request.TeamSize))
            sb.AppendLine($"**Team Size:** {request.TeamSize}");

        if (request.RequiredSkills.Count > 0)
            sb.AppendLine($"**Required Skills:** {string.Join(", ", request.RequiredSkills)}");

        if (request.PreferredSkills.Count > 0)
            sb.AppendLine($"**Preferred Skills:** {string.Join(", ", request.PreferredSkills)}");

        if (!string.IsNullOrWhiteSpace(request.Responsibilities))
        {
            sb.AppendLine();
            sb.AppendLine("**Key Responsibilities Context (expand on these):**");
            sb.AppendLine(request.Responsibilities);
        }

        if (!string.IsNullOrWhiteSpace(request.Requirements))
        {
            sb.AppendLine();
            sb.AppendLine("**Additional Requirements (include these):**");
            sb.AppendLine(request.Requirements);
        }

        if (!string.IsNullOrWhiteSpace(request.SalaryRange))
            sb.AppendLine($"**Salary Range:** {request.SalaryRange}");

        if (!string.IsNullOrWhiteSpace(request.PolicyContext))
        {
            sb.AppendLine();
            sb.AppendLine("**Organization Policy Context (must comply with):**");
            sb.AppendLine(request.PolicyContext);
        }

        if (!string.IsNullOrWhiteSpace(request.Tone))
            sb.AppendLine($"\n**Desired tone:** {request.Tone}");

        return sb.ToString();
    }

    private static string BuildFallbackDescription(JobDescriptionRequest request)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# {request.Title}");
        builder.AppendLine();
        builder.AppendLine($"**Department:** {request.Department}");
        if (!string.IsNullOrWhiteSpace(request.Location))
            builder.AppendLine($"**Location:** {request.Location}");
        if (!string.IsNullOrWhiteSpace(request.ExperienceLevel))
            builder.AppendLine($"**Experience Level:** {request.ExperienceLevel}");
        if (!string.IsNullOrWhiteSpace(request.EmploymentType))
            builder.AppendLine($"**Employment Type:** {request.EmploymentType}");
        builder.AppendLine();

        builder.AppendLine("## About the Role");
        builder.AppendLine($"We are looking for a {request.Title} to join our {request.Department} team.");
        builder.AppendLine();

        if (!string.IsNullOrWhiteSpace(request.Responsibilities))
        {
            builder.AppendLine("## Key Responsibilities");
            builder.AppendLine(request.Responsibilities);
            builder.AppendLine();
        }

        if (request.RequiredSkills.Count > 0)
        {
            builder.AppendLine("## Required Skills");
            foreach (var skill in request.RequiredSkills)
                builder.AppendLine($"- {skill}");
            builder.AppendLine();
        }

        if (request.PreferredSkills.Count > 0)
        {
            builder.AppendLine("## Preferred Skills");
            foreach (var skill in request.PreferredSkills)
                builder.AppendLine($"- {skill}");
            builder.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(request.Requirements))
        {
            builder.AppendLine("## Requirements");
            builder.AppendLine(request.Requirements);
            builder.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(request.Benefits))
        {
            builder.AppendLine("## What We Offer");
            builder.AppendLine(request.Benefits);
            builder.AppendLine();
        }

        builder.AppendLine("---");
        builder.AppendLine("*We are an equal opportunity employer. All qualified applicants will receive consideration without regard to race, color, religion, gender, national origin, disability, or any other protected status.*");
        builder.AppendLine();
        builder.AppendLine("*Note: This is a template description. Connect Azure AI Foundry for AI-generated, policy-compliant descriptions.*");

        return builder.ToString().Trim();
    }

    private ChatClient CreateChatClient()
    {
        var rawEndpoint = _options.OpenAIEndpoint;
        // Strip /openai/v1 suffix if present — AzureOpenAIClient doesn't want it
        rawEndpoint = System.Text.RegularExpressions.Regex.Replace(rawEndpoint, @"/openai(/v\d+)?/?$", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var endpoint = new Uri(rawEndpoint, UriKind.Absolute);
        var client = string.IsNullOrWhiteSpace(_options.OpenAIKey)
            ? new AzureOpenAIClient(endpoint, new DefaultAzureCredential())
            : new AzureOpenAIClient(endpoint, new ApiKeyCredential(_options.OpenAIKey));

        return client.GetChatClient(_options.OpenAIDeploymentName);
    }

    private bool CanUseOpenAi()
    {
        return !string.IsNullOrWhiteSpace(_options.OpenAIEndpoint)
            && !string.IsNullOrWhiteSpace(_options.OpenAIDeploymentName)
            && (!string.IsNullOrWhiteSpace(_options.OpenAIKey) || true);
    }
}
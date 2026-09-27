namespace WebApplication1.Services;

public class JiraOptions
{
    public const string SectionName = "Jira";

    /// <summary>Atlassian account email used for API token auth.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Jira API token (https://id.atlassian.com/manage-profile/security/api-tokens).</summary>
    public string ApiToken { get; set; } = string.Empty;

    /// <summary>Base URL of the Jira site, used to look up the connected account.</summary>
    public string SiteUrl { get; set; } = string.Empty;
}

public class AiOptions
{
    public const string SectionName = "Ai";

    /// <summary>OpenAI-compatible endpoint. Default is Groq's free API.</summary>
    public string Endpoint { get; set; } = "https://api.groq.com/openai/v1/chat/completions";

    /// <summary>API key for the free AI provider (e.g. Groq).</summary>
    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = "openai/gpt-oss-120b";
}

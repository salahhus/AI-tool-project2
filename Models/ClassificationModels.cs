namespace WebApplication1.Models;

public class ClassifyRequest
{
    /// <summary>Full Jira ticket URL, e.g. https://mycompany.atlassian.net/browse/ABC-123</summary>
    public string JiraUrl { get; set; } = string.Empty;
}

/// <summary>Used when the Jira REST API is not reachable (no API token) and the ticket text is pasted manually.</summary>
public class ClassifyTextRequest
{
    public string Summary { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;
}

/// <summary>Re-generates the explanation for an opportunity chosen by the user instead of by the model.</summary>
public class ExplainRequest
{
    public string JiraUrl { get; set; } = string.Empty;

    public string Opportunity { get; set; } = string.Empty;
}

public class ClassifyResponse
{
    public string IssueKey { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Opportunity { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string HighlightedText { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
}

public class JiraIssue
{
    public string Key { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

/// <summary>The Atlassian account the configured API token belongs to.</summary>
public class JiraAccount
{
    public string DisplayName { get; set; } = string.Empty;
    public string EmailAddress { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
    public bool IsConnected { get; set; }
    public string? Error { get; set; }
}

public static class ArkikOpportunities
{
    public static readonly string[] All =
    [
        "Demand across multiple initiatives",
        "M2S knowledge and workflows",
        "Backlog prioritization",
        "Internal bug prevention",
        "Team delivery speed and multitasking",
        "Understanding operational needs",
        "Unit and end-to-end test coverage",
        "A shared global product vision",
        "Performance and technical improvements",
        "Proactive monitoring and support",
        "Growing technical debt",
        "Clearer requirements and definitions"
    ];
}

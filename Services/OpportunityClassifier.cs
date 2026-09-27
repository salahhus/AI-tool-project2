using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using WebApplication1.Models;

namespace WebApplication1.Services;

public interface IOpportunityClassifier
{
    Task<ClassifyResponse> ClassifyAsync(JiraIssue issue, CancellationToken cancellationToken);

    /// <summary>Re-generates the justification and highlight for an opportunity chosen by the user.</summary>
    Task<ClassifyResponse> ExplainAsync(JiraIssue issue, string opportunity, CancellationToken cancellationToken);
}

/// <summary>
/// Classifies a Jira ticket into one of the Arkik opportunities using a free
/// OpenAI-compatible AI provider (Groq by default). Falls back to a local
/// keyword heuristic when no API key is configured or the AI call fails.
/// </summary>
public class OpportunityClassifier : IOpportunityClassifier
{
    private static readonly Dictionary<string, string[]> Keywords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Demand across multiple initiatives"] = ["multiple initiatives", "many projects", "parallel", "competing", "demand"],
        ["M2S knowledge and workflows"] = ["m2s", "workflow", "knowledge transfer", "process documentation"],
        ["Backlog prioritization"] = ["backlog", "prioriti", "roadmap order", "grooming"],
        ["Internal bug prevention"] = ["bug", "defect", "regression", "hotfix", "error in production"],
        ["Team delivery speed and multitasking"] = ["delivery speed", "velocity", "multitask", "context switch", "deadline"],
        ["Understanding operational needs"] = ["operation", "customer need", "business need", "plant", "dispatch", "on site", "field", "user need"],
        ["Unit and end-to-end test coverage"] = ["unit test", "e2e", "end-to-end", "coverage", "automated test"],
        ["A shared global product vision"] = ["product vision", "global", "strategy", "alignment", "roadmap vision"],
        ["Performance and technical improvements"] = ["performance", "slow", "latency", "optimiz", "memory", "scalab"],
        ["Proactive monitoring and support"] = ["monitor", "alert", "observab", "logging", "support ticket", "incident"],
        ["Growing technical debt"] = ["technical debt", "legacy", "refactor", "deprecated", "cleanup"],
        ["Clearer requirements and definitions"] = ["requirement", "acceptance criteria", "definition", "unclear", "ambiguous", "spec"]
    };

    private readonly HttpClient _httpClient;
    private readonly AiOptions _options;
    private readonly ILogger<OpportunityClassifier> _logger;

    public OpportunityClassifier(HttpClient httpClient, IOptions<AiOptions> options, ILogger<OpportunityClassifier> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ClassifyResponse> ClassifyAsync(JiraIssue issue, CancellationToken cancellationToken)
    {
        var text = $"{issue.Summary}\n{issue.Description}".Trim();

        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            try
            {
                var aiResult = await ClassifyWithAiAsync(text, cancellationToken);
                if (aiResult is not null)
                {
                    return aiResult;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "AI classification failed, falling back to keyword heuristic.");
            }
        }

        return ClassifyWithKeywords(text);
    }

    public async Task<ClassifyResponse> ExplainAsync(JiraIssue issue, string opportunity, CancellationToken cancellationToken)
    {
        var normalized = ArkikOpportunities.All
            .FirstOrDefault(x => string.Equals(x, opportunity, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException($"'{opportunity}' is not a known Arkik opportunity.", nameof(opportunity));

        var text = $"{issue.Summary}\n{issue.Description}".Trim();

        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            try
            {
                var aiResult = await ExplainWithAiAsync(text, normalized, cancellationToken);
                if (aiResult is not null)
                {
                    return aiResult;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "AI explanation failed, falling back to keyword heuristic.");
            }
        }

        return ExplainWithKeywords(text, normalized);
    }

    private async Task<ClassifyResponse?> ClassifyWithAiAsync(string text, CancellationToken cancellationToken)
    {
        var systemPrompt =
            "You classify Jira tickets into exactly one Arkik opportunity. " +
            "Allowed values: " + string.Join("; ", ArkikOpportunities.All) + ". " +
            "Also return the exact sentence or phrase from the ticket that best justifies the choice. " +
            "Answer with JSON only: {\"opportunity\":\"...\",\"confidence\":0.0,\"reason\":\"...\",\"highlight\":\"...\"}";

        var payload = new
        {
            model = _options.Model,
            temperature = 0.0,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = "Jira ticket:\n" + text }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, _options.Endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("AI provider returned {StatusCode}: {Body}", (int)response.StatusCode, body);
            return null;
        }

        using var document = JsonDocument.Parse(body);
        var content = document.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        using var result = JsonDocument.Parse(content);
        var root = result.RootElement;

        var opportunity = root.TryGetProperty("opportunity", out var o) ? o.GetString() ?? string.Empty : string.Empty;
        var normalized = ArkikOpportunities.All
            .FirstOrDefault(x => string.Equals(x, opportunity, StringComparison.OrdinalIgnoreCase));

        if (normalized is null)
        {
            return null;
        }

        var highlight = root.TryGetProperty("highlight", out var h) ? h.GetString() ?? string.Empty : string.Empty;

        return new ClassifyResponse
        {
            Opportunity = normalized,
            Confidence = root.TryGetProperty("confidence", out var c) && c.ValueKind == JsonValueKind.Number ? c.GetDouble() : 0.8,
            Reason = root.TryGetProperty("reason", out var r) ? r.GetString() ?? string.Empty : string.Empty,
            HighlightedText = BuildHighlight(text, highlight),
            Source = "ai"
        };
    }

    private async Task<ClassifyResponse?> ExplainWithAiAsync(string text, string opportunity, CancellationToken cancellationToken)
    {
        var systemPrompt =
            $"The user has selected the Arkik opportunity \"{opportunity}\" for a Jira ticket. " +
            "Explain how the ticket relates to that opportunity, and quote the exact sentence or phrase from the ticket " +
            "that best supports it. Set 'confidence' to how well the ticket actually fits the selected opportunity (0 to 1). " +
            "Answer with JSON only: {\"confidence\":0.0,\"reason\":\"...\",\"highlight\":\"...\"}";

        var payload = new
        {
            model = _options.Model,
            temperature = 0.0,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = "Jira ticket:\n" + text }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, _options.Endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("AI provider returned {StatusCode}: {Body}", (int)response.StatusCode, body);
            return null;
        }

        using var document = JsonDocument.Parse(body);
        var content = document.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        using var result = JsonDocument.Parse(content);
        var root = result.RootElement;
        var highlight = root.TryGetProperty("highlight", out var h) ? h.GetString() ?? string.Empty : string.Empty;

        return new ClassifyResponse
        {
            Opportunity = opportunity,
            Confidence = root.TryGetProperty("confidence", out var c) && c.ValueKind == JsonValueKind.Number ? c.GetDouble() : 0.8,
            Reason = root.TryGetProperty("reason", out var r) ? r.GetString() ?? string.Empty : string.Empty,
            HighlightedText = BuildHighlight(text, highlight),
            Source = "ai"
        };
    }

    private static ClassifyResponse ExplainWithKeywords(string text, string opportunity)
    {
        var keywords = Keywords.TryGetValue(opportunity, out var found) ? found : [];
        var matched = keywords.Where(k => text.Contains(k, StringComparison.OrdinalIgnoreCase)).ToArray();
        var firstHit = matched.FirstOrDefault() ?? string.Empty;

        return new ClassifyResponse
        {
            Opportunity = opportunity,
            Confidence = matched.Length == 0 ? 0.1 : Math.Min(0.4 + (0.15 * matched.Length), 0.9),
            Reason = matched.Length == 0
                ? $"No keyword for '{opportunity}' was found in this ticket, so the link is weak."
                : $"Keyword heuristic matched {matched.Length} term(s) for '{opportunity}': {string.Join(", ", matched)}.",
            HighlightedText = BuildHighlight(text, firstHit),
            Source = "keyword-fallback"
        };
    }

    private static ClassifyResponse ClassifyWithKeywords(string text)
    {
        var best = string.Empty;
        var bestScore = 0;
        var bestKeyword = string.Empty;

        foreach (var (opportunity, keywords) in Keywords)
        {
            var score = 0;
            var firstHit = string.Empty;

            foreach (var keyword in keywords)
            {
                if (text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    score++;
                    if (firstHit.Length == 0)
                    {
                        firstHit = keyword;
                    }
                }
            }

            if (score > bestScore)
            {
                bestScore = score;
                best = opportunity;
                bestKeyword = firstHit;
            }
        }

        if (bestScore == 0)
        {
            best = "Understanding operational needs";
        }

        return new ClassifyResponse
        {
            Opportunity = best,
            Confidence = bestScore == 0 ? 0.2 : Math.Min(0.4 + (0.15 * bestScore), 0.9),
            Reason = bestScore == 0
                ? "No AI provider configured and no keyword matched; defaulted to the most common opportunity."
                : $"Keyword heuristic matched {bestScore} term(s), first match: '{bestKeyword}'.",
            HighlightedText = BuildHighlight(text, bestKeyword),
            Source = "keyword-fallback"
        };
    }

    /// <summary>Wraps the justifying phrase in ==markers== so the caller can render it highlighted.</summary>
    private static string BuildHighlight(string text, string phrase)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(phrase))
        {
            return text;
        }

        var index = text.IndexOf(phrase, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return text;
        }

        return text[..index] + "==" + text.Substring(index, phrase.Length) + "==" + text[(index + phrase.Length)..];
    }
}

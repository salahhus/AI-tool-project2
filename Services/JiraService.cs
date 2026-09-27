using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using WebApplication1.Models;

namespace WebApplication1.Services;

public interface IJiraService
{
    Task<JiraIssue> GetIssueAsync(string jiraUrl, CancellationToken cancellationToken);

    /// <summary>Returns the Atlassian account the configured API token belongs to.</summary>
    Task<JiraAccount> GetCurrentUserAsync(CancellationToken cancellationToken);
}

public class JiraService : IJiraService
{
    private static readonly Regex IssueKeyRegex = new(@"[A-Z][A-Z0-9_]+-\d+", RegexOptions.Compiled);

    private readonly HttpClient _httpClient;
    private readonly JiraOptions _options;

    public JiraService(HttpClient httpClient, IOptions<JiraOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<JiraIssue> GetIssueAsync(string jiraUrl, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(jiraUrl, UriKind.Absolute, out var uri))
        {
            throw new ArgumentException("Jira URL is not a valid absolute URL.", nameof(jiraUrl));
        }

        var match = IssueKeyRegex.Match(uri.AbsoluteUri);
        if (!match.Success)
        {
            throw new ArgumentException("Could not find an issue key (e.g. ABC-123) in the Jira URL.", nameof(jiraUrl));
        }

        var issueKey = match.Value;
        var requestUri = new Uri(uri, $"/rest/api/3/issue/{issueKey}?fields=summary,description");

        var hasCredentials = !string.IsNullOrWhiteSpace(_options.Email) && !string.IsNullOrWhiteSpace(_options.ApiToken);
        if (!hasCredentials)
        {
            throw new ArgumentException(
                "Jira credentials are not configured. Set 'Jira:Email' and 'Jira:ApiToken' (create a token at " +
                "https://id.atlassian.com/manage-profile/security/api-tokens), ideally via user-secrets.", nameof(jiraUrl));
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.Email}:{_options.ApiToken}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var hint = (int)response.StatusCode switch
            {
                404 => $"Jira returns 404 both when issue '{issueKey}' does not exist and when the authenticated user cannot see it. " +
                       $"Verify the issue key, that '{requestUri.Host}' is the correct Jira site, and that '{_options.Email}' has browse permission on that project.",
                401 => "Authentication failed. Check 'Jira:Email' and that 'Jira:ApiToken' is a valid, non-expired Atlassian API token.",
                403 => "Access forbidden. The account is authenticated but lacks permission to view this issue.",
                _ => null
            };

            throw new HttpRequestException($"Jira request failed ({(int)response.StatusCode}): {body}{(hint is null ? string.Empty : " " + hint)}");
        }

        using var document = JsonDocument.Parse(body);
        var fields = document.RootElement.GetProperty("fields");

        var summary = fields.TryGetProperty("summary", out var summaryElement) && summaryElement.ValueKind == JsonValueKind.String
            ? summaryElement.GetString() ?? string.Empty
            : string.Empty;

        var description = fields.TryGetProperty("description", out var descriptionElement)
            ? ExtractText(descriptionElement)
            : string.Empty;

        return new JiraIssue
        {
            Key = issueKey,
            Summary = summary,
            Description = description.Trim()
        };
    }

    public async Task<JiraAccount> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.Email) || string.IsNullOrWhiteSpace(_options.ApiToken))
        {
            return new JiraAccount { Error = "Jira credentials are not configured." };
        }

        if (!Uri.TryCreate(_options.SiteUrl, UriKind.Absolute, out var siteUri))
        {
            return new JiraAccount { Error = "Set 'Jira:SiteUrl' to show the connected account." };
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(siteUri, "/rest/api/3/myself"));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Authorization = CreateBasicAuth();

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new JiraAccount { Error = $"Jira rejected the token ({(int)response.StatusCode})." };
            }

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var root = document.RootElement;

            return new JiraAccount
            {
                IsConnected = true,
                DisplayName = root.TryGetProperty("displayName", out var name) ? name.GetString() ?? string.Empty : string.Empty,
                EmailAddress = root.TryGetProperty("emailAddress", out var mail) ? mail.GetString() ?? _options.Email : _options.Email,
                AvatarUrl = root.TryGetProperty("avatarUrls", out var avatars) && avatars.TryGetProperty("24x24", out var avatar)
                    ? avatar.GetString() ?? string.Empty
                    : string.Empty
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new JiraAccount { Error = $"Could not reach Jira: {ex.Message}" };
        }
    }

    private AuthenticationHeaderValue CreateBasicAuth()
    {
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.Email}:{_options.ApiToken}"));
        return new AuthenticationHeaderValue("Basic", credentials);
    }

    /// <summary>
    /// Flattens Atlassian Document Format (or plain text) into readable text, keeping
    /// headings (prefixed with '#') and list items (prefixed with '-') so the UI can format them.
    /// </summary>
    private static string ExtractText(JsonElement element)
    {
        var builder = new StringBuilder();
        Walk(element, builder, listDepth: 0);

        // Collapse runs of blank lines left behind by empty ADF nodes.
        var lines = builder.ToString()
            .Replace("\r\n", "\n")
            .Split('\n')
            .Select(line => line.TrimEnd());

        var cleaned = new List<string>();
        foreach (var line in lines)
        {
            if (line.Length == 0 && (cleaned.Count == 0 || cleaned[^1].Length == 0))
            {
                continue;
            }

            cleaned.Add(line);
        }

        return string.Join(Environment.NewLine, cleaned);

        static void Walk(JsonElement node, StringBuilder builder, int listDepth)
        {
            switch (node.ValueKind)
            {
                case JsonValueKind.String:
                    builder.Append(node.GetString());
                    break;

                case JsonValueKind.Array:
                    foreach (var item in node.EnumerateArray())
                    {
                        Walk(item, builder, listDepth);
                    }
                    break;

                case JsonValueKind.Object:
                    var type = node.TryGetProperty("type", out var typeElement) && typeElement.ValueKind == JsonValueKind.String
                        ? typeElement.GetString()
                        : null;

                    switch (type)
                    {
                        case "heading":
                            var level = node.TryGetProperty("attrs", out var attrs) &&
                                        attrs.TryGetProperty("level", out var levelElement) &&
                                        levelElement.ValueKind == JsonValueKind.Number
                                ? levelElement.GetInt32()
                                : 2;

                            builder.AppendLine();
                            builder.Append(new string('#', Math.Clamp(level, 1, 4))).Append(' ');
                            AppendChildren(node, builder, listDepth);
                            builder.AppendLine();
                            return;

                        case "bulletList":
                        case "orderedList":
                            // A nested list must start on its own line, not glued to the parent item's text.
                            if (builder.Length > 0 && builder[^1] != '\n')
                            {
                                builder.AppendLine();
                            }

                            AppendChildren(node, builder, listDepth + 1);
                            return;

                        case "listItem":
                            builder.Append(new string(' ', Math.Max(0, listDepth - 1) * 2)).Append("- ");
                            AppendChildren(node, builder, listDepth);
                            builder.AppendLine();
                            return;

                        case "paragraph":
                            AppendChildren(node, builder, listDepth);
                            // Inside a list the item itself adds the line break.
                            if (listDepth == 0)
                            {
                                builder.AppendLine();
                            }
                            return;

                        case "hardBreak":
                            builder.AppendLine();
                            return;

                        case "rule":
                            builder.AppendLine().AppendLine("---");
                            return;

                        default:
                            if (node.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
                            {
                                builder.Append(text.GetString());
                            }

                            AppendChildren(node, builder, listDepth);
                            return;
                    }
            }
        }

        static void AppendChildren(JsonElement node, StringBuilder builder, int listDepth)
        {
            if (node.TryGetProperty("content", out var content))
            {
                Walk(content, builder, listDepth);
            }
        }
    }
}

using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using WebApplication1.Services;

namespace WebApplication1.Tests;

public class JiraServiceTests
{
    private const string ValidUrl = "https://example.atlassian.net/browse/ARKIK-42798";

    private static JiraService CreateService(StubHttpMessageHandler handler, string email = "user@example.com", string token = "token")
        => new(new HttpClient(handler), Options.Create(new JiraOptions
        {
            Email = email,
            ApiToken = token,
            SiteUrl = "https://example.atlassian.net"
        }));

    private static StubHttpMessageHandler OkHandler(string description = """{"type":"doc","content":[{"type":"paragraph","content":[{"type":"text","text":"Operations need visibility."}]}]}""")
        => new(HttpStatusCode.OK, "{\"fields\":{\"summary\":\"MS - Action Log\",\"description\":" + description + "}}");

    [Fact]
    public async Task GetIssueAsync_ParsesSummaryAndAdfDescription()
    {
        var service = CreateService(OkHandler());

        var issue = await service.GetIssueAsync(ValidUrl, CancellationToken.None);

        Assert.Equal("ARKIK-42798", issue.Key);
        Assert.Equal("MS - Action Log", issue.Summary);
        Assert.Equal("Operations need visibility.", issue.Description);
    }

    [Fact]
    public async Task GetIssueAsync_CallsRestApiWithBasicAuth()
    {
        var handler = OkHandler();
        var service = CreateService(handler, "user@example.com", "secret");

        await service.GetIssueAsync(ValidUrl, CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("https://example.atlassian.net/rest/api/3/issue/ARKIK-42798?fields=summary,description", request.RequestUri!.ToString());
        Assert.Equal("Basic", request.Headers.Authorization?.Scheme);
        Assert.Equal(
            Convert.ToBase64String(Encoding.UTF8.GetBytes("user@example.com:secret")),
            request.Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task GetIssueAsync_HandlesPlainTextDescription()
    {
        var service = CreateService(OkHandler("\"Just plain text.\""));

        var issue = await service.GetIssueAsync(ValidUrl, CancellationToken.None);

        Assert.Equal("Just plain text.", issue.Description);
    }

    [Fact]
    public async Task GetIssueAsync_HandlesNullDescription()
    {
        var service = CreateService(OkHandler("null"));

        var issue = await service.GetIssueAsync(ValidUrl, CancellationToken.None);

        Assert.Equal(string.Empty, issue.Description);
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("/browse/ARKIK-1")]
    public async Task GetIssueAsync_Throws_ForInvalidUrl(string url)
    {
        var service = CreateService(OkHandler());

        await Assert.ThrowsAsync<ArgumentException>(() => service.GetIssueAsync(url, CancellationToken.None));
    }

    [Fact]
    public async Task GetIssueAsync_Throws_WhenUrlHasNoIssueKey()
    {
        var service = CreateService(OkHandler());

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetIssueAsync("https://example.atlassian.net/jira/dashboards", CancellationToken.None));

        Assert.Contains("issue key", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetIssueAsync_Throws_WhenCredentialsMissing()
    {
        var handler = OkHandler();
        var service = CreateService(handler, email: string.Empty, token: string.Empty);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.GetIssueAsync(ValidUrl, CancellationToken.None));

        Assert.Contains("Jira credentials are not configured", ex.Message);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task GetIssueAsync_ExplainsThat404MayMeanNoPermission()
    {
        var service = CreateService(new StubHttpMessageHandler(
            HttpStatusCode.NotFound,
            """{"errorMessages":["Issue does not exist or you do not have permission to see it."]}"""));

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => service.GetIssueAsync(ValidUrl, CancellationToken.None));

        Assert.Contains("404", ex.Message);
        Assert.Contains("browse permission", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetIssueAsync_ExplainsAuthenticationFailure()
    {
        var service = CreateService(new StubHttpMessageHandler(HttpStatusCode.Unauthorized, "{}"));

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => service.GetIssueAsync(ValidUrl, CancellationToken.None));

        Assert.Contains("Authentication failed", ex.Message);
    }

    [Fact]
    public async Task GetIssueAsync_MarksHeadingsWithHashes()
    {
        const string adf = """
            {"type":"doc","content":[
              {"type":"heading","attrs":{"level":3},"content":[{"type":"text","text":"Key Features"}]},
              {"type":"paragraph","content":[{"type":"text","text":"Body text."}]}
            ]}
            """;
        var service = CreateService(OkHandler(adf));

        var issue = await service.GetIssueAsync(ValidUrl, CancellationToken.None);

        Assert.Contains("### Key Features", issue.Description);
        Assert.Contains("Body text.", issue.Description);
    }

    [Fact]
    public async Task GetIssueAsync_MarksBulletsAndNestedBulletsOnTheirOwnLines()
    {
        const string adf = """
            {"type":"doc","content":[
              {"type":"bulletList","content":[
                {"type":"listItem","content":[
                  {"type":"paragraph","content":[{"type":"text","text":"Location in UI:"}]},
                  {"type":"bulletList","content":[
                    {"type":"listItem","content":[{"type":"paragraph","content":[{"type":"text","text":"Web tab"}]}]}
                  ]}
                ]}
              ]}
            ]}
            """;
        var service = CreateService(OkHandler(adf));

        var issue = await service.GetIssueAsync(ValidUrl, CancellationToken.None);
        var lines = issue.Description.Split(Environment.NewLine);

        Assert.Contains("- Location in UI:", lines);
        Assert.Contains("  - Web tab", lines);
    }

    [Fact]
    public async Task GetIssueAsync_CollapsesRepeatedBlankLines()
    {
        const string adf = """
            {"type":"doc","content":[
              {"type":"paragraph","content":[{"type":"text","text":"First"}]},
              {"type":"paragraph"},
              {"type":"paragraph"},
              {"type":"paragraph","content":[{"type":"text","text":"Second"}]}
            ]}
            """;
        var service = CreateService(OkHandler(adf));

        var issue = await service.GetIssueAsync(ValidUrl, CancellationToken.None);

        Assert.DoesNotContain(
            Environment.NewLine + Environment.NewLine + Environment.NewLine,
            issue.Description);
    }

    [Fact]
    public async Task GetIssueAsync_ConvertsHardBreaksToNewLines()
    {
        const string adf = """
            {"type":"doc","content":[
              {"type":"paragraph","content":[
                {"type":"text","text":"Line one"},
                {"type":"hardBreak"},
                {"type":"text","text":"Line two"}
              ]}
            ]}
            """;
        var service = CreateService(OkHandler(adf));

        var issue = await service.GetIssueAsync(ValidUrl, CancellationToken.None);
        var lines = issue.Description.Split(Environment.NewLine);

        Assert.Contains("Line one", lines);
        Assert.Contains("Line two", lines);
    }

    [Fact]
    public async Task GetCurrentUserAsync_ReturnsAccount_WhenTokenValid()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK,
            """{"displayName":"Husni Salax","emailAddress":"husni@ext.cemex.com","avatarUrls":{"24x24":"https://avatar/24"}}""");
        var service = CreateService(handler);

        var account = await service.GetCurrentUserAsync(CancellationToken.None);

        Assert.True(account.IsConnected);
        Assert.Equal("Husni Salax", account.DisplayName);
        Assert.Equal("https://avatar/24", account.AvatarUrl);
        Assert.Equal("https://example.atlassian.net/rest/api/3/myself", handler.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async Task GetCurrentUserAsync_ReportsError_WhenCredentialsMissing()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "{}");
        var service = CreateService(handler, email: string.Empty, token: string.Empty);

        var account = await service.GetCurrentUserAsync(CancellationToken.None);

        Assert.False(account.IsConnected);
        Assert.Contains("not configured", account.Error);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task GetCurrentUserAsync_ReportsError_WhenTokenRejected()
    {
        var service = CreateService(new StubHttpMessageHandler(HttpStatusCode.Unauthorized, "{}"));

        var account = await service.GetCurrentUserAsync(CancellationToken.None);

        Assert.False(account.IsConnected);
        Assert.Contains("401", account.Error);
    }
}

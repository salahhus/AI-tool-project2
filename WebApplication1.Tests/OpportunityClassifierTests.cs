using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WebApplication1.Models;
using WebApplication1.Services;

namespace WebApplication1.Tests;

public class OpportunityClassifierTests
{
    private static OpportunityClassifier CreateClassifier(StubHttpMessageHandler handler, string apiKey)
    {
        var options = Options.Create(new AiOptions
        {
            Endpoint = "https://stub.local/chat",
            ApiKey = apiKey,
            Model = "test-model"
        });

        return new OpportunityClassifier(
            new HttpClient(handler),
            options,
            NullLogger<OpportunityClassifier>.Instance);
    }

    private static string AiResponse(string json) =>
        "{\"choices\":[{\"message\":{\"content\":" + System.Text.Json.JsonSerializer.Serialize(json) + "}}]}";

    private static JiraIssue Issue(string summary, string description) =>
        new() { Key = "ABC-1", Summary = summary, Description = description };

    [Fact]
    public async Task ClassifyAsync_UsesKeywordFallback_WhenApiKeyMissing()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "{}");
        var classifier = CreateClassifier(handler, apiKey: string.Empty);

        var result = await classifier.ClassifyAsync(
            Issue("Slow page", "The dashboard is slow and needs optimization for performance."),
            CancellationToken.None);

        Assert.Equal("keyword-fallback", result.Source);
        Assert.Equal("Performance and technical improvements", result.Opportunity);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task ClassifyAsync_ReturnsDefaultOpportunity_WhenNoKeywordMatches()
    {
        var classifier = CreateClassifier(new StubHttpMessageHandler(HttpStatusCode.OK, "{}"), string.Empty);

        var result = await classifier.ClassifyAsync(Issue("zzz", "qqq"), CancellationToken.None);

        Assert.Equal("Understanding operational needs", result.Opportunity);
        Assert.Equal(0.2, result.Confidence);
    }

    [Fact]
    public async Task ClassifyAsync_UsesAi_WhenApiKeyConfigured()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, AiResponse(
            """{"opportunity":"Backlog prioritization","confidence":0.91,"reason":"Refers to grooming.","highlight":"reorder the backlog"}"""));
        var classifier = CreateClassifier(handler, "key-123");

        var result = await classifier.ClassifyAsync(
            Issue("Planning", "We must reorder the backlog before the next sprint."),
            CancellationToken.None);

        Assert.Equal("ai", result.Source);
        Assert.Equal("Backlog prioritization", result.Opportunity);
        Assert.Equal(0.91, result.Confidence);
        Assert.Contains("==reorder the backlog==", result.HighlightedText);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
        Assert.Equal("key-123", request.Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task ClassifyAsync_FallsBackToKeywords_WhenAiReturnsUnknownOpportunity()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, AiResponse(
            """{"opportunity":"Something invented","confidence":0.9,"reason":"n/a","highlight":"n/a"}"""));
        var classifier = CreateClassifier(handler, "key-123");

        var result = await classifier.ClassifyAsync(
            Issue("Tests", "We need more unit test coverage."),
            CancellationToken.None);

        Assert.Equal("keyword-fallback", result.Source);
        Assert.Equal("Unit and end-to-end test coverage", result.Opportunity);
    }

    [Fact]
    public async Task ClassifyAsync_FallsBackToKeywords_WhenAiRequestFails()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.TooManyRequests, """{"error":"rate limited"}""");
        var classifier = CreateClassifier(handler, "key-123");

        var result = await classifier.ClassifyAsync(
            Issue("Monitoring", "We need better alerting and observability."),
            CancellationToken.None);

        Assert.Equal("keyword-fallback", result.Source);
        Assert.Equal("Proactive monitoring and support", result.Opportunity);
    }

    [Fact]
    public async Task ExplainAsync_HonorsUserSelectedOpportunity()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, AiResponse(
            """{"confidence":0.35,"reason":"Weak link.","highlight":"legacy module"}"""));
        var classifier = CreateClassifier(handler, "key-123");

        var result = await classifier.ExplainAsync(
            Issue("Cleanup", "The legacy module should be replaced."),
            "Growing technical debt",
            CancellationToken.None);

        Assert.Equal("Growing technical debt", result.Opportunity);
        Assert.Equal(0.35, result.Confidence);
        Assert.Contains("==legacy module==", result.HighlightedText);
    }

    [Fact]
    public async Task ExplainAsync_ReportsWeakLink_WhenNoKeywordMatches()
    {
        var classifier = CreateClassifier(new StubHttpMessageHandler(HttpStatusCode.OK, "{}"), string.Empty);

        var result = await classifier.ExplainAsync(
            Issue("Action log", "Show customers what happened to their order."),
            "Growing technical debt",
            CancellationToken.None);

        Assert.Equal("Growing technical debt", result.Opportunity);
        Assert.Equal(0.1, result.Confidence);
        Assert.Contains("weak", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExplainAsync_Throws_ForUnknownOpportunity()
    {
        var classifier = CreateClassifier(new StubHttpMessageHandler(HttpStatusCode.OK, "{}"), string.Empty);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            classifier.ExplainAsync(Issue("a", "b"), "Not an Arkik opportunity", CancellationToken.None));
    }

    [Fact]
    public async Task ExplainAsync_IsCaseInsensitive_ForOpportunityName()
    {
        var classifier = CreateClassifier(new StubHttpMessageHandler(HttpStatusCode.OK, "{}"), string.Empty);

        var result = await classifier.ExplainAsync(
            Issue("Bug", "A regression was found in production."),
            "internal BUG prevention",
            CancellationToken.None);

        Assert.Equal("Internal bug prevention", result.Opportunity);
    }

    [Fact]
    public async Task ClassifyAsync_AlwaysReturnsKnownOpportunity()
    {
        var classifier = CreateClassifier(new StubHttpMessageHandler(HttpStatusCode.OK, "{}"), string.Empty);

        var result = await classifier.ClassifyAsync(Issue(string.Empty, string.Empty), CancellationToken.None);

        Assert.Contains(result.Opportunity, ArkikOpportunities.All);
    }
}

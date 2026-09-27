using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using WebApplication1.Controllers;
using WebApplication1.Models;
using WebApplication1.Services;

namespace WebApplication1.Tests;

public class JiraOpportunityControllerTests
{
    private sealed class FakeJiraService : IJiraService
    {
        public Exception? ThrowOnGet { get; init; }

        public JiraIssue Issue { get; init; } = new() { Key = "ARKIK-1", Summary = "S", Description = "D" };

        public Task<JiraIssue> GetIssueAsync(string jiraUrl, CancellationToken cancellationToken)
            => ThrowOnGet is not null ? Task.FromException<JiraIssue>(ThrowOnGet) : Task.FromResult(Issue);

        public Task<JiraAccount> GetCurrentUserAsync(CancellationToken cancellationToken)
            => Task.FromResult(new JiraAccount { IsConnected = true, DisplayName = "Test User" });
    }

    private sealed class FakeClassifier : IOpportunityClassifier
    {
        public string? LastOpportunity { get; private set; }

        public Task<ClassifyResponse> ClassifyAsync(JiraIssue issue, CancellationToken cancellationToken)
            => Task.FromResult(new ClassifyResponse { Opportunity = "Backlog prioritization", Source = "ai" });

        public Task<ClassifyResponse> ExplainAsync(JiraIssue issue, string opportunity, CancellationToken cancellationToken)
        {
            LastOpportunity = opportunity;
            return Task.FromResult(new ClassifyResponse { Opportunity = opportunity, Source = "ai" });
        }
    }

    private static JiraOpportunityController CreateController(IJiraService jira, IOpportunityClassifier classifier)
        => new(jira, classifier);

    [Fact]
    public async Task Classify_ReturnsResultEnrichedWithIssueFields()
    {
        var controller = CreateController(new FakeJiraService(), new FakeClassifier());

        var action = await controller.Classify(new ClassifyRequest { JiraUrl = "https://x.atlassian.net/browse/ARKIK-1" }, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(action.Result);
        var body = Assert.IsType<ClassifyResponse>(ok.Value);
        Assert.Equal("ARKIK-1", body.IssueKey);
        Assert.Equal("S", body.Summary);
        Assert.Equal("Backlog prioritization", body.Opportunity);
    }

    [Fact]
    public async Task Classify_ReturnsBadRequest_WhenUrlMissing()
    {
        var controller = CreateController(new FakeJiraService(), new FakeClassifier());

        var action = await controller.Classify(new ClassifyRequest { JiraUrl = "  " }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(action.Result);
    }

    [Fact]
    public async Task Classify_ReturnsBadGateway_WhenJiraCallFails()
    {
        var controller = CreateController(
            new FakeJiraService { ThrowOnGet = new HttpRequestException("boom") },
            new FakeClassifier());

        var action = await controller.Classify(new ClassifyRequest { JiraUrl = "https://x.atlassian.net/browse/ARKIK-1" }, CancellationToken.None);

        var status = Assert.IsType<ObjectResult>(action.Result);
        Assert.Equal(StatusCodes.Status502BadGateway, status.StatusCode);
    }

    [Fact]
    public async Task Explain_PassesSelectedOpportunityToClassifier()
    {
        var classifier = new FakeClassifier();
        var controller = CreateController(new FakeJiraService(), classifier);

        var action = await controller.Explain(
            new ExplainRequest { JiraUrl = "https://x.atlassian.net/browse/ARKIK-1", Opportunity = "Growing technical debt" },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(action.Result);
        var body = Assert.IsType<ClassifyResponse>(ok.Value);
        Assert.Equal("Growing technical debt", classifier.LastOpportunity);
        Assert.Equal("Growing technical debt", body.Opportunity);
    }

    [Fact]
    public async Task Explain_ReturnsBadRequest_WhenOpportunityMissing()
    {
        var controller = CreateController(new FakeJiraService(), new FakeClassifier());

        var action = await controller.Explain(
            new ExplainRequest { JiraUrl = "https://x.atlassian.net/browse/ARKIK-1", Opportunity = "" },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(action.Result);
    }

    [Fact]
    public async Task ClassifyText_WorksWithoutJira()
    {
        var controller = CreateController(
            new FakeJiraService { ThrowOnGet = new HttpRequestException("should not be called") },
            new FakeClassifier());

        var action = await controller.ClassifyText(
            new ClassifyTextRequest { Summary = "Dispatch", Description = "Operations need visibility." },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(action.Result);
        var body = Assert.IsType<ClassifyResponse>(ok.Value);
        Assert.Equal("MANUAL", body.IssueKey);
    }

    [Fact]
    public async Task ClassifyText_ReturnsBadRequest_WhenTextEmpty()
    {
        var controller = CreateController(new FakeJiraService(), new FakeClassifier());

        var action = await controller.ClassifyText(new ClassifyTextRequest(), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(action.Result);
    }

    [Fact]
    public void GetOpportunities_ReturnsTwelveOpportunities()
    {
        var controller = CreateController(new FakeJiraService(), new FakeClassifier());

        var ok = Assert.IsType<OkObjectResult>(controller.GetOpportunities().Result);
        var values = Assert.IsAssignableFrom<IEnumerable<string>>(ok.Value);

        Assert.Equal(12, values.Count());
        Assert.Contains("Understanding operational needs", values);
    }
}

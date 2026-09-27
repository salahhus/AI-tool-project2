using Microsoft.AspNetCore.Mvc;
using WebApplication1.Models;
using WebApplication1.Services;

namespace WebApplication1.Controllers;

[ApiController]
[Route("api/[controller]")]
public class JiraOpportunityController : ControllerBase
{
    private readonly IJiraService _jiraService;
    private readonly IOpportunityClassifier _classifier;

    public JiraOpportunityController(IJiraService jiraService, IOpportunityClassifier classifier)
    {
        _jiraService = jiraService;
        _classifier = classifier;
    }

    /// <summary>Returns the Arkik opportunity that best matches the Jira ticket description.</summary>
    [HttpPost("classify")]
    public async Task<ActionResult<ClassifyResponse>> Classify([FromBody] ClassifyRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.JiraUrl))
        {
            return BadRequest("jiraUrl is required.");
        }

        JiraIssue issue;
        try
        {
            issue = await _jiraService.GetIssueAsync(request.JiraUrl, cancellationToken);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, ex.Message);
        }

        var result = await _classifier.ClassifyAsync(issue, cancellationToken);
        result.IssueKey = issue.Key;
        result.Summary = issue.Summary;
        result.Description = issue.Description;

        return Ok(result);
    }

    /// <summary>Lists the available Arkik opportunities.</summary>
    [HttpGet("opportunities")]
    public ActionResult<IEnumerable<string>> GetOpportunities() => Ok(ArkikOpportunities.All);

    /// <summary>Re-generates the justification and highlight for a user-selected opportunity.</summary>
    [HttpPost("explain")]
    public async Task<ActionResult<ClassifyResponse>> Explain([FromBody] ExplainRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.JiraUrl) || string.IsNullOrWhiteSpace(request.Opportunity))
        {
            return BadRequest("jiraUrl and opportunity are required.");
        }

        JiraIssue issue;
        try
        {
            issue = await _jiraService.GetIssueAsync(request.JiraUrl, cancellationToken);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, ex.Message);
        }

        ClassifyResponse result;
        try
        {
            result = await _classifier.ExplainAsync(issue, request.Opportunity, cancellationToken);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }

        result.IssueKey = issue.Key;
        result.Summary = issue.Summary;
        result.Description = issue.Description;

        return Ok(result);
    }

    /// <summary>
    /// Classifies pasted ticket text. Use this when a Jira API token is not available
    /// (for example when the Atlassian org disables API tokens for managed accounts).
    /// </summary>
    [HttpPost("classify-text")]
    public async Task<ActionResult<ClassifyResponse>> ClassifyText([FromBody] ClassifyTextRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Summary) && string.IsNullOrWhiteSpace(request.Description))
        {
            return BadRequest("summary or description is required.");
        }

        var issue = new JiraIssue
        {
            Key = "MANUAL",
            Summary = request.Summary,
            Description = request.Description
        };

        var result = await _classifier.ClassifyAsync(issue, cancellationToken);
        result.IssueKey = issue.Key;
        result.Summary = issue.Summary;
        result.Description = issue.Description;

        return Ok(result);
    }
}

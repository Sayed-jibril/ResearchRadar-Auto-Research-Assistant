using Microsoft.AspNetCore.Mvc;
using ScholarLens.Api.DTOs;
using ScholarLens.Api.Services;
using System.ComponentModel.DataAnnotations;

namespace ScholarLens.Api.Controllers.Api;

/// <summary>
/// API controller for paper summarization functionality
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class SummarizeController : ControllerBase
{
    private readonly SummarizeService _summarizeService;
    private readonly ILogger<SummarizeController> _logger;

    public SummarizeController(SummarizeService summarizeService, ILogger<SummarizeController> logger)
    {
        _summarizeService = summarizeService;
        _logger = logger;
    }

    /// <summary>
    /// Generate AI summaries for selected papers
    /// </summary>
    /// <param name="request">Summarization parameters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Structured summaries for each paper</returns>
    /// <response code="200">Returns summarization results</response>
    /// <response code="400">Invalid request parameters</response>
    /// <response code="500">Internal server error</response>
    [HttpPost]
    [ProducesResponseType(typeof(SummarizeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<SummarizeResponse>> SummarizeAsync(
        [FromBody] SummarizeRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Summarize request received for {Count} papers", request.PaperIds.Count);

            // Validate request
            if (request.PaperIds.Count > 20)
            {
                return BadRequest(new ProblemDetails
                {
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                    Title = "Too many papers",
                    Detail = "Maximum 20 papers can be summarized at once",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            if (!new[] { "technical", "executive" }.Contains(request.SummaryStyle))
            {
                return BadRequest(new ProblemDetails
                {
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                    Title = "Invalid summary style",
                    Detail = "Summary style must be 'technical' or 'executive'",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            if (!new[] { "en", "zh" }.Contains(request.Language))
            {
                return BadRequest(new ProblemDetails
                {
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                    Title = "Unsupported language",
                    Detail = "Currently only 'en' (English) and 'zh' (Chinese) are supported",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            var response = await _summarizeService.SummarizePapersAsync(request, cancellationToken);

            _logger.LogInformation("Summarization completed: {Success} successful, {Failed} failed", 
                response.Metadata.SuccessCount, response.Metadata.FailedCount);

            return Ok(response);
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning(ex, "Validation error in summarize request");
            return BadRequest(new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                Title = "Validation error",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing summarize request");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
                Title = "Internal server error",
                Detail = "An error occurred while processing the summarization request",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Get summarization service health status
    /// </summary>
    /// <returns>Service health information</returns>
    [HttpGet("health")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetHealthAsync()
    {
        var isHealthy = await _summarizeService.HealthCheckAsync();
        
        return Ok(new
        {
            Service = "SummarizeController",
            Status = isHealthy ? "Healthy" : "Degraded",
            NlpServiceConnected = isHealthy,
            Timestamp = DateTime.UtcNow,
            Version = "1.0.0"
        });
    }
}

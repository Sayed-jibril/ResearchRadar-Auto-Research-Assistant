using Microsoft.AspNetCore.Mvc;
using ScholarLens.Api.DTOs;
using ScholarLens.Api.Services;
using System.ComponentModel.DataAnnotations;

namespace ScholarLens.Api.Controllers.Api;

/// <summary>
/// API controller for paper text ingestion functionality
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class IngestController : ControllerBase
{
    private readonly IngestService _ingestService;
    private readonly ILogger<IngestController> _logger;

    public IngestController(IngestService ingestService, ILogger<IngestController> logger)
    {
        _ingestService = ingestService;
        _logger = logger;
    }

    /// <summary>
    /// Extract text from PDFs of selected papers
    /// </summary>
    /// <param name="request">List of paper IDs to process</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Ingestion results with status per paper</returns>
    /// <response code="200">Returns ingestion results</response>
    /// <response code="400">Invalid request parameters</response>
    /// <response code="500">Internal server error</response>
    [HttpPost]
    [ProducesResponseType(typeof(IngestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IngestResponse>> IngestAsync(
        [FromBody] IngestRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Ingest request received for {Count} papers", request.PaperIds.Count);

            // Validate request
            if (request.PaperIds.Count > 50)
            {
                return BadRequest(new ProblemDetails
                {
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                    Title = "Too many papers",
                    Detail = "Maximum 50 papers can be processed at once",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            var response = await _ingestService.IngestPapersAsync(request, cancellationToken);

            _logger.LogInformation("Ingest completed: {Success} successful, {Failed} failed", 
                response.Summary.SuccessCount, response.Summary.FailedCount);

            return Ok(response);
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning(ex, "Validation error in ingest request");
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
            _logger.LogError(ex, "Error processing ingest request");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
                Title = "Internal server error",
                Detail = "An error occurred while processing the ingestion request",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Get ingestion service health status
    /// </summary>
    /// <returns>Service health information</returns>
    [HttpGet("health")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetHealthAsync()
    {
        var isHealthy = await _ingestService.HealthCheckAsync();
        
        return Ok(new
        {
            Service = "IngestController",
            Status = isHealthy ? "Healthy" : "Degraded",
            NlpServiceConnected = isHealthy,
            Timestamp = DateTime.UtcNow,
            Version = "1.0.0"
        });
    }
}

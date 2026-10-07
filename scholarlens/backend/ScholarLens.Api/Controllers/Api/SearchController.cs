using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Cors;
using ScholarLens.Api.DTOs;
using ScholarLens.Api.Services;
using System.ComponentModel.DataAnnotations;

namespace ScholarLens.Api.Controllers.Api;

/// <summary>
/// API controller for academic paper search functionality
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[EnableCors("AllowFrontend")]
public class SearchController : ControllerBase
{
    private readonly SearchService _searchService;
    private readonly ILogger<SearchController> _logger;

    public SearchController(SearchService searchService, ILogger<SearchController> logger)
    {
        _searchService = searchService;
        _logger = logger;
    }

    // CORS preflight requests are now handled by ASP.NET Core CORS middleware

    /// <summary>
    /// Simple test endpoint for debugging
    /// </summary>
    [HttpPost("test")]
    public ActionResult<object> TestSearch([FromBody] object request)
    {
        _logger.LogInformation("Test search endpoint called");
        return Ok(new { 
            message = "Search endpoint is working", 
            timestamp = DateTime.UtcNow,
            request = request 
        });
    }

    /// <summary>
    /// Search for academic papers across multiple sources
    /// </summary>
    /// <param name="request">Search parameters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Search results with metadata</returns>
    /// <response code="200">Returns search results</response>
    /// <response code="400">Invalid search parameters</response>
    /// <response code="500">Internal server error</response>
    [HttpPost]
    [ProducesResponseType(typeof(SearchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<SearchResponse>> SearchAsync(
        [FromBody] SearchRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Search request received: {Query}", request.Query);

            // Validate year range
            if (request.YearFrom.HasValue && request.YearTo.HasValue && 
                request.YearFrom > request.YearTo)
            {
                return BadRequest(new ProblemDetails
                {
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                    Title = "Invalid year range",
                    Detail = "YearFrom cannot be greater than YearTo",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            // Validate years are reasonable
            var currentYear = DateTime.Now.Year;
            if (request.YearFrom.HasValue && (request.YearFrom < 1900 || request.YearFrom > currentYear + 1))
            {
                return BadRequest(new ProblemDetails
                {
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                    Title = "Invalid year from",
                    Detail = $"YearFrom must be between 1900 and {currentYear + 1}",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            if (request.YearTo.HasValue && (request.YearTo < 1900 || request.YearTo > currentYear + 1))
            {
                return BadRequest(new ProblemDetails
                {
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                    Title = "Invalid year to",
                    Detail = $"YearTo must be between 1900 and {currentYear + 1}",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            // Call the real search service to get actual papers from external APIs
            var response = await _searchService.SearchAsync(request, cancellationToken);

            _logger.LogInformation("Search completed: {ResultCount} results for query '{Query}'", 
                response.Results.Count, request.Query);

            return Ok(response);
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning(ex, "Validation error in search request");
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
            _logger.LogError(ex, "Error processing search request for query: {Query}", request.Query);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
                Title = "Internal server error",
                Detail = "An error occurred while processing the search request",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Get search statistics and health information
    /// </summary>
    /// <returns>Search service status</returns>
    [HttpGet("status")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public ActionResult GetStatus()
    {
        return Ok(new
        {
            Service = "SearchController",
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            Version = "1.0.0"
        });
    }
}

using Microsoft.AspNetCore.Mvc;
using ScholarLens.Api.DTOs;
using ScholarLens.Api.Services;
using System.ComponentModel.DataAnnotations;

namespace ScholarLens.Api.Controllers.Api;

/// <summary>
/// API controller for report generation functionality
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ReportController : ControllerBase
{
    private readonly ReportService _reportService;
    private readonly ILogger<ReportController> _logger;

    public ReportController(ReportService reportService, ILogger<ReportController> logger)
    {
        _reportService = reportService;
        _logger = logger;
    }

    /// <summary>
    /// Generate a comprehensive research report
    /// </summary>
    /// <param name="request">Report generation parameters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Report metadata with download URLs</returns>
    /// <response code="200">Returns report generation result</response>
    /// <response code="400">Invalid request parameters</response>
    /// <response code="500">Internal server error</response>
    [HttpPost]
    [ProducesResponseType(typeof(ReportResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ReportResponse>> GenerateReportAsync(
        [FromBody] ReportRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Report generation request received for topic: {Topic}", request.Topic);

            // Validate request
            if (request.PaperIds != null && request.PaperIds.Count > 100)
            {
                return BadRequest(new ProblemDetails
                {
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                    Title = "Too many papers",
                    Detail = "Maximum 100 papers can be included in a report",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            if (request.K > 50)
            {
                return BadRequest(new ProblemDetails
                {
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                    Title = "Invalid K value",
                    Detail = "K must be 50 or less when auto-selecting papers",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            // Validate sections
            var validSections = new[] { "cover", "executive", "overview", "methodology", 
                "key-findings", "comparison", "charts", "per-paper", "gaps", "references" };
            
            var invalidSections = request.Sections.Except(validSections).ToList();
            if (invalidSections.Any())
            {
                return BadRequest(new ProblemDetails
                {
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                    Title = "Invalid sections",
                    Detail = $"Invalid sections: {string.Join(", ", invalidSections)}",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            // Validate charts
            var validCharts = new[] { "by-year", "oa-vs-paywalled", "source-breakdown" };
            var invalidCharts = request.Charts.Except(validCharts).ToList();
            if (invalidCharts.Any())
            {
                return BadRequest(new ProblemDetails
                {
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                    Title = "Invalid charts",
                    Detail = $"Invalid charts: {string.Join(", ", invalidCharts)}",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            var response = await _reportService.GenerateReportAsync(request, cancellationToken);

            _logger.LogInformation("Report generated successfully: {ReportId} with {PaperCount} papers", 
                response.ReportId, response.Metadata.PapersIncluded);

            return Ok(response);
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning(ex, "Validation error in report request");
            return BadRequest(new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                Title = "Validation error",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation in report generation");
            return BadRequest(new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                Title = "Invalid operation",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing report request for topic: {Topic}", request.Topic);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
                Title = "Internal server error",
                Detail = "An error occurred while generating the report",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Get report metadata by ID
    /// </summary>
    /// <param name="id">Report ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Report metadata</returns>
    /// <response code="200">Returns report metadata</response>
    /// <response code="404">Report not found</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetReportAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var report = await _reportService.GetReportAsync(id, cancellationToken);
            
            if (report == null)
            {
                return NotFound(new ProblemDetails
                {
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.5.4",
                    Title = "Report not found",
                    Detail = $"Report with ID {id} was not found",
                    Status = StatusCodes.Status404NotFound
                });
            }

            return Ok(new
            {
                Id = report.Id,
                Topic = report.Topic?.Query,
                Status = report.Status,
                CreatedAt = report.CreatedAt,
                PdfUrl = !string.IsNullOrEmpty(report.PdfPath) ? $"/api/report/{id}.pdf" : null,
                HtmlUrl = !string.IsNullOrEmpty(report.HtmlPath) ? $"/api/report/{id}.html" : null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving report {ReportId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
                Title = "Internal server error",
                Detail = "An error occurred while retrieving the report",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Download report PDF
    /// </summary>
    /// <param name="id">Report ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>PDF file</returns>
    /// <response code="200">Returns PDF file</response>
    /// <response code="404">Report or PDF not found</response>
    [HttpGet("{id}.pdf")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DownloadReportPdfAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var pdfBytes = await _reportService.GetReportPdfAsync(id, cancellationToken);
            
            return File(pdfBytes, "application/pdf", $"research_report_{id}.pdf");
        }
        catch (FileNotFoundException ex)
        {
            _logger.LogWarning(ex, "Report PDF not found for ID {ReportId}", id);
            return NotFound(new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.4",
                Title = "Report PDF not found",
                Detail = ex.Message,
                Status = StatusCodes.Status404NotFound
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading report PDF {ReportId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
                Title = "Internal server error",
                Detail = "An error occurred while downloading the report",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Get report generation service status
    /// </summary>
    /// <returns>Service health information</returns>
    [HttpGet("status")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public ActionResult GetStatus()
    {
        return Ok(new
        {
            Service = "ReportController",
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            Version = "1.0.0",
            SupportedSections = new[] { "cover", "executive", "overview", "methodology", 
                "key-findings", "comparison", "charts", "per-paper", "gaps", "references" },
            SupportedCharts = new[] { "by-year", "oa-vs-paywalled", "source-breakdown" }
        });
    }
}

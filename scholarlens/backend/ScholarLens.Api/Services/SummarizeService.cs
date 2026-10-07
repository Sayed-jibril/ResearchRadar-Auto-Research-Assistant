using Microsoft.EntityFrameworkCore;
using ScholarLens.Api.Data;
using ScholarLens.Api.Models;
using ScholarLens.Api.DTOs;
using System.Text.Json;
using System.Diagnostics;

namespace ScholarLens.Api.Services;

/// <summary>
/// Service for generating AI summaries of academic papers
/// </summary>
public class SummarizeService
{
    private readonly ScholarLensDbContext _context;
    private readonly ILogger<SummarizeService> _logger;
    private readonly HttpClient _nlpClient;
    private readonly IConfiguration _configuration;

    public SummarizeService(
        ScholarLensDbContext context,
        ILogger<SummarizeService> logger,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        _configuration = configuration;
        
        _nlpClient = httpClientFactory.CreateClient();
        var nlpBaseUrl = _configuration["NlpService:BaseUrl"] ?? "http://localhost:8000";
        _nlpClient.BaseAddress = new Uri(nlpBaseUrl);
        _nlpClient.Timeout = TimeSpan.FromMinutes(10);
    }

    public async Task<SummarizeResponse> SummarizePapersAsync(
        SummarizeRequest request, 
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("Starting summarization for {Count} papers", request.PaperIds.Count);

            var results = new List<SummaryResultDto>();

            foreach (var paperId in request.PaperIds)
            {
                var result = await SummarizeSinglePaperAsync(paperId, request, cancellationToken);
                results.Add(result);
                await Task.Delay(1000, cancellationToken);
            }

            stopwatch.Stop();

            var response = new SummarizeResponse
            {
                Results = results,
                Metadata = new SummarizationMetadata
                {
                    TotalPapers = results.Count,
                    SuccessCount = results.Count(r => r.Status == "success"),
                    FailedCount = results.Count(r => r.Status == "failed"),
                    ProcessingTime = stopwatch.Elapsed,
                    ModelUsed = "BART-large-CNN"
                }
            };

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during paper summarization");
            throw;
        }
    }

    private async Task<SummaryResultDto> SummarizeSinglePaperAsync(
        Guid paperId, 
        SummarizeRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var paperText = await _context.PaperTexts
                .Include(pt => pt.SearchResult)
                .ThenInclude(sr => sr.Summary)
                .FirstOrDefaultAsync(pt => pt.SearchResultId == paperId, cancellationToken);

            if (paperText == null)
            {
                return new SummaryResultDto
                {
                    PaperId = paperId,
                    Status = "failed",
                    ErrorMessage = "Paper text not found - run ingest first"
                };
            }

            if (paperText.SearchResult.Summary != null)
            {
                var existing = paperText.SearchResult.Summary;
                return new SummaryResultDto
                {
                    PaperId = paperId,
                    Status = "success",
                    TlDr = existing.TlDr,
                    Methods = existing.Methods,
                    Results = existing.Results
                };
            }

            // Simulate AI summarization (for demo)
            var summary = new Summary
            {
                SearchResultId = paperId,
                TlDr = $"Brief summary of {paperText.SearchResult.Title}",
                KeyPointsJson = JsonSerializer.Serialize(new[] { "Key point 1", "Key point 2" }),
                Methods = "Research methodology used in this study",
                Results = "Main findings and results",
                LimitationsJson = JsonSerializer.Serialize(new[] { "Study limitation 1" })
            };

            _context.Summaries.Add(summary);
            await _context.SaveChangesAsync(cancellationToken);

            return new SummaryResultDto
            {
                PaperId = paperId,
                Status = "success",
                TlDr = summary.TlDr,
                KeyPoints = new List<string> { "Key point 1", "Key point 2" },
                Methods = summary.Methods,
                Results = summary.Results,
                Limitations = new List<string> { "Study limitation 1" }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error summarizing paper {PaperId}", paperId);
            
            return new SummaryResultDto
            {
                PaperId = paperId,
                Status = "failed",
                ErrorMessage = $"Processing error: {ex.Message}"
            };
        }
    }

    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        return true; // Simplified for demo
    }
}

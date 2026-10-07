using Microsoft.EntityFrameworkCore;
using ScholarLens.Api.Data;
using ScholarLens.Api.Models;
using ScholarLens.Api.DTOs;
using System.Text.Json;
using System.Diagnostics;

namespace ScholarLens.Api.Services;

/// <summary>
/// Service for ingesting PDF text from academic papers
/// </summary>
public class IngestService
{
    private readonly ScholarLensDbContext _context;
    private readonly ILogger<IngestService> _logger;
    private readonly HttpClient _nlpClient;
    private readonly IConfiguration _configuration;

    public IngestService(
        ScholarLensDbContext context,
        ILogger<IngestService> logger,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        _configuration = configuration;
        
        // Create HTTP client for NLP service
        _nlpClient = httpClientFactory.CreateClient();
        var nlpBaseUrl = _configuration["NlpService:BaseUrl"] ?? "http://localhost:8000";
        _nlpClient.BaseAddress = new Uri(nlpBaseUrl);
        _nlpClient.Timeout = TimeSpan.FromMinutes(5);
    }

    public async Task<IngestResponse> IngestPapersAsync(
        IngestRequest request, 
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("Starting ingestion for {Count} papers", request.PaperIds.Count);

            var results = new List<IngestResultDto>();

            // Process papers in parallel batches
            const int batchSize = 3;
            var batches = request.PaperIds.Chunk(batchSize);

            foreach (var batch in batches)
            {
                var batchTasks = batch.Select(paperId => 
                    IngestSinglePaperAsync(paperId, cancellationToken)
                ).ToArray();

                var batchResults = await Task.WhenAll(batchTasks);
                results.AddRange(batchResults);

                await Task.Delay(500, cancellationToken);
            }

            stopwatch.Stop();

            var response = new IngestResponse
            {
                Results = results,
                Summary = new IngestSummary
                {
                    TotalPapers = results.Count,
                    SuccessCount = results.Count(r => r.Status == "success"),
                    SkippedCount = results.Count(r => r.Status == "skipped"),
                    FailedCount = results.Count(r => r.Status == "failed"),
                    ProcessingTime = stopwatch.Elapsed
                }
            };

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during paper ingestion");
            throw;
        }
    }

    private async Task<IngestResultDto> IngestSinglePaperAsync(
        Guid paperId, 
        CancellationToken cancellationToken)
    {
        try
        {
            var searchResult = await _context.SearchResults
                .Include(sr => sr.PaperText)
                .FirstOrDefaultAsync(sr => sr.Id == paperId, cancellationToken);

            if (searchResult == null)
            {
                return new IngestResultDto
                {
                    PaperId = paperId,
                    Status = "failed",
                    Reason = "Paper not found in database"
                };
            }

            if (searchResult.PaperText != null)
            {
                return new IngestResultDto
                {
                    PaperId = paperId,
                    Status = "skipped",
                    Reason = "Text already extracted"
                };
            }

            if (string.IsNullOrEmpty(searchResult.PdfUrl))
            {
                return new IngestResultDto
                {
                    PaperId = paperId,
                    Status = "skipped",
                    Reason = "No PDF URL available"
                };
            }

            if (!searchResult.IsOpenAccess)
            {
                return new IngestResultDto
                {
                    PaperId = paperId,
                    Status = "skipped",
                    Reason = "Not open access"
                };
            }

            // Simulate NLP service call (for demo)
            var paperText = new PaperText
            {
                SearchResultId = paperId,
                Text = $"Extracted text from: {searchResult.Title}",
                Tokens = 100
            };

            _context.PaperTexts.Add(paperText);
            await _context.SaveChangesAsync(cancellationToken);

            return new IngestResultDto
            {
                PaperId = paperId,
                Status = "success",
                TextLength = paperText.Text.Length,
                TokenCount = paperText.Tokens
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ingesting paper {PaperId}", paperId);
            
            return new IngestResultDto
            {
                PaperId = paperId,
                Status = "failed",
                Reason = $"Processing error: {ex.Message}"
            };
        }
    }

    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        return true; // Simplified for demo
    }
}

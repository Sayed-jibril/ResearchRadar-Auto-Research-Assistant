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
        _nlpClient.Timeout = TimeSpan.FromMinutes(5); // PDF processing can take time
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

            // Process papers in parallel batches to avoid overwhelming the NLP service
            const int batchSize = 3;
            var batches = request.PaperIds.Chunk(batchSize);

            foreach (var batch in batches)
            {
                var batchTasks = batch.Select(paperId => 
                    IngestSinglePaperAsync(paperId, cancellationToken)
                ).ToArray();

                var batchResults = await Task.WhenAll(batchTasks);
                results.AddRange(batchResults);

                // Small delay between batches to be respectful
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

            _logger.LogInformation("Ingestion completed: {Success} success, {Skipped} skipped, {Failed} failed", 
                response.Summary.SuccessCount, response.Summary.SkippedCount, response.Summary.FailedCount);

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
            // Get paper from database
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

            // Check if already ingested
            if (searchResult.PaperText != null)
            {
                return new IngestResultDto
                {
                    PaperId = paperId,
                    Status = "skipped",
                    Reason = "Text already extracted",
                    TextLength = searchResult.PaperText.Text.Length,
                    TokenCount = searchResult.PaperText.Tokens
                };
            }

            // Check if PDF URL is available
            if (string.IsNullOrEmpty(searchResult.PdfUrl))
            {
                return new IngestResultDto
                {
                    PaperId = paperId,
                    Status = "skipped",
                    Reason = "No PDF URL available"
                };
            }

            // Check if it's open access
            if (!searchResult.IsOpenAccess)
            {
                return new IngestResultDto
                {
                    PaperId = paperId,
                    Status = "skipped",
                    Reason = "Not open access - respecting copyright"
                };
            }

            _logger.LogDebug("Extracting text from PDF: {PdfUrl}", searchResult.PdfUrl);

            // Call NLP service to extract text
            var extractRequest = new { pdf_url = searchResult.PdfUrl };
            var requestJson = JsonSerializer.Serialize(extractRequest);
            var content = new StringContent(requestJson, System.Text.Encoding.UTF8, "application/json");

            var response = await _nlpClient.PostAsync("/api/v1/extract-text", content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("NLP service error for paper {PaperId}: {Error}", paperId, errorContent);
                
                return new IngestResultDto
                {
                    PaperId = paperId,
                    Status = "failed",
                    Reason = $"NLP service error: {response.StatusCode}"
                };
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var extractResponse = JsonSerializer.Deserialize<NlpExtractResponse>(responseJson);

            if (extractResponse == null || string.IsNullOrEmpty(extractResponse.Text))
            {
                return new IngestResultDto
                {
                    PaperId = paperId,
                    Status = "failed",
                    Reason = "Empty text extracted from PDF"
                };
            }

            // Calculate token count (rough estimation)
            var tokenCount = EstimateTokenCount(extractResponse.Text);

            // Save to database
            var paperText = new PaperText
            {
                SearchResultId = paperId,
                Text = extractResponse.Text,
                Tokens = tokenCount
            };

            _context.PaperTexts.Add(paperText);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogDebug("Successfully extracted {CharCount} characters from paper {PaperId}", 
                extractResponse.Chars, paperId);

            return new IngestResultDto
            {
                PaperId = paperId,
                Status = "success",
                TextLength = extractResponse.Chars,
                TokenCount = tokenCount
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

    private int EstimateTokenCount(string text)
    {
        // Rough estimation: 1 token ≈ 4 characters
        // This is a simplification - in production, use proper tokenizer
        return text.Length / 4;
    }

    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _nlpClient.GetAsync("/health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Response model from NLP service text extraction
/// </summary>
public class NlpExtractResponse
{
    public string Text { get; set; } = string.Empty;
    public int Chars { get; set; }
}

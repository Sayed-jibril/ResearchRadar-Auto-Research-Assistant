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
        
        // Create HTTP client for NLP service
        _nlpClient = httpClientFactory.CreateClient();
        var nlpBaseUrl = _configuration["NlpService:BaseUrl"] ?? "http://localhost:8000";
        _nlpClient.BaseAddress = new Uri(nlpBaseUrl);
        _nlpClient.Timeout = TimeSpan.FromMinutes(10); // Summarization can take time
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

            // Process papers in parallel batches
            const int batchSize = 2; // Smaller batches for summarization (more resource intensive)
            var batches = request.PaperIds.Chunk(batchSize);

            foreach (var batch in batches)
            {
                var batchTasks = batch.Select(paperId => 
                    SummarizeSinglePaperAsync(paperId, request, cancellationToken)
                ).ToArray();

                var batchResults = await Task.WhenAll(batchTasks);
                results.AddRange(batchResults);

                // Delay between batches for AI service
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
                    ModelUsed = "BART-large-CNN" // This should come from NLP service
                }
            };

            _logger.LogInformation("Summarization completed: {Success} success, {Failed} failed", 
                response.Metadata.SuccessCount, response.Metadata.FailedCount);

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
            // Get paper text from database
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

            // Check if already summarized
            if (paperText.SearchResult.Summary != null)
            {
                var existingSummary = paperText.SearchResult.Summary;
                var existingKeyPoints = new List<string>();
                var existingLimitations = new List<string>();

                try
                {
                    if (!string.IsNullOrEmpty(existingSummary.KeyPointsJson))
                        existingKeyPoints = JsonSerializer.Deserialize<List<string>>(existingSummary.KeyPointsJson) ?? new();
                    if (!string.IsNullOrEmpty(existingSummary.LimitationsJson))
                        existingLimitations = JsonSerializer.Deserialize<List<string>>(existingSummary.LimitationsJson) ?? new();
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Error deserializing existing summary JSON for paper {PaperId}", paperId);
                }

                return new SummaryResultDto
                {
                    PaperId = paperId,
                    Status = "success",
                    TlDr = existingSummary.TlDr,
                    KeyPoints = existingKeyPoints,
                    Methods = existingSummary.Methods,
                    Results = existingSummary.Results,
                    Limitations = existingLimitations
                };
            }

            _logger.LogDebug("Summarizing paper text: {TextLength} characters", paperText.Text.Length);

            // Call NLP service to generate summary
            var summarizeRequest = new
            {
                text = paperText.Text,
                style = request.SummaryStyle,
                lang = request.Language,
                max_tokens = request.MaxTokensPerPaper
            };

            var requestJson = JsonSerializer.Serialize(summarizeRequest);
            var content = new StringContent(requestJson, System.Text.Encoding.UTF8, "application/json");

            var response = await _nlpClient.PostAsync("/api/v1/summarize", content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("NLP service error for paper {PaperId}: {Error}", paperId, errorContent);
                
                return new SummaryResultDto
                {
                    PaperId = paperId,
                    Status = "failed",
                    ErrorMessage = $"NLP service error: {response.StatusCode}"
                };
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var nlpResponse = JsonSerializer.Deserialize<NlpSummarizeResponse>(responseJson);

            if (nlpResponse == null)
            {
                return new SummaryResultDto
                {
                    PaperId = paperId,
                    Status = "failed",
                    ErrorMessage = "Empty response from NLP service"
                };
            }

            // Save to database
            var summary = new Summary
            {
                SearchResultId = paperId,
                TlDr = nlpResponse.TlDr ?? string.Empty,
                KeyPointsJson = JsonSerializer.Serialize(nlpResponse.KeyPoints ?? new List<string>()),
                Methods = nlpResponse.Methods ?? string.Empty,
                Results = nlpResponse.Results ?? string.Empty,
                LimitationsJson = JsonSerializer.Serialize(nlpResponse.Limitations ?? new List<string>())
            };

            _context.Summaries.Add(summary);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogDebug("Successfully summarized paper {PaperId}", paperId);

            return new SummaryResultDto
            {
                PaperId = paperId,
                Status = "success",
                TlDr = summary.TlDr,
                KeyPoints = nlpResponse.KeyPoints ?? new List<string>(),
                Methods = summary.Methods,
                Results = summary.Results,
                Limitations = nlpResponse.Limitations ?? new List<string>()
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
/// Response model from NLP service summarization
/// </summary>
public class NlpSummarizeResponse
{
    [JsonPropertyName("tl_dr")]
    public string? TlDr { get; set; }

    [JsonPropertyName("key_points")]
    public List<string>? KeyPoints { get; set; }

    [JsonPropertyName("methods")]
    public string? Methods { get; set; }

    [JsonPropertyName("results")]
    public string? Results { get; set; }

    [JsonPropertyName("limitations")]
    public List<string>? Limitations { get; set; }
}

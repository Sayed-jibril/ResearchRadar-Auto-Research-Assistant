using Microsoft.EntityFrameworkCore;
using ScholarLens.Api.Data;
using ScholarLens.Api.Models;
using ScholarLens.Api.Services.ExternalApis;
using ScholarLens.Api.DTOs;
using System.Diagnostics;
using System.Text.Json;

namespace ScholarLens.Api.Services;

/// <summary>
/// Main search service that coordinates multiple external APIs and handles ranking/deduplication
/// </summary>
public class SearchService
{
    private readonly ScholarLensDbContext _context;
    private readonly ILogger<SearchService> _logger;
    private readonly List<IPaperSearchClient> _searchClients;
    private readonly IOpenAccessClient _openAccessClient;
    private readonly IRankingService _rankingService;

    public SearchService(
        ScholarLensDbContext context,
        ILogger<SearchService> logger,
        IEnumerable<IPaperSearchClient> searchClients,
        IOpenAccessClient openAccessClient,
        IRankingService rankingService)
    {
        _context = context;
        _logger = logger;
        _searchClients = searchClients.ToList();
        _openAccessClient = openAccessClient;
        _rankingService = rankingService;
    }

    public async Task<SearchResponse> SearchAsync(
        SearchRequest request, 
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("Starting search for query: {Query}", request.Query);

            // Create or get existing topic
            var topic = await GetOrCreateTopicAsync(request, cancellationToken);

            // Search all external sources in parallel
            var searchTasks = _searchClients.Select(client => 
                SearchClientSafelyAsync(client, request, cancellationToken)
            ).ToArray();

            var allResults = await Task.WhenAll(searchTasks);
            var flatResults = allResults.SelectMany(results => results).ToList();

            _logger.LogInformation("Found {Count} total papers from {Sources} sources", 
                flatResults.Count, _searchClients.Count);

            // Deduplicate papers
            var deduplicatedResults = await DeduplicatePapersAsync(flatResults, cancellationToken);
            
            _logger.LogInformation("After deduplication: {Count} unique papers", deduplicatedResults.Count);

            // Enhance with open access information
            await EnhanceWithOpenAccessAsync(deduplicatedResults, cancellationToken);

            // Filter by open access if requested
            if (request.OpenAccessOnly)
            {
                deduplicatedResults = deduplicatedResults.Where(p => p.IsOpenAccess).ToList();
                _logger.LogInformation("After OA filter: {Count} papers", deduplicatedResults.Count);
            }

            // Rank results
            var rankedResults = await _rankingService.RankPapersAsync(
                request.Query, deduplicatedResults, cancellationToken);

            // Take top results
            var finalResults = rankedResults.Take(request.Limit).ToList();

            // Save to database
            var savedResults = await SaveSearchResultsAsync(topic, finalResults, cancellationToken);

            stopwatch.Stop();

            // Build response
            var response = new SearchResponse
            {
                Results = savedResults.Select(MapToDto).ToList(),
                Metadata = new SearchMetadata
                {
                    Query = request.Query,
                    TotalResults = flatResults.Count,
                    FilteredResults = finalResults.Count,
                    ProcessingTime = stopwatch.Elapsed,
                    SourceCounts = flatResults.GroupBy(p => p.Source)
                        .ToDictionary(g => g.Key, g => g.Count())
                }
            };

            _logger.LogInformation("Search completed in {Duration}ms", stopwatch.ElapsedMilliseconds);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during search for query: {Query}", request.Query);
            throw;
        }
    }

    private async Task<List<ExternalPaper>> SearchClientSafelyAsync(
        IPaperSearchClient client, 
        SearchRequest request, 
        CancellationToken cancellationToken)
    {
        try
        {
            return await client.SearchAsync(
                request.Query, 
                request.YearFrom, 
                request.YearTo, 
                request.Limit, 
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error searching {Source}", client.SourceName);
            return new List<ExternalPaper>();
        }
    }

    private async Task<Topic> GetOrCreateTopicAsync(SearchRequest request, CancellationToken cancellationToken)
    {
        // Look for existing topic with same parameters
        var existingTopic = await _context.Topics
            .FirstOrDefaultAsync(t => 
                t.Query == request.Query &&
                t.Language == request.Language &&
                t.YearFrom == request.YearFrom &&
                t.YearTo == request.YearTo, 
                cancellationToken);

        if (existingTopic != null)
        {
            return existingTopic;
        }

        // Create new topic
        var topic = new Topic
        {
            Query = request.Query,
            Language = request.Language,
            YearFrom = request.YearFrom,
            YearTo = request.YearTo
        };

        _context.Topics.Add(topic);
        await _context.SaveChangesAsync(cancellationToken);

        return topic;
    }

    private async Task<List<ExternalPaper>> DeduplicatePapersAsync(
        List<ExternalPaper> papers, 
        CancellationToken cancellationToken)
    {
        var deduplicatedPapers = new List<ExternalPaper>();
        var seenDois = new HashSet<string>();
        var seenTitles = new List<(string title, ExternalPaper paper)>();

        foreach (var paper in papers)
        {
            // First check DOI (most reliable)
            if (!string.IsNullOrEmpty(paper.Doi))
            {
                if (seenDois.Contains(paper.Doi))
                {
                    _logger.LogDebug("Duplicate DOI found: {DOI}", paper.Doi);
                    continue;
                }
                seenDois.Add(paper.Doi);
                deduplicatedPapers.Add(paper);
                seenTitles.Add((paper.Title, paper));
                continue;
            }

            // Check title similarity (using Jaro-Winkler distance)
            var isDuplicate = false;
            foreach (var (existingTitle, existingPaper) in seenTitles)
            {
                var similarity = CalculateJaroWinklerSimilarity(paper.Title, existingTitle);
                if (similarity > 0.92) // Threshold for considering titles as duplicates
                {
                    _logger.LogDebug("Duplicate title found: {Title} (similarity: {Similarity:F3})", 
                        paper.Title, similarity);
                    
                    // Keep the paper with more information (prefer one with DOI, PDF URL, etc.)
                    if (IsPreferablePaper(paper, existingPaper))
                    {
                        // Replace existing paper
                        deduplicatedPapers.Remove(existingPaper);
                        seenTitles.Remove((existingTitle, existingPaper));
                        deduplicatedPapers.Add(paper);
                        seenTitles.Add((paper.Title, paper));
                    }
                    
                    isDuplicate = true;
                    break;
                }
            }

            if (!isDuplicate)
            {
                deduplicatedPapers.Add(paper);
                seenTitles.Add((paper.Title, paper));
            }
        }

        return deduplicatedPapers;
    }

    private bool IsPreferablePaper(ExternalPaper candidate, ExternalPaper existing)
    {
        // Scoring system to determine which paper has more/better information
        var candidateScore = 0;
        var existingScore = 0;

        // DOI is valuable
        if (!string.IsNullOrEmpty(candidate.Doi)) candidateScore += 3;
        if (!string.IsNullOrEmpty(existing.Doi)) existingScore += 3;

        // PDF URL is valuable
        if (!string.IsNullOrEmpty(candidate.PdfUrl)) candidateScore += 2;
        if (!string.IsNullOrEmpty(existing.PdfUrl)) existingScore += 2;

        // Abstract is valuable
        if (!string.IsNullOrEmpty(candidate.Abstract)) candidateScore += 2;
        if (!string.IsNullOrEmpty(existing.Abstract)) existingScore += 2;

        // Open access is preferred
        if (candidate.IsOpenAccess) candidateScore += 1;
        if (existing.IsOpenAccess) existingScore += 1;

        // More authors is slightly better
        candidateScore += candidate.Authors.Count;
        existingScore += existing.Authors.Count;

        return candidateScore > existingScore;
    }

    private double CalculateJaroWinklerSimilarity(string str1, string str2)
    {
        // Simplified Jaro-Winkler implementation
        // In production, consider using a proper library like Fastenshtein
        
        if (string.IsNullOrEmpty(str1) || string.IsNullOrEmpty(str2))
            return 0.0;

        str1 = str1.ToLowerInvariant().Trim();
        str2 = str2.ToLowerInvariant().Trim();

        if (str1 == str2)
            return 1.0;

        // Simple character-based similarity as approximation
        var commonChars = str1.Intersect(str2).Count();
        var totalChars = (str1.Length + str2.Length) / 2.0;
        
        return commonChars / totalChars;
    }

    private async Task EnhanceWithOpenAccessAsync(
        List<ExternalPaper> papers, 
        CancellationToken cancellationToken)
    {
        // Process in batches to avoid overwhelming the Unpaywall API
        const int batchSize = 10;
        var batches = papers.Where(p => !string.IsNullOrEmpty(p.Doi))
                           .Chunk(batchSize);

        foreach (var batch in batches)
        {
            var tasks = batch.Select(async paper =>
            {
                try
                {
                    var openAccessInfo = await _openAccessClient.LookupAsync(paper.Doi!, cancellationToken);
                    if (openAccessInfo != null)
                    {
                        // Update paper with open access information
                        paper = paper with 
                        { 
                            IsOpenAccess = openAccessInfo.IsOpenAccess,
                            PdfUrl = openAccessInfo.PdfUrl ?? paper.PdfUrl
                        };
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error looking up open access for DOI: {DOI}", paper.Doi);
                }
            });

            await Task.WhenAll(tasks);

            // Add small delay between batches to be respectful to the API
            await Task.Delay(100, cancellationToken);
        }
    }

    private async Task<List<SearchResult>> SaveSearchResultsAsync(
        Topic topic, 
        List<ExternalPaper> papers, 
        CancellationToken cancellationToken)
    {
        var searchResults = new List<SearchResult>();

        foreach (var paper in papers)
        {
            var searchResult = new SearchResult
            {
                TopicId = topic.Id,
                Source = paper.Source,
                Title = paper.Title,
                AuthorsJson = JsonSerializer.Serialize(paper.Authors),
                Abstract = paper.Abstract,
                Doi = paper.Doi,
                Url = paper.Url,
                PdfUrl = paper.PdfUrl,
                Year = paper.Year,
                Venue = paper.Venue,
                IsOpenAccess = paper.IsOpenAccess,
                Score = 0.0, // Will be set by ranking service
                RawJson = JsonSerializer.Serialize(paper.RawData)
            };

            searchResults.Add(searchResult);
        }

        _context.SearchResults.AddRange(searchResults);
        await _context.SaveChangesAsync(cancellationToken);

        return searchResults;
    }

    private SearchResultDto MapToDto(SearchResult searchResult)
    {
        var authors = new List<AuthorDto>();
        try
        {
            if (!string.IsNullOrEmpty(searchResult.AuthorsJson))
            {
                var externalAuthors = JsonSerializer.Deserialize<List<ExternalAuthor>>(searchResult.AuthorsJson);
                authors = externalAuthors?.Select(a => new AuthorDto 
                { 
                    Name = a.Name, 
                    Affiliation = a.Affiliation 
                }).ToList() ?? new List<AuthorDto>();
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Error deserializing authors JSON for paper {Id}", searchResult.Id);
        }

        return new SearchResultDto
        {
            Id = searchResult.Id,
            Source = searchResult.Source,
            Title = searchResult.Title,
            Authors = authors,
            Abstract = searchResult.Abstract,
            Doi = searchResult.Doi,
            Url = searchResult.Url,
            PdfUrl = searchResult.PdfUrl,
            Year = searchResult.Year,
            Venue = searchResult.Venue,
            IsOpenAccess = searchResult.IsOpenAccess,
            Score = searchResult.Score,
            CreatedAt = searchResult.CreatedAt.DateTime
        };
    }
}

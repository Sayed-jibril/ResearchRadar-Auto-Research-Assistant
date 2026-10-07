using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using System.Text.Json.Serialization;

namespace ScholarLens.Api.Services.ExternalApis;

public class SemanticScholarClient : IPaperSearchClient
{
    private readonly HttpClient _httpClient;
    private readonly IDistributedCache _cache;
    private readonly ILogger<SemanticScholarClient> _logger;
    private readonly SemanticScholarOptions _options;

    public string SourceName => "semantic";

    public SemanticScholarClient(
        HttpClient httpClient,
        IDistributedCache cache,
        ILogger<SemanticScholarClient> logger,
        IOptions<SemanticScholarOptions> options)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
        _options = options.Value;
        _httpClient.BaseAddress = new Uri(_options.BaseUrl);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "ScholarLens/1.0");
    }

    public async Task<List<ExternalPaper>> SearchAsync(
        string query, 
        int? fromYear = null, 
        int? toYear = null, 
        int limit = 25,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheKey = $"semantic:search:{query}:{fromYear}:{toYear}:{limit}";
            var cachedResult = await _cache.GetStringAsync(cacheKey, cancellationToken);
            
            if (!string.IsNullOrEmpty(cachedResult))
            {
                var cached = JsonSerializer.Deserialize<List<ExternalPaper>>(cachedResult);
                return cached ?? new List<ExternalPaper>();
            }

            var url = BuildSearchUrl(query, fromYear, toYear, limit);
            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var semanticResponse = JsonSerializer.Deserialize<SemanticScholarResponse>(content);
            var papers = ParseSemanticScholarResponse(semanticResponse);

            var cacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
            };
            
            await _cache.SetStringAsync(
                cacheKey, 
                JsonSerializer.Serialize(papers), 
                cacheOptions, 
                cancellationToken);

            return papers;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching Semantic Scholar API");
            return new List<ExternalPaper>();
        }
    }

    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/paper/search?query=test&limit=1", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private string BuildSearchUrl(string query, int? fromYear, int? toYear, int limit)
    {
        var encodedQuery = Uri.EscapeDataString(query);
        var url = $"/paper/search?query={encodedQuery}&limit={limit}";

        if (fromYear.HasValue)
        {
            url += $"&year={fromYear}";
        }
        if (toYear.HasValue && toYear != fromYear)
        {
            url += $"-{toYear}";
        }

        url += "&fields=paperId,title,authors,abstract,year,venue,doi,url,openAccessPdf,citationCount";
        return url;
    }

    private List<ExternalPaper> ParseSemanticScholarResponse(SemanticScholarResponse? response)
    {
        if (response?.Data == null)
            return new List<ExternalPaper>();

        var papers = new List<ExternalPaper>();

        foreach (var item in response.Data)
        {
            try
            {
                var paper = new ExternalPaper
                {
                    Source = SourceName,
                    Title = item.Title ?? string.Empty,
                    Authors = item.Authors?.Select(a => new ExternalAuthor 
                    { 
                        Name = a.Name ?? string.Empty,
                        Affiliation = null
                    }).ToList() ?? new List<ExternalAuthor>(),
                    Abstract = item.Abstract ?? string.Empty,
                    Doi = item.Doi,
                    Url = item.Url,
                    PdfUrl = item.OpenAccessPdf?.Url,
                    Year = item.Year,
                    Venue = item.Venue?.Name,
                    IsOpenAccess = item.OpenAccessPdf != null,
                    RawData = item
                };

                if (!string.IsNullOrEmpty(paper.Title))
                {
                    papers.Add(paper);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error parsing Semantic Scholar item");
            }
        }

        return papers;
    }
}

public class SemanticScholarOptions
{
    public string BaseUrl { get; set; } = "https://api.semanticscholar.org/graph/v1";
}

public class SemanticScholarResponse
{
    [JsonPropertyName("data")]
    public SemanticScholarPaper[]? Data { get; set; }
}

public class SemanticScholarPaper
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("authors")]
    public SemanticScholarAuthor[]? Authors { get; set; }

    [JsonPropertyName("abstract")]
    public string? Abstract { get; set; }

    [JsonPropertyName("year")]
    public int? Year { get; set; }

    [JsonPropertyName("venue")]
    public SemanticScholarVenue? Venue { get; set; }

    [JsonPropertyName("doi")]
    public string? Doi { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("openAccessPdf")]
    public SemanticScholarOpenAccess? OpenAccessPdf { get; set; }
}

public class SemanticScholarAuthor
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

public class SemanticScholarVenue
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

public class SemanticScholarOpenAccess
{
    [JsonPropertyName("url")]
    public string? Url { get; set; }
}

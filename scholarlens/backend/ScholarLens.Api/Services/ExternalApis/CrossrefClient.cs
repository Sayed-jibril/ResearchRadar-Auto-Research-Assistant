using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace ScholarLens.Api.Services.ExternalApis;

/// <summary>
/// Client for Crossref API - provides comprehensive academic metadata
/// </summary>
public class CrossrefClient : IPaperSearchClient
{
    private readonly HttpClient _httpClient;
    private readonly IDistributedCache _cache;
    private readonly ILogger<CrossrefClient> _logger;
    private readonly CrossrefOptions _options;

    public string SourceName => "crossref";

    public CrossrefClient(
        HttpClient httpClient,
        IDistributedCache cache,
        ILogger<CrossrefClient> logger,
        IOptions<CrossrefOptions> options)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
        _options = options.Value;

        // Configure HttpClient
        _httpClient.BaseAddress = new Uri(_options.BaseUrl);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "ScholarLens/1.0 (mailto:contact@scholarlens.com)");
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
            var cacheKey = $"crossref:search:{query}:{fromYear}:{toYear}:{limit}";
            var cachedResult = await _cache.GetStringAsync(cacheKey, cancellationToken);
            
            if (!string.IsNullOrEmpty(cachedResult))
            {
                _logger.LogDebug("Cache hit for Crossref search: {Query}", query);
                var cached = JsonSerializer.Deserialize<List<ExternalPaper>>(cachedResult);
                return cached ?? new List<ExternalPaper>();
            }

            _logger.LogInformation("Searching Crossref for: {Query}", query);

            var url = BuildSearchUrl(query, fromYear, toYear, limit);
            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var crossrefResponse = JsonSerializer.Deserialize<CrossrefResponse>(content);

            var papers = ParseCrossrefResponse(crossrefResponse);

            // Cache for 24 hours
            var cacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
            };
            
            await _cache.SetStringAsync(
                cacheKey, 
                JsonSerializer.Serialize(papers), 
                cacheOptions, 
                cancellationToken);

            _logger.LogInformation("Found {Count} papers from Crossref", papers.Count);
            return papers;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching Crossref API");
            return new List<ExternalPaper>();
        }
    }

    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/works?rows=1", cancellationToken);
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
        var url = $"/works?query={encodedQuery}&rows={limit}&sort=relevance&order=desc";

        // Add date filters if provided
        if (fromYear.HasValue || toYear.HasValue)
        {
            var from = fromYear ?? 1900;
            var to = toYear ?? DateTime.Now.Year;
            url += $"&filter=from-pub-date:{from},until-pub-date:{to}";
        }

        // Request specific fields to reduce response size
        url += "&select=DOI,title,author,abstract,published-print,published-online,container-title,URL,type,subject";

        return url;
    }

    private List<ExternalPaper> ParseCrossrefResponse(CrossrefResponse? response)
    {
        if (response?.Message?.Items == null)
            return new List<ExternalPaper>();

        var papers = new List<ExternalPaper>();

        foreach (var item in response.Message.Items)
        {
            try
            {
                var paper = new ExternalPaper
                {
                    Source = SourceName,
                    Title = GetTitle(item),
                    Authors = GetAuthors(item),
                    Abstract = GetAbstract(item),
                    Doi = GetDoi(item),
                    Url = GetUrl(item),
                    Year = GetYear(item),
                    Venue = GetVenue(item),
                    IsOpenAccess = false, // Will be determined by Unpaywall lookup
                    RawData = item
                };

                if (!string.IsNullOrEmpty(paper.Title))
                {
                    papers.Add(paper);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error parsing Crossref item");
            }
        }

        return papers;
    }

    private string GetTitle(JsonElement item)
    {
        if (item.TryGetProperty("title", out var titleProp) && titleProp.ValueKind == JsonValueKind.Array)
        {
            var titles = titleProp.EnumerateArray().ToList();
            if (titles.Count > 0 && titles[0].ValueKind == JsonValueKind.String)
            {
                return titles[0].GetString() ?? string.Empty;
            }
        }
        return string.Empty;
    }

    private List<ExternalAuthor> GetAuthors(JsonElement item)
    {
        var authors = new List<ExternalAuthor>();

        if (item.TryGetProperty("author", out var authorProp) && authorProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var author in authorProp.EnumerateArray())
            {
                var name = string.Empty;
                var affiliation = string.Empty;

                if (author.TryGetProperty("given", out var given) && given.ValueKind == JsonValueKind.String &&
                    author.TryGetProperty("family", out var family) && family.ValueKind == JsonValueKind.String)
                {
                    name = $"{given.GetString()} {family.GetString()}".Trim();
                }

                if (author.TryGetProperty("affiliation", out var affiliationProp) && 
                    affiliationProp.ValueKind == JsonValueKind.Array)
                {
                    var affiliations = affiliationProp.EnumerateArray().ToList();
                    if (affiliations.Count > 0 && affiliations[0].TryGetProperty("name", out var affiliationName))
                    {
                        affiliation = affiliationName.GetString() ?? string.Empty;
                    }
                }

                if (!string.IsNullOrEmpty(name))
                {
                    authors.Add(new ExternalAuthor { Name = name, Affiliation = affiliation });
                }
            }
        }

        return authors;
    }

    private string GetAbstract(JsonElement item)
    {
        if (item.TryGetProperty("abstract", out var abstractProp) && abstractProp.ValueKind == JsonValueKind.String)
        {
            return abstractProp.GetString() ?? string.Empty;
        }
        return string.Empty;
    }

    private string? GetDoi(JsonElement item)
    {
        if (item.TryGetProperty("DOI", out var doiProp) && doiProp.ValueKind == JsonValueKind.String)
        {
            return doiProp.GetString();
        }
        return null;
    }

    private string? GetUrl(JsonElement item)
    {
        if (item.TryGetProperty("URL", out var urlProp) && urlProp.ValueKind == JsonValueKind.String)
        {
            return urlProp.GetString();
        }
        return null;
    }

    private int? GetYear(JsonElement item)
    {
        // Try published-print first, then published-online
        foreach (var dateProp in new[] { "published-print", "published-online" })
        {
            if (item.TryGetProperty(dateProp, out var publishedProp) &&
                publishedProp.TryGetProperty("date-parts", out var datePartsProp) &&
                datePartsProp.ValueKind == JsonValueKind.Array)
            {
                var dateParts = datePartsProp.EnumerateArray().ToList();
                if (dateParts.Count > 0 && dateParts[0].ValueKind == JsonValueKind.Array)
                {
                    var yearArray = dateParts[0].EnumerateArray().ToList();
                    if (yearArray.Count > 0 && yearArray[0].ValueKind == JsonValueKind.Number)
                    {
                        return yearArray[0].GetInt32();
                    }
                }
            }
        }
        return null;
    }

    private string? GetVenue(JsonElement item)
    {
        if (item.TryGetProperty("container-title", out var containerProp) && containerProp.ValueKind == JsonValueKind.Array)
        {
            var containers = containerProp.EnumerateArray().ToList();
            if (containers.Count > 0 && containers[0].ValueKind == JsonValueKind.String)
            {
                return containers[0].GetString();
            }
        }
        return null;
    }
}

// Configuration options
public class CrossrefOptions
{
    public string BaseUrl { get; set; } = "https://api.crossref.org";
}

// Response models for JSON deserialization
public class CrossrefResponse
{
    public CrossrefMessage? Message { get; set; }
}

public class CrossrefMessage
{
    public JsonElement[]? Items { get; set; }
}

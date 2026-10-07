using System.Xml.Linq;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace ScholarLens.Api.Services.ExternalApis;

/// <summary>
/// Client for arXiv API - provides access to preprints and open access papers
/// </summary>
public class ArxivClient : IPaperSearchClient
{
    private readonly HttpClient _httpClient;
    private readonly IDistributedCache _cache;
    private readonly ILogger<ArxivClient> _logger;
    private readonly ArxivOptions _options;

    public string SourceName => "arxiv";

    public ArxivClient(
        HttpClient httpClient,
        IDistributedCache cache,
        ILogger<ArxivClient> logger,
        IOptions<ArxivOptions> options)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
        _options = options.Value;

        // Configure HttpClient
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
            var cacheKey = $"arxiv:search:{query}:{fromYear}:{toYear}:{limit}";
            var cachedResult = await _cache.GetStringAsync(cacheKey, cancellationToken);
            
            if (!string.IsNullOrEmpty(cachedResult))
            {
                _logger.LogDebug("Cache hit for arXiv search: {Query}", query);
                var cached = JsonSerializer.Deserialize<List<ExternalPaper>>(cachedResult);
                return cached ?? new List<ExternalPaper>();
            }

            _logger.LogInformation("Searching arXiv for: {Query}", query);

            var url = BuildSearchUrl(query, fromYear, toYear, limit);
            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var papers = ParseArxivResponse(content);

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

            _logger.LogInformation("Found {Count} papers from arXiv", papers.Count);
            return papers;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching arXiv API");
            return new List<ExternalPaper>();
        }
    }

    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("?search_query=all:test&max_results=1", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private string BuildSearchUrl(string query, int? fromYear, int? toYear, int limit)
    {
        // Build arXiv search query
        var searchQuery = $"all:{Uri.EscapeDataString(query)}";
        
        // Add date range if specified
        if (fromYear.HasValue || toYear.HasValue)
        {
            var from = fromYear ?? 1991; // arXiv started in 1991
            var to = toYear ?? DateTime.Now.Year;
            
            // arXiv uses YYYYMM format for date ranges
            searchQuery += $" AND submittedDate:[{from}01 TO {to}12]";
        }

        var url = $"?search_query={searchQuery}&start=0&max_results={limit}&sortBy=relevance&sortOrder=descending";
        return url;
    }

    private List<ExternalPaper> ParseArxivResponse(string xmlContent)
    {
        var papers = new List<ExternalPaper>();

        try
        {
            var doc = XDocument.Parse(xmlContent);
            var atomNs = XNamespace.Get("http://www.w3.org/2005/Atom");
            var arxivNs = XNamespace.Get("http://arxiv.org/schemas/atom");

            var entries = doc.Descendants(atomNs + "entry");

            foreach (var entry in entries)
            {
                try
                {
                    var paper = ParseArxivEntry(entry, atomNs, arxivNs);
                    if (!string.IsNullOrEmpty(paper.Title))
                    {
                        papers.Add(paper);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error parsing arXiv entry");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing arXiv XML response");
        }

        return papers;
    }

    private ExternalPaper ParseArxivEntry(XElement entry, XNamespace atomNs, XNamespace arxivNs)
    {
        var id = entry.Element(atomNs + "id")?.Value ?? string.Empty;
        var title = entry.Element(atomNs + "title")?.Value?.Trim() ?? string.Empty;
        var summary = entry.Element(atomNs + "summary")?.Value?.Trim() ?? string.Empty;
        var published = entry.Element(atomNs + "published")?.Value;

        // Extract arXiv ID and construct PDF URL
        var arxivId = ExtractArxivId(id);
        var pdfUrl = !string.IsNullOrEmpty(arxivId) ? $"https://arxiv.org/pdf/{arxivId}.pdf" : null;

        // Parse authors
        var authors = entry.Elements(atomNs + "author")
            .Select(author => new ExternalAuthor
            {
                Name = author.Element(atomNs + "name")?.Value?.Trim() ?? string.Empty,
                Affiliation = author.Elements(arxivNs + "affiliation")
                    .FirstOrDefault()?.Value?.Trim()
            })
            .Where(author => !string.IsNullOrEmpty(author.Name))
            .ToList();

        // Parse categories to determine venue/subject
        var categories = entry.Elements(atomNs + "category")
            .Select(cat => cat.Attribute("term")?.Value)
            .Where(term => !string.IsNullOrEmpty(term))
            .ToList();

        var venue = categories.Count > 0 ? $"arXiv ({string.Join(", ", categories.Take(3))})" : "arXiv";

        // Parse publication year
        int? year = null;
        if (DateTime.TryParse(published, out var publishedDate))
        {
            year = publishedDate.Year;
        }

        // Extract DOI if available
        string? doi = null;
        var doiElement = entry.Elements(arxivNs + "doi").FirstOrDefault();
        if (doiElement != null)
        {
            doi = doiElement.Value?.Trim();
        }

        return new ExternalPaper
        {
            Source = SourceName,
            Title = title,
            Authors = authors,
            Abstract = summary,
            Doi = doi,
            Url = id,
            PdfUrl = pdfUrl,
            Year = year,
            Venue = venue,
            IsOpenAccess = true, // arXiv papers are always open access
            RawData = new
            {
                arxiv_id = arxivId,
                categories = categories,
                published = published,
                xml_entry = entry.ToString()
            }
        };
    }

    private string? ExtractArxivId(string url)
    {
        // Extract arXiv ID from URLs like "http://arxiv.org/abs/2301.00001v1"
        var uri = new Uri(url);
        var segments = uri.Segments;
        
        if (segments.Length >= 2 && segments[^2] == "abs/")
        {
            var idWithVersion = segments[^1];
            // Remove version suffix if present (e.g., "v1", "v2")
            var id = System.Text.RegularExpressions.Regex.Replace(idWithVersion, @"v\d+$", "");
            return id;
        }
        
        return null;
    }
}

// Configuration options
public class ArxivOptions
{
    public string BaseUrl { get; set; } = "http://export.arxiv.org/api/query";
}

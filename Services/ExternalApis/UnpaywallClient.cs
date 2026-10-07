using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using System.Text.Json.Serialization;

namespace ScholarLens.Api.Services.ExternalApis;

/// <summary>
/// Client for Unpaywall API - provides open access PDF locations
/// </summary>
public class UnpaywallClient : IOpenAccessClient
{
    private readonly HttpClient _httpClient;
    private readonly IDistributedCache _cache;
    private readonly ILogger<UnpaywallClient> _logger;
    private readonly UnpaywallOptions _options;

    public string SourceName => "unpaywall";

    public UnpaywallClient(
        HttpClient httpClient,
        IDistributedCache cache,
        ILogger<UnpaywallClient> logger,
        IOptions<UnpaywallOptions> options)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
        _options = options.Value;

        // Configure HttpClient
        _httpClient.BaseAddress = new Uri(_options.BaseUrl);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "ScholarLens/1.0");
    }

    public async Task<OpenAccessInfo?> LookupAsync(
        string doi, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(doi))
            return null;

        try
        {
            var cacheKey = $"unpaywall:doi:{doi}";
            var cachedResult = await _cache.GetStringAsync(cacheKey, cancellationToken);
            
            if (!string.IsNullOrEmpty(cachedResult))
            {
                _logger.LogDebug("Cache hit for Unpaywall DOI: {DOI}", doi);
                return JsonSerializer.Deserialize<OpenAccessInfo>(cachedResult);
            }

            _logger.LogDebug("Looking up DOI in Unpaywall: {DOI}", doi);

            var encodedDoi = Uri.EscapeDataString(doi);
            var url = $"/{encodedDoi}?email={Uri.EscapeDataString(_options.Email)}";
            
            var response = await _httpClient.GetAsync(url, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogDebug("DOI not found in Unpaywall: {DOI}", doi);
                    return new OpenAccessInfo { IsOpenAccess = false };
                }
                response.EnsureSuccessStatusCode();
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var unpaywallResponse = JsonSerializer.Deserialize<UnpaywallResponse>(content);

            var openAccessInfo = ParseUnpaywallResponse(unpaywallResponse);

            // Cache for 7 days (open access status doesn't change frequently)
            var cacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7)
            };
            
            await _cache.SetStringAsync(
                cacheKey, 
                JsonSerializer.Serialize(openAccessInfo), 
                cacheOptions, 
                cancellationToken);

            return openAccessInfo;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error looking up DOI in Unpaywall: {DOI}", doi);
            return null;
        }
    }

    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Use a known DOI for health check
            var response = await _httpClient.GetAsync($"/10.1371/journal.pone.0000001?email={Uri.EscapeDataString(_options.Email)}", cancellationToken);
            return response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NotFound;
        }
        catch
        {
            return false;
        }
    }

    private OpenAccessInfo ParseUnpaywallResponse(UnpaywallResponse? response)
    {
        if (response == null)
        {
            return new OpenAccessInfo { IsOpenAccess = false };
        }

        var isOpenAccess = response.IsOa ?? false;
        string? pdfUrl = null;
        string? license = null;

        // Find best open access location
        if (isOpenAccess && response.OaLocations != null)
        {
            // Prefer repository versions over publisher versions
            var bestLocation = response.OaLocations
                .Where(loc => !string.IsNullOrEmpty(loc.UrlForPdf))
                .OrderByDescending(loc => loc.HostType == "repository" ? 1 : 0)
                .ThenByDescending(loc => loc.HasRepositoryCopy)
                .FirstOrDefault();

            if (bestLocation != null)
            {
                pdfUrl = bestLocation.UrlForPdf;
                license = bestLocation.License;
            }
        }

        DateTime? updated = null;
        if (!string.IsNullOrEmpty(response.Updated) && DateTime.TryParse(response.Updated, out var parsedDate))
        {
            updated = parsedDate;
        }

        return new OpenAccessInfo
        {
            IsOpenAccess = isOpenAccess,
            PdfUrl = pdfUrl,
            License = license,
            Updated = updated
        };
    }
}

// Configuration options
public class UnpaywallOptions
{
    public string BaseUrl { get; set; } = "https://api.unpaywall.org/v2";
    public string Email { get; set; } = "you@example.com";
}

// Response models for JSON deserialization
public class UnpaywallResponse
{
    [JsonPropertyName("doi")]
    public string? Doi { get; set; }

    [JsonPropertyName("is_oa")]
    public bool? IsOa { get; set; }

    [JsonPropertyName("oa_locations")]
    public UnpaywallOaLocation[]? OaLocations { get; set; }

    [JsonPropertyName("updated")]
    public string? Updated { get; set; }

    [JsonPropertyName("oa_date")]
    public string? OaDate { get; set; }
}

public class UnpaywallOaLocation
{
    [JsonPropertyName("endpoint_id")]
    public string? EndpointId { get; set; }

    [JsonPropertyName("evidence")]
    public string? Evidence { get; set; }

    [JsonPropertyName("has_repository_copy")]
    public bool? HasRepositoryCopy { get; set; }

    [JsonPropertyName("host_type")]
    public string? HostType { get; set; }

    [JsonPropertyName("is_best")]
    public bool? IsBest { get; set; }

    [JsonPropertyName("license")]
    public string? License { get; set; }

    [JsonPropertyName("oa_date")]
    public string? OaDate { get; set; }

    [JsonPropertyName("pmh_id")]
    public string? PmhId { get; set; }

    [JsonPropertyName("repository_institution")]
    public string? RepositoryInstitution { get; set; }

    [JsonPropertyName("updated")]
    public string? Updated { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("url_for_landing_page")]
    public string? UrlForLandingPage { get; set; }

    [JsonPropertyName("url_for_pdf")]
    public string? UrlForPdf { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }
}

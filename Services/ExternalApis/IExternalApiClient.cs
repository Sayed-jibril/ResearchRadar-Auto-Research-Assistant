namespace ScholarLens.Api.Services.ExternalApis;

/// <summary>
/// Base interface for external API clients
/// </summary>
public interface IExternalApiClient
{
    string SourceName { get; }
    Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for academic paper search APIs
/// </summary>
public interface IPaperSearchClient : IExternalApiClient
{
    Task<List<ExternalPaper>> SearchAsync(
        string query, 
        int? fromYear = null, 
        int? toYear = null, 
        int limit = 25,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for open access paper lookup
/// </summary>
public interface IOpenAccessClient : IExternalApiClient
{
    Task<OpenAccessInfo?> LookupAsync(
        string doi, 
        CancellationToken cancellationToken = default);
}

/// <summary>
/// External paper representation (normalized across sources)
/// </summary>
public record ExternalPaper
{
    public string Source { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public List<ExternalAuthor> Authors { get; init; } = new();
    public string Abstract { get; init; } = string.Empty;
    public string? Doi { get; init; }
    public string? Url { get; init; }
    public string? PdfUrl { get; init; }
    public int? Year { get; init; }
    public string? Venue { get; init; }
    public bool IsOpenAccess { get; init; }
    public object? RawData { get; init; } // Store original API response
}

/// <summary>
/// External author representation
/// </summary>
public record ExternalAuthor
{
    public string Name { get; init; } = string.Empty;
    public string? Affiliation { get; init; }
}

/// <summary>
/// Open access information
/// </summary>
public record OpenAccessInfo
{
    public bool IsOpenAccess { get; init; }
    public string? PdfUrl { get; init; }
    public string? License { get; init; }
    public DateTime? Updated { get; init; }
}

using System.ComponentModel.DataAnnotations;

namespace ScholarLens.Api.DTOs;

/// <summary>
/// Request model for academic paper search
/// </summary>
public record SearchRequest
{
    [Required]
    [StringLength(1000, MinimumLength = 3)]
    public string Query { get; init; } = string.Empty;
    
    public int? YearFrom { get; init; }
    public int? YearTo { get; init; }
    
    [Range(1, 100)]
    public int Limit { get; init; } = 25;
    
    [StringLength(5)]
    public string Language { get; init; } = "en";
    
    public bool OpenAccessOnly { get; init; } = false;
}

/// <summary>
/// Response model for search results
/// </summary>
public record SearchResponse
{
    public List<SearchResultDto> Results { get; init; } = new();
    public SearchMetadata Metadata { get; init; } = new();
}

/// <summary>
/// Individual search result
/// </summary>
public record SearchResultDto
{
    public Guid Id { get; init; }
    public string Source { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public List<AuthorDto> Authors { get; init; } = new();
    public string Abstract { get; init; } = string.Empty;
    public string? Doi { get; init; }
    public string? Url { get; init; }
    public string? PdfUrl { get; init; }
    public int? Year { get; init; }
    public string? Venue { get; init; }
    public bool IsOpenAccess { get; init; }
    public double Score { get; init; }
    public DateTime CreatedAt { get; init; }
}

/// <summary>
/// Author information
/// </summary>
public record AuthorDto
{
    public string Name { get; init; } = string.Empty;
    public string? Affiliation { get; init; }
}

/// <summary>
/// Search metadata and statistics
/// </summary>
public record SearchMetadata
{
    public string Query { get; init; } = string.Empty;
    public int TotalResults { get; init; }
    public int FilteredResults { get; init; }
    public TimeSpan ProcessingTime { get; init; }
    public Dictionary<string, int> SourceCounts { get; init; } = new();
}

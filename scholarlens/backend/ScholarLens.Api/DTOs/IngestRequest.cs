using System.ComponentModel.DataAnnotations;

namespace ScholarLens.Api.DTOs;

/// <summary>
/// Request model for paper text ingestion
/// </summary>
public record IngestRequest
{
    [Required]
    [MinLength(1)]
    public List<Guid> PaperIds { get; init; } = new();
}

/// <summary>
/// Response model for ingestion results
/// </summary>
public record IngestResponse
{
    public List<IngestResultDto> Results { get; init; } = new();
    public IngestSummary Summary { get; init; } = new();
}

/// <summary>
/// Individual ingestion result
/// </summary>
public record IngestResultDto
{
    public Guid PaperId { get; init; }
    public string Status { get; init; } = string.Empty; // "success", "skipped", "failed"
    public string? Reason { get; init; }
    public int? TextLength { get; init; }
    public int? TokenCount { get; init; }
}

/// <summary>
/// Overall ingestion summary
/// </summary>
public record IngestSummary
{
    public int TotalPapers { get; init; }
    public int SuccessCount { get; init; }
    public int SkippedCount { get; init; }
    public int FailedCount { get; init; }
    public TimeSpan ProcessingTime { get; init; }
}

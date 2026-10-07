using System.ComponentModel.DataAnnotations;

namespace ScholarLens.Api.DTOs;

/// <summary>
/// Request model for paper summarization
/// </summary>
public record SummarizeRequest
{
    [Required]
    [MinLength(1)]
    public List<Guid> PaperIds { get; init; } = new();
    
    public string SummaryStyle { get; init; } = "technical"; // "technical" or "executive"
    
    [StringLength(5)]
    public string Language { get; init; } = "en";
    
    [Range(100, 2000)]
    public int MaxTokensPerPaper { get; init; } = 1200;
}

/// <summary>
/// Response model for summarization results
/// </summary>
public record SummarizeResponse
{
    public List<SummaryResultDto> Results { get; init; } = new();
    public SummarizationMetadata Metadata { get; init; } = new();
}

/// <summary>
/// Individual summary result
/// </summary>
public record SummaryResultDto
{
    public Guid PaperId { get; init; }
    public string Status { get; init; } = string.Empty; // "success", "failed"
    public string? TlDr { get; init; }
    public List<string> KeyPoints { get; init; } = new();
    public string? Methods { get; init; }
    public string? Results { get; init; }
    public List<string> Limitations { get; init; } = new();
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Summarization metadata
/// </summary>
public record SummarizationMetadata
{
    public int TotalPapers { get; init; }
    public int SuccessCount { get; init; }
    public int FailedCount { get; init; }
    public TimeSpan ProcessingTime { get; init; }
    public string ModelUsed { get; init; } = string.Empty;
}

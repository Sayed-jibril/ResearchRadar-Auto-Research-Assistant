using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ScholarLens.Api.Models;

/// <summary>
/// Stores AI-generated summaries of academic papers
/// </summary>
public class Summary
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    [ForeignKey(nameof(SearchResult))]
    public Guid SearchResultId { get; set; }
    
    /// <summary>
    /// Brief summary (≤60 words)
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string TlDr { get; set; } = string.Empty;
    
    /// <summary>
    /// JSON array of key points: ["Point 1", "Point 2", ...]
    /// </summary>
    [Column(TypeName = "jsonb")]
    public string KeyPointsJson { get; set; } = "[]";
    
    /// <summary>
    /// Methodology description
    /// </summary>
    [MaxLength(2000)]
    public string Methods { get; set; } = string.Empty;
    
    /// <summary>
    /// Results and findings
    /// </summary>
    [MaxLength(2000)]
    public string Results { get; set; } = string.Empty;
    
    /// <summary>
    /// JSON array of limitations: ["Limitation 1", "Limitation 2", ...]
    /// </summary>
    [Column(TypeName = "jsonb")]
    public string LimitationsJson { get; set; } = "[]";
    
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    
    // Navigation property
    public virtual SearchResult SearchResult { get; set; } = null!;
}

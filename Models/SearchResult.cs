using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ScholarLens.Api.Models;

/// <summary>
/// Represents a single academic paper found during search
/// </summary>
public class SearchResult
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    [ForeignKey(nameof(Topic))]
    public Guid TopicId { get; set; }
    
    [Required]
    [MaxLength(40)]
    public string Source { get; set; } = string.Empty; // "crossref", "arxiv", "semantic", etc.
    
    [Required]
    [MaxLength(2000)]
    public string Title { get; set; } = string.Empty;
    
    /// <summary>
    /// JSON array of author objects: [{"name": "John Doe", "affiliation": "..."}]
    /// </summary>
    [Column(TypeName = "jsonb")]
    public string AuthorsJson { get; set; } = "[]";
    
    [MaxLength(5000)]
    public string Abstract { get; set; } = string.Empty;
    
    [MaxLength(256)]
    public string? Doi { get; set; }
    
    [MaxLength(2000)]
    public string? Url { get; set; }
    
    [MaxLength(2000)]
    public string? PdfUrl { get; set; }
    
    public int? Year { get; set; }
    
    [MaxLength(500)]
    public string? Venue { get; set; }
    
    public bool IsOpenAccess { get; set; } = false;
    
    /// <summary>
    /// Relevance score (0.0 - 1.0) based on keyword + semantic + recency
    /// </summary>
    public double Score { get; set; } = 0.0;
    
    /// <summary>
    /// Raw JSON response from the source API for debugging
    /// </summary>
    [Column(TypeName = "jsonb")]
    public string RawJson { get; set; } = "{}";
    
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    
    // Navigation properties
    public virtual Topic Topic { get; set; } = null!;
    public virtual PaperText? PaperText { get; set; }
    public virtual Summary? Summary { get; set; }
}

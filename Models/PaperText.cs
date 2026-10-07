using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ScholarLens.Api.Models;

/// <summary>
/// Stores extracted text content from academic paper PDFs
/// </summary>
public class PaperText
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    [ForeignKey(nameof(SearchResult))]
    public Guid SearchResultId { get; set; }
    
    /// <summary>
    /// Full extracted text from the PDF
    /// </summary>
    [Required]
    public string Text { get; set; } = string.Empty;
    
    /// <summary>
    /// Number of tokens in the text (for processing limits)
    /// </summary>
    public int Tokens { get; set; } = 0;
    
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    
    // Navigation property
    public virtual SearchResult SearchResult { get; set; } = null!;
}

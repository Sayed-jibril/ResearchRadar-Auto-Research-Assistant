using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ScholarLens.Api.Models;

/// <summary>
/// Represents a generated research report
/// </summary>
public class Report
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    [ForeignKey(nameof(Topic))]
    public Guid TopicId { get; set; }
    
    /// <summary>
    /// JSON configuration used to generate the report
    /// Contains: sections, charts, language, branding, etc.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public string ParametersJson { get; set; } = "{}";
    
    /// <summary>
    /// Path to generated HTML file (for preview)
    /// </summary>
    [MaxLength(500)]
    public string? HtmlPath { get; set; }
    
    /// <summary>
    /// Path to generated PDF file
    /// </summary>
    [MaxLength(500)]
    public string? PdfPath { get; set; }
    
    /// <summary>
    /// Report generation status: "pending", "processing", "completed", "failed"
    /// </summary>
    [Required]
    [MaxLength(24)]
    public string Status { get; set; } = "pending";
    
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    
    // Navigation property
    public virtual Topic Topic { get; set; } = null!;
}

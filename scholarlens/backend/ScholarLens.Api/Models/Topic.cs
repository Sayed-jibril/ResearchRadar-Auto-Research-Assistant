using System.ComponentModel.DataAnnotations;

namespace ScholarLens.Api.Models;

/// <summary>
/// Represents a research topic/query submitted for paper search
/// </summary>
public class Topic
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    [MaxLength(1000)]
    public string Query { get; set; } = string.Empty;
    
    [MaxLength(5)]
    public string Language { get; set; } = "en";
    
    public int? YearFrom { get; set; }
    public int? YearTo { get; set; }
    
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    
    // Navigation properties
    public virtual ICollection<SearchResult> SearchResults { get; set; } = new List<SearchResult>();
    public virtual ICollection<Report> Reports { get; set; } = new List<Report>();
}

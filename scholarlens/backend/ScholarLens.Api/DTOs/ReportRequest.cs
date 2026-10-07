using System.ComponentModel.DataAnnotations;

namespace ScholarLens.Api.DTOs;

/// <summary>
/// Request model for report generation
/// </summary>
public record ReportRequest
{
    [Required]
    [StringLength(500, MinimumLength = 3)]
    public string Topic { get; init; } = string.Empty;
    
    public List<Guid>? PaperIds { get; init; }
    
    [Range(1, 50)]
    public int K { get; init; } = 10; // Auto-pick top K papers if PaperIds not provided
    
    public List<string> Sections { get; init; } = new()
    {
        "cover", "executive", "overview", "methodology", 
        "key-findings", "comparison", "charts", "per-paper", 
        "gaps", "references"
    };
    
    public List<string> Charts { get; init; } = new()
    {
        "by-year", "oa-vs-paywalled", "source-breakdown"
    };
    
    [StringLength(5)]
    public string Language { get; init; } = "en";
    
    public ReportBrandingDto Branding { get; init; } = new();
}

/// <summary>
/// Response model for report generation
/// </summary>
public record ReportResponse
{
    public Guid ReportId { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? PdfUrl { get; init; }
    public string? HtmlUrl { get; init; }
    public DateTime CreatedAt { get; init; }
    public ReportMetadata Metadata { get; init; } = new();
}

/// <summary>
/// Branding configuration for reports
/// </summary>
public record ReportBrandingDto
{
    public string? LogoUrl { get; init; }
    public string BrandName { get; init; } = "ScholarLens";
    public string PrimaryColor { get; init; } = "#204ECF";
}

/// <summary>
/// Report generation metadata
/// </summary>
public record ReportMetadata
{
    public int PapersIncluded { get; init; }
    public int PagesGenerated { get; init; }
    public TimeSpan GenerationTime { get; init; }
    public string TemplateVersion { get; init; } = "1.0";
    public List<string> SectionsGenerated { get; init; } = new();
    public List<string> ChartsGenerated { get; init; } = new();
}

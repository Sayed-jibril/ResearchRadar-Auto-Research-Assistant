using Microsoft.EntityFrameworkCore;
using ScholarLens.Api.Data;
using ScholarLens.Api.Models;
using ScholarLens.Api.DTOs;
using System.Text.Json;
using System.Diagnostics;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ScholarLens.Api.Services;

/// <summary>
/// Service for generating branded PDF reports from research results
/// </summary>
public class ReportService
{
    private readonly ScholarLensDbContext _context;
    private readonly ILogger<ReportService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    public ReportService(
        ScholarLensDbContext context,
        ILogger<ReportService> logger,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        _context = context;
        _logger = logger;
        _configuration = configuration;
        _environment = environment;

        // Configure QuestPDF
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task<ReportResponse> GenerateReportAsync(
        ReportRequest request, 
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("Starting report generation for topic: {Topic}", request.Topic);

            // Get papers for the report
            var papers = await GetPapersForReportAsync(request, cancellationToken);
            
            if (!papers.Any())
            {
                throw new InvalidOperationException("No papers found for report generation");
            }

            // Create report entity
            var report = new Report
            {
                TopicId = await GetOrCreateTopicIdAsync(request.Topic, cancellationToken),
                ParametersJson = JsonSerializer.Serialize(request),
                Status = "generating"
            };

            _context.Reports.Add(report);
            await _context.SaveChangesAsync(cancellationToken);

            // Generate PDF
            var pdfPath = await GeneratePdfReportAsync(report.Id, request, papers, cancellationToken);
            
            stopwatch.Stop();

            // Update report status
            report.Status = "completed";
            report.PdfPath = pdfPath;
            await _context.SaveChangesAsync(cancellationToken);

            var response = new ReportResponse
            {
                ReportId = report.Id,
                Status = "completed",
                PdfUrl = $"/api/report/{report.Id}.pdf",
                CreatedAt = report.CreatedAt.DateTime,
                Metadata = new ReportMetadata
                {
                    PapersIncluded = papers.Count,
                    PagesGenerated = 1, // Will be calculated properly in full implementation
                    GenerationTime = stopwatch.Elapsed,
                    TemplateVersion = "1.0",
                    SectionsGenerated = request.Sections,
                    ChartsGenerated = request.Charts
                }
            };

            _logger.LogInformation("Report generated successfully: {ReportId} with {PaperCount} papers", 
                report.Id, papers.Count);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating report for topic: {Topic}", request.Topic);
            throw;
        }
    }

    private async Task<List<SearchResult>> GetPapersForReportAsync(
        ReportRequest request, 
        CancellationToken cancellationToken)
    {
        IQueryable<SearchResult> query = _context.SearchResults
            .Include(sr => sr.Summary)
            .Include(sr => sr.PaperText);

        if (request.PaperIds != null && request.PaperIds.Any())
        {
            // Use specific papers
            query = query.Where(sr => request.PaperIds.Contains(sr.Id));
        }
        else
        {
            // Auto-select top K papers for the topic
            query = query.Where(sr => sr.Topic.Query.Contains(request.Topic))
                         .OrderByDescending(sr => sr.Score)
                         .Take(request.K);
        }

        return await query.ToListAsync(cancellationToken);
    }

    private async Task<Guid> GetOrCreateTopicIdAsync(string topicQuery, CancellationToken cancellationToken)
    {
        var topic = await _context.Topics
            .FirstOrDefaultAsync(t => t.Query == topicQuery, cancellationToken);

        if (topic == null)
        {
            topic = new Topic { Query = topicQuery, Language = "en" };
            _context.Topics.Add(topic);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return topic.Id;
    }

    private async Task<string> GeneratePdfReportAsync(
        Guid reportId, 
        ReportRequest request, 
        List<SearchResult> papers, 
        CancellationToken cancellationToken)
    {
        var reportsDir = Path.Combine(_environment.ContentRootPath, "Reports");
        Directory.CreateDirectory(reportsDir);
        
        var fileName = $"report_{reportId}.pdf";
        var filePath = Path.Combine(reportsDir, fileName);

        // Generate PDF using QuestPDF
        var document = CreateReportDocument(request, papers);
        document.GeneratePdf(filePath);

        return filePath;
    }

    private IDocument CreateReportDocument(ReportRequest request, List<SearchResult> papers)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(11));

                page.Header()
                    .Text($"{request.Branding.BrandName} Research Report")
                    .FontSize(20)
                    .FontColor(request.Branding.PrimaryColor)
                    .Bold();

                page.Content()
                    .PaddingVertical(1, Unit.Centimetre)
                    .Column(column =>
                    {
                        // Cover section
                        if (request.Sections.Contains("cover"))
                        {
                            column.Item().Element(CreateCoverSection(request));
                        }

                        // Executive summary
                        if (request.Sections.Contains("executive"))
                        {
                            column.Item().Element(CreateExecutiveSummary(request, papers));
                        }

                        // Papers overview
                        if (request.Sections.Contains("overview"))
                        {
                            column.Item().Element(CreatePapersOverview(papers));
                        }

                        // Key findings
                        if (request.Sections.Contains("key-findings"))
                        {
                            column.Item().Element(CreateKeyFindings(papers));
                        }

                        // Per-paper details
                        if (request.Sections.Contains("per-paper"))
                        {
                            column.Item().Element(CreatePerPaperSection(papers));
                        }

                        // Charts section
                        if (request.Sections.Contains("charts"))
                        {
                            column.Item().Element(CreateChartsSection(papers, request.Charts));
                        }

                        // References
                        if (request.Sections.Contains("references"))
                        {
                            column.Item().Element(CreateReferencesSection(papers));
                        }
                    });

                page.Footer()
                    .AlignCenter()
                    .Text(x =>
                    {
                        x.Span("Generated by ");
                        x.Span(request.Branding.BrandName).FontColor(request.Branding.PrimaryColor);
                        x.Span($" on {DateTime.Now:yyyy-MM-dd HH:mm}");
                    });
            });
        });
    }

    private static Action<IContainer> CreateCoverSection(ReportRequest request)
    {
        return container =>
        {
            container.Column(column =>
            {
                column.Item().AlignCenter().Text(request.Topic)
                    .FontSize(24).Bold().FontColor("#2c3e50");
                
                column.Item().PaddingTop(20).AlignCenter()
                    .Text("Research Literature Review")
                    .FontSize(16).FontColor("#7f8c8d");
                
                column.Item().PaddingTop(40).AlignCenter()
                    .Text($"Generated on {DateTime.Now:MMMM dd, yyyy}")
                    .FontSize(12);
            });
        };
    }

    private static Action<IContainer> CreateExecutiveSummary(ReportRequest request, List<SearchResult> papers)
    {
        return container =>
        {
            container.Column(column =>
            {
                column.Item().PaddingTop(30).Text("Executive Summary")
                    .FontSize(18).Bold().FontColor("#2c3e50");
                
                column.Item().PaddingTop(10).Text(text =>
                {
                    text.Line($"This report analyzes {papers.Count} academic papers related to '{request.Topic}'.");
                    text.Line($"The papers span from {papers.Where(p => p.Year.HasValue).Min(p => p.Year)} to {papers.Where(p => p.Year.HasValue).Max(p => p.Year)}.");
                    text.Line($"Open access papers comprise {papers.Count(p => p.IsOpenAccess)} of the total ({papers.Count(p => p.IsOpenAccess) * 100 / papers.Count}%).");
                });
            });
        };
    }

    private static Action<IContainer> CreatePapersOverview(List<SearchResult> papers)
    {
        return container =>
        {
            container.Column(column =>
            {
                column.Item().PaddingTop(20).Text("Papers Overview")
                    .FontSize(16).Bold().FontColor("#2c3e50");
                
                column.Item().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(CellStyle).Text("Title").Bold();
                        header.Cell().Element(CellStyle).Text("Year").Bold();
                        header.Cell().Element(CellStyle).Text("Source").Bold();
                        header.Cell().Element(CellStyle).Text("Open Access").Bold();
                    });

                    foreach (var paper in papers.Take(10)) // Show first 10
                    {
                        table.Cell().Element(CellStyle).Text(paper.Title.Length > 60 
                            ? paper.Title[..60] + "..." 
                            : paper.Title);
                        table.Cell().Element(CellStyle).Text(paper.Year?.ToString() ?? "N/A");
                        table.Cell().Element(CellStyle).Text(paper.Source);
                        table.Cell().Element(CellStyle).Text(paper.IsOpenAccess ? "✓" : "✗");
                    }
                });

                if (papers.Count > 10)
                {
                    column.Item().PaddingTop(5).Text($"... and {papers.Count - 10} more papers")
                        .FontSize(10).Italic().FontColor("#7f8c8d");
                }
            });
        };
    }

    private static Action<IContainer> CreateKeyFindings(List<SearchResult> papers)
    {
        return container =>
        {
            container.Column(column =>
            {
                column.Item().PaddingTop(20).Text("Key Findings")
                    .FontSize(16).Bold().FontColor("#2c3e50");
                
                var summaries = papers.Where(p => p.Summary != null).ToList();
                
                if (summaries.Any())
                {
                    column.Item().PaddingTop(10).Text($"Summary analysis based on {summaries.Count} papers with extracted summaries:");
                    
                    foreach (var paper in summaries.Take(5))
                    {
                        column.Item().PaddingTop(10).Column(subColumn =>
                        {
                            subColumn.Item().Text(paper.Title).Bold().FontSize(12);
                            if (!string.IsNullOrEmpty(paper.Summary?.TlDr))
                            {
                                subColumn.Item().PaddingTop(5).Text(paper.Summary.TlDr)
                                    .FontSize(10).FontColor("#34495e");
                            }
                        });
                    }
                }
                else
                {
                    column.Item().PaddingTop(10).Text("No extracted summaries available. Run summarization to get key findings.")
                        .FontColor("#e74c3c").Italic();
                }
            });
        };
    }

    private static Action<IContainer> CreatePerPaperSection(List<SearchResult> papers)
    {
        return container =>
        {
            container.Column(column =>
            {
                column.Item().PaddingTop(20).Text("Paper Details")
                    .FontSize(16).Bold().FontColor("#2c3e50");
                
                foreach (var paper in papers.Take(5))
                {
                    column.Item().PaddingTop(15).Column(paperColumn =>
                    {
                        paperColumn.Item().Text(paper.Title).Bold().FontSize(12);
                        
                        if (!string.IsNullOrEmpty(paper.Abstract))
                        {
                            paperColumn.Item().PaddingTop(5).Text("Abstract:")
                                .FontSize(10).Bold();
                            paperColumn.Item().Text(paper.Abstract.Length > 300 
                                ? paper.Abstract[..300] + "..."
                                : paper.Abstract)
                                .FontSize(10);
                        }
                        
                        paperColumn.Item().PaddingTop(5).Text(text =>
                        {
                            if (!string.IsNullOrEmpty(paper.Doi))
                                text.Line($"DOI: {paper.Doi}");
                            if (paper.Year.HasValue)
                                text.Line($"Year: {paper.Year}");
                            if (!string.IsNullOrEmpty(paper.Venue))
                                text.Line($"Venue: {paper.Venue}");
                        }).FontSize(9).FontColor("#7f8c8d");
                    });
                }
            });
        };
    }

    private static Action<IContainer> CreateChartsSection(List<SearchResult> papers, List<string> charts)
    {
        return container =>
        {
            container.Column(column =>
            {
                column.Item().PaddingTop(20).Text("Statistics & Charts")
                    .FontSize(16).Bold().FontColor("#2c3e50");
                
                if (charts.Contains("by-year"))
                {
                    var yearGroups = papers.Where(p => p.Year.HasValue)
                        .GroupBy(p => p.Year.Value)
                        .OrderBy(g => g.Key)
                        .Take(10);
                    
                    column.Item().PaddingTop(10).Text("Papers by Year:")
                        .Bold().FontSize(12);
                    
                    foreach (var group in yearGroups)
                    {
                        column.Item().Text($"{group.Key}: {group.Count()} papers")
                            .FontSize(10);
                    }
                }

                if (charts.Contains("source-breakdown"))
                {
                    var sourceGroups = papers.GroupBy(p => p.Source)
                        .OrderByDescending(g => g.Count());
                    
                    column.Item().PaddingTop(15).Text("Papers by Source:")
                        .Bold().FontSize(12);
                    
                    foreach (var group in sourceGroups)
                    {
                        column.Item().Text($"{group.Key}: {group.Count()} papers")
                            .FontSize(10);
                    }
                }

                if (charts.Contains("oa-vs-paywalled"))
                {
                    var oaCount = papers.Count(p => p.IsOpenAccess);
                    var paywallCount = papers.Count - oaCount;
                    
                    column.Item().PaddingTop(15).Text("Open Access vs Paywalled:")
                        .Bold().FontSize(12);
                    
                    column.Item().Text($"Open Access: {oaCount} papers ({oaCount * 100 / papers.Count}%)")
                        .FontSize(10);
                    column.Item().Text($"Paywalled: {paywallCount} papers ({paywallCount * 100 / papers.Count}%)")
                        .FontSize(10);
                }
            });
        };
    }

    private static Action<IContainer> CreateReferencesSection(List<SearchResult> papers)
    {
        return container =>
        {
            container.Column(column =>
            {
                column.Item().PaddingTop(20).Text("References")
                    .FontSize(16).Bold().FontColor("#2c3e50");
                
                for (int i = 0; i < papers.Count; i++)
                {
                    var paper = papers[i];
                    column.Item().PaddingTop(5).Text(text =>
                    {
                        text.Span($"[{i + 1}] ");
                        text.Span(paper.Title).Bold();
                        
                        if (!string.IsNullOrEmpty(paper.AuthorsJson))
                        {
                            try
                            {
                                var authors = JsonSerializer.Deserialize<List<dynamic>>(paper.AuthorsJson);
                                if (authors != null && authors.Any())
                                {
                                    text.Span($" by {string.Join(", ", authors.Take(3).Select(a => a.ToString()))}");
                                    if (authors.Count > 3) text.Span(" et al.");
                                }
                            }
                            catch { /* Ignore JSON parsing errors */ }
                        }
                        
                        if (paper.Year.HasValue)
                            text.Span($" ({paper.Year})");
                        
                        if (!string.IsNullOrEmpty(paper.Doi))
                            text.Span($". DOI: {paper.Doi}");
                    }).FontSize(9);
                }
            });
        };
    }

    private static IContainer CellStyle(IContainer container)
    {
        return container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(5);
    }

    public async Task<byte[]> GetReportPdfAsync(Guid reportId, CancellationToken cancellationToken = default)
    {
        var report = await _context.Reports
            .FirstOrDefaultAsync(r => r.Id == reportId, cancellationToken);

        if (report == null || string.IsNullOrEmpty(report.PdfPath))
        {
            throw new FileNotFoundException("Report not found or PDF not generated");
        }

        if (!File.Exists(report.PdfPath))
        {
            throw new FileNotFoundException("Report PDF file not found on disk");
        }

        return await File.ReadAllBytesAsync(report.PdfPath, cancellationToken);
    }

    public async Task<Report?> GetReportAsync(Guid reportId, CancellationToken cancellationToken = default)
    {
        return await _context.Reports
            .Include(r => r.Topic)
            .FirstOrDefaultAsync(r => r.Id == reportId, cancellationToken);
    }
}

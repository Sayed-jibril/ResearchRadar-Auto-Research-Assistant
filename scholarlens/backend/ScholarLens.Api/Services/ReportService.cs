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
                    PagesGenerated = 1,
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
            query = query.Where(sr => request.PaperIds.Contains(sr.Id));
        }
        else
        {
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
        await Task.Run(() => document.GeneratePdf(filePath), cancellationToken);

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

                page.Header().Text($"{request.Branding.BrandName} Research Report")
                    .FontSize(20).Bold();

                page.Content().PaddingVertical(1, Unit.Centimetre).Column(column =>
                {
                    // Cover
                    column.Item().AlignCenter().Text(request.Topic)
                        .FontSize(24).Bold();
                    
                    column.Item().PaddingTop(20).AlignCenter()
                        .Text("Research Literature Review")
                        .FontSize(16);

                    // Summary
                    column.Item().PaddingTop(30).Text("Executive Summary")
                        .FontSize(18).Bold();
                    
                    column.Item().PaddingTop(10).Text($"This report analyzes {papers.Count} academic papers related to '{request.Topic}'.");

                    // Papers
                    column.Item().PaddingTop(20).Text("Papers Overview")
                        .FontSize(16).Bold();
                    
                    var count = 0;
                    foreach (var paper in papers.Take(5))
                    {
                        count++;
                        column.Item().PaddingTop(10).Text($"{count}. {paper.Title}")
                            .FontSize(12).Bold();
                        
                        if (!string.IsNullOrEmpty(paper.Doi))
                            column.Item().Text($"   DOI: {paper.Doi}").FontSize(10);
                        
                        if (paper.Year.HasValue)
                            column.Item().Text($"   Year: {paper.Year}").FontSize(10);
                        
                        column.Item().Text($"   Source: {paper.Source}").FontSize(10);
                    }

                    if (papers.Count > 5)
                    {
                        column.Item().PaddingTop(5).Text($"... and {papers.Count - 5} more papers").FontSize(10);
                    }

                    // Stats
                    column.Item().PaddingTop(20).Text("Statistics").FontSize(16).Bold();

                    var oaCount = papers.Count(p => p.IsOpenAccess);
                    column.Item().PaddingTop(10).Text($"Open Access: {oaCount}/{papers.Count} papers").FontSize(12);

                    var sourceGroups = papers.GroupBy(p => p.Source);
                    foreach (var group in sourceGroups)
                    {
                        column.Item().Text($"{group.Key}: {group.Count()} papers").FontSize(10);
                    }
                });

                page.Footer().AlignCenter().Text($"Generated by {request.Branding.BrandName} on {DateTime.Now:yyyy-MM-dd}");
            });
        });
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

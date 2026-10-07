using Microsoft.EntityFrameworkCore;
using ScholarLens.Api.Models;

namespace ScholarLens.Api.Data;

/// <summary>
/// Entity Framework DbContext for ScholarLens database
/// </summary>
public class ScholarLensDbContext : DbContext
{
    public ScholarLensDbContext(DbContextOptions<ScholarLensDbContext> options) : base(options)
    {
    }

    public DbSet<Topic> Topics { get; set; }
    public DbSet<SearchResult> SearchResults { get; set; }
    public DbSet<PaperText> PaperTexts { get; set; }
    public DbSet<Summary> Summaries { get; set; }
    public DbSet<Report> Reports { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure table names (snake_case for PostgreSQL convention)
        modelBuilder.Entity<Topic>().ToTable("topics");
        modelBuilder.Entity<SearchResult>().ToTable("search_results");
        modelBuilder.Entity<PaperText>().ToTable("paper_texts");
        modelBuilder.Entity<Summary>().ToTable("summaries");
        modelBuilder.Entity<Report>().ToTable("reports");

        // Configure indexes for performance
        
        // Unique index on SearchResult.Doi (nullable but unique when not null)
        modelBuilder.Entity<SearchResult>()
            .HasIndex(sr => sr.Doi)
            .IsUnique()
            .HasFilter("doi IS NOT NULL");

        // Index on TopicId for foreign key lookups
        modelBuilder.Entity<SearchResult>()
            .HasIndex(sr => sr.TopicId);

        // Index on Year for filtering
        modelBuilder.Entity<SearchResult>()
            .HasIndex(sr => sr.Year);

        // GIN indexes for JSON columns (PostgreSQL specific)
        modelBuilder.Entity<SearchResult>()
            .HasIndex(sr => sr.AuthorsJson)
            .HasMethod("gin");

        modelBuilder.Entity<SearchResult>()
            .HasIndex(sr => sr.RawJson)
            .HasMethod("gin");

        modelBuilder.Entity<Summary>()
            .HasIndex(s => s.KeyPointsJson)
            .HasMethod("gin");

        modelBuilder.Entity<Summary>()
            .HasIndex(s => s.LimitationsJson)
            .HasMethod("gin");

        modelBuilder.Entity<Report>()
            .HasIndex(r => r.ParametersJson)
            .HasMethod("gin");

        // One-to-one relationships
        modelBuilder.Entity<PaperText>()
            .HasOne(pt => pt.SearchResult)
            .WithOne(sr => sr.PaperText)
            .HasForeignKey<PaperText>(pt => pt.SearchResultId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Summary>()
            .HasOne(s => s.SearchResult)
            .WithOne(sr => sr.Summary)
            .HasForeignKey<Summary>(s => s.SearchResultId)
            .OnDelete(DeleteBehavior.Cascade);

        // One-to-many relationships
        modelBuilder.Entity<SearchResult>()
            .HasOne(sr => sr.Topic)
            .WithMany(t => t.SearchResults)
            .HasForeignKey(sr => sr.TopicId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Report>()
            .HasOne(r => r.Topic)
            .WithMany(t => t.Reports)
            .HasForeignKey(r => r.TopicId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

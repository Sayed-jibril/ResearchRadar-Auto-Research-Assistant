using ScholarLens.Api.Services.ExternalApis;

namespace ScholarLens.Api.Services;

/// <summary>
/// Interface for paper ranking services
/// </summary>
public interface IRankingService
{
    Task<List<ExternalPaper>> RankPapersAsync(
        string query, 
        List<ExternalPaper> papers, 
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of paper ranking using BM25 + semantic similarity + recency decay
/// </summary>
public class RankingService : IRankingService
{
    private readonly ILogger<RankingService> _logger;

    // Ranking weights as specified in the requirements
    private const double KeywordWeight = 0.6;
    private const double SemanticWeight = 0.3; // Will be 0 for now (no embeddings yet)
    private const double RecencyWeight = 0.1;

    public RankingService(ILogger<RankingService> logger)
    {
        _logger = logger;
    }

    public async Task<List<ExternalPaper>> RankPapersAsync(
        string query, 
        List<ExternalPaper> papers, 
        CancellationToken cancellationToken = default)
    {
        if (!papers.Any())
            return papers;

        _logger.LogInformation("Ranking {Count} papers for query: {Query}", papers.Count, query);

        var currentYear = DateTime.Now.Year;
        var queryTerms = TokenizeQuery(query);

        var rankedPapers = new List<(ExternalPaper paper, double score)>();

        foreach (var paper in papers)
        {
            var keywordScore = CalculateBM25Score(queryTerms, paper);
            var semanticScore = 0.0; // TODO: Implement semantic similarity when embeddings are available
            var recencyScore = CalculateRecencyScore(paper.Year, currentYear);

            var finalScore = (KeywordWeight * keywordScore) + 
                           (SemanticWeight * semanticScore) + 
                           (RecencyWeight * recencyScore);

            rankedPapers.Add((paper, finalScore));
        }

        // Sort by score descending
        var sortedPapers = rankedPapers
            .OrderByDescending(x => x.score)
            .Select(x => x.paper)
            .ToList();

        _logger.LogInformation("Ranking completed. Top score: {TopScore:F3}", 
            rankedPapers.Any() ? rankedPapers.Max(x => x.score) : 0);

        return sortedPapers;
    }

    private List<string> TokenizeQuery(string query)
    {
        // Simple tokenization - split on whitespace and punctuation
        var tokens = query.ToLowerInvariant()
            .Split(new[] { ' ', '\t', '\n', '\r', '.', ',', ';', ':', '!', '?' }, 
                   StringSplitOptions.RemoveEmptyEntries)
            .Where(token => token.Length > 2) // Remove very short tokens
            .Distinct()
            .ToList();

        return tokens;
    }

    private double CalculateBM25Score(List<string> queryTerms, ExternalPaper paper)
    {
        // Simplified BM25 implementation
        // In production, consider using a proper IR library

        var k1 = 1.2; // Term frequency saturation parameter
        var b = 0.75;  // Length normalization parameter

        // Combine title and abstract for scoring
        var documentText = $"{paper.Title} {paper.Abstract}".ToLowerInvariant();
        var documentTokens = TokenizeText(documentText);
        
        if (!documentTokens.Any())
            return 0.0;

        var avgDocLength = documentTokens.Count; // Simplified - should be corpus average
        var docLength = documentTokens.Count;

        var score = 0.0;

        foreach (var term in queryTerms)
        {
            var termFreq = documentTokens.Count(token => token.Contains(term) || term.Contains(token));
            
            if (termFreq == 0)
                continue;

            // Simplified IDF calculation (would need corpus statistics for proper IDF)
            var idf = Math.Log(100.0 / Math.Max(1, termFreq)); // Assume corpus of 100 docs

            var tf = (termFreq * (k1 + 1)) / 
                     (termFreq + k1 * (1 - b + b * (docLength / avgDocLength)));

            score += idf * tf;
        }

        // Normalize score
        return Math.Min(1.0, score / queryTerms.Count);
    }

    private List<string> TokenizeText(string text)
    {
        return text.Split(new[] { ' ', '\t', '\n', '\r', '.', ',', ';', ':', '!', '?' }, 
                         StringSplitOptions.RemoveEmptyEntries)
                  .Where(token => token.Length > 2)
                  .ToList();
    }

    private double CalculateRecencyScore(int? paperYear, int currentYear)
    {
        if (!paperYear.HasValue)
            return 0.0;

        var yearDiff = currentYear - paperYear.Value;
        
        // Exponential decay: exp(-0.15 * yearDiff)
        // More recent papers get higher scores
        return Math.Exp(-0.15 * Math.Max(0, yearDiff));
    }
}

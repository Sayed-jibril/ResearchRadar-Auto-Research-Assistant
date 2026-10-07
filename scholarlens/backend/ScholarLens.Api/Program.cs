using Microsoft.EntityFrameworkCore;
using ScholarLens.Api.Data;
using ScholarLens.Api.Services;
using ScholarLens.Api.Services.ExternalApis;
using Serilog;
using Polly;
using Polly.Extensions.Http;
using FluentValidation.AspNetCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (connectionString == "InMemory")
{
    // Use in-memory database for demo
    builder.Services.AddDbContext<ScholarLensDbContext>(options =>
        options.UseInMemoryDatabase("ScholarLensDemo"));
}
else
{
    // Use PostgreSQL for production
    builder.Services.AddDbContext<ScholarLensDbContext>(options =>
        options.UseNpgsql(connectionString));
}

// Add Redis for caching
var redisConnection = builder.Configuration.GetConnectionString("Redis");
if (redisConnection != "InMemory")
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnection ?? "localhost:6379";
    });
}
else
{
    // Use in-memory cache for demo
    builder.Services.AddDistributedMemoryCache();
}

// Configure external API options
builder.Services.Configure<CrossrefOptions>(
    builder.Configuration.GetSection("ExternalApis:Crossref"));
builder.Services.Configure<ArxivOptions>(
    builder.Configuration.GetSection("ExternalApis:Arxiv"));
builder.Services.Configure<SemanticScholarOptions>(
    builder.Configuration.GetSection("ExternalApis:SemanticScholar"));
builder.Services.Configure<UnpaywallOptions>(
    builder.Configuration.GetSection("ExternalApis:Unpaywall"));

// Helper method for retry policy
static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
}

// Register HTTP clients with Polly policies
builder.Services.AddHttpClient<CrossrefClient>()
    .AddPolicyHandler(GetRetryPolicy());
builder.Services.AddHttpClient<ArxivClient>()
    .AddPolicyHandler(GetRetryPolicy());
builder.Services.AddHttpClient<SemanticScholarClient>()
    .AddPolicyHandler(GetRetryPolicy());
builder.Services.AddHttpClient<UnpaywallClient>()
    .AddPolicyHandler(GetRetryPolicy());

// Register services
builder.Services.AddScoped<IPaperSearchClient, CrossrefClient>();
builder.Services.AddScoped<IPaperSearchClient, ArxivClient>();
builder.Services.AddScoped<IPaperSearchClient, SemanticScholarClient>();
builder.Services.AddScoped<IOpenAccessClient, UnpaywallClient>();
builder.Services.AddScoped<IRankingService, RankingService>();
builder.Services.AddScoped<SearchService>();
builder.Services.AddScoped<IngestService>();
builder.Services.AddScoped<SummarizeService>();
builder.Services.AddScoped<ReportService>();

// Add FluentValidation
builder.Services.AddFluentValidationAutoValidation();

builder.Services.AddControllers();

// Add CORS support
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "http://localhost:3001", "http://localhost:3002")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials()
              .SetPreflightMaxAge(TimeSpan.FromMinutes(5));
    });
});

// Add OpenAPI/Swagger services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo 
    { 
        Title = "ScholarLens API", 
        Version = "v1",
        Description = "API for academic research assistance and paper analysis"
    });
});

// Add health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ScholarLensDbContext>();

var app = builder.Build();

// Migrate database on startup (development only)
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<ScholarLensDbContext>();
    
    // Only migrate if using PostgreSQL, not in-memory database  
    var dbConnectionString = app.Configuration.GetConnectionString("DefaultConnection");
    if (dbConnectionString != "InMemory")
    {
        await context.Database.MigrateAsync();
    }
    else
    {
        // For in-memory database, just ensure it's created
        await context.Database.EnsureCreatedAsync();
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "ScholarLens API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();

app.UseCors("AllowFrontend");

app.UseAuthorization();

app.MapControllers();

// Add health check endpoint
app.MapHealthChecks("/health");

// Remove the custom OPTIONS handler - let CORS middleware handle it

// Add root endpoint
app.MapGet("/", () => new { 
    message = "ScholarLens API", 
    version = "1.0.0", 
    endpoints = new { 
        health = "/health", 
        api = "/api", 
        swagger = "/swagger" 
    } 
});

app.Run();


using Neo4j.Driver;
using Recommendation.Consumers;
using Recommendation.Persistence;
using Recommendation.Services;
using Asp.Versioning;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);

        options.AssumeDefaultVersionWhenUnspecified = true;

        options.ReportApiVersions = true;

        options.ApiVersionReader =
            new UrlSegmentApiVersionReader();
    })
    .AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });

// ========================================
// NEO4J DATABASE CONNECTION
// ========================================

builder.Services.AddSingleton<IDriver>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();

    var uri = config["Neo4j:Uri"]
        ?? throw new InvalidOperationException(
            "Neo4j URI is missing");

    var username = config["Neo4j:Username"]
        ?? throw new InvalidOperationException(
            "Neo4j username is missing");

    var password = config["Neo4j:Password"]
        ?? throw new InvalidOperationException(
            "Neo4j password is missing");

    return GraphDatabase.Driver(
        uri,
        AuthTokens.Basic(username, password));
});

// ========================================
// GRAPH REPOSITORY
// ========================================

builder.Services.AddSingleton<GraphRepository>();
builder.Services.AddSingleton<RecommendationQueries>();

// ========================================
// RABBITMQ CONSUMER
// ========================================

builder.Services.AddHostedService<RecommendationConsumer>();

// ========================================
// OLLAMA (LLM RANKING)
// ========================================

builder.Services.AddHttpClient<OllamaClient>(client =>
{
    var baseUrl = builder.Configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";
    client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
    // Local models on CPU can be slow; fall back to graph ranking after this.
    client.Timeout = TimeSpan.FromSeconds(
        builder.Configuration.GetValue("Ollama:TimeoutSeconds", 60));
});

builder.Services.AddScoped<RecommendationService>();

var app = builder.Build();

var apiVersionSet = app.NewApiVersionSet()
    .HasApiVersion(new ApiVersion(1, 0))
    .ReportApiVersions()
    .Build();

var api = app.MapGroup("/api/v{version:apiVersion}")
    .WithApiVersionSet(apiVersionSet);

// ========================================
// API V1
// ========================================

var v1 = api.MapGroup("")
    .HasApiVersion(new ApiVersion(1, 0));

// Service status.
v1.MapGet("/", () =>
    Results.Ok(new
    {
        Service = "Recommendation",
        Status = "Running",
        Version = "1.0"
    }));

// Neo4j health.
v1.MapGet("/health/neo4j", async (IDriver driver) =>
{
    try
    {
        await driver.VerifyConnectivityAsync();

        return Results.Ok(new
        {
            Status = "Connected to Neo4j"
        });
    }
    catch
    {
        return Results.StatusCode(503);
    }
});

// Recommendations.
v1.MapGet("/recommendations/{loanerId:int}", async (
    int loanerId,
    int? count,
    RecommendationService service,
    CancellationToken cancellationToken) =>
{
    var take = Math.Clamp(count ?? 5, 1, 20);

    return Results.Ok(
        await service.RecommendAsync(
            loanerId,
            take,
            cancellationToken));
});

app.Run();

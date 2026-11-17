using Neo4j.Driver;
using Recommendation.Consumers;
using Recommendation.Persistence;
using Recommendation.Services;

internal class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddSingleton(_ =>
        {
            var config = builder.Configuration;

            return GraphDatabase.Driver(
                config["Neo4j:Uri"]!,
                AuthTokens.Basic(
                    config["Neo4j:Username"]!,
                    config["Neo4j:Password"]!));
        });

        builder.Services.AddSingleton<GraphRepository>();
        builder.Services.AddSingleton<RecommendationQueries>();
        builder.Services.AddHostedService<RecommendationConsumer>();

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

        app.MapGet("/", () => "Recommendation service is running");

        app.MapGet("/recommendations/{loanerId:int}", async (
            int loanerId,
            int? count,
            RecommendationService service,
            CancellationToken cancellationToken) =>
        {
            var take = Math.Clamp(count ?? 5, 1, 20);
            return Results.Ok(await service.RecommendAsync(loanerId, take, cancellationToken));
        });

        app.Run();
    }
}
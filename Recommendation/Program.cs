using Neo4j.Driver;

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

 //       builder.Services.AddHostedService<RecommendationConsumer>();

        var app = builder.Build();

        app.MapGet("/", () => "Recommendation service is running");

        app.Run();
    }
}
using Catalog.GraphQL;
using Catalog.Messaging;
using Catalog.Persistence.Read;
using Catalog.Persistence.Write;
using Catalog.Services;
using Catalog.Workers;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;



var builder = WebApplication.CreateBuilder(args);

//SQL stuff
var connectionString = builder.Configuration
    .GetConnectionString("CatalogWrite")
    ?? throw new InvalidOperationException(
        "Catalog MySQL connection string is missing");

builder.Services.AddDbContext<CatalogWriteDbContext>(
    options => options.UseMySql(
        connectionString,
        new MySqlServerVersion(new Version(8, 4, 0))));



// ---------------------------------------
// MongoDB - CQRS READ DATABASE
// ---------------------------------------

// Read connection settings
var mongoConnectionString =
    builder.Configuration["Mongo:ConnectionString"]
    ?? throw new InvalidOperationException(
        "MongoDB connection string is missing");

var mongoDatabaseName =
    builder.Configuration["Mongo:DatabaseName"]
    ?? "catalog_read";

var mongoCollectionName =
    builder.Configuration["Mongo:CollectionName"]
    ?? "items";

// Register MongoClient as a singleton
builder.Services.AddSingleton<IMongoClient>(
    _ => new MongoClient(mongoConnectionString));

// Register the database
builder.Services.AddSingleton<IMongoDatabase>(sp =>
{
    var client = sp.GetRequiredService<IMongoClient>();

    return client.GetDatabase(mongoDatabaseName);
});

// Register the Catalog items collection
builder.Services.AddSingleton<
    IMongoCollection<CatalogItemDocument>>(sp =>
    {
        var database =
            sp.GetRequiredService<IMongoDatabase>();

        return database.GetCollection<CatalogItemDocument>(
            mongoCollectionName);
    });

//graphql stuff
builder.Services
    .AddGraphQLServer()
    .AddQueryType<Query>()
    .AddMutationType<Mutation>();


//command service
builder.Services.AddScoped<CatalogCommandService>();

//RabbitMQ event publisher
builder.Services.AddSingleton<CatalogEventPublisher>();

//background worker for outbox processing
builder.Services.AddHostedService<CatalogOutboxWorker>();

// Add services to the container.

var app = builder.Build();

//more graphql stuff
app.MapGraphQL("/graphql");


// Configure the HTTP request pipeline.

app.UseHttpsRedirection();


app.Run();



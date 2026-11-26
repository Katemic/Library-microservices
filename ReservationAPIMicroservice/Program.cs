
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using ReservationAPIMicroservice.Data;
using ReservationAPIMicroservice.Services;
using ReservationAPIMicroservice.Services.RabbitMQ;
using System.Reflection;
using Asp.Versioning;

var builder = WebApplication.CreateBuilder(args);
Console.WriteLine("Starting app");
builder.Services.AddControllers();

builder.Services.AddDbContext<ReservationDbContext>(options =>
    options.UseMySql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        new MySqlServerVersion(new Version(8, 0, 0))
    ));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services
    .AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ReportApiVersions = true;
        options.ApiVersionReader = new UrlSegmentApiVersionReader();
    })
    .AddMvc();

// RabbitMQ
var rabbitHost = builder.Configuration["RabbitMQ:Host"] ?? "localhost";
var rabbitPort = int.Parse(
    builder.Configuration["RabbitMQ:Port"] ?? "5672");

builder.Services.AddSingleton<IConnection>(_ =>
{
    var factory = new ConnectionFactory
    {
        HostName = rabbitHost,
        Port = rabbitPort,
        UserName = builder.Configuration["RabbitMQ:User"] ?? "guest",
        Password = builder.Configuration["RabbitMQ:Password"] ?? "guest"
    };

    return factory.CreateConnectionAsync()
        .GetAwaiter()
        .GetResult();
});

builder.Services.AddScoped<
    IReservationService,
    ReservationService>();
builder.Services.AddScoped<
    IRabbitMqPublisher,
    RabbitMqPublisher>();
builder.Services.AddHostedService<InventoryConsumerService>();
builder.Services.AddHostedService<ItemConsumerService>();

var app = builder.Build();

app.MapGet("/health", () =>
    Results.Ok(new
    {
        status = "ok",
        service = "reservation"
    }));

app.UseSwagger();
app.UseSwaggerUI();

Console.WriteLine("Mapping Controllers");
try
{
    app.MapControllers();
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.ToString());
    throw;
}

app.Run();

public partial class Program { }
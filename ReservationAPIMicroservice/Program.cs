
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using ReservationAPIMicroservice.Data;
using ReservationAPIMicroservice.Messaging;
using ReservationAPIMicroservice.Services;
using ReservationAPIMicroservice.Services.Consumers;
using System.Reflection;

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

builder.Services.AddSingleton<RabbitMqPublisher>();

builder.Services.AddHostedService<InventoryConsumerService>();
builder.Services.AddHostedService<ItemConsumerService>();

var app = builder.Build();

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

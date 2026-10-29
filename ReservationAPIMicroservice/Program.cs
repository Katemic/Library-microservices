
using Microsoft.EntityFrameworkCore;
using ReservationAPIMicroservice.Data;
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

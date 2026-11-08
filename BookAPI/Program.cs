using BookAPI.Repositories;
using BookAPI.ServiceDiscovery;
using BookAPI.Settings;
using Common.Models;
using Consul;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.Configure<MongoDbSettings>(builder.Configuration.GetSection("MongoDbSettings"));
builder.Services.AddSingleton<IMongoDbSettings>(provider => provider.GetRequiredService<IOptions<MongoDbSettings>>().Value);

builder.Services.AddScoped<IMongoRepository<Book>, MongoRepository<Book>>();

// Health check: ping la MongoDB
builder.Services.AddSingleton<MongoHealthCheck>();
builder.Services.AddHealthChecks().AddCheck<MongoHealthCheck>("mongodb");

// Înregistrare în Consul
builder.Services.AddSingleton<IConsulClient>(_ => new ConsulClient(cfg =>
{
    var address = builder.Configuration["ServiceDiscovery:ConsulAddress"] ?? "http://consul:8500";
    cfg.Address = new Uri(address);
}));
builder.Services.AddHostedService<ConsulRegistrationService>();

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
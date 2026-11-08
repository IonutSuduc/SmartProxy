using Ocelot.Cache.CacheManager;
using Ocelot.DependencyInjection;
using Ocelot.Discovery.Consul;
using Ocelot.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);

builder.Services
    .AddOcelot(builder.Configuration)
    .AddConsul<BookApiConsulServiceBuilder>()
    .AddCacheManager(x => x.WithDictionaryHandle());

var app = builder.Build();

await app.UseOcelot();

app.Run();
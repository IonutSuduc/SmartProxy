using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Ocelot.Cache.CacheManager;

var builder = WebApplication.CreateBuilder(args);

// 1. Îi spunem .NET-ului să citească regulile din fișierul ocelot.json
builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);

// 2. Înregistrăm serviciile Ocelot (Echivalentul ConfigureServices din tutorial)
builder.Services
    .AddOcelot(builder.Configuration)
    .AddCacheManager(x => x.WithDictionaryHandle());

var app = builder.Build();

// 3. Activăm middleware-ul Ocelot (Echivalentul Configure din tutorial)
await app.UseOcelot();

app.Run();
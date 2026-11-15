using Ocelot.DependencyInjection;
using Ocelot.Discovery.Consul;
using Ocelot.Middleware;
using Proxy.Sync.Cache;
using Proxy.Sync.Helpers;
using Proxy.Sync.Queue;
using Proxy.Sync.Services;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);

builder.Services
    .AddOcelot(builder.Configuration)
    .AddConsul<BookApiConsulServiceBuilder>();

// Redis: AbortOnConnectFail=false, ca Proxy-ul să pornească chiar dacă Redis nu e încă gata
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
{
    var options = ConfigurationOptions.Parse(builder.Configuration["Redis:ConnectionString"] ?? "redis:6379");
    options.AbortOnConnectFail = false;
    return ConnectionMultiplexer.Connect(options);
});

builder.Services.AddHttpClient("bookapi", c => c.Timeout = TimeSpan.FromSeconds(2));
builder.Services.AddSingleton<IBookCache, RedisBookCache>();
builder.Services.AddSingleton<IWriteQueue, RedisWriteQueue>();
builder.Services.AddSingleton<InstanceRegistry>();
builder.Services.AddSingleton<WriteSyncService>();
builder.Services.AddHostedService<ReplayService>();

var app = builder.Build();

app.Use(async (context, next) =>
{
    var method = context.Request.Method;

    // GET: cache-aside în Redis
    if (HttpMethods.IsGet(method))
    {
        var cache = context.RequestServices.GetRequiredService<IBookCache>();
        var key = RedisBookCache.KeyFor(context.Request.Path, context.Request.QueryString);

        var cached = await cache.GetAsync(key);
        if (cached != null)
        {
            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
            context.Response.Headers["X-Cache"] = "HIT";
            await context.Response.WriteAsync(cached);
            return;
        }

        // Miss: lăsăm Ocelot să răspundă, captăm răspunsul și îl salvăm
        var original = context.Response.Body;
        using var buffer = new MemoryStream();
        context.Response.Body = buffer;
        try
        {
            await next();

            buffer.Position = 0;
            if (context.Response.StatusCode == 200 && buffer.Length > 0)
            {
                var text = await new StreamReader(buffer).ReadToEndAsync();
                await cache.SetAsync(key, text);
            }

            context.Response.Headers["X-Cache"] = "MISS";
            buffer.Position = 0;
            await buffer.CopyToAsync(original);
        }
        finally
        {
            context.Response.Body = original;
        }
        return;
    }

    // POST/PUT/DELETE: broadcast + coadă (invalidarea cache-ului se face în WriteSyncService)
    var isPostOrPut = HttpMethods.IsPost(method) || HttpMethods.IsPut(method);
    var isWrite = isPostOrPut || HttpMethods.IsDelete(method);

    if (!isWrite)
    {
        await next();
        return;
    }

    string? body = null;
    if (isPostOrPut)
    {
        using var reader = new StreamReader(context.Request.Body);
        var raw = await reader.ReadToEndAsync();
        body = BookBody.EnsureId(raw);

        if (body == null)
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync("Body JSON invalid");
            return;
        }
    }

    var sync = context.RequestServices.GetRequiredService<WriteSyncService>();
    var result = await sync.BroadcastAsync(
        method,
        context.Request.Path + context.Request.QueryString,
        body,
        context.RequestAborted);

    context.Response.StatusCode = result.StatusCode;
    if (!string.IsNullOrEmpty(result.Body))
    {
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(result.Body);
    }
});

await app.UseOcelot();

app.Run();
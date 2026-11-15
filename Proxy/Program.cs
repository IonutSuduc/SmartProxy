using Proxy.Sync.Cache;
using Proxy.Sync.Helpers;
using Proxy.Sync.Queue;
using Proxy.Sync.Resilience;
using Proxy.Sync.Services;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddSingleton<CircuitBreakerRegistry>();
builder.Services.AddSingleton<GetForwarder>();
builder.Services.AddSingleton<WriteSyncService>();
builder.Services.AddHostedService<ReplayService>();

var app = builder.Build();

app.Run(async context =>
{
    var method = context.Request.Method;

    // GET: cache-aside în Redis + forwarder cu circuit breaker
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

        // Miss: trimitem la o instanță, cu circuit breaker și retry pe alta
        var forwarder = context.RequestServices.GetRequiredService<GetForwarder>();
        var getResult = await forwarder.ForwardAsync(
            context.Request.Path + context.Request.QueryString, context.RequestAborted);

        if (getResult == null)
        {
            context.Response.StatusCode = 503;
            await context.Response.WriteAsync("Nicio instanță disponibilă");
            return;
        }

        if (getResult.StatusCode == 200 && getResult.Body.Length > 0)
            await cache.SetAsync(key, getResult.Body);

        context.Response.StatusCode = getResult.StatusCode;
        context.Response.ContentType = getResult.ContentType ?? "application/json";
        context.Response.Headers["X-Cache"] = "MISS";
        context.Response.Headers["X-Served-By"] = getResult.InstanceId;
        await context.Response.WriteAsync(getResult.Body);
        return;
    }

    // POST/PUT/DELETE: broadcast + coadă (invalidarea cache-ului se face în WriteSyncService)
    var isPostOrPut = HttpMethods.IsPost(method) || HttpMethods.IsPut(method);
    var isWrite = isPostOrPut || HttpMethods.IsDelete(method);

    if (!isWrite)
    {
        context.Response.StatusCode = 405;
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

app.Run();
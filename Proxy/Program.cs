using Proxy.Sync.Cache;
using Proxy.Sync.Helpers;
using Proxy.Sync.Queue;
using Proxy.Sync.Resilience;
using Proxy.Sync.Services;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

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

    // Formatul cerut de client: XML dacă Accept conține "xml", altfel JSON
    var acceptHeader = context.Request.Headers.Accept.ToString();
    var wantsXml = BookBody.IsXml(acceptHeader);
    var accept = wantsXml ? "application/xml" : "application/json";
    var format = wantsXml ? "xml" : "json";

    // GET: cache-aside în Redis + forwarder cu circuit breaker
    if (HttpMethods.IsGet(method))
    {
        var cache = context.RequestServices.GetRequiredService<IBookCache>();
        var key = RedisBookCache.KeyFor(context.Request.Path, context.Request.QueryString, format);

        var cached = await cache.GetAsync(key);
        if (cached != null)
        {
            context.Response.StatusCode = 200;
            context.Response.ContentType = accept;
            context.Response.Headers["X-Cache"] = "HIT";
            await context.Response.WriteAsync(cached);
            return;
        }

        var forwarder = context.RequestServices.GetRequiredService<GetForwarder>();
        var getResult = await forwarder.ForwardAsync(
            context.Request.Path + context.Request.QueryString, accept, context.RequestAborted);

        if (getResult == null)
        {
            context.Response.StatusCode = 503;
            await context.Response.WriteAsync("Nicio instanță disponibilă");
            return;
        }

        if (getResult.StatusCode == 200 && getResult.Body.Length > 0)
            await cache.SetAsync(key, getResult.Body);

        context.Response.StatusCode = getResult.StatusCode;
        context.Response.Headers["X-Cache"] = "MISS";
        context.Response.Headers["X-Served-By"] = getResult.InstanceId;

        // 204 și alte răspunsuri fără body nu permit scrierea în corp
        if (getResult.StatusCode != 204 && !string.IsNullOrEmpty(getResult.Body))
        {
            context.Response.ContentType = getResult.ContentType ?? accept;
            await context.Response.WriteAsync(getResult.Body);
        }
        return;
    }

    var isPostOrPut = HttpMethods.IsPost(method) || HttpMethods.IsPut(method);
    var isWrite = isPostOrPut || HttpMethods.IsDelete(method);

    if (!isWrite)
    {
        context.Response.StatusCode = 405;
        return;
    }

    string? body = null;
    var contentType = "application/json";
    if (isPostOrPut)
    {
        var requestIsXml = BookBody.IsXml(context.Request.ContentType);
        contentType = requestIsXml ? "application/xml" : "application/json";

        using var reader = new StreamReader(context.Request.Body);
        var raw = await reader.ReadToEndAsync();
        body = BookBody.EnsureId(raw, requestIsXml);

        if (body == null)
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync("Body invalid");
            return;
        }
    }

    var sync = context.RequestServices.GetRequiredService<WriteSyncService>();
    var result = await sync.BroadcastAsync(
        method,
        context.Request.Path + context.Request.QueryString,
        body,
        contentType,
        accept,
        context.RequestAborted);

    context.Response.StatusCode = result.StatusCode;
    if (!string.IsNullOrEmpty(result.Body))
    {
        context.Response.ContentType = result.ContentType ?? accept;
        await context.Response.WriteAsync(result.Body);
    }
});

app.Run();
using Proxy.Sync.Resilience;

namespace Proxy.Sync.Services
{
    public record ForwardResult(int StatusCode, string Body, string? ContentType, string InstanceId);

    public class GetForwarder
    {
        private readonly InstanceRegistry _registry;
        private readonly CircuitBreakerRegistry _breakers;
        private readonly IHttpClientFactory _httpFactory;
        private readonly ILogger<GetForwarder> _logger;
        private int _next;

        public GetForwarder(InstanceRegistry registry, CircuitBreakerRegistry breakers,
            IHttpClientFactory httpFactory, ILogger<GetForwarder> logger)
        {
            _registry = registry;
            _breakers = breakers;
            _httpFactory = httpFactory;
            _logger = logger;
        }

        // Round Robin peste instanțele sănătoase, cu retry pe următoarea la eșec. null = nicio instanță nu a răspuns.
        public async Task<ForwardResult?> ForwardAsync(string pathAndQuery, CancellationToken ct)
        {
            var candidates = _registry.Instances.Where(i => i.Healthy).ToList();
            if (candidates.Count == 0) return null;

            var start = Interlocked.Increment(ref _next) & int.MaxValue;

            for (var n = 0; n < candidates.Count; n++)
            {
                var inst = candidates[(start + n) % candidates.Count];
                if (!_breakers.AllowRequest(inst.Id)) continue;

                try
                {
                    var client = _httpFactory.CreateClient("bookapi");
                    using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(inst.BaseUri, pathAndQuery));
                    using var response = await client.SendAsync(request, ct);
                    var body = await response.Content.ReadAsStringAsync(ct);
                    var code = (int)response.StatusCode;

                    if (code >= 500)
                    {
                        _breakers.RecordFailure(inst.Id);
                        _logger.LogWarning("GET {Path} pe {Id}: {Status}, încercăm altă instanță", pathAndQuery, inst.Id, code);
                        continue;
                    }

                    _breakers.RecordSuccess(inst.Id);
                    return new ForwardResult(code, body, response.Content.Headers.ContentType?.ToString(), inst.Id);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    _breakers.Release(inst.Id);
                    throw;
                }
                catch (Exception ex)
                {
                    _breakers.RecordFailure(inst.Id);
                    _logger.LogWarning("GET {Path} pe {Id} a eșuat ({Message}), încercăm altă instanță",
                        pathAndQuery, inst.Id, ex.Message);
                }
            }

            return null;
        }
    }
}
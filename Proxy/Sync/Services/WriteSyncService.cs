using Proxy.Sync.Cache;
using Proxy.Sync.Models;
using Proxy.Sync.Queue;
using Proxy.Sync.Resilience;
using System.Text;

namespace Proxy.Sync.Services
{
    public class WriteSyncService
    {
        private readonly InstanceRegistry _registry;
        private readonly IWriteQueue _queue;
        private readonly IHttpClientFactory _httpFactory;
        private readonly IBookCache _cache;
        private readonly ILogger<WriteSyncService> _logger;
        private readonly CircuitBreakerRegistry _breakers;

        public WriteSyncService(InstanceRegistry registry, IWriteQueue queue,
            IHttpClientFactory httpFactory, IBookCache cache, CircuitBreakerRegistry breakers,
            ILogger<WriteSyncService> logger)
        {
            _registry = registry;
            _queue = queue;
            _httpFactory = httpFactory;
            _cache = cache;
            _breakers = breakers;
            _logger = logger;
        }

        public async Task<SyncResult> BroadcastAsync(string method, string pathAndQuery, string? body,
            string contentType, string accept, CancellationToken ct)
        {
            var instances = _registry.Instances.ToList();
            if (instances.Count == 0)
                return new SyncResult(503, "{\"error\":\"Nicio instanță BookAPI cunoscută\"}", "application/json");

            var write = new PendingWrite(method, pathAndQuery, body, DateTime.UtcNow, contentType, accept);

            var acquired = new List<SemaphoreSlim>();
            try
            {
                foreach (var inst in instances)
                {
                    var l = _registry.LockFor(inst.Id);
                    await l.WaitAsync(ct);
                    acquired.Add(l);
                }

                var attempts = await Task.WhenAll(instances.Select(i => AttemptAsync(i, write)));
                var applied = attempts.Where(a => a.Outcome == Outcome.Applied).ToList();

                if (applied.Count > 0)
                {
                    foreach (var failed in attempts.Where(a => a.Outcome == Outcome.Failed))
                    {
                        _queue.Enqueue(failed.Instance.Id, write);
                        _logger.LogWarning("{Method} {Path} pus în coadă pentru {Id} (în coadă: {Count})",
                            method, pathAndQuery, failed.Instance.Id, _queue.Count(failed.Instance.Id));
                    }

                    await _cache.InvalidateAllAsync();
                    return new SyncResult(applied[0].StatusCode, applied[0].Body, applied[0].ContentType);
                }

                var rejected = attempts.FirstOrDefault(a => a.Outcome == Outcome.Rejected);
                if (rejected != null)
                    return new SyncResult(rejected.StatusCode, rejected.Body, rejected.ContentType);

                return new SyncResult(503, "{\"error\":\"Nicio instanță disponibilă\"}", "application/json");
            }
            finally
            {
                foreach (var l in acquired) l.Release();
            }
        }

        private Task<Attempt> AttemptAsync(BookInstance inst, PendingWrite write)
        {
            if (!inst.Healthy || _queue.Count(inst.Id) > 0 || !_breakers.AllowRequest(inst.Id))
                return Task.FromResult(new Attempt(inst, Outcome.Failed, 503, ""));

            return SendAsync(inst, write, replay: false);
        }

        public async Task<Attempt> SendAsync(BookInstance inst, PendingWrite write, bool replay)
        {
            // La replay, POST devine PUT (upsert), deci este idempotent
            var method = replay && write.Method == "POST" ? "PUT" : write.Method;

            try
            {
                var client = _httpFactory.CreateClient("bookapi");
                using var request = new HttpRequestMessage(new HttpMethod(method), new Uri(inst.BaseUri, write.PathAndQuery));
                request.Headers.TryAddWithoutValidation("Accept", write.Accept);
                if (write.Body != null)
                    request.Content = new StringContent(write.Body, Encoding.UTF8, write.ContentType);

                using var response = await client.SendAsync(request);
                var text = await response.Content.ReadAsStringAsync();
                var code = (int)response.StatusCode;

                var outcome = code is >= 200 and < 300 ? Outcome.Applied
                            : code is >= 400 and < 500 ? Outcome.Rejected
                            : Outcome.Failed;

                if (outcome == Outcome.Failed) _breakers.RecordFailure(inst.Id);
                else _breakers.RecordSuccess(inst.Id);

                return new Attempt(inst, outcome, code, text, response.Content.Headers.ContentType?.MediaType);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("{Method} către {Id} a eșuat: {Message}", method, inst.Id, ex.Message);
                _breakers.RecordFailure(inst.Id);
                return new Attempt(inst, Outcome.Failed, 503, "");
            }
        }
    }
}
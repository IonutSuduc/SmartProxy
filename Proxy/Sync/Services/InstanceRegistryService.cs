using Consul;
using Proxy.Sync.Models;
using System.Collections.Concurrent;

namespace Proxy.Sync.Services
{
    public class InstanceRegistry
    {
        private readonly ConcurrentDictionary<string, BookInstance> _known = new();
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();
        private readonly IConsulClient _consul;
        private readonly ILogger<InstanceRegistry> _logger;
        private const string ServiceName = "bookapi";

        public InstanceRegistry(IConfiguration config, ILogger<InstanceRegistry> logger)
        {
            _logger = logger;
            var address = config["Consul:Address"] ?? "http://consul:8500";
            _consul = new ConsulClient(c => c.Address = new Uri(address));
        }

        // Toate instanțele văzute vreodată, ordonate după Id (ordine fixă pentru lock-uri)
        public IReadOnlyList<BookInstance> Instances => _known.Values.OrderBy(i => i.Id).ToList();

        public SemaphoreSlim LockFor(string id) => _locks.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));

        public async Task RefreshAsync(CancellationToken ct)
        {
            try
            {
                // passingOnly = false: vrem și instanțele critice, ca să le putem ține în evidență
                var result = await _consul.Health.Service(ServiceName, "", false, ct);
                var seen = new HashSet<string>();

                foreach (var entry in result.Response)
                {
                    var healthy = entry.Checks.All(c => c.Status.Status == "passing");
                    var uri = new Uri($"http://{entry.Service.Address}:{entry.Service.Port}");
                    _known[entry.Service.ID] = new BookInstance(entry.Service.ID, uri, healthy);
                    seen.Add(entry.Service.ID);
                }

                // Instanțe șterse din Consul (după DeregisterCriticalServiceAfter): le păstrăm, dar nesănătoase
                foreach (var kv in _known)
                {
                    if (!seen.Contains(kv.Key) && kv.Value.Healthy)
                        _known[kv.Key] = kv.Value with { Healthy = false };
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Consul indisponibil la refresh: {Message}", ex.Message);
            }
        }
    }
}

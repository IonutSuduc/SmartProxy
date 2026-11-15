using System.Collections.Concurrent;

namespace Proxy.Sync.Resilience
{
    public class CircuitBreakerRegistry
    {
        private const int FailureThreshold = 3;
        private static readonly TimeSpan BreakDuration = TimeSpan.FromSeconds(10);

        private class Circuit
        {
            public readonly object Lock = new();
            public int Failures;
            public DateTime? OpenedAtUtc;
            public bool TrialInFlight;
        }

        private readonly ConcurrentDictionary<string, Circuit> _circuits = new();
        private readonly ILogger<CircuitBreakerRegistry> _logger;

        public CircuitBreakerRegistry(ILogger<CircuitBreakerRegistry> logger) => _logger = logger;

        private Circuit Get(string id) => _circuits.GetOrAdd(id, _ => new Circuit());

        // true = se poate trimite cererea. După pauză permite o singură cerere de test (half-open).
        public bool AllowRequest(string id)
        {
            var c = Get(id);
            lock (c.Lock)
            {
                if (c.OpenedAtUtc is null) return true;
                if (DateTime.UtcNow - c.OpenedAtUtc < BreakDuration) return false;
                if (c.TrialInFlight) return false;
                c.TrialInFlight = true;
                _logger.LogWarning("Circuit {Id}: HALF-OPEN, trimitem o cerere de test", id);
                return true;
            }
        }

        public void RecordSuccess(string id)
        {
            var c = Get(id);
            lock (c.Lock)
            {
                if (c.OpenedAtUtc != null)
                    _logger.LogWarning("Circuit {Id}: CLOSED, instanța a revenit", id);
                c.Failures = 0;
                c.OpenedAtUtc = null;
                c.TrialInFlight = false;
            }
        }

        public void RecordFailure(string id)
        {
            var c = Get(id);
            lock (c.Lock)
            {
                var wasTrial = c.TrialInFlight;
                c.TrialInFlight = false;
                c.Failures++;
                if (wasTrial || c.Failures >= FailureThreshold)
                {
                    c.OpenedAtUtc = DateTime.UtcNow;
                    _logger.LogWarning("Circuit {Id}: OPEN pentru {Seconds}s (eșecuri: {Failures})",
                        id, BreakDuration.TotalSeconds, c.Failures);
                }
            }
        }

        // Cererea a fost anulată de client, nu e vina instanței
        public void Release(string id)
        {
            var c = Get(id);
            lock (c.Lock) { c.TrialInFlight = false; }
        }
    }
}
using Proxy.Sync.Models;
using Proxy.Sync.Queue;

namespace Proxy.Sync.Services
{
    public class ReplayService : BackgroundService
    {
        private readonly InstanceRegistry _registry;
        private readonly IWriteQueue _queue;
        private readonly WriteSyncService _sync;
        private readonly ILogger<ReplayService> _logger;

        public ReplayService(InstanceRegistry registry, IWriteQueue queue, WriteSyncService sync, ILogger<ReplayService> logger)
        {
            _registry = registry;
            _queue = queue;
            _sync = sync;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("ReplayService pornit");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await _registry.RefreshAsync(stoppingToken);

                    foreach (var inst in _registry.Instances.Where(i => _queue.Count(i.Id) > 0).ToList())
                    {
                        _logger.LogInformation("Coadă nenulă: {Id} healthy={Healthy} coadă={Count}",
                            inst.Id, inst.Healthy, _queue.Count(inst.Id));

                        if (inst.Healthy)
                            await DrainAsync(inst, stoppingToken);
                    }
                }
                catch (Exception ex) when (stoppingToken.IsCancellationRequested)
                {
                    // Oprire reală a aplicației
                    _logger.LogInformation("ReplayService oprit ({Type})", ex.GetType().Name);
                    break;
                }
                catch (Exception ex)
                {
                    // Orice altă eroare (inclusiv timeout-uri) nu mai oprește serviciul
                    _logger.LogError(ex, "Eroare în ReplayService, continuăm");
                }

                try { await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken); }
                catch (OperationCanceledException) { break; }
            }

            _logger.LogWarning("ReplayService a ieșit din buclă");
        }

        private async Task DrainAsync(BookInstance inst, CancellationToken ct)
        {
            var gate = _registry.LockFor(inst.Id);
            await gate.WaitAsync(ct);
            try
            {
                while (_queue.TryPeek(inst.Id, out var write) && write != null)
                {
                    var attempt = await _sync.SendAsync(inst, write, replay: true);

                    if (attempt.Outcome == Outcome.Failed)
                    {
                        _logger.LogWarning("Replay {Method} {Path} pe {Id} a eșuat: {Status} {Body}",
                            write.Method, write.PathAndQuery, inst.Id, attempt.StatusCode, attempt.Body);
                        break;
                    }

                    _queue.Dequeue(inst.Id);
                    _logger.LogInformation("Replay {Method} {Path} pe {Id}: {Status} (rămase: {Left})",
                        write.Method, write.PathAndQuery, inst.Id, attempt.StatusCode, _queue.Count(inst.Id));
                }
            }
            finally
            {
                gate.Release();
            }
        }
    }
}
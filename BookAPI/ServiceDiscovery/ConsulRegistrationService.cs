using Consul;

namespace BookAPI.ServiceDiscovery
{
    public class ServiceDiscoverySettings
    {
        public string ConsulAddress { get; set; } = "http://consul:8500";
        public string ServiceName { get; set; } = "bookapi";
        public string ServiceId { get; set; } = "bookapi1";
        public string ServiceHost { get; set; } = "localhost";
        public int ServicePort { get; set; } = 8080;
    }

    public class ConsulRegistrationService : IHostedService
    {
        private readonly IConsulClient _consul;
        private readonly ServiceDiscoverySettings _settings;
        private readonly ILogger<ConsulRegistrationService> _logger;

        public ConsulRegistrationService(
            IConsulClient consul,
            IConfiguration configuration,
            ILogger<ConsulRegistrationService> logger)
        {
            _consul = consul;
            _logger = logger;
            _settings = configuration.GetSection("ServiceDiscovery").Get<ServiceDiscoverySettings>()
                        ?? new ServiceDiscoverySettings();
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var registration = new AgentServiceRegistration
            {
                ID = _settings.ServiceId,
                Name = _settings.ServiceName,
                Address = _settings.ServiceHost,
                Port = _settings.ServicePort,
                Check = new AgentServiceCheck
                {
                    HTTP = $"http://{_settings.ServiceHost}:{_settings.ServicePort}/health",
                    Interval = TimeSpan.FromSeconds(1),
                    Timeout = TimeSpan.FromSeconds(1),
                    // Consul scoate complet instanța după 1 minut în stare critică
                    DeregisterCriticalServiceAfter = TimeSpan.FromMinutes(1)
                }
            };

            // Consul poate fi încă în curs de pornire, deci încercăm de mai multe ori
            for (var attempt = 1; attempt <= 10; attempt++)
            {
                try
                {
                    await _consul.Agent.ServiceDeregister(registration.ID, cancellationToken);
                    await _consul.Agent.ServiceRegister(registration, cancellationToken);
                    _logger.LogInformation("Înregistrat în Consul ca {Id}", registration.ID);
                    return;
                }
                catch (Exception ex) when (attempt < 10)
                {
                    _logger.LogWarning("Consul indisponibil (încercarea {Attempt}): {Message}", attempt, ex.Message);
                    await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                }
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            try { await _consul.Agent.ServiceDeregister(_settings.ServiceId, cancellationToken); }
            catch { /* la oprire ignorăm erorile */ }
        }
    }
}
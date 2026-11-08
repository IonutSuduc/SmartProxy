using BookAPI.Settings;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BookAPI.ServiceDiscovery
{
    public class MongoHealthCheck : IHealthCheck
    {
        private readonly MongoClient _client;

        public MongoHealthCheck(IMongoDbSettings settings)
        {
            var clientSettings = MongoClientSettings.FromConnectionString(settings.ConnectionString);
            // Timeout scurt, ca health check-ul să eșueze repede când Mongo e oprit
            clientSettings.ServerSelectionTimeout = TimeSpan.FromSeconds(1);
            clientSettings.ConnectTimeout = TimeSpan.FromSeconds(1);
            _client = new MongoClient(clientSettings);
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                await _client.GetDatabase("admin")
                    .RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1), cancellationToken: cancellationToken);
                return HealthCheckResult.Healthy("MongoDB răspunde");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("MongoDB nu răspunde", ex);
            }
        }
    }
}
using StackExchange.Redis;

namespace Proxy.Sync.Cache
{
    public interface IBookCache
    {
        Task<string?> GetAsync(string key);
        Task SetAsync(string key, string value);
        Task InvalidateAllAsync();
    }

    public class RedisBookCache : IBookCache
    {
        private const string Prefix = "books:";
        private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);

        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<RedisBookCache> _logger;

        public RedisBookCache(IConnectionMultiplexer redis, ILogger<RedisBookCache> logger)
        {
            _redis = redis;
            _logger = logger;
        }

        public static string KeyFor(PathString path, QueryString query, string format)
            => Prefix + format + ":" + path.Value?.TrimEnd('/').ToLowerInvariant() + query.Value;

        public async Task<string?> GetAsync(string key)
        {
            try
            {
                var value = await _redis.GetDatabase().StringGetAsync(key);
                return value.IsNullOrEmpty ? null : value.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Redis indisponibil la citire cache: {Message}", ex.Message);
                return null;
            }
        }

        public async Task SetAsync(string key, string value)
        {
            try { await _redis.GetDatabase().StringSetAsync(key, value, Ttl); }
            catch (Exception ex) { _logger.LogWarning("Redis indisponibil la scriere cache: {Message}", ex.Message); }
        }

        // Șterge lista și toate cărțile individuale din cache
        public async Task InvalidateAllAsync()
        {
            try
            {
                var db = _redis.GetDatabase();
                foreach (var endpoint in _redis.GetEndPoints())
                {
                    var server = _redis.GetServer(endpoint);
                    foreach (var key in server.Keys(pattern: Prefix + "*"))
                        await db.KeyDeleteAsync(key);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Nu s-a putut invalida cache-ul Redis: {Message}", ex.Message);
            }
        }
    }
}
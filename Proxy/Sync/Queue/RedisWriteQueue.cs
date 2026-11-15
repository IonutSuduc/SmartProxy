using Proxy.Sync.Models;
using StackExchange.Redis;
using System.Text.Json;

namespace Proxy.Sync.Queue
{
    public class RedisWriteQueue : IWriteQueue
    {
        private readonly IDatabase _db;

        public RedisWriteQueue(IConnectionMultiplexer redis) => _db = redis.GetDatabase();

        private static string Key(string instanceId) => $"queue:{instanceId}";

        public void Enqueue(string instanceId, PendingWrite write)
            => _db.ListRightPush(Key(instanceId), JsonSerializer.Serialize(write));

        public bool TryPeek(string instanceId, out PendingWrite? write)
        {
            var value = _db.ListGetByIndex(Key(instanceId), 0);
            if (value.IsNullOrEmpty)
            {
                write = null;
                return false;
            }

            write = JsonSerializer.Deserialize<PendingWrite>(value.ToString());
            return write != null;
        }

        public void Dequeue(string instanceId) => _db.ListLeftPop(Key(instanceId));

        public int Count(string instanceId) => (int)_db.ListLength(Key(instanceId));
    }
}
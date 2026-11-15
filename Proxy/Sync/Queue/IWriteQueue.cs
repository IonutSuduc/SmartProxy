using Proxy.Sync.Models;

namespace Proxy.Sync.Queue
{
    public interface IWriteQueue
    {
        void Enqueue(string instanceId, PendingWrite write);
        bool TryPeek(string instanceId, out PendingWrite? write);
        void Dequeue(string instanceId);
        int Count(string instanceId);
    }
}

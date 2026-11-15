namespace Proxy.Sync.Models
{
    public record PendingWrite(string Method, string PathAndQuery, string? Body, DateTime CreatedAtUtc);
    public record BookInstance(string Id, Uri BaseUri, bool Healthy);

    public enum Outcome { Applied, Rejected, Failed }
    public record Attempt(BookInstance Instance, Outcome Outcome, int StatusCode, string Body);
    public record SyncResult(int StatusCode, string Body);
}

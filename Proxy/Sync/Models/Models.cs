namespace Proxy.Sync.Models
{
    public record PendingWrite(string Method, string PathAndQuery, string? Body, DateTime CreatedAtUtc,
        string ContentType = "application/json", string Accept = "application/json");
    public record BookInstance(string Id, Uri BaseUri, bool Healthy);

    public enum Outcome { Applied, Rejected, Failed }
    public record Attempt(BookInstance Instance, Outcome Outcome, int StatusCode, string Body, string? ContentType = null);
    public record SyncResult(int StatusCode, string Body, string? ContentType = null);
}
using System.Text.Json.Nodes;

namespace Proxy.Sync.Helpers
{
    public static class BookBody
    {
        // Returnează null dacă body-ul nu e un obiect JSON valid
        public static string? EnsureId(string body)
        {
            try
            {
                if (JsonNode.Parse(body) is not JsonObject obj)
                    return null;

                var key = obj.Select(p => p.Key)
                             .FirstOrDefault(k => string.Equals(k, "id", StringComparison.OrdinalIgnoreCase)) ?? "id";

                var current = obj[key]?.ToString();
                if (!Guid.TryParse(current, out var id) || id == Guid.Empty)
                    obj[key] = Guid.NewGuid().ToString();

                return obj.ToJsonString();
            }
            catch
            {
                return null;
            }
        }
    }
}

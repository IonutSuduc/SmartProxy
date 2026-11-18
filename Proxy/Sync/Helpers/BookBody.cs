using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace Proxy.Sync.Helpers
{
    public static class BookBody
    {
        public static bool IsXml(string? mediaType)
            => mediaType != null && mediaType.Contains("xml", StringComparison.OrdinalIgnoreCase);

        // Completează Id (dacă lipsește) și LastChangedAt (mereu), o singură dată, în Proxy.
        // Returnează null dacă body-ul nu e valid.
        public static string? EnsureId(string body, bool xml)
        {
            var changedAt = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'");
            return xml ? EnsureIdXml(body, changedAt) : EnsureIdJson(body, changedAt);
        }

        private static string? EnsureIdJson(string body, string changedAt)
        {
            try
            {
                if (JsonNode.Parse(body) is not JsonObject obj)
                    return null;

                var idKey = obj.Select(p => p.Key)
                               .FirstOrDefault(k => string.Equals(k, "id", StringComparison.OrdinalIgnoreCase)) ?? "id";

                var current = obj[idKey]?.ToString();
                if (!Guid.TryParse(current, out var id) || id == Guid.Empty)
                    obj[idKey] = Guid.NewGuid().ToString();

                var timeKey = obj.Select(p => p.Key)
                                 .FirstOrDefault(k => string.Equals(k, "lastChangedAt", StringComparison.OrdinalIgnoreCase)) ?? "lastChangedAt";
                obj[timeKey] = changedAt;

                return obj.ToJsonString();
            }
            catch { return null; }
        }

        private static string? EnsureIdXml(string body, string changedAt)
        {
            try
            {
                var doc = XDocument.Parse(body);
                var root = doc.Root;
                if (root == null) return null;

                var idEl = root.Elements()
                    .FirstOrDefault(e => string.Equals(e.Name.LocalName, "id", StringComparison.OrdinalIgnoreCase));

                if (idEl == null)
                {
                    // XmlSerializer cere numele exact al proprietății: Id
                    idEl = new XElement(root.Name.Namespace + "Id");
                    root.AddFirst(idEl);
                }

                if (!Guid.TryParse(idEl.Value, out var id) || id == Guid.Empty)
                    idEl.Value = Guid.NewGuid().ToString();

                var timeEl = root.Elements()
                    .FirstOrDefault(e => string.Equals(e.Name.LocalName, "lastChangedAt", StringComparison.OrdinalIgnoreCase));

                if (timeEl == null)
                {
                    timeEl = new XElement(root.Name.Namespace + "LastChangedAt");
                    root.Add(timeEl);
                }
                timeEl.Value = changedAt;

                return root.ToString(SaveOptions.DisableFormatting);
            }
            catch { return null; }
        }
    }
}
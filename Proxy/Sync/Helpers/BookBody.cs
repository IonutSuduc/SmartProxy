using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace Proxy.Sync.Helpers
{
    public static class BookBody
    {
        public static bool IsXml(string? mediaType)
            => mediaType != null && mediaType.Contains("xml", StringComparison.OrdinalIgnoreCase);

        // Returnează null dacă body-ul nu e valid
        public static string? EnsureId(string body, bool xml)
            => xml ? EnsureIdXml(body) : EnsureIdJson(body);

        private static string? EnsureIdJson(string body)
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
            catch { return null; }
        }

        private static string? EnsureIdXml(string body)
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

                return root.ToString(SaveOptions.DisableFormatting);
            }
            catch { return null; }
        }
    }
}
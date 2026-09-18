using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;

namespace ost_uploader
{
    public class EventFileData
    {
        public Dictionary<string, string> StationMap { get; set; } = new Dictionary<string, string>();
        // Sites discovered in the event metadata. Title is a friendly label (e.g., "Staging" or "Production").
        public List<SiteEntry> Sites { get; set; } = new List<SiteEntry>();

        // Back-compat fields
        public string? OstUrl { get; set; }
        public bool UseStaging { get; set; } = false;
        // Explicit per-site convenience properties
        public string? ProductionUrl { get; set; }
        public string? StagingUrl { get; set; }
    }

    public class SiteEntry
    {
        public string Title { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string EventSlug { get; set; } = string.Empty;
        public int EventGroupId { get; set; }
    }

    public static class EventFileLoader
    {
        public static bool TryLoad(string path, out EventFileData data)
        {
            data = new EventFileData();

            if (!File.Exists(path)) return false;
            if (!string.Equals(Path.GetExtension(path), ".zip", StringComparison.OrdinalIgnoreCase)) return false;

            try
            {
                string jsonText = string.Empty;

                using var fs = File.OpenRead(path);
                using var za = new ZipArchive(fs, ZipArchiveMode.Read, leaveOpen: false);

                // Event packages use stations.json as the metadata source.
                ZipArchiveEntry? chosen = za.Entries.FirstOrDefault(e => string.Equals(e.Name, "stations.json", StringComparison.OrdinalIgnoreCase));
                if (chosen == null)
                {
                    return false;
                }

                using (var entryStream = chosen.Open())
                using (var sr = new StreamReader(entryStream))
                {
                    jsonText = sr.ReadToEnd();
                }

                using var doc = JsonDocument.Parse(jsonText);
                var root = doc.RootElement;

                // stations.json schema: event.openSplitTime.{production,staging}
                if (!root.TryGetProperty("event", out var eventElem) || eventElem.ValueKind != JsonValueKind.Object)
                    return false;

                if (!eventElem.TryGetProperty("openSplitTime", out var ostElem) || ostElem.ValueKind != JsonValueKind.Object)
                    return false;

                // production is expected; staging is optional
                if (ostElem.TryGetProperty("production", out var productionElem) && productionElem.ValueKind == JsonValueKind.Object)
                {
                    var productionSite = BuildSiteEntry("Production", "https://www.opensplittime.org", productionElem);
                    if (productionSite != null)
                    {
                        data.Sites.Add(productionSite);
                        data.ProductionUrl = productionSite.Url;
                        data.OstUrl = productionSite.Url;
                    }
                }

                if (ostElem.TryGetProperty("staging", out var stagingElem) && stagingElem.ValueKind == JsonValueKind.Object)
                {
                    var stagingSite = BuildSiteEntry("Staging", "https://staging.opensplittime.org", stagingElem);
                    if (stagingSite != null)
                    {
                        data.Sites.Add(stagingSite);
                        data.StagingUrl = stagingSite.Url;
                        data.UseStaging = true;
                        // staging must be the default if present
                        data.OstUrl = stagingSite.Url;
                    }
                }

                // Station mapping in this schema comes from openSplitTime.splitNames
                if (ostElem.TryGetProperty("splitNames", out var splitNamesElem) && splitNamesElem.ValueKind == JsonValueKind.Object)
                {
                    foreach (var p in splitNamesElem.EnumerateObject())
                    {
                        if (p.Value.ValueKind == JsonValueKind.String)
                        {
                            data.StationMap[p.Name] = p.Value.GetString() ?? string.Empty;
                        }
                    }
                }

                return true;
            }
            catch
            {
                data = new EventFileData();
                return false;
            }
        }

        private static SiteEntry? BuildSiteEntry(string title, string baseUrl, JsonElement siteElem)
        {
            if (!TryGetString(siteElem, "name", out var slug) || string.IsNullOrWhiteSpace(slug))
                return null;

            if (!siteElem.TryGetProperty("id", out var idElem))
                return null;

            int eventGroupId;
            if (idElem.ValueKind == JsonValueKind.Number)
            {
                if (!idElem.TryGetInt32(out eventGroupId)) return null;
            }
            else if (idElem.ValueKind == JsonValueKind.String)
            {
                if (!int.TryParse(idElem.GetString(), out eventGroupId)) return null;
            }
            else
            {
                return null;
            }

            var connectionUrl = $"{baseUrl.TrimEnd('/')}/api/v1/event_groups/{eventGroupId}";
            return new SiteEntry
            {
                Title = title,
                Url = connectionUrl,
                EventSlug = slug,
                EventGroupId = eventGroupId
            };
        }

        private static bool TryGetString(JsonElement elem, string propertyName, out string? value)
        {
            value = null;
            if (elem.ValueKind != JsonValueKind.Object) return false;
            if (elem.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
            {
                value = prop.GetString();
                return true;
            }

            // try case-insensitive
            foreach (var p in elem.EnumerateObject())
            {
                if (string.Equals(p.Name, propertyName, StringComparison.OrdinalIgnoreCase) && p.Value.ValueKind == JsonValueKind.String)
                {
                    value = p.Value.GetString();
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetObjectAsMap(JsonElement root, string propName, Dictionary<string, string> map)
        {
            if (!root.TryGetProperty(propName, out var prop))
                return false;

            if (prop.ValueKind != JsonValueKind.Object) return false;

            foreach (var p in prop.EnumerateObject())
            {
                if (p.Value.ValueKind == JsonValueKind.String)
                {
                    map[p.Name] = p.Value.GetString() ?? string.Empty;
                }
            }

            return true;
        }
    }
}

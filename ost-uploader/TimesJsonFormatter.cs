using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ost_uploader
{
    public class TimesJsonFormatter
    {
        private readonly string _source;
        private readonly string _splitName;
        private readonly HashSet<string> _allowedKinds;

        public TimesJsonFormatter(string source, string splitName, IEnumerable<string>? allowedKinds = null)
        {
            _source = source;
            _splitName = splitName;
            _allowedKinds = allowedKinds == null
                ? new HashSet<string>(new[] { "in", "out" })
                : new HashSet<string>(
                    allowedKinds
                        .Where(kind => !string.IsNullOrWhiteSpace(kind))
                        .Select(kind => kind.Trim().ToLowerInvariant()));
        }

        public string Format(List<TimeEntry> entries)
        {
            var data = new List<JsonApiRawTime>();
            var isDrop = false;

            foreach (var entry in entries)
            {
                if (entry.IsDidNotStart) continue; // DNS rows stay visible in the grid but are never uploaded

                isDrop = (entry.DropType == "withdrew" || entry.DropType == "medical" || entry.DropType == "timeout");

                if (entry.TimeIn.HasValue && _allowedKinds.Contains("in"))
                {
                    data.Add(new JsonApiRawTime
                    {
                        Type = "raw_time",
                        Attributes = new JsonApiRawTimeAttributes
                        {
                            Source = _source,
                            SubSplitKind = "in",
                            WithPacer = "false",
                            EnteredTime = FormatDateTime(entry.TimeIn.Value),
                            SplitName = _splitName,
                            BibNumber = entry.BibId,
                            StoppedHere = isDrop ? "true" : "false"
                        }
                    });
                }
                if (entry.TimeOut.HasValue && _allowedKinds.Contains("out"))
                {
                    data.Add(new JsonApiRawTime
                    {
                        Type = "raw_time",
                        Attributes = new JsonApiRawTimeAttributes
                        {
                            Source = _source,
                            SubSplitKind = "out",
                            WithPacer = "false",
                            EnteredTime = FormatDateTime(entry.TimeOut.Value),
                            SplitName = _splitName,
                            BibNumber = entry.BibId,
                            StoppedHere = isDrop ? "true" : "false"
                        }
                    });
                }
            }

            var root = new JsonApiRoot
            {
                Data = data,
                DataFormat = "jsonapi_batch",
                LimitedResponse = "true"
            };

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            };

            return JsonSerializer.Serialize(root, options);
        }

        private static string FormatDateTime(DateTime dt)
        {
            // Required shape: "YYYY-MM-DD HH:MM:SS±HH:MM"
            var dto = new DateTimeOffset(dt, TimeZoneInfo.Local.GetUtcOffset(dt));
            return dto.ToString("yyyy-MM-dd HH:mm:sszzz");
        }

        private class JsonApiRoot
        {
            [JsonPropertyName("data")]
            public List<JsonApiRawTime> Data { get; set; }

            [JsonPropertyName("data_format")]
            public string DataFormat { get; set; }

            [JsonPropertyName("limited_response")]
            public string LimitedResponse { get; set; }
        }

        private class JsonApiRawTime
        {
            [JsonPropertyName("type")]
            public string Type { get; set; }

            [JsonPropertyName("attributes")]
            public JsonApiRawTimeAttributes Attributes { get; set; }
        }

        private class JsonApiRawTimeAttributes
        {
            [JsonPropertyName("source")]
            public string Source { get; set; }

            [JsonPropertyName("sub_split_kind")]
            public string SubSplitKind { get; set; }

            [JsonPropertyName("with_pacer")]
            public string WithPacer { get; set; }

            [JsonPropertyName("entered_time")]
            public string EnteredTime { get; set; }

            [JsonPropertyName("split_name")]
            public string SplitName { get; set; }

            [JsonPropertyName("bib_number")]
            public string BibNumber { get; set; }

            [JsonPropertyName("stopped_here")]
            public string StoppedHere { get; set; }
        }
    }
}
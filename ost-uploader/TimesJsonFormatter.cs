using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ost_uploader
{
    public class TimesJsonFormatter
    {
        private readonly string _source;
        private readonly string _splitName;

        public TimesJsonFormatter(string source, string splitName)
        {
            _source = source;
            _splitName = splitName;
        }

        public string Format(List<TimeEntry> entries)
        {
            var data = new List<JsonApiRawTime>();
            var isDNF = false;

            foreach (var entry in entries)
            {
                if(entry.DnfType == "withdrew" || entry.DnfType == "medical" || entry.DnfType == "timeout")
                    isDNF = true;

                if (entry.TimeIn.HasValue)
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
                            StoppedHere = isDNF ? "true" : "false"
                        }
                    });
                }
                if (entry.TimeOut.HasValue)
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
                            StoppedHere = isDNF ? "true" : "false"
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
            // Example: "2023-08-09 09:16:01-6:00"
            var offset = TimeZoneInfo.Local.GetUtcOffset(dt);
            var offsetStr = $"{(offset.Hours >= 0 ? "+" : "-")}{Math.Abs(offset.Hours)}:00";
            return dt.ToString("yyyy-MM-dd HH:mm:ss") + offsetStr;
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
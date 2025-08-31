using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection.PortableExecutable;

namespace ost_uploader
{
    public class TimesImporter
    {
        public (CsvHeader Header, List<TimeEntry>) Import(string csvPath)
        {
            var entries = new List<TimeEntry>();
            CsvHeader header = null;
            using var reader = new StreamReader(csvPath);
            int lineNum = 0;

            // Skip metadata/header lines
            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                lineNum++;

                if (lineNum == 1)
                {
                    // Read header line
                    var stationHeaderFields = line.Split(',');
                    header = new CsvHeader(stationHeaderFields);
                    continue;
                }

                if (lineNum <= 2) continue; // skip metadata line

                var fields = line.Split(',');

                if (fields.Length < 8) continue; // skip malformed lines

                var entry = new TimeEntry
                {
                    Index = int.TryParse(fields[0], out var idx) ? idx : 0,
                    Sent = int.TryParse(fields[1], out var sent) ? sent : 0,
                    BibId = fields[2],
                    TimeIn = ParseDateTime(fields[3]),
                    TimeOut = ParseDateTime(fields[4]),
                    DnfType = fields[5],
                    DnfStation = fields[6],
                    Note = fields[7]
                };

                entries.Add(entry);
            }
            return (header, entries);
        }

        private DateTime? ParseDateTime(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            if (DateTime.TryParseExact(
                value,
                "HH:mm:ss dd MMM yyyy",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var dt))
            {
                return dt;
            }
            return null;
        }
    }

    public class CsvHeader
    {
        public IReadOnlyList<string> Fields { get; }

        public CsvHeader(string[] fields)
        {
            Fields = fields;
        }
    }

    public class TimeEntry
    {
        public int Index { get; set; }
        public int Sent { get; set; }
        public required string BibId { get; set; }
        public DateTime? TimeIn { get; set; }
        public DateTime? TimeOut { get; set; }
        public string DnfType { get; set; }
        public string DnfStation { get; set; }
        public string Note { get; set; }
    }
}
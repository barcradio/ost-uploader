using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection.PortableExecutable;
using System.Text.RegularExpressions;

namespace ost_uploader
{
    public class TimesImporter
    {
        public (CsvHeader? Header, List<TimeEntry>) Import(string csvPath)
        {
            var entries = new List<TimeEntry>();
            CsvHeader? header = null;
            using var reader = new StreamReader(csvPath);
            int lineNum = 0;

            // Skip metadata/header lines
            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine() ?? string.Empty;
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
                    DropType = fields[5],
                    DropStation = fields[6],
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
        // Duplicate bib entries (e.g. "101.2") are re-sends for an already-recorded bib and must be skipped on upload.
        private static readonly Regex DuplicateBibPattern = new(@"^\d+\.2$", RegexOptions.Compiled);
        private const string StartLineStation = "0-start-line";

        public int Index { get; set; }
        public int Sent { get; set; }
        public required string BibId { get; set; }
        public DateTime? TimeIn { get; set; }
        public DateTime? TimeOut { get; set; }
        public string? DropType { get; set; }
        public string? DropStation { get; set; }
        public string? Note { get; set; }

        public bool IsDuplicate => IsDuplicateBib(BibId);
        public string? VerificationNote => IsDuplicate
            ? UiStrings.Verification_DuplicateBib
            : IsDidNotStartAtOtherStation
                ? UiStrings.Format(UiStrings.Verification_DidNotStartAtStation, DropStation!.Trim())
                : IsDidNotStart ? UiStrings.Verification_DidNotStart : null;

        public static bool IsDuplicateBib(string? bibId) =>
            !string.IsNullOrWhiteSpace(bibId) && DuplicateBibPattern.IsMatch(bibId);
        public bool IsDidNotStart => string.Equals(DropType?.Trim(), "did-not-start", StringComparison.OrdinalIgnoreCase);
        public bool IsDidNotStartAtOtherStation =>
            IsDidNotStart && !string.IsNullOrWhiteSpace(DropStation) &&
            !string.Equals(DropStation.Trim(), StartLineStation, StringComparison.OrdinalIgnoreCase);
        public bool WillUpload => !IsDuplicate && !IsDidNotStart;
        public string ReadyStatus => WillUpload ? UiStrings.Upload_ReadyYes : UiStrings.Upload_ReadyNo;
        public bool NeedsAttention => IsDuplicate || IsDidNotStart;
    }
}
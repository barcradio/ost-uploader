using System.IO;
using ost_uploader;

namespace ost_uploader.Tests;

public class TimesImporterTests
{
    private static string WriteCsv(string content)
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void Import_ParsesHeaderFromFirstLine()
    {
        var csv = "Index,Sent,BibId,TimeIn,TimeOut,DropType,DropStation,Note\n" +
                   "metadata,line,to,skip,,,,\n" +
                   "1,1,101,08:00:00 01 Jan 2026,,,,\n";
        var path = WriteCsv(csv);

        var (header, entries) = new TimesImporter().Import(path);

        Assert.NotNull(header);
        Assert.Equal(new[] { "Index", "Sent", "BibId", "TimeIn", "TimeOut", "DropType", "DropStation", "Note" }, header!.Fields);
    }

    [Fact]
    public void Import_SkipsMetadataLineAndParsesDataRows()
    {
        var csv = "Index,Sent,BibId,TimeIn,TimeOut,DropType,DropStation,Note\n" +
                   "metadata,line,to,skip,,,,\n" +
                   "1,1,101,08:00:00 01 Jan 2026,09:00:00 01 Jan 2026,,,\n";
        var path = WriteCsv(csv);

        var (_, entries) = new TimesImporter().Import(path);

        var entry = Assert.Single(entries);
        Assert.Equal(1, entry.Index);
        Assert.Equal(1, entry.Sent);
        Assert.Equal("101", entry.BibId);
        Assert.Equal(new DateTime(2026, 1, 1, 8, 0, 0), entry.TimeIn);
        Assert.Equal(new DateTime(2026, 1, 1, 9, 0, 0), entry.TimeOut);
    }

    [Fact]
    public void Import_SkipsRowsWithFewerThanEightFields()
    {
        var csv = "Index,Sent,BibId,TimeIn,TimeOut,DropType,DropStation,Note\n" +
                   "metadata,line,to,skip,,,,\n" +
                   "1,1,101,08:00:00 01 Jan 2026\n";
        var path = WriteCsv(csv);

        var (_, entries) = new TimesImporter().Import(path);

        Assert.Empty(entries);
    }

    [Fact]
    public void Import_BlankTimeFieldsBecomeNull()
    {
        var csv = "Index,Sent,BibId,TimeIn,TimeOut,DropType,DropStation,Note\n" +
                   "metadata,line,to,skip,,,,\n" +
                   "1,1,101,,,,,\n";
        var path = WriteCsv(csv);

        var (_, entries) = new TimesImporter().Import(path);

        var entry = Assert.Single(entries);
        Assert.Null(entry.TimeIn);
        Assert.Null(entry.TimeOut);
    }

    [Fact]
    public void Import_UnparseableTimeFieldBecomesNull()
    {
        var csv = "Index,Sent,BibId,TimeIn,TimeOut,DropType,DropStation,Note\n" +
                   "metadata,line,to,skip,,,,\n" +
                   "1,1,101,not-a-date,,,,\n";
        var path = WriteCsv(csv);

        var (_, entries) = new TimesImporter().Import(path);

        var entry = Assert.Single(entries);
        Assert.Null(entry.TimeIn);
    }

    [Fact]
    public void Import_NonNumericIndexAndSentDefaultToZero()
    {
        var csv = "Index,Sent,BibId,TimeIn,TimeOut,DropType,DropStation,Note\n" +
                   "metadata,line,to,skip,,,,\n" +
                   "abc,xyz,101,,,,,\n";
        var path = WriteCsv(csv);

        var (_, entries) = new TimesImporter().Import(path);

        var entry = Assert.Single(entries);
        Assert.Equal(0, entry.Index);
        Assert.Equal(0, entry.Sent);
    }

    [Theory]
    [InlineData("101.2", true)]
    [InlineData("101", false)]
    [InlineData("*", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsDuplicateBib_DetectsDotTwoSuffix(string? bibId, bool expected)
    {
        Assert.Equal(expected, TimeEntry.IsDuplicateBib(bibId));
    }

    [Theory]
    [InlineData("did-not-start", "0-start-line", true, "Did not start. Verify with station.")]
    [InlineData(" DID-NOT-START ", "1-logan-peak", true, "Did not start at station '1-logan-peak'. Verify with station.")]
    [InlineData("withdrew", "1-logan-peak", false, null)]
    [InlineData("", "", false, null)]
    [InlineData(null, null, false, null)]
    public void IsDidNotStart_DetectsDidNotStartDropReason(string? dropType, string? dropStation, bool expected, string? expectedNote)
    {
        var entry = new TimeEntry { BibId = "101", DropType = dropType, DropStation = dropStation };

        Assert.Equal(expected, entry.IsDidNotStart);
        Assert.Equal(expected, entry.NeedsAttention);
        Assert.Equal(!expected, entry.WillUpload);
        Assert.Equal(expected ? "No" : "Yes", entry.ReadyStatus);
        Assert.Equal(expectedNote, entry.VerificationNote);
    }
}

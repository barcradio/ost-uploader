using System.Text.Json;
using ost_uploader;

namespace ost_uploader.Tests;

public class TimesJsonFormatterTests
{
    private static JsonElement ParseData(string json) =>
        JsonDocument.Parse(json).RootElement.Clone().GetProperty("data");

    [Fact]
    public void Format_EmitsEnvelopeProperties()
    {
        var json = new TimesJsonFormatter("aid-1", "Start").Format(new List<TimeEntry>());
        var root = JsonDocument.Parse(json).RootElement;

        Assert.Equal("jsonapi_batch", root.GetProperty("data_format").GetString());
        Assert.Equal("true", root.GetProperty("limited_response").GetString());
        Assert.Equal(0, root.GetProperty("data").GetArrayLength());
    }

    [Fact]
    public void Format_EntryWithOnlyTimeIn_ProducesSingleInRecord()
    {
        var entries = new List<TimeEntry>
        {
            new() { BibId = "101", TimeIn = new DateTime(2026, 1, 1, 8, 0, 0) }
        };

        var json = new TimesJsonFormatter("aid-1", "Start").Format(entries);
        var data = ParseData(json);

        var record = Assert.Single(data.EnumerateArray());
        var attrs = record.GetProperty("attributes");
        Assert.Equal("in", attrs.GetProperty("sub_split_kind").GetString());
        Assert.Equal("101", attrs.GetProperty("bib_number").GetString());
    }

    [Fact]
    public void Format_EntryWithBothTimes_ProducesInAndOutRecords()
    {
        var entries = new List<TimeEntry>
        {
            new()
            {
                BibId = "101",
                TimeIn = new DateTime(2026, 1, 1, 8, 0, 0),
                TimeOut = new DateTime(2026, 1, 1, 8, 5, 0)
            }
        };

        var json = new TimesJsonFormatter("aid-1", "Start").Format(entries);
        var data = ParseData(json).EnumerateArray().ToList();

        Assert.Equal(2, data.Count);
        Assert.Equal("in", data[0].GetProperty("attributes").GetProperty("sub_split_kind").GetString());
        Assert.Equal("out", data[1].GetProperty("attributes").GetProperty("sub_split_kind").GetString());
    }

    [Fact]
    public void Format_AllowedKindsFilterRecords()
    {
        var entries = new List<TimeEntry>
        {
            new()
            {
                BibId = "101",
                TimeIn = new DateTime(2026, 1, 1, 8, 0, 0),
                TimeOut = new DateTime(2026, 1, 1, 8, 5, 0)
            }
        };

        var json = new TimesJsonFormatter("aid-1", "Start", new[] { "out" }).Format(entries);
        var data = ParseData(json).EnumerateArray().ToList();

        var record = Assert.Single(data);
        Assert.Equal("out", record.GetProperty("attributes").GetProperty("sub_split_kind").GetString());
    }

    [Fact]
    public void Format_ExplicitlyEmptyAllowedKinds_ProducesNoRecords()
    {
        var entries = new List<TimeEntry>
        {
            new()
            {
                BibId = "101",
                TimeIn = new DateTime(2026, 1, 1, 8, 0, 0),
                TimeOut = new DateTime(2026, 1, 1, 8, 5, 0)
            }
        };

        var json = new TimesJsonFormatter("aid-1", "Start", Array.Empty<string>()).Format(entries);
        var data = ParseData(json);

        Assert.Equal(0, data.GetArrayLength());
    }

    [Theory]
    [InlineData("withdrew", "true")]
    [InlineData("medical", "true")]
    [InlineData("timeout", "true")]
    [InlineData("", "false")]
    [InlineData("finished", "false")]
    public void Format_DropTypeControlsStoppedHereFlag(string dropType, string expectedStoppedHere)
    {
        var entries = new List<TimeEntry>
        {
            new() { BibId = "101", TimeIn = new DateTime(2026, 1, 1, 8, 0, 0), DropType = dropType }
        };

        var json = new TimesJsonFormatter("aid-1", "Start").Format(entries);
        var record = ParseData(json).EnumerateArray().Single();

        Assert.Equal(expectedStoppedHere, record.GetProperty("attributes").GetProperty("stopped_here").GetString());
    }

    [Fact]
    public void Format_EnteredTimeUsesExpectedDateTimeShape()
    {
        var entries = new List<TimeEntry>
        {
            new() { BibId = "101", TimeIn = new DateTime(2026, 1, 1, 8, 0, 0) }
        };

        var json = new TimesJsonFormatter("aid-1", "Start").Format(entries);
        var enteredTime = ParseData(json).EnumerateArray().Single()
            .GetProperty("attributes").GetProperty("entered_time").GetString();

        Assert.Matches(@"^2026-01-01 08:00:00[+-]\d{2}:\d{2}$", enteredTime);
    }
}

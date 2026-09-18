using System.IO;
using System.IO.Compression;
using ost_uploader;

namespace ost_uploader.Tests;

public class EventFileLoaderTests
{
    private static string WriteZip(string? stationsJson, string entryName = "stations.json")
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".zip");
        using (var fs = File.Create(path))
        using (var za = new ZipArchive(fs, ZipArchiveMode.Create))
        {
            if (stationsJson != null)
            {
                var entry = za.CreateEntry(entryName);
                using var writer = new StreamWriter(entry.Open());
                writer.Write(stationsJson);
            }
        }
        return path;
    }

    [Fact]
    public void TryLoad_MissingFile_ReturnsFalse()
    {
        var result = EventFileLoader.TryLoad(Path.Combine(Path.GetTempPath(), "does-not-exist.zip"), out _);
        Assert.False(result);
    }

    [Fact]
    public void TryLoad_NonZipExtension_ReturnsFalse()
    {
        var path = Path.GetTempFileName(); // .tmp extension
        var result = EventFileLoader.TryLoad(path, out _);
        Assert.False(result);
    }

    [Fact]
    public void TryLoad_ZipWithoutStationsJson_ReturnsFalse()
    {
        var path = WriteZip("{}", entryName: "other.json");
        var result = EventFileLoader.TryLoad(path, out _);
        Assert.False(result);
    }

    [Fact]
    public void TryLoad_MalformedJson_ReturnsFalse()
    {
        var path = WriteZip("{ not valid json");
        var result = EventFileLoader.TryLoad(path, out _);
        Assert.False(result);
    }

    [Fact]
    public void TryLoad_ProductionOnly_PopulatesProductionSiteAndDefaultsToProduction()
    {
        var json = """
        {
          "event": {
            "openSplitTime": {
              "production": { "name": "bear-100-2026", "id": 42 }
            }
          }
        }
        """;
        var path = WriteZip(json);

        var result = EventFileLoader.TryLoad(path, out var data);

        Assert.True(result);
        var site = Assert.Single(data.Sites);
        Assert.Equal("Production", site.Title);
        Assert.Equal("bear-100-2026", site.EventSlug);
        Assert.Equal(42, site.EventGroupId);
        Assert.Equal("https://www.opensplittime.org/api/v1/event_groups/42", data.OstUrl);
        Assert.False(data.UseStaging);
    }

    [Fact]
    public void TryLoad_StagingPresent_DefaultsToStagingAndSetsUseStagingTrue()
    {
        var json = """
        {
          "event": {
            "openSplitTime": {
              "production": { "name": "bear-100-2026", "id": 42 },
              "staging": { "name": "bear-100-2026-staging", "id": "99" }
            }
          }
        }
        """;
        var path = WriteZip(json);

        var result = EventFileLoader.TryLoad(path, out var data);

        Assert.True(result);
        Assert.Equal(2, data.Sites.Count);
        Assert.True(data.UseStaging);
        Assert.Equal("https://staging.opensplittime.org/api/v1/event_groups/99", data.OstUrl);
        Assert.Equal("https://staging.opensplittime.org/api/v1/event_groups/99", data.StagingUrl);
        Assert.Equal("https://www.opensplittime.org/api/v1/event_groups/42", data.ProductionUrl);
    }

    [Fact]
    public void TryLoad_SplitNames_PopulatesStationMap()
    {
        var json = """
        {
          "event": {
            "openSplitTime": {
              "production": { "name": "bear-100-2026", "id": 42 },
              "splitNames": { "0-start-line": "Start", "13-finish-line": "Finish" }
            }
          }
        }
        """;
        var path = WriteZip(json);

        var result = EventFileLoader.TryLoad(path, out var data);

        Assert.True(result);
        Assert.Equal("Start", data.StationMap["0-start-line"]);
        Assert.Equal("Finish", data.StationMap["13-finish-line"]);
    }
}

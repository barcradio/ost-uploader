using ost_uploader;

namespace ost_uploader.Tests;

public class StationSplitMapperTests
{
    [Fact]
    public void DefaultConstructor_MapsStationsToSplitsByPosition()
    {
        var mapper = new StationSplitMapper();

        Assert.Equal(mapper.StationNames.Count, mapper.SplitNames.Count);
        Assert.Equal("Start", mapper.StationSplitMap["0-start-line"]);
        Assert.Equal("Finish", mapper.StationSplitMap["13-finish-line"]);
    }

    [Fact]
    public void NullOverrides_DoesNotThrowAndKeepsDefaults()
    {
        var mapper = new StationSplitMapper(null);

        Assert.Equal("Start", mapper.StationSplitMap["0-start-line"]);
    }

    [Fact]
    public void Overrides_AddNewMapping()
    {
        var overrides = new Dictionary<string, string> { ["14-extra-station"] = "Extra Split" };

        var mapper = new StationSplitMapper(overrides);

        Assert.Equal("Extra Split", mapper.StationSplitMap["14-extra-station"]);
        // Existing defaults remain untouched.
        Assert.Equal("Start", mapper.StationSplitMap["0-start-line"]);
    }

    [Fact]
    public void Overrides_ReplaceExistingMapping()
    {
        var overrides = new Dictionary<string, string> { ["0-start-line"] = "Custom Start" };

        var mapper = new StationSplitMapper(overrides);

        Assert.Equal("Custom Start", mapper.StationSplitMap["0-start-line"]);
    }
}

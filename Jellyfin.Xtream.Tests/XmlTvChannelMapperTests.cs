using System.Xml.Linq;
using Jellyfin.Xtream.Client;
using Jellyfin.Xtream.Client.Models;
using Xunit;

namespace Jellyfin.Xtream.Tests;

public class XmlTvChannelMapperTests
{
    [Fact]
    public void BuildChannelMapping_UsesEpgChannelIdAndStreamId()
    {
        var streams = new List<StreamInfo>
        {
            new() { StreamId = 42, EpgChannelId = "epg-1", Name = "News One" },
        };

        var mapping = XmlTvChannelMapper.BuildChannelMapping(streams);
        Assert.True(mapping.ContainsKey("epg-1"));
        Assert.True(mapping.ContainsKey("42"));
        Assert.Contains(42, mapping["epg-1"]);
    }

    [Fact]
    public void BuildStreamToXmlTvChannelIds_MatchesDisplayName()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sample.xmltv.xml");
        var doc = XDocument.Load(path);
        var streams = new List<StreamInfo>
        {
            new() { StreamId = 7, EpgChannelId = string.Empty, Name = "News One" },
        };

        var lookup = XmlTvChannelMapper.BuildStreamToXmlTvChannelIds(streams, doc);
        Assert.Contains("epg-1", lookup[7]);
    }

    [Fact]
    public void GetProgrammesForStream_ReturnsProgrammesForMatchingChannel()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sample.xmltv.xml");
        var doc = XDocument.Load(path);
        var programmes = XmlTvParser.ParseProgrammes(doc);
        var streams = new List<StreamInfo>
        {
            new() { StreamId = 42, EpgChannelId = string.Empty, Name = "Other Channel" },
        };
        var lookup = XmlTvChannelMapper.BuildStreamToXmlTvChannelIds(streams, doc);

        var results = XmlTvChannelMapper.GetProgrammesForStream(42, lookup, programmes);
        Assert.Single(results);
        Assert.Equal("Afternoon Show", results[0].Title);
    }
}

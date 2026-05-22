using System.Xml.Linq;
using Jellyfin.Xtream.Client;
using Xunit;

namespace Jellyfin.Xtream.Tests;

public class XmlTvParserTests
{
    [Theory]
    [InlineData("20240101080000 +0000")]
    [InlineData("20240101080000 +00:00")]
    public void ParseXmlTvDate_ParsesOffsetFormats(string raw)
    {
        var dt = XmlTvParser.ParseXmlTvDate(raw);
        Assert.NotEqual(DateTime.MinValue, dt);
        Assert.Equal(2024, dt.Year);
        Assert.Equal(1, dt.Month);
        Assert.Equal(1, dt.Day);
    }

    [Fact]
    public void ParseXmlTvDate_ReturnsMinValueForInvalid()
    {
        Assert.Equal(DateTime.MinValue, XmlTvParser.ParseXmlTvDate(string.Empty));
        Assert.Equal(DateTime.MinValue, XmlTvParser.ParseXmlTvDate("not-a-date"));
    }

    [Fact]
    public void ParseProgrammes_GroupsByChannel()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sample.xmltv.xml");
        var doc = XDocument.Load(path);
        var programmes = XmlTvParser.ParseProgrammes(doc);

        Assert.Equal(2, programmes.Count);
        Assert.Contains("epg-1", programmes.Keys);
        Assert.Single(programmes["epg-1"]);
        Assert.Equal("Morning News", programmes["epg-1"][0].Title);
    }
}

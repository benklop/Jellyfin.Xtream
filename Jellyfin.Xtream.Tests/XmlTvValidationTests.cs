using Jellyfin.Xtream.Client;
using Jellyfin.Xtream.Service;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Xtream.Tests;

public class XmlTvValidationTests
{
    [Fact]
    public void LogHistoricalDepthWarning_ReturnsWarningWhenDepthLow()
    {
        var programmes = new Dictionary<string, List<XmlTvProgramme>>
        {
            ["ch"] = [new XmlTvProgramme(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow, "Show", string.Empty)],
        };

        string? warning = XmlTvValidation.LogHistoricalDepthWarning(programmes, 7, NullLogger.Instance);
        Assert.NotNull(warning);
    }

    [Fact]
    public void LogHistoricalDepthWarning_ReturnsNullWhenDepthSufficient()
    {
        var programmes = new Dictionary<string, List<XmlTvProgramme>>
        {
            ["ch"] = [new XmlTvProgramme(DateTime.UtcNow.AddDays(-10), DateTime.UtcNow, "Show", string.Empty)],
        };

        string? warning = XmlTvValidation.LogHistoricalDepthWarning(programmes, 7, NullLogger.Instance);
        Assert.Null(warning);
    }

    [Fact]
    public void GetCachePath_UsesConfiguredPath()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "jellyfin-xtream-test-" + Guid.NewGuid().ToString("N"));
        string cacheFile = Path.Combine(tempDir, "cache", "xmltv.xml");
        string result = XmlTvValidation.GetCachePath(cacheFile);
        Assert.Equal(cacheFile, result);
        Assert.True(Directory.Exists(Path.GetDirectoryName(cacheFile)!));
        Directory.Delete(tempDir, true);
    }
}

// Copyright (C) 2022  Kevin Jilissen

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Xtream.Client;

/// <summary>
/// Helper class for XMLTV validation and disk caching.
/// </summary>
public static class XmlTvValidation
{
    /// <summary>
    /// Logs a warning when XMLTV historical depth is below the desired range; does not block parsing.
    /// </summary>
    /// <param name="programmesByChannel">Parsed programmes keyed by channel id.</param>
    /// <param name="requiredHistoricalDays">The desired minimum historical days.</param>
    /// <param name="logger">Instance of the <see cref="ILogger"/> interface.</param>
    /// <returns>An optional warning message when depth is low.</returns>
    public static string? LogHistoricalDepthWarning(
        Dictionary<string, List<Service.XmlTvProgramme>> programmesByChannel,
        int requiredHistoricalDays,
        ILogger logger)
    {
        var startTimes = programmesByChannel.Values
            .SelectMany(p => p)
            .Select(p => p.Start)
            .Where(dt => dt != DateTime.MinValue)
            .ToList();

        if (startTimes.Count == 0)
        {
            return null;
        }

        var oldestDate = startTimes.Min();
        var newestDate = startTimes.Max();
        var daysCovered = (newestDate - oldestDate).TotalDays;
        var historicalDays = (DateTime.UtcNow - oldestDate).TotalDays;

        if (historicalDays < requiredHistoricalDays)
        {
            string warning =
                $"XMLTV has only {historicalDays:F1} days of historical data ({requiredHistoricalDays} desired); using available programmes";
            logger.LogWarning(
                "XMLTV historical depth below desired range: {HistoricalDays:F1} days found, {RequiredDays} days desired",
                historicalDays,
                requiredHistoricalDays);
            return warning;
        }

        logger.LogInformation(
            "XMLTV historical depth OK: {HistoricalDays:F1} days of historical data, {TotalDays:F1} days total coverage",
            historicalDays,
            daysCovered);

        return null;
    }

    /// <summary>
    /// Gets the path to use for disk caching of XMLTV data.
    /// </summary>
    /// <param name="configPath">The configured cache path (may be empty).</param>
    /// <returns>The absolute path to use for caching.</returns>
    public static string GetCachePath(string configPath)
    {
        string path;
        if (!string.IsNullOrWhiteSpace(configPath))
        {
            path = configPath;
            // Create all directories in the configured path
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }
        else
        {
            // Use plugin's data directory from Plugin.Instance
            var plugin = Plugin.Instance;
            string dataPath = plugin.DataFolderPath;

            if (!Directory.Exists(dataPath))
            {
                Directory.CreateDirectory(dataPath);
            }

            path = Path.Combine(dataPath, "xmltv_cache.xml");
        }

        return path;
    }

    /// <summary>
    /// Create a mapping of channel IDs to their EPG identifiers based on stream info.
    /// </summary>
    /// <param name="streams">List of stream information.</param>
    /// <returns>Dictionary mapping EPG channel IDs to lists of stream IDs.</returns>
    public static Dictionary<string, HashSet<int>> BuildChannelMapping(IEnumerable<Models.StreamInfo> streams)
    {
        var mapping = new Dictionary<string, HashSet<int>>();
        foreach (var stream in streams)
        {
            // Use EPG channel ID if available, otherwise use stream ID
            string epgId = string.IsNullOrWhiteSpace(stream.EpgChannelId)
                ? stream.StreamId.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : stream.EpgChannelId;

            if (!mapping.TryGetValue(epgId, out var streamIds))
            {
                streamIds = new HashSet<int>();
                mapping[epgId] = streamIds;
            }

            streamIds.Add(stream.StreamId);
        }

        return mapping;
    }
}

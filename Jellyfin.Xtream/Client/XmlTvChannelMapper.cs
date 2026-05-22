// Copyright (C) 2022  Kevin Jilissen

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using Jellyfin.Xtream.Client.Models;
using Jellyfin.Xtream.Service;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Xtream.Client;

/// <summary>
/// Maps Xtream streams to XMLTV channel identifiers.
/// </summary>
public static class XmlTvChannelMapper
{
    /// <summary>
    /// Create a mapping of channel IDs to their EPG identifiers based on stream info.
    /// </summary>
    /// <param name="streams">List of stream information.</param>
    /// <returns>Dictionary mapping EPG channel IDs to lists of stream IDs.</returns>
    public static Dictionary<string, HashSet<int>> BuildChannelMapping(IEnumerable<StreamInfo> streams)
    {
        var mapping = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);
        foreach (var stream in streams)
        {
            foreach (string epgId in GetStreamXmlTvChannelIds(stream))
            {
                if (!mapping.TryGetValue(epgId, out var streamIds))
                {
                    streamIds = new HashSet<int>();
                    mapping[epgId] = streamIds;
                }

                streamIds.Add(stream.StreamId);
            }
        }

        return mapping;
    }

    /// <summary>
    /// Builds XMLTV channel id aliases for each stream, including matches from the XMLTV channel list.
    /// </summary>
    /// <param name="streams">Configured live streams.</param>
    /// <param name="doc">The XMLTV document.</param>
    /// <returns>Stream id to ordered list of XMLTV channel keys to try.</returns>
    public static Dictionary<int, List<string>> BuildStreamToXmlTvChannelIds(
        IEnumerable<StreamInfo> streams,
        XDocument doc)
    {
        var result = new Dictionary<int, List<string>>();
        var xmlChannels = doc.Descendants("channel").ToList();

        foreach (var stream in streams)
        {
            var ids = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            void AddId(string? id)
            {
                if (!string.IsNullOrWhiteSpace(id) && seen.Add(id))
                {
                    ids.Add(id);
                }
            }

            AddId(string.IsNullOrWhiteSpace(stream.EpgChannelId)
                ? null
                : stream.EpgChannelId);
            AddId(stream.StreamId.ToString(CultureInfo.InvariantCulture));

            string streamName = stream.Name ?? string.Empty;
            foreach (var channel in xmlChannels)
            {
                string? channelId = channel.Attribute("id")?.Value;
                if (string.IsNullOrEmpty(channelId))
                {
                    continue;
                }

                var displayNames = channel.Elements("display-name")
                    .Select(e => e.Value)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .ToList();

                if (displayNames.Any(n => string.Equals(n, streamName, StringComparison.OrdinalIgnoreCase)))
                {
                    AddId(channelId);
                }

                string? tvgId = channel.Element("tvg-id")?.Value
                    ?? channel.Attribute("tvg-id")?.Value;
                if (!string.IsNullOrWhiteSpace(tvgId)
                    && !string.IsNullOrWhiteSpace(stream.EpgChannelId)
                    && string.Equals(tvgId, stream.EpgChannelId, StringComparison.OrdinalIgnoreCase))
                {
                    AddId(channelId);
                }
            }

            result[stream.StreamId] = ids;
        }

        return result;
    }

    /// <summary>
    /// Collects programmes for a stream by trying each XMLTV channel alias.
    /// </summary>
    /// <param name="streamId">The stream id.</param>
    /// <param name="streamToChannelIds">Stream to XMLTV channel id aliases.</param>
    /// <param name="programmesByChannelId">Parsed programmes.</param>
    /// <param name="logger">Logger for unmatched streams.</param>
    /// <returns>Programmes for the stream.</returns>
    public static IReadOnlyList<XmlTvProgramme> GetProgrammesForStream(
        int streamId,
        Dictionary<int, List<string>> streamToChannelIds,
        Dictionary<string, List<XmlTvProgramme>> programmesByChannelId,
        ILogger? logger = null)
    {
        var results = new List<XmlTvProgramme>();
        var seen = new HashSet<(DateTime Start, DateTime End, string Title)>();

        if (!streamToChannelIds.TryGetValue(streamId, out var channelIds))
        {
            logger?.LogWarning("No XMLTV channel aliases defined for streamId {StreamId}", streamId);
            return results;
        }

        foreach (string channelId in channelIds)
        {
            if (!programmesByChannelId.TryGetValue(channelId, out var progs))
            {
                continue;
            }

            foreach (var p in progs)
            {
                var key = (p.Start, p.End, p.Title);
                if (seen.Add(key))
                {
                    results.Add(p);
                }
            }
        }

        if (results.Count == 0)
        {
            logger?.LogWarning(
                "No XMLTV programmes matched streamId {StreamId} (tried channel ids: {ChannelIds})",
                streamId,
                string.Join(", ", channelIds));
        }

        return results;
    }

    private static IEnumerable<string> GetStreamXmlTvChannelIds(StreamInfo stream)
    {
        if (!string.IsNullOrWhiteSpace(stream.EpgChannelId))
        {
            yield return stream.EpgChannelId;
        }

        yield return stream.StreamId.ToString(CultureInfo.InvariantCulture);
    }
}

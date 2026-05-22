// Copyright (C) 2022  Kevin Jilissen

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Jellyfin.Xtream.Client;
using Jellyfin.Xtream.Client.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Xtream.Service;

/// <summary>
/// Loads, caches, and parses XMLTV EPG data with single-flight coordination.
/// </summary>
public class XmlTvEpgService
{
    private const int NegativeCacheMinutes = 3;

    private readonly IXtreamClient _xtreamClient;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<XmlTvEpgService> _logger;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _loadLocks = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="XmlTvEpgService"/> class.
    /// </summary>
    /// <param name="xtreamClient">The Xtream API client.</param>
    /// <param name="memoryCache">The memory cache.</param>
    /// <param name="logger">The logger.</param>
    public XmlTvEpgService(IXtreamClient xtreamClient, IMemoryCache memoryCache, ILogger<XmlTvEpgService> logger)
    {
        _xtreamClient = xtreamClient;
        _memoryCache = memoryCache;
        _logger = logger;
    }

    /// <summary>
    /// Gets the shared parsed XMLTV programme index.
    /// </summary>
    /// <param name="streams">Configured live streams used for historical day detection and channel mapping.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The programme index (possibly empty after a failed load).</returns>
    public async Task<XmlTvProgrammeIndex> GetProgrammeIndexAsync(
        IEnumerable<StreamInfo> streams,
        CancellationToken cancellationToken)
    {
        Plugin plugin = Plugin.Instance;
        string cacheKey = $"xtream-xmltv-{plugin.DataVersion}";
        string failedKey = $"xtream-xmltv-failed-{plugin.DataVersion}";

        if (_memoryCache.TryGetValue(cacheKey, out XmlTvProgrammeIndex? cached) && cached is not null)
        {
            return cached;
        }

        if (_memoryCache.TryGetValue(failedKey, out _))
        {
            return XmlTvProgrammeIndex.Empty;
        }

        SemaphoreSlim loadLock = _loadLocks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
        await loadLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_memoryCache.TryGetValue(cacheKey, out cached) && cached is not null)
            {
                return cached;
            }

            if (_memoryCache.TryGetValue(failedKey, out _))
            {
                return XmlTvProgrammeIndex.Empty;
            }

            var streamList = streams as IList<StreamInfo> ?? streams.ToList();
            XmlTvProgrammeIndex index = await LoadProgrammeIndexAsync(plugin, streamList, cancellationToken).ConfigureAwait(false);

            if (index.LoadSucceeded)
            {
                _memoryCache.Set(
                    cacheKey,
                    index,
                    DateTimeOffset.Now.AddMinutes(plugin.Configuration.XmlTvCacheMinutes));
            }
            else
            {
                _memoryCache.Set(failedKey, true, DateTimeOffset.Now.AddMinutes(NegativeCacheMinutes));
                _memoryCache.Set(cacheKey, index, DateTimeOffset.Now.AddMinutes(NegativeCacheMinutes));
            }

            return index;
        }
        finally
        {
            loadLock.Release();
        }
    }

    /// <summary>
    /// Gets programmes for a stream within a UTC time range.
    /// </summary>
    /// <param name="streamId">The stream identifier.</param>
    /// <param name="streams">Configured live streams.</param>
    /// <param name="startUtc">Inclusive start of the window (UTC).</param>
    /// <param name="endUtc">Exclusive end of the window (UTC).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Matching programmes.</returns>
    public async Task<IReadOnlyList<XmlTvProgramme>> GetProgrammesForStreamAsync(
        int streamId,
        IEnumerable<StreamInfo> streams,
        DateTime startUtc,
        DateTime endUtc,
        CancellationToken cancellationToken)
    {
        XmlTvProgrammeIndex index = await GetProgrammeIndexAsync(streams, cancellationToken).ConfigureAwait(false);
        if (!index.LoadSucceeded)
        {
            return Array.Empty<XmlTvProgramme>();
        }

        var channelMapping = XmlTvValidation.BuildChannelMapping(streams);
        var results = new List<XmlTvProgramme>();
        var seen = new HashSet<(DateTime Start, DateTime End, string Title)>();

        foreach (var kvp in channelMapping)
        {
            if (!kvp.Value.Contains(streamId))
            {
                continue;
            }

            if (!index.ProgrammesByChannelId.TryGetValue(kvp.Key, out var progs))
            {
                continue;
            }

            foreach (var p in progs)
            {
                if (p.End < startUtc || p.Start >= endUtc)
                {
                    continue;
                }

                var key = (p.Start, p.End, p.Title);
                if (seen.Add(key))
                {
                    results.Add(p);
                }
            }
        }

        if (results.Count == 0)
        {
            _logger.LogWarning("No XMLTV programmes matched streamId {StreamId} in the requested window", streamId);
        }

        return results;
    }

    private async Task<XmlTvProgrammeIndex> LoadProgrammeIndexAsync(
        Plugin plugin,
        IList<StreamInfo> streams,
        CancellationToken cancellationToken)
    {
        try
        {
            string xml = await LoadXmlAsync(plugin, streams, cancellationToken).ConfigureAwait(false);

            int requiredDays = plugin.Configuration.XmlTvHistoricalDays;
            if (requiredDays <= 0)
            {
                requiredDays = streams.Where(s => s.TvArchive).Select(s => s.TvArchiveDuration).DefaultIfEmpty(7).Max();
            }

            var doc = XDocument.Parse(xml);
            var programmesByChannel = ParseProgrammes(doc);
            string? warning = XmlTvValidation.LogHistoricalDepthWarning(programmesByChannel, requiredDays, _logger);

            if (programmesByChannel.Count == 0)
            {
                _logger.LogError("XMLTV feed contains no usable programme entries");
                return XmlTvProgrammeIndex.Empty;
            }

            return new XmlTvProgrammeIndex
            {
                ProgrammesByChannelId = programmesByChannel,
                LoadSucceeded = true,
                WarningMessage = warning,
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(
                ex,
                "Failed to download XMLTV feed from {Url}. Status: {StatusCode}",
                plugin.Configuration.XmlTvUrl ?? "default",
                ex.StatusCode);
            return XmlTvProgrammeIndex.Empty;
        }
        catch (Exception ex) when (ex is System.Xml.XmlException or InvalidDataException)
        {
            _logger.LogError(ex, "Failed to parse XMLTV feed");
            return XmlTvProgrammeIndex.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error loading XMLTV feed");
            return XmlTvProgrammeIndex.Empty;
        }
    }

    private async Task<string> LoadXmlAsync(Plugin plugin, IList<StreamInfo> streams, CancellationToken cancellationToken)
    {
        string diskCachePath = XmlTvValidation.GetCachePath(plugin.Configuration.XmlTvCachePath);
        int cacheMinutes = plugin.Configuration.XmlTvCacheMinutes;

        if (plugin.Configuration.XmlTvDiskCache && File.Exists(diskCachePath))
        {
            DateTime writeTime = File.GetLastWriteTimeUtc(diskCachePath);
            if (DateTime.UtcNow - writeTime <= TimeSpan.FromMinutes(cacheMinutes))
            {
                return await File.ReadAllTextAsync(diskCachePath, cancellationToken).ConfigureAwait(false);
            }

            _logger.LogInformation("XMLTV disk cache expired, re-downloading");
        }

        int requiredDays = plugin.Configuration.XmlTvHistoricalDays;
        if (requiredDays <= 0)
        {
            requiredDays = streams.Where(s => s.TvArchive).Select(s => s.TvArchiveDuration).DefaultIfEmpty(7).Max();
        }

        string xml = await _xtreamClient.GetXmlTvAsync(
            plugin.Creds,
            plugin.Configuration.XmlTvUrl,
            requiredDays,
            cancellationToken).ConfigureAwait(false);

        if (plugin.Configuration.XmlTvDiskCache)
        {
            await File.WriteAllTextAsync(diskCachePath, xml, cancellationToken).ConfigureAwait(false);
        }

        return xml;
    }

    private Dictionary<string, List<XmlTvProgramme>> ParseProgrammes(XDocument doc)
    {
        var mapping = new Dictionary<string, List<XmlTvProgramme>>(StringComparer.Ordinal);

        foreach (var prog in doc.Descendants("programme"))
        {
            string? ch = prog.Attribute("channel")?.Value;
            if (string.IsNullOrEmpty(ch))
            {
                continue;
            }

            string? startRaw = prog.Attribute("start")?.Value;
            string? stopRaw = prog.Attribute("stop")?.Value;
            if (string.IsNullOrEmpty(startRaw) || string.IsNullOrEmpty(stopRaw))
            {
                continue;
            }

            try
            {
                DateTime start = ParseXmlTvDate(startRaw);
                DateTime stop = ParseXmlTvDate(stopRaw);
                if (start == DateTime.MinValue || stop == DateTime.MinValue || start >= stop)
                {
                    continue;
                }

                string title = prog.Element("title")?.Value ?? string.Empty;
                string desc = prog.Element("desc")?.Value ?? string.Empty;

                if (!mapping.TryGetValue(ch, out var list))
                {
                    list = new List<XmlTvProgramme>();
                    mapping[ch] = list;
                }

                list.Add(new XmlTvProgramme(start, stop, title, desc));
            }
            catch
            {
                // Skip programmes with unparseable dates
            }
        }

        return mapping;
    }

    private static DateTime ParseXmlTvDate(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return DateTime.MinValue;
        }

        string s = raw.Replace(" ", string.Empty, StringComparison.Ordinal);
        if (s.Length > 14)
        {
            string zone = s[^5..];
            if ((zone[0] == '+' || zone[0] == '-') && int.TryParse(zone.AsSpan(1), out _))
            {
                string zoneWithColon = zone.Insert(3, ":");
                s = s[..^5] + zoneWithColon;
            }
        }

        if (DateTime.TryParseExact(
                s,
                "yyyyMMddHHmmsszzz",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                out var dt))
        {
            return dt.ToUniversalTime();
        }

        return DateTime.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
    }
}

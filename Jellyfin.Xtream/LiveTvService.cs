// Copyright (C) 2022  Kevin Jilissen

// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.

// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Xtream.Client;
using Jellyfin.Xtream.Client.Models;
using Jellyfin.Xtream.Service;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Model.Dto;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Xtream;

/// <summary>
/// Class LiveTvService.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="LiveTvService"/> class.
/// </remarks>
/// <param name="appHost">Instance of the <see cref="IServerApplicationHost"/> interface.</param>
/// <param name="httpClientFactory">Instance of the <see cref="IHttpClientFactory"/> interface.</param>
/// <param name="logger">Instance of the <see cref="ILogger"/> interface.</param>
/// <param name="memoryCache">Instance of the <see cref="IMemoryCache"/> interface.</param>
/// <param name="xtreamClient">Instance of the <see cref="IXtreamClient"/> interface.</param>
/// <param name="xmlTvEpgService">Instance of the <see cref="XmlTvEpgService"/> class.</param>
public class LiveTvService(
    IServerApplicationHost appHost,
    IHttpClientFactory httpClientFactory,
    ILogger<LiveTvService> logger,
    IMemoryCache memoryCache,
    IXtreamClient xtreamClient,
    XmlTvEpgService xmlTvEpgService) : ILiveTvService, ISupportsDirectStreamProvider
{
    private const int EmptyChannelCacheMinutes = 5;

    /// <inheritdoc />
    public string Name => "Xtream Live";

    /// <inheritdoc />
    public string HomePageUrl => string.Empty;

    /// <inheritdoc />
    public async Task<IEnumerable<ChannelInfo>> GetChannelsAsync(CancellationToken cancellationToken)
    {
        Plugin plugin = Plugin.Instance;
        List<ChannelInfo> items = new List<ChannelInfo>();
        foreach (StreamInfo channel in await plugin.StreamService.GetLiveStreamsWithOverrides(cancellationToken).ConfigureAwait(false))
        {
            ParsedName parsed = plugin.StreamService.ParseName(channel.Name, FilterScope.LiveTvItem);
            items.Add(new ChannelInfo()
            {
                Id = StreamService.ToGuid(StreamService.LiveTvPrefix, channel.StreamId, 0, 0).ToString(),
                Number = channel.Num.ToString(CultureInfo.InvariantCulture),
                ImageUrl = channel.StreamIcon,
                Name = parsed.Title,
                Tags = parsed.Tags,
            });
        }

        return items;
    }

    /// <inheritdoc />
    public Task CancelTimerAsync(string timerId, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public Task CreateTimerAsync(TimerInfo info, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public Task<IEnumerable<TimerInfo>> GetTimersAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<IEnumerable<TimerInfo>>(new List<TimerInfo>());
    }

    /// <inheritdoc />
    public Task<IEnumerable<SeriesTimerInfo>> GetSeriesTimersAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<IEnumerable<SeriesTimerInfo>>(new List<SeriesTimerInfo>());
    }

    /// <inheritdoc />
    public Task CreateSeriesTimerAsync(SeriesTimerInfo info, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public Task UpdateSeriesTimerAsync(SeriesTimerInfo info, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public Task UpdateTimerAsync(TimerInfo updatedTimer, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public Task CancelSeriesTimerAsync(string timerId, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public async Task<List<MediaSourceInfo>> GetChannelStreamMediaSources(string channelId, CancellationToken cancellationToken)
    {
        MediaSourceInfo source = await GetChannelStream(channelId, string.Empty, cancellationToken).ConfigureAwait(false);
        return new List<MediaSourceInfo> { source };
    }

    /// <inheritdoc />
    public Task<MediaSourceInfo> GetChannelStream(string channelId, string streamId, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public Task CloseLiveStream(string id, CancellationToken cancellationToken)
    {
        logger.LogInformation("Closing livestream {ChannelId}", id);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<SeriesTimerInfo> GetNewTimerDefaultsAsync(CancellationToken cancellationToken, ProgramInfo? program = null)
    {
        return Task.FromResult(new SeriesTimerInfo
        {
            PostPaddingSeconds = 120,
            PrePaddingSeconds = 120,
            RecordAnyChannel = false,
            RecordAnyTime = true,
            RecordNewOnly = false
        });
    }

    /// <inheritdoc />
    public async Task<IEnumerable<ProgramInfo>> GetProgramsAsync(string channelId, DateTime startDateUtc, DateTime endDateUtc, CancellationToken cancellationToken)
    {
        Guid guid = Guid.Parse(channelId);
        StreamService.FromGuid(guid, out int prefix, out int streamId, out int _, out int _);
        if (prefix != StreamService.LiveTvPrefix)
        {
            throw new ArgumentException("Unsupported channel");
        }

        string key = $"xtream-epg-{channelId}";
        if (memoryCache.TryGetValue(key, out ICollection<ProgramInfo>? cached))
        {
            return FilterPrograms(cached!, startDateUtc, endDateUtc);
        }

        var items = new List<ProgramInfo>();
        Plugin plugin = Plugin.Instance;
        logger.LogInformation(
            "GetProgramsAsync for channel {ChannelId}, streamId {StreamId}. UseXmlTv: {UseXmlTv}, XmlTvUrl: '{XmlTvUrl}'",
            channelId,
            streamId,
            plugin.Configuration.UseXmlTv,
            plugin.Configuration.XmlTvUrl ?? "(default)");

        if (plugin.Configuration.UseXmlTv)
        {
            logger.LogInformation("Using XMLTV for EPG data (streamId: {StreamId})", streamId);

            string streamsCacheKey = $"xtream-liveStreams-{plugin.DataVersion}";
            if (!memoryCache.TryGetValue(streamsCacheKey, out IEnumerable<StreamInfo>? allStreams))
            {
                allStreams = await plugin.StreamService.GetLiveStreams(cancellationToken).ConfigureAwait(false);
                memoryCache.Set(streamsCacheKey, allStreams, TimeSpan.FromMinutes(plugin.Configuration.XmlTvCacheMinutes));
            }

            if (allStreams?.FirstOrDefault(s => s.StreamId == streamId) != null)
            {
                XmlTvProgrammeIndex index = await xmlTvEpgService.GetProgrammeIndexAsync(allStreams, cancellationToken).ConfigureAwait(false);
                IReadOnlyList<XmlTvProgramme> progs = XmlTvChannelMapper.GetProgrammesForStream(
                    streamId,
                    index.StreamToChannelIds,
                    index.ProgrammesByChannelId,
                    logger);

                int localId = 1;
                foreach (var p in progs)
                {
                    items.Add(new ProgramInfo
                    {
                        Id = StreamService.ToGuid(StreamService.EpgPrefix, streamId, localId++, 0).ToString(),
                        ChannelId = channelId,
                        StartDate = p.Start,
                        EndDate = p.End,
                        Name = p.Title,
                        Overview = p.Description,
                    });
                }
            }
        }
        else
        {
            logger.LogDebug("Using per-channel EPG API for streamId: {StreamId}", streamId);
            try
            {
                EpgListings epgs = await xtreamClient.GetEpgInfoAsync(plugin.Creds, streamId, cancellationToken).ConfigureAwait(false);

                if (epgs?.Listings == null)
                {
                    logger.LogWarning("No EPG data returned for streamId: {StreamId}", streamId);
                }
                else
                {
                    foreach (EpgInfo epg in epgs.Listings)
                    {
                        items.Add(new ProgramInfo
                        {
                            Id = StreamService.ToGuid(StreamService.EpgPrefix, streamId, epg.Id, 0).ToString(),
                            ChannelId = channelId,
                            StartDate = epg.Start,
                            EndDate = epg.End,
                            Name = epg.Title,
                            Overview = epg.Description,
                        });
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                logger.LogError(
                    ex,
                    "Failed to fetch per-channel EPG for streamId {StreamId}. Status: {StatusCode}",
                    streamId,
                    ex.StatusCode);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error fetching EPG for streamId {StreamId}", streamId);
            }
        }

        memoryCache.Set(
            key,
            items,
            DateTimeOffset.Now.AddMinutes(items.Count > 0 ? 30 : EmptyChannelCacheMinutes));

        return FilterPrograms(items, startDateUtc, endDateUtc);
    }

    private static IEnumerable<ProgramInfo> FilterPrograms(
        ICollection<ProgramInfo> items,
        DateTime startDateUtc,
        DateTime endDateUtc) =>
        from epg in items
        where epg.EndDate >= startDateUtc && epg.StartDate < endDateUtc
        select epg;

    /// <inheritdoc />
    public Task ResetTuner(string id, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public async Task<ILiveStream> GetChannelStreamWithDirectStreamProvider(string channelId, string streamId, List<ILiveStream> currentLiveStreams, CancellationToken cancellationToken)
    {
        Guid guid = Guid.Parse(channelId);
        StreamService.FromGuid(guid, out int prefix, out int channel, out int _, out int _);
        if (prefix != StreamService.LiveTvPrefix)
        {
            throw new ArgumentException("Unsupported channel");
        }

        Plugin plugin = Plugin.Instance;
        MediaSourceInfo mediaSourceInfo = plugin.StreamService.GetMediaSourceInfo(StreamType.Live, channel, restream: true);
        ILiveStream? stream = currentLiveStreams.Find(stream => stream.TunerHostId == Restream.TunerHost && stream.MediaSource.Id == mediaSourceInfo.Id);

        if (stream == null)
        {
            stream = new Restream(appHost, httpClientFactory, logger, mediaSourceInfo);
            await stream.Open(cancellationToken).ConfigureAwait(false);
        }

        stream.ConsumerCount++;
        return stream;
    }
}

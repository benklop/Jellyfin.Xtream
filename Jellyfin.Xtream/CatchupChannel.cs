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
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Xtream.Client;
using Jellyfin.Xtream.Client.Models;
using Jellyfin.Xtream.Service;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Channels;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Xtream;

/// <summary>
/// The Xtream Codes API channel.
/// </summary>
/// <param name="logger">Instance of the <see cref="ILogger"/> interface.</param>
/// <param name="xtreamClient">Instance of the <see cref="IXtreamClient"/> interface.</param>
/// <param name="xmlTvEpgService">Instance of the <see cref="XmlTvEpgService"/> class.</param>
public class CatchupChannel(ILogger<CatchupChannel> logger, IXtreamClient xtreamClient, XmlTvEpgService xmlTvEpgService) : IChannel, IDisableMediaSourceDisplay
{
    private readonly ILogger<CatchupChannel> _logger = logger;
    private readonly XmlTvEpgService _xmlTvEpgService = xmlTvEpgService;

    /// <inheritdoc />
    public string? Name => "Xtream Catch-up";

    /// <inheritdoc />
    public string? Description => "Rewatch IPTV streamed from the Xtream-compatible server.";

    /// <inheritdoc />
    public string DataVersion => Plugin.Instance.DataVersion + DateTime.Today.ToShortDateString();

    /// <inheritdoc />
    public string HomePageUrl => string.Empty;

    /// <inheritdoc />
    public ChannelParentalRating ParentalRating => ChannelParentalRating.GeneralAudience;

    /// <inheritdoc />
    public InternalChannelFeatures GetChannelFeatures()
    {
        return new InternalChannelFeatures
        {
            ContentTypes = [
                ChannelMediaContentType.TvExtra,
            ],
            MediaTypes = [
                ChannelMediaType.Video
            ],
        };
    }

    /// <inheritdoc />
    public Task<DynamicImageResponse> GetChannelImage(ImageType type, CancellationToken cancellationToken)
    {
        switch (type)
        {
            default:
                throw new ArgumentException("Unsupported image type: " + type);
        }
    }

    /// <inheritdoc />
    public IEnumerable<ImageType> GetSupportedChannelImages() => new List<ImageType>
    {
        // ImageType.Primary
    };

    /// <inheritdoc />
    public async Task<ChannelItemResult> GetChannelItems(InternalChannelItemQuery query, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrEmpty(query.FolderId))
            {
                return await GetChannels(cancellationToken).ConfigureAwait(false);
            }

            Guid guid = Guid.Parse(query.FolderId);
            StreamService.FromGuid(guid, out int prefix, out int categoryId, out int channelId, out int date);

            if (date == 0)
            {
                return await GetDays(categoryId, channelId, cancellationToken).ConfigureAwait(false);
            }

            return await GetStreams(categoryId, channelId, date, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get channel items");
            throw;
        }
    }

    private async Task<ChannelItemResult> GetChannels(CancellationToken cancellationToken)
    {
        Plugin plugin = Plugin.Instance;
        List<ChannelItemInfo> items = [];
        foreach (StreamInfo channel in await plugin.StreamService.GetLiveStreamsWithOverrides(cancellationToken).ConfigureAwait(false))
        {
            if (!channel.TvArchive)
            {
                // Channel has no catch-up support.
                continue;
            }

            ParsedName parsedName = plugin.StreamService.ParseName(channel.Name, FilterScope.LiveTvItem);
            items.Add(new ChannelItemInfo()
            {
                Id = StreamService.ToGuid(StreamService.CatchupPrefix, channel.CategoryId ?? 0, channel.StreamId, 0).ToString(),
                ImageUrl = channel.StreamIcon,
                Name = parsedName.Title,
                Tags = new List<string>(parsedName.Tags),
                Type = ChannelItemType.Folder,
            });
        }

        ChannelItemResult result = new ChannelItemResult()
        {
            Items = items,
            TotalRecordCount = items.Count
        };
        return result;
    }

    private async Task<ChannelItemResult> GetDays(int categoryId, int channelId, CancellationToken cancellationToken)
    {
        Plugin plugin = Plugin.Instance;

        List<StreamInfo> streams = await xtreamClient.GetLiveStreamsByCategoryAsync(plugin.Creds, categoryId, cancellationToken).ConfigureAwait(false);
        StreamInfo channel = streams.FirstOrDefault(s => s.StreamId == channelId)
            ?? throw new ArgumentException($"Channel with id {channelId} not found in category {categoryId}");
        ParsedName parsedName = plugin.StreamService.ParseName(channel.Name, FilterScope.LiveTvItem);

        List<ChannelItemInfo> items = [];
        for (int i = 0; i <= channel.TvArchiveDuration; i++)
        {
            DateTime channelDay = DateTime.Today.AddDays(-i);
            int day = (int)(channelDay - DateTime.UnixEpoch).TotalDays;
            items.Add(new()
            {
                Id = StreamService.ToGuid(StreamService.CatchupPrefix, channel.CategoryId ?? 0, channel.StreamId, day).ToString(),
                ImageUrl = channel.StreamIcon,
                Name = channelDay.ToLocalTime().ToString("ddd dd'-'MM'-'yyyy", CultureInfo.InvariantCulture),
                Tags = new List<string>(parsedName.Tags),
                Type = ChannelItemType.Folder,
            });
        }

        ChannelItemResult result = new()
        {
            Items = items,
            TotalRecordCount = items.Count
        };
        return result;
    }

    private async Task<ChannelItemResult> GetStreams(int categoryId, int channelId, int day, CancellationToken cancellationToken)
    {
        DateTime start = DateTime.UnixEpoch.AddDays(day);
        DateTime end = start.AddDays(1);
        Plugin plugin = Plugin.Instance;

        List<StreamInfo> streams = await xtreamClient.GetLiveStreamsByCategoryAsync(plugin.Creds, categoryId, cancellationToken).ConfigureAwait(false);
        StreamInfo channel = streams.FirstOrDefault(s => s.StreamId == channelId)
            ?? throw new ArgumentException($"Channel with id {channelId} not found in category {categoryId}");

        if (plugin.Configuration.UseXmlTv)
        {
            IEnumerable<StreamInfo> configuredStreams = await plugin.StreamService.GetLiveStreams(cancellationToken).ConfigureAwait(false);
            IReadOnlyList<XmlTvProgramme> programmes = await _xmlTvEpgService.GetProgrammesForStreamAsync(
                channelId,
                configuredStreams,
                start,
                end,
                cancellationToken).ConfigureAwait(false);

            return BuildStreamItemsFromProgrammes(plugin, channel, channelId, day, programmes);
        }

        EpgListings epgs = await xtreamClient.GetEpgInfoAsync(plugin.Creds, channelId, cancellationToken).ConfigureAwait(false);
        List<ChannelItemInfo> items = [];

        // Create fallback single-stream catch-up if no EPG is available.
        if (epgs.Listings.Count == 0)
        {
            return BuildNoEpgFallback(plugin, channelId, day, start);
        }

        foreach (EpgInfo epg in epgs.Listings.Where(epg => epg.Start <= end && epg.End >= start))
        {
            items.Add(CreateCatchupItem(plugin, channel, channelId, day, epg.Title, epg.Description, epg.Start, epg.StartLocalTime, epg.End, epg.Id));
        }

        return new ChannelItemResult
        {
            Items = items,
            TotalRecordCount = items.Count
        };
    }

    private ChannelItemResult BuildStreamItemsFromProgrammes(
        Plugin plugin,
        StreamInfo channel,
        int channelId,
        int day,
        IReadOnlyList<XmlTvProgramme> programmes)
    {
        if (programmes.Count == 0)
        {
            DateTime start = DateTime.UnixEpoch.AddDays(day);
            return BuildNoEpgFallback(plugin, channelId, day, start);
        }

        List<ChannelItemInfo> items = [];
        foreach (XmlTvProgramme programme in programmes)
        {
            DateTime startLocal = programme.Start.ToLocalTime();
            int epgId = Math.Abs(HashCode.Combine(programme.Start.Ticks, programme.Title));
            items.Add(CreateCatchupItem(
                plugin,
                channel,
                channelId,
                day,
                programme.Title,
                programme.Description,
                programme.Start,
                startLocal,
                programme.End,
                epgId));
        }

        return new ChannelItemResult
        {
            Items = items,
            TotalRecordCount = items.Count
        };
    }

    private static ChannelItemResult BuildNoEpgFallback(Plugin plugin, int channelId, int day, DateTime start)
    {
        int durationMinutes = 24 * 60;
        return new ChannelItemResult
        {
            Items = new List<ChannelItemInfo>()
            {
                new()
                {
                    ContentType = ChannelMediaContentType.TvExtra,
                    Id = StreamService.ToGuid(StreamService.CatchupStreamPrefix, channelId, 0, day).ToString(),
                    IsLiveStream = false,
                    MediaSources = [
                        plugin.StreamService.GetMediaSourceInfo(StreamType.CatchUp, channelId, start: start, durationMinutes: durationMinutes)
                    ],
                    MediaType = ChannelMediaType.Video,
                    Name = "No EPG available",
                    RunTimeTicks = durationMinutes * TimeSpan.TicksPerMinute,
                    Type = ChannelItemType.Media,
                }
            },
            TotalRecordCount = 1
        };
    }

    private static ChannelItemInfo CreateCatchupItem(
        Plugin plugin,
        StreamInfo channel,
        int channelId,
        int day,
        string title,
        string description,
        DateTime startUtc,
        DateTime startLocal,
        DateTime endUtc,
        int epgId)
    {
        ParsedName parsedName = plugin.StreamService.ParseName(title, FilterScope.LiveTvItem);
        int durationMinutes = (int)Math.Ceiling((endUtc - startUtc).TotalMinutes);
        string dateTitle = startUtc.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);
        return new ChannelItemInfo
        {
            ContentType = ChannelMediaContentType.TvExtra,
            DateCreated = startUtc,
            Id = StreamService.ToGuid(StreamService.CatchupStreamPrefix, channel.StreamId, epgId, day).ToString(),
            IsLiveStream = false,
            MediaSources = [
                plugin.StreamService.GetMediaSourceInfo(StreamType.CatchUp, channelId, start: startLocal, durationMinutes: durationMinutes)
            ],
            MediaType = ChannelMediaType.Video,
            Name = $"{dateTitle} - {parsedName.Title}",
            Overview = description,
            PremiereDate = startUtc,
            RunTimeTicks = durationMinutes * TimeSpan.TicksPerMinute,
            Tags = new List<string>(parsedName.Tags),
            Type = ChannelItemType.Media,
        };
    }

    /// <inheritdoc />
    public bool IsEnabledFor(string userId)
    {
        return Plugin.Instance.Configuration.IsCatchupVisible;
    }
}

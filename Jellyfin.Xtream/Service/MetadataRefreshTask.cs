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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Xtream.Client;
using Jellyfin.Xtream.Client.Models;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Xtream.Service;

/// <summary>
/// Scheduled task to pre-download metadata for all configured Xtream VOD and Series items.
/// </summary>
/// <param name="xtreamClient">Instance of the <see cref="IXtreamClient"/> interface.</param>
/// <param name="logger">Instance of the <see cref="ILogger{MetadataRefreshTask}"/> interface.</param>
public class MetadataRefreshTask(IXtreamClient xtreamClient, ILogger<MetadataRefreshTask> logger) : IScheduledTask
{
    /// <inheritdoc />
    public string Name => "Pre-download Xtream Metadata";

    /// <inheritdoc />
    public string Key => "XtreamMetadataRefresh";

    /// <inheritdoc />
    public string Description => "Pre-downloads metadata (thumbnails, descriptions, ratings, etc.) for all configured Xtream VOD and Series content.";

    /// <inheritdoc />
    public string Category => "Xtream";

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting Xtream metadata refresh task");
        Plugin plugin = Plugin.Instance;
        int totalItems = 0;

        try
        {
            // Refresh VOD metadata
            if (plugin.Configuration.IsVodVisible)
            {
                logger.LogInformation("Processing VOD metadata");
                totalItems += await RefreshVodMetadata(progress, cancellationToken).ConfigureAwait(false);
            }

            // Refresh Series metadata
            if (plugin.Configuration.IsSeriesVisible)
            {
                logger.LogInformation("Processing Series metadata");
                totalItems += await RefreshSeriesMetadata(progress, cancellationToken).ConfigureAwait(false);
            }

            logger.LogInformation("Xtream metadata refresh completed. Processed {TotalItems} items", totalItems);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during metadata refresh task");
            throw;
        }
    }

    private async Task<int> RefreshVodMetadata(IProgress<double> progress, CancellationToken cancellationToken)
    {
        int processedCount = 0;
        int totalCount = 0;
        Plugin plugin = Plugin.Instance;

        try
        {
            // Get all configured VOD categories
            IEnumerable<Category> categories = await plugin.StreamService.GetVodCategories(cancellationToken).ConfigureAwait(false);

            foreach (Category category in categories)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                logger.LogDebug("Processing VOD category: {CategoryName}", category.CategoryName);

                // Get all streams in this category
                IEnumerable<StreamInfo> streams = await plugin.StreamService.GetVodStreams(category.CategoryId, cancellationToken).ConfigureAwait(false);
                List<StreamInfo> streamList = streams.ToList();
                totalCount += streamList.Count;

                foreach (StreamInfo stream in streamList)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    try
                    {
                        // Pre-fetch metadata from Xtream API
                        // This will ensure the metadata is available for Jellyfin to use
                        logger.LogDebug("Fetching metadata for VOD: {StreamName} (ID: {StreamId})", stream.Name, stream.StreamId);
                        VodStreamInfo vodInfo = await xtreamClient.GetVodInfoAsync(plugin.Creds, stream.StreamId, cancellationToken).ConfigureAwait(false);

                        if (vodInfo.Info != null)
                        {
                            logger.LogTrace("Retrieved metadata for VOD: {StreamName}", stream.Name);
                        }

                        processedCount++;
                        progress?.Report((double)processedCount / Math.Max(totalCount, 1) * 50.0); // VOD is first 50%
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to refresh metadata for VOD stream {StreamId}", stream.StreamId);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error refreshing VOD metadata");
        }

        return processedCount;
    }

    private async Task<int> RefreshSeriesMetadata(IProgress<double> progress, CancellationToken cancellationToken)
    {
        int processedCount = 0;
        int totalCount = 0;
        Plugin plugin = Plugin.Instance;

        try
        {
            // Get all configured Series categories
            IEnumerable<Category> categories = await plugin.StreamService.GetSeriesCategories(cancellationToken).ConfigureAwait(false);

            foreach (Category category in categories)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                logger.LogDebug("Processing Series category: {CategoryName}", category.CategoryName);

                // Get all series in this category
                IEnumerable<Client.Models.Series> seriesList = await plugin.StreamService.GetSeries(category.CategoryId, cancellationToken).ConfigureAwait(false);
                List<Client.Models.Series> series = seriesList.ToList();
                totalCount += series.Count;

                foreach (Client.Models.Series seriesItem in series)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    try
                    {
                        // Pre-fetch series info from Xtream API
                        // This will ensure the metadata is available for Jellyfin to use
                        logger.LogDebug("Fetching metadata for Series: {SeriesName} (ID: {SeriesId})", seriesItem.Name, seriesItem.SeriesId);
                        SeriesStreamInfo seriesInfo = await xtreamClient.GetSeriesStreamsBySeriesAsync(plugin.Creds, seriesItem.SeriesId, cancellationToken).ConfigureAwait(false);

                        if (seriesInfo.Info != null)
                        {
                            logger.LogTrace("Retrieved metadata for Series: {SeriesName}", seriesItem.Name);
                        }

                        processedCount++;
                        progress?.Report(50.0 + ((double)processedCount / Math.Max(totalCount, 1) * 50.0)); // Series is second 50%
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to refresh metadata for Series {SeriesId}", seriesItem.SeriesId);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error refreshing Series metadata");
        }

        return processedCount;
    }

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        // No default triggers - user can schedule manually
        return [];
    }
}

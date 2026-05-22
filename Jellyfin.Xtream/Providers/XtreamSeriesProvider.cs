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
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Xtream.Client;
using Jellyfin.Xtream.Client.Models;
using Jellyfin.Xtream.Service;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Xtream.Providers;

/// <summary>
/// The Xtream Codes Series metadata provider.
/// </summary>
/// <param name="logger">Instance of the <see cref="ILogger"/> interface.</param>
/// <param name="providerManager">Instance of the <see cref="IProviderManager"/> interface.</param>
/// <param name="xtreamClient">Instance of the <see cref="IXtreamClient"/> interface.</param>
public partial class XtreamSeriesProvider(ILogger<SeriesChannel> logger, IProviderManager providerManager, IXtreamClient xtreamClient) : ICustomMetadataProvider<MediaBrowser.Controller.Entities.TV.Series>, IPreRefreshProvider
{
    /// <summary>
    /// The name of the provider.
    /// </summary>
    public const string ProviderName = "XtreamSeriesProvider";

    /// <inheritdoc/>
    public string Name => ProviderName;

    /// <summary>
    /// Regex pattern to extract year from parenthetical content like "(2024)" or "(2024) (US)".
    /// </summary>
    [GeneratedRegex(@"\((\d{4})\)")]
    private static partial Regex YearPattern();

    /// <summary>
    /// Extracts year from series name if present in format like "(2024)" or "(2024) (US)".
    /// </summary>
    /// <param name="name">The series name to parse.</param>
    /// <returns>The extracted year or null if not found.</returns>
    private static int? ExtractYearFromName(string name)
    {
        Match match = YearPattern().Match(name);
        if (match.Success && int.TryParse(match.Groups[1].Value, out int year))
        {
            // Sanity check: year should be reasonable (between 1900 and current year + 5)
            if (year >= 1900 && year <= DateTime.UtcNow.Year + 5)
            {
                return year;
            }
        }

        return null;
    }

    /// <inheritdoc/>
    public async Task<ItemUpdateType> FetchAsync(MediaBrowser.Controller.Entities.TV.Series item, MetadataRefreshOptions options, CancellationToken cancellationToken)
    {
        if (!Plugin.Instance.Configuration.IsSeriesVisible)
        {
            return ItemUpdateType.None;
        }

        string? idStr = item.GetProviderId(ProviderName);
        if (idStr is not null)
        {
            logger.LogDebug("Getting metadata for series {Id}", idStr);
            int id = int.Parse(idStr, CultureInfo.InvariantCulture);
            SeriesStreamInfo series = await xtreamClient.GetSeriesStreamsBySeriesAsync(Plugin.Instance.Creds, id, cancellationToken).ConfigureAwait(false);
            Client.Models.SeriesInfo? info = series.Info;

            if (info is null)
            {
                return ItemUpdateType.None;
            }

            item.Overview ??= info.Plot;
            item.CommunityRating ??= (float)info.Rating;

            if (info.Genre is string genres)
            {
                item.Genres ??= genres.Split(',').Select(genre => genre.Trim()).ToArray();
            }

            if (!item.HasProviderId(MetadataProvider.Tmdb))
            {
                if (Plugin.Instance.Configuration.IsTmdbSeriesOverride)
                {
                    // Extract year from series name if present (e.g., "Series Name (2024)" or "Series Name (2024) (US)")
                    int? year = ExtractYearFromName(info.Name ?? string.Empty);

                    // Try to fetch the TMDB id to get proper metadata.
                    RemoteSearchQuery<MediaBrowser.Controller.Providers.SeriesInfo> query = new()
                    {
                        SearchInfo = new()
                        {
                            Name = Plugin.Instance.StreamService.ParseName(info.Name ?? string.Empty, FilterScope.SeriesItem).Title,
                            Year = year,
                        },
                        SearchProviderName = "TheMovieDb",
                    };
                    IEnumerable<RemoteSearchResult> results = await providerManager.GetRemoteSearchResults<MediaBrowser.Controller.Entities.TV.Series, MediaBrowser.Controller.Providers.SeriesInfo>(query, cancellationToken).ConfigureAwait(false);
                    if (results.Any())
                    {
                        RemoteSearchResult tmdbSeries = results.First();
                        if (tmdbSeries.HasProviderId(MetadataProvider.Tmdb))
                        {
                            string? queryId = tmdbSeries.GetProviderId(MetadataProvider.Tmdb);
                            if (queryId is not null)
                            {
                                options.ReplaceAllMetadata = true;
                                item.SetProviderId(MetadataProvider.Tmdb, queryId);
                            }
                        }
                    }
                }
            }
        }

        return ItemUpdateType.MetadataImport;
    }
}

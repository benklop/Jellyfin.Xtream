// Copyright (C) 2022  Kevin Jilissen

using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Xtream.Api.Models;
using Jellyfin.Xtream.Client;
using Jellyfin.Xtream.Client.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Xtream.Api;

/// <summary>
/// The Jellyfin Xtream configuration API.
/// </summary>
[ApiController]
[Route("[controller]")]
[Produces(MediaTypeNames.Application.Json)]
public class XtreamController : ControllerBase
{
    private readonly IXtreamClient _xtreamClient;
    private readonly ILogger<XtreamController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="XtreamController"/> class.
    /// </summary>
    /// <param name="xtreamClient">The Xtream client.</param>
    /// <param name="logger">The logger.</param>
    public XtreamController(IXtreamClient xtreamClient, ILogger<XtreamController> logger)
    {
        _xtreamClient = xtreamClient;
        _logger = logger;
    }

    /// <summary>
    /// Log a configuration change.
    /// </summary>
    /// <param name="request">The log request.</param>
    /// <returns>A <see cref="NoContentResult"/> indicating success.</returns>
    /// <response code="204">Configuration change logged successfully.</response>
    [HttpPost("LogConfigChange")]
    [ProducesResponseType(204)]
    [Authorize(Policy = "RequiresElevation")]
    public ActionResult LogConfigChange([FromBody] LogConfigChangeRequest request)
    {
        _logger.LogInformation(
            "Xtream plugin configuration changed: {Page} settings updated. RemoteIp: {RemoteIp}",
            request.Page,
            HttpContext?.Connection?.RemoteIpAddress);

        // Log the current plugin configuration state
        var plugin = Plugin.Instance;
        _logger.LogDebug(
            "Current plugin configuration state - UseXmlTv: {UseXmlTv}, XmlTvUrl: {XmlTvUrl}",
            plugin.Configuration.UseXmlTv,
            plugin.Configuration.XmlTvUrl);

        return NoContent();
    }

    private static CategoryResponse CreateCategoryResponse(Category category) =>
        new()
        {
            Id = category.CategoryId,
            Name = category.CategoryName,
        };

    private static ItemResponse CreateItemResponse(StreamInfo stream) =>
        new()
        {
            Id = stream.StreamId,
            Name = stream.Name,
            HasCatchup = stream.TvArchive,
            CatchupDuration = stream.TvArchiveDuration,
        };

    private static ItemResponse CreateItemResponse(Series series) =>
        new()
        {
            Id = series.SeriesId,
            Name = series.Name,
            HasCatchup = false,
            CatchupDuration = 0,
        };

    private static ChannelResponse CreateChannelResponse(StreamInfo stream) =>
        new()
        {
            Id = stream.StreamId,
            LogoUrl = stream.StreamIcon,
            Name = stream.Name,
            Number = stream.Num,
        };

    /// <summary>
    /// Test the configured provider.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token for cancelling requests.</param>
    /// <returns>An enumerable containing the categories.</returns>
    [Authorize(Policy = "RequiresElevation")]
    [HttpGet("TestProvider")]
    public async Task<ActionResult<ProviderTestResponse>> TestProvider(CancellationToken cancellationToken)
    {
        Plugin plugin = Plugin.Instance;
        PlayerApi info = await _xtreamClient.GetUserAndServerInfoAsync(plugin.Creds, cancellationToken).ConfigureAwait(false);
        return Ok(new ProviderTestResponse()
        {
            ActiveConnections = info.UserInfo.ActiveCons,
            ExpiryDate = info.UserInfo.ExpDate,
            MaxConnections = info.UserInfo.MaxConnections,
            ServerTime = info.ServerInfo.TimeNow,
            ServerTimezone = info.ServerInfo.Timezone,
            Status = info.UserInfo.Status,
            SupportsMpegTs = info.UserInfo.AllowedOutputFormats.Contains("ts"),
        });
    }

    /// <summary>
    /// Get all Live TV categories.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token for cancelling requests.</param>
    /// <returns>An enumerable containing the categories.</returns>
    [Authorize(Policy = "RequiresElevation")]
    [HttpGet("LiveCategories")]
    public async Task<ActionResult<IEnumerable<CategoryResponse>>> GetLiveCategories(CancellationToken cancellationToken)
    {
        Plugin plugin = Plugin.Instance;
        List<Category> categories = await _xtreamClient.GetLiveCategoryAsync(plugin.Creds, cancellationToken).ConfigureAwait(false);
        return Ok(categories.Select(CreateCategoryResponse));
    }

    /// <summary>
    /// Get all Live TV streams for the given category.
    /// </summary>
    /// <param name="categoryId">The category for which to fetch the streams.</param>
    /// <param name="cancellationToken">The cancellation token for cancelling requests.</param>
    /// <returns>An enumerable containing the streams.</returns>
    [Authorize(Policy = "RequiresElevation")]
    [HttpGet("LiveCategories/{categoryId}")]
    public async Task<ActionResult<IEnumerable<StreamInfo>>> GetLiveStreams(int categoryId, CancellationToken cancellationToken)
    {
        Plugin plugin = Plugin.Instance;
        List<StreamInfo> streams = await _xtreamClient.GetLiveStreamsByCategoryAsync(
          plugin.Creds,
          categoryId,
          cancellationToken).ConfigureAwait(false);
        return Ok(streams.Select(CreateItemResponse));
    }

    /// <summary>
    /// Get all VOD categories.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token for cancelling requests.</param>
    /// <returns>An enumerable containing the categories.</returns>
    [Authorize(Policy = "RequiresElevation")]
    [HttpGet("VodCategories")]
    public async Task<ActionResult<IEnumerable<CategoryResponse>>> GetVodCategories(CancellationToken cancellationToken)
    {
        if (!Plugin.Instance.Configuration.IsVodVisible)
        {
            return Ok(Enumerable.Empty<CategoryResponse>());
        }

        Plugin plugin = Plugin.Instance;
        List<Category> categories = await _xtreamClient.GetVodCategoryAsync(plugin.Creds, cancellationToken).ConfigureAwait(false);
        return Ok(categories.Select(CreateCategoryResponse));
    }

    /// <summary>
    /// Get all VOD streams for the given category.
    /// </summary>
    /// <param name="categoryId">The category for which to fetch the streams.</param>
    /// <param name="cancellationToken">The cancellation token for cancelling requests.</param>
    /// <returns>An enumerable containing the streams.</returns>
    [Authorize(Policy = "RequiresElevation")]
    [HttpGet("VodCategories/{categoryId}")]
    public async Task<ActionResult<IEnumerable<StreamInfo>>> GetVodStreams(int categoryId, CancellationToken cancellationToken)
    {
        if (!Plugin.Instance.Configuration.IsVodVisible)
        {
            return Ok(Enumerable.Empty<StreamInfo>());
        }

        Plugin plugin = Plugin.Instance;
        List<StreamInfo> streams = await _xtreamClient.GetVodStreamsByCategoryAsync(
          plugin.Creds,
          categoryId,
          cancellationToken).ConfigureAwait(false);
        return Ok(streams.Select(CreateItemResponse));
    }

    /// <summary>
    /// Get all Series categories.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token for cancelling requests.</param>
    /// <returns>An enumerable containing the categories.</returns>
    [Authorize(Policy = "RequiresElevation")]
    [HttpGet("SeriesCategories")]
    public async Task<ActionResult<IEnumerable<CategoryResponse>>> GetSeriesCategories(CancellationToken cancellationToken)
    {
        if (!Plugin.Instance.Configuration.IsSeriesVisible)
        {
            return Ok(Enumerable.Empty<CategoryResponse>());
        }

        Plugin plugin = Plugin.Instance;
        List<Category> categories = await _xtreamClient.GetSeriesCategoryAsync(plugin.Creds, cancellationToken).ConfigureAwait(false);
        return Ok(categories.Select(CreateCategoryResponse));
    }

    /// <summary>
    /// Get all Series streams for the given category.
    /// </summary>
    /// <param name="categoryId">The category for which to fetch the streams.</param>
    /// <param name="cancellationToken">The cancellation token for cancelling requests.</param>
    /// <returns>An enumerable containing the streams.</returns>
    [Authorize(Policy = "RequiresElevation")]
    [HttpGet("SeriesCategories/{categoryId}")]
    public async Task<ActionResult<IEnumerable<StreamInfo>>> GetSeriesStreams(int categoryId, CancellationToken cancellationToken)
    {
        if (!Plugin.Instance.Configuration.IsSeriesVisible)
        {
            return Ok(Enumerable.Empty<StreamInfo>());
        }

        Plugin plugin = Plugin.Instance;
        List<Series> series = await _xtreamClient.GetSeriesByCategoryAsync(
          plugin.Creds,
          categoryId,
          cancellationToken).ConfigureAwait(false);
        return Ok(series.Select(CreateItemResponse));
    }

    /// <summary>
    /// Get all configured TV channels.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token for cancelling requests.</param>
    /// <returns>An enumerable containing the streams.</returns>
    [Authorize(Policy = "RequiresElevation")]
    [HttpGet("LiveTv")]
    public async Task<ActionResult<IEnumerable<StreamInfo>>> GetLiveTvChannels(CancellationToken cancellationToken)
    {
        IEnumerable<StreamInfo> streams = await Plugin.Instance.StreamService.GetLiveStreams(cancellationToken).ConfigureAwait(false);
        var channels = streams.Select(CreateChannelResponse).ToList();
        return Ok(channels);
    }

    /// <summary>
    /// Test a name filter against sample data from all content types.
    /// </summary>
    /// <param name="request">The filter test request.</param>
    /// <param name="cancellationToken">The cancellation token for cancelling requests.</param>
    /// <returns>Filter test results showing before/after for sample items.</returns>
    [Authorize(Policy = "RequiresElevation")]
    [HttpPost("TestFilter")]
#pragma warning disable CA3012 // Review code for regex injection vulnerabilities
    public async Task<ActionResult<FilterTestResponse>> TestFilter([FromBody] FilterTestRequest request, CancellationToken cancellationToken)
#pragma warning restore CA3012 // Review code for regex injection vulnerabilities
    {
        var response = new FilterTestResponse();

#pragma warning disable CA3012 // Review code for regex injection vulnerabilities
        try
        {
            var regex = new System.Text.RegularExpressions.Regex(request.Pattern, System.Text.RegularExpressions.RegexOptions.None, System.TimeSpan.FromSeconds(1));
#pragma warning restore CA3012 // Review code for regex injection vulnerabilities

            // Sample Live TV categories (max 5)
            var liveTvCategories = await _xtreamClient.GetLiveCategoryAsync(Plugin.Instance.Creds, cancellationToken).ConfigureAwait(false);
            foreach (var category in liveTvCategories.Take(5))
            {
                var after = regex.Replace(category.CategoryName, request.Replacement);
                response.LiveTvCategories.Add(new FilterTestItem
                {
                    Before = category.CategoryName,
                    After = after,
                    Changed = after != category.CategoryName
                });
            }

            // Sample Live TV items from first category (max 5)
            if (liveTvCategories.Count > 0)
            {
                var liveStreams = await _xtreamClient.GetLiveStreamsByCategoryAsync(Plugin.Instance.Creds, liveTvCategories[0].CategoryId, cancellationToken).ConfigureAwait(false);
                foreach (var stream in liveStreams.Take(5))
                {
                    var after = regex.Replace(stream.Name, request.Replacement);
                    response.LiveTvItems.Add(new FilterTestItem
                    {
                        Before = stream.Name,
                        After = after,
                        Changed = after != stream.Name
                    });
                }
            }

            if (Plugin.Instance.Configuration.IsVodVisible)
            {
                // Sample VOD categories (max 5)
                var vodCategories = await _xtreamClient.GetVodCategoryAsync(Plugin.Instance.Creds, cancellationToken).ConfigureAwait(false);
                foreach (var category in vodCategories.Take(5))
                {
                    var after = regex.Replace(category.CategoryName, request.Replacement);
                    response.VodCategories.Add(new FilterTestItem
                    {
                        Before = category.CategoryName,
                        After = after,
                        Changed = after != category.CategoryName
                    });
                }

                // Sample VOD items from first category (max 5)
                if (vodCategories.Count > 0)
                {
                    var vodStreams = await _xtreamClient.GetVodStreamsByCategoryAsync(Plugin.Instance.Creds, vodCategories[0].CategoryId, cancellationToken).ConfigureAwait(false);
                    foreach (var stream in vodStreams.Take(5))
                    {
                        var after = regex.Replace(stream.Name, request.Replacement);
                        response.VodItems.Add(new FilterTestItem
                        {
                            Before = stream.Name,
                            After = after,
                            Changed = after != stream.Name
                        });
                    }
                }
            }

            if (Plugin.Instance.Configuration.IsSeriesVisible)
            {
                // Sample Series categories (max 5)
                var seriesCategories = await _xtreamClient.GetSeriesCategoryAsync(Plugin.Instance.Creds, cancellationToken).ConfigureAwait(false);
                foreach (var category in seriesCategories.Take(5))
                {
                    var after = regex.Replace(category.CategoryName, request.Replacement);
                    response.SeriesCategories.Add(new FilterTestItem
                    {
                        Before = category.CategoryName,
                        After = after,
                        Changed = after != category.CategoryName
                    });
                }

                // Sample Series items from first category (max 5)
                if (seriesCategories.Count > 0)
                {
                    var series = await _xtreamClient.GetSeriesByCategoryAsync(Plugin.Instance.Creds, seriesCategories[0].CategoryId, cancellationToken).ConfigureAwait(false);
                    foreach (var s in series.Take(5))
                    {
                        var after = regex.Replace(s.Name, request.Replacement);
                        response.SeriesItems.Add(new FilterTestItem
                        {
                            Before = s.Name,
                            After = after,
                            Changed = after != s.Name
                        });
                    }
                }
            }
        }
        catch (System.Text.RegularExpressions.RegexMatchTimeoutException)
        {
            return BadRequest("Regex pattern timed out. Please simplify your pattern.");
        }
        catch (System.ArgumentException ex)
        {
            return BadRequest($"Invalid regex pattern: {ex.Message}");
        }

        return Ok(response);
    }
}

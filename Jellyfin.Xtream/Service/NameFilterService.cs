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
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Jellyfin.Xtream.Configuration;

namespace Jellyfin.Xtream.Service;

/// <summary>
/// Service for applying name filters to channel and group names.
/// </summary>
public class NameFilterService
{
    /// <summary>
    /// Applies all enabled name filters to the specified input string.
    /// </summary>
    /// <param name="input">The input string to filter.</param>
    /// <param name="filters">The collection of name filters to apply.</param>
    /// <param name="scope">The scope where the filter is being applied.</param>
    /// <returns>The filtered string after all enabled filters have been applied.</returns>
    public string ApplyFilters(string input, System.Collections.Generic.IEnumerable<NameFilter> filters, FilterScope scope)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return input;
        }

        var result = input;

        // Apply filters in order, respecting their scope settings
        foreach (var filter in filters.Where(f => f.IsEnabled && ShouldApplyFilter(f, scope)).OrderBy(f => f.Order))
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(filter.Pattern))
                {
                    var regex = new Regex(filter.Pattern, RegexOptions.None, TimeSpan.FromSeconds(1));
                    result = regex.Replace(result, filter.Replacement ?? string.Empty);
                }
            }
            catch (ArgumentException)
            {
                // Invalid regex pattern, skip this filter
                continue;
            }
            catch (RegexMatchTimeoutException)
            {
                // Regex took too long, skip this filter
                continue;
            }
        }

        return result.Trim();
    }

    private static bool ShouldApplyFilter(NameFilter filter, FilterScope scope)
    {
        return scope switch
        {
            FilterScope.LiveTvCategory => filter.ApplyToLiveTvCategories,
            FilterScope.LiveTvItem => filter.ApplyToLiveTvItems,
            FilterScope.VodCategory => filter.ApplyToVodCategories,
            FilterScope.VodItem => filter.ApplyToVodItems,
            FilterScope.SeriesCategory => filter.ApplyToSeriesCategories,
            FilterScope.SeriesItem => filter.ApplyToSeriesItems,
            _ => true
        };
    }
}

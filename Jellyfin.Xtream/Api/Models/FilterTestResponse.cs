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

using System.Collections.Generic;

namespace Jellyfin.Xtream.Api.Models;

/// <summary>
/// Response model containing filter test results.
/// </summary>
public class FilterTestResponse
{
    /// <summary>
    /// Gets the sample Live TV categories with before/after names.
    /// </summary>
#pragma warning disable CA1002 // Do not expose generic lists
    public List<FilterTestItem> LiveTvCategories { get; } = [];

    /// <summary>
    /// Gets the sample Live TV items with before/after names.
    /// </summary>
    public List<FilterTestItem> LiveTvItems { get; } = [];

    /// <summary>
    /// Gets the sample VOD categories with before/after names.
    /// </summary>
    public List<FilterTestItem> VodCategories { get; } = [];

    /// <summary>
    /// Gets the sample VOD items with before/after names.
    /// </summary>
    public List<FilterTestItem> VodItems { get; } = [];

    /// <summary>
    /// Gets the sample Series categories with before/after names.
    /// </summary>
    public List<FilterTestItem> SeriesCategories { get; } = [];

    /// <summary>
    /// Gets the sample Series items with before/after names.
    /// </summary>
    public List<FilterTestItem> SeriesItems { get; } = [];
#pragma warning restore CA1002 // Do not expose generic lists
}

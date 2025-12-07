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

namespace Jellyfin.Xtream.Configuration;

/// <summary>
/// Represents a name filter using regular expressions to clean channel and group names.
/// </summary>
public class NameFilter
{
    /// <summary>
    /// Gets or sets the regular expression pattern to match.
    /// Use capture groups to specify which parts to keep.
    /// </summary>
    public string Pattern { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the replacement string.
    /// Use $1, $2, etc. to reference capture groups.
    /// Use empty string to remove matched text entirely.
    /// </summary>
    public string Replacement { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a description/label for this filter.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether this filter is enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the order/priority of this filter (lower numbers run first).
    /// </summary>
    public int Order { get; set; }
}

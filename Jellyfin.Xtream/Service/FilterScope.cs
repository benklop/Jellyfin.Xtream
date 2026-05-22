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

namespace Jellyfin.Xtream.Service;

/// <summary>
/// Specifies the scope where a name filter should be applied.
/// </summary>
public enum FilterScope
{
    /// <summary>
    /// Apply to Live TV category names.
    /// </summary>
    LiveTvCategory,

    /// <summary>
    /// Apply to Live TV channel/item names.
    /// </summary>
    LiveTvItem,

    /// <summary>
    /// Apply to VOD category names.
    /// </summary>
    VodCategory,

    /// <summary>
    /// Apply to VOD item names.
    /// </summary>
    VodItem,

    /// <summary>
    /// Apply to Series category names.
    /// </summary>
    SeriesCategory,

    /// <summary>
    /// Apply to Series item names.
    /// </summary>
    SeriesItem
}

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
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace Jellyfin.Xtream.Client;

/// <summary>
/// Custom DateTime converter that handles malformed dates from Xtream API.
/// Handles dates like "2024-01-18 (USA)" by extracting the date portion.
/// </summary>
public partial class LenientDateTimeConverter : JsonConverter
{
    [GeneratedRegex(@"(\d{4}-\d{2}-\d{2})")]
    private static partial Regex DatePattern();

    /// <inheritdoc />
    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(DateTime) || objectType == typeof(DateTime?);
    }

    /// <inheritdoc />
    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        if (reader.Value == null)
        {
            return null;
        }

        if (reader.Value is DateTime dt)
        {
            return dt;
        }

        string? dateString = reader.Value as string;
        if (string.IsNullOrWhiteSpace(dateString))
        {
            return null;
        }

        // Try to parse the full string as-is first
        if (DateTime.TryParse(dateString, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime result))
        {
            return result;
        }

        // Extract date pattern (YYYY-MM-DD) and try parsing just that
        Match match = DatePattern().Match(dateString);
        if (match.Success)
        {
            if (DateTime.TryParseExact(match.Groups[1].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime extractedDate))
            {
                return extractedDate;
            }
        }

        // If we can't parse and the property is nullable, return null
        if (objectType == typeof(DateTime?))
        {
            return null;
        }

        // For non-nullable DateTime, throw to maintain compatibility
        throw new JsonSerializationException($"Could not convert string to DateTime: {dateString}");
    }

    /// <inheritdoc />
    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        if (value == null)
        {
            writer.WriteNull();
        }
        else
        {
            writer.WriteValue(((DateTime)value).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }
    }
}

// Copyright (C) 2022  Kevin Jilissen

using System.Collections.Generic;

namespace Jellyfin.Xtream.Service;

/// <summary>
/// Parsed XMLTV programme data keyed by XMLTV channel id.
/// </summary>
public sealed class XmlTvProgrammeIndex
{
    /// <summary>
    /// Gets programmes grouped by XMLTV channel identifier.
    /// </summary>
    public Dictionary<string, List<XmlTvProgramme>> ProgrammesByChannelId { get; init; } = [];

    /// <summary>
    /// Gets a value indicating whether the feed was loaded and parsed successfully.
    /// </summary>
    public bool LoadSucceeded { get; init; }

    /// <summary>
    /// Gets an optional warning from validation (e.g. limited historical depth).
    /// </summary>
    public string? WarningMessage { get; init; }

    /// <summary>
    /// Gets an empty index used after failed loads.
    /// </summary>
    public static XmlTvProgrammeIndex Empty { get; } = new() { LoadSucceeded = false };
}

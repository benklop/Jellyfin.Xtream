// Copyright (C) 2022  Kevin Jilissen

using System;

namespace Jellyfin.Xtream.Service;

/// <summary>
/// A single programme entry from an XMLTV feed.
/// </summary>
public sealed class XmlTvProgramme
{
    /// <summary>
    /// Initializes a new instance of the <see cref="XmlTvProgramme"/> class.
    /// </summary>
    /// <param name="start">Programme start time (UTC).</param>
    /// <param name="end">Programme end time (UTC).</param>
    /// <param name="title">Programme title.</param>
    /// <param name="description">Programme description.</param>
    public XmlTvProgramme(DateTime start, DateTime end, string title, string description)
    {
        Start = start;
        End = end;
        Title = title;
        Description = description;
    }

    /// <summary>
    /// Gets the programme start time (UTC).
    /// </summary>
    public DateTime Start { get; }

    /// <summary>
    /// Gets the programme end time (UTC).
    /// </summary>
    public DateTime End { get; }

    /// <summary>
    /// Gets the programme title.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Gets the programme description.
    /// </summary>
    public string Description { get; }
}

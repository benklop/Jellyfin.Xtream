// Copyright (C) 2022  Kevin Jilissen

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml.Linq;
using Jellyfin.Xtream.Service;

namespace Jellyfin.Xtream.Client;

/// <summary>
/// Parses XMLTV date formats and programme elements.
/// </summary>
public static class XmlTvParser
{
    /// <summary>
    /// Parses an XMLTV date/time string to UTC.
    /// </summary>
    /// <param name="raw">The raw attribute value.</param>
    /// <returns>Parsed UTC time, or <see cref="DateTime.MinValue"/> when invalid.</returns>
    public static DateTime ParseXmlTvDate(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return DateTime.MinValue;
        }

        string s = raw.Replace(" ", string.Empty, StringComparison.Ordinal);
        if (s.Length > 14)
        {
            string zone = s[^5..];
            if ((zone[0] == '+' || zone[0] == '-') && int.TryParse(zone.AsSpan(1), out _))
            {
                string zoneWithColon = zone.Insert(3, ":");
                s = s[..^5] + zoneWithColon;
            }
        }

        if (DateTime.TryParseExact(
                s,
                "yyyyMMddHHmmsszzz",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                out var dt))
        {
            return dt.ToUniversalTime();
        }

        return DateTime.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
    }

    /// <summary>
    /// Parses all programme elements from an XMLTV document.
    /// </summary>
    /// <param name="doc">The XMLTV document.</param>
    /// <returns>Programmes keyed by XMLTV channel id.</returns>
    public static Dictionary<string, List<XmlTvProgramme>> ParseProgrammes(XDocument doc)
    {
        var mapping = new Dictionary<string, List<XmlTvProgramme>>(StringComparer.Ordinal);

        foreach (var prog in doc.Descendants("programme"))
        {
            string? ch = prog.Attribute("channel")?.Value;
            if (string.IsNullOrEmpty(ch))
            {
                continue;
            }

            string? startRaw = prog.Attribute("start")?.Value;
            string? stopRaw = prog.Attribute("stop")?.Value;
            if (string.IsNullOrEmpty(startRaw) || string.IsNullOrEmpty(stopRaw))
            {
                continue;
            }

            try
            {
                DateTime start = ParseXmlTvDate(startRaw);
                DateTime stop = ParseXmlTvDate(stopRaw);
                if (start == DateTime.MinValue || stop == DateTime.MinValue || start >= stop)
                {
                    continue;
                }

                string title = prog.Element("title")?.Value ?? string.Empty;
                string desc = prog.Element("desc")?.Value ?? string.Empty;

                if (!mapping.TryGetValue(ch, out var list))
                {
                    list = new List<XmlTvProgramme>();
                    mapping[ch] = list;
                }

                list.Add(new XmlTvProgramme(start, stop, title, desc));
            }
            catch (FormatException)
            {
                // Skip programmes with unparseable dates
            }
        }

        return mapping;
    }
}

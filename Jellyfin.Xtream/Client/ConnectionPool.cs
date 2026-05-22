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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Jellyfin.Xtream.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Xtream.Client;

/// <summary>
/// Manages a pool of Xtream API connections and provides load balancing across multiple credentials.
/// </summary>
public class ConnectionPool
{
    private readonly ILogger<ConnectionPool> _logger;
    private readonly ConcurrentDictionary<string, ConnectionState> _connectionStates = new();
    private int _nextCredentialIndex;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectionPool"/> class.
    /// </summary>
    /// <param name="logger">Instance of the <see cref="ILogger{ConnectionPool}"/> interface.</param>
    public ConnectionPool(ILogger<ConnectionPool> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Gets a connection from the pool using round-robin load balancing.
    /// Falls back to legacy single credential if no additional credentials are configured.
    /// </summary>
    /// <param name="baseUrl">The base URL for the Xtream API.</param>
    /// <param name="legacyUsername">The legacy single username (for backward compatibility).</param>
    /// <param name="legacyPassword">The legacy single password (for backward compatibility).</param>
    /// <param name="credentials">The list of additional credentials.</param>
    /// <returns>A <see cref="ConnectionInfo"/> instance.</returns>
    public ConnectionInfo GetConnection(
        string baseUrl,
        string legacyUsername,
        string legacyPassword,
        IReadOnlyList<CredentialInfo> credentials)
    {
        // Build list of all available credentials
        List<CredentialInfo> allCredentials = [];

        // Add legacy credentials if configured
        if (!string.IsNullOrWhiteSpace(legacyUsername) && !string.IsNullOrWhiteSpace(legacyPassword))
        {
            allCredentials.Add(new CredentialInfo
            {
                Username = legacyUsername,
                Password = legacyPassword,
                IsEnabled = true,
                Label = "Primary"
            });
        }

        // Add additional credentials that are enabled
        if (credentials != null)
        {
            allCredentials.AddRange(credentials.Where(c => c.IsEnabled));
        }

        if (allCredentials.Count == 0)
        {
            _logger.LogWarning("No credentials configured, returning empty connection");
            return new ConnectionInfo(baseUrl, string.Empty, string.Empty);
        }

        // If only one credential, return it directly
        if (allCredentials.Count == 1)
        {
            CredentialInfo cred = allCredentials[0];
            _logger.LogDebug("Using single credential: {Label}", string.IsNullOrWhiteSpace(cred.Label) ? cred.Username : cred.Label);
            return new ConnectionInfo(baseUrl, cred.Username, cred.Password);
        }

        // Round-robin selection across multiple credentials
        int index = Interlocked.Increment(ref _nextCredentialIndex) % allCredentials.Count;
        CredentialInfo selectedCred = allCredentials[index];

        string key = $"{selectedCred.Username}:{selectedCred.Password}";
        ConnectionState state = _connectionStates.GetOrAdd(key, _ => new ConnectionState());
        state.LastUsed = DateTime.UtcNow;
        state.UseCount++;

        _logger.LogDebug(
            "Selected credential {Index}/{Total}: {Label} (used {Count} times)",
            index + 1,
            allCredentials.Count,
            string.IsNullOrWhiteSpace(selectedCred.Label) ? selectedCred.Username : selectedCred.Label,
            state.UseCount);

        return new ConnectionInfo(baseUrl, selectedCred.Username, selectedCred.Password);
    }

    /// <summary>
    /// Gets statistics about connection usage.
    /// </summary>
    /// <returns>A dictionary of connection keys to their usage statistics.</returns>
    public Dictionary<string, (DateTime LastUsed, int UseCount)> GetStatistics()
    {
        return _connectionStates.ToDictionary(
            kvp => kvp.Key,
            kvp => (kvp.Value.LastUsed, kvp.Value.UseCount));
    }

    /// <summary>
    /// Resets the connection pool statistics.
    /// </summary>
    public void ResetStatistics()
    {
        _connectionStates.Clear();
        _nextCredentialIndex = 0;
        _logger.LogInformation("Connection pool statistics reset");
    }

    private sealed class ConnectionState
    {
        public DateTime LastUsed { get; set; } = DateTime.UtcNow;

        public int UseCount { get; set; }
    }
}

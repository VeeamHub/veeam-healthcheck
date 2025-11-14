using System;
using System.Collections.Generic;

namespace VeeamHealthCheck.Startup;

/// <summary>
/// In-memory credential store. Credentials are stored only for the lifetime of the application.
/// No credentials are persisted to disk.
/// </summary>
public static class CredentialStore
{
    private static readonly Dictionary<string, (string Username, string Password)> _cache = new();

    /// <summary>
    /// Retrieves cached credentials for the specified server.
    /// </summary>
    /// <param name="server">The server hostname or identifier</param>
    /// <returns>Username and password tuple if found, null otherwise</returns>
    public static (string Username, string Password)? Get(string server)
    {
        if (_cache.TryGetValue(server, out var val))
        {
            return val;
        }
        return null;
    }

    /// <summary>
    /// Stores credentials in memory for the specified server.
    /// </summary>
    /// <param name="server">The server hostname or identifier</param>
    /// <param name="username">The username</param>
    /// <param name="password">The password</param>
    public static void Set(string server, string username, string password)
    {
        _cache[server] = (username, password);
    }

    /// <summary>
    /// Clears all cached credentials from memory.
    /// </summary>
    public static void Clear()
    {
        _cache.Clear();
    }
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using VeeamHealthCheck.Shared;

namespace VeeamHealthCheck.Startup;

public class CredentialRecord
{
    public string Username { get; set; }
    public string PasswordEnc { get; set; } // Base64 string
}

public static class CredentialStore
{
    private static readonly string StorePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "VeeamHealthCheck", "creds.json");

    private static Dictionary<string, (string Username, byte[] PasswordEnc)> _cache;

    static CredentialStore()
    {
        InitializeCache();
    }

    private static void InitializeCache()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StorePath));
            // log the path for debugging purposes
            CGlobals.Logger.Debug($"Credential store path: {StorePath}");

            if (File.Exists(StorePath))
            {
                var json = File.ReadAllText(StorePath);

                // Handle empty or whitespace-only files
                if (string.IsNullOrWhiteSpace(json))
                {
                    CGlobals.Logger.Debug("Credential store file is empty, initializing fresh cache");
                    _cache = new Dictionary<string, (string, byte[])>();
                    return;
                }

                var dict = JsonSerializer.Deserialize<Dictionary<string, CredentialRecord>>(json);

                if (dict == null)
                {
                    CGlobals.Logger.Debug("Credential store deserialized to null, initializing fresh cache");
                    _cache = new Dictionary<string, (string, byte[])>();
                    return;
                }

                _cache = new Dictionary<string, (string Username, byte[] PasswordEnc)>();

                foreach (var kvp in dict)
                {
                    try
                    {
                        // Skip entries with null/empty password
                        if (string.IsNullOrEmpty(kvp.Value?.PasswordEnc) || string.IsNullOrEmpty(kvp.Value?.Username))
                            continue;

                        var passwordBytes = Convert.FromBase64String(kvp.Value.PasswordEnc);
                        if (passwordBytes.Length > 0)
                        {
                            _cache[kvp.Key] = (kvp.Value.Username, passwordBytes);
                        }
                    }
                    catch (FormatException)
                    {
                        // Invalid Base64, skip this entry
                        CGlobals.Logger.Debug($"Skipping credential entry with invalid Base64 encoding for key: {kvp.Key}");
                    }
                }
            }
            else
            {
                _cache = new Dictionary<string, (string, byte[])>();
            }
        }
        catch (JsonException ex)
        {
            CGlobals.Logger.Warning($"Credential store file is malformed, initializing fresh cache. Error: {ex.Message}");
            _cache = new Dictionary<string, (string, byte[])>();
        }
        catch (Exception ex)
        {
            CGlobals.Logger.Error($"Failed to initialize credential store: {ex.Message}");
            _cache = new Dictionary<string, (string, byte[])>();
        }
    }

    /// <summary>
    /// Clears all stored credentials from memory and disk.
    /// </summary>
    public static void Clear()
    {
        try
        {
            _cache = new Dictionary<string, (string, byte[])>();

            if (File.Exists(StorePath))
            {
                File.Delete(StorePath);
                CGlobals.Logger.Info("Stored credentials cleared successfully");
            }
            else
            {
                CGlobals.Logger.Debug("No credential store file to delete");
            }
        }
        catch (Exception ex)
        {
            CGlobals.Logger.Error($"Failed to clear credential store: {ex.Message}");
        }
    }

    /// <summary>
    /// Checks if there are any stored credentials.
    /// </summary>
    public static bool HasStoredCredentials()
    {
        return _cache != null && _cache.Count > 0;
    }

    public static (string Username, string Password)? Get(string server)
    {
        if (_cache.TryGetValue(server, out var val))
        {
            if (val.PasswordEnc == null || val.PasswordEnc.Length == 0)
                return null; // Prevent null/empty password decryption

            var password = Encoding.UTF8.GetString(
                ProtectedData.Unprotect(val.PasswordEnc, null, DataProtectionScope.CurrentUser));
            return (val.Username, password);
        }
        return null;
    }

    public static void Set(string server, string username, string password)
    {
        var enc = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(password), null, DataProtectionScope.CurrentUser);
        _cache[server] = (username, enc);

        // Convert to serializable dictionary
        var serializable = _cache.ToDictionary(
            kvp => kvp.Key,
            kvp => new CredentialRecord
            {
                Username = kvp.Value.Username,
                PasswordEnc = Convert.ToBase64String(kvp.Value.PasswordEnc)
            });

        File.WriteAllText(StorePath, JsonSerializer.Serialize(serializable, new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>
    /// Gets all server names that have stored credentials.
    /// </summary>
    public static List<string> GetAllServers()
    {
        return _cache?.Keys.ToList() ?? new List<string>();
    }

    /// <summary>
    /// Removes credentials for a specific server from memory and disk.
    /// </summary>
    /// <param name="server">The server name to remove credentials for</param>
    /// <returns>True if credentials were removed, false if no credentials existed for the server</returns>
    public static bool Remove(string server)
    {
        try
        {
            if (_cache.Remove(server))
            {
                // Update the file with remaining credentials
                var serializable = _cache.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new CredentialRecord
                    {
                        Username = kvp.Value.Username,
                        PasswordEnc = Convert.ToBase64String(kvp.Value.PasswordEnc)
                    });

                if (_cache.Count == 0)
                {
                    // If no credentials left, delete the file
                    if (File.Exists(StorePath))
                    {
                        File.Delete(StorePath);
                    }
                }
                else
                {
                    // Write remaining credentials back to file
                    File.WriteAllText(StorePath, JsonSerializer.Serialize(serializable, new JsonSerializerOptions { WriteIndented = true }));
                }

                CGlobals.Logger.Info($"Removed credentials for server: {server}");
                return true;
            }
            else
            {
                CGlobals.Logger.Debug($"No credentials found for server: {server}");
                return false;
            }
        }
        catch (Exception ex)
        {
            CGlobals.Logger.Error($"Failed to remove credentials for {server}: {ex.Message}");
            return false;
        }
    }
}
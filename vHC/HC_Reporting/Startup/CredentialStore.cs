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
        Directory.CreateDirectory(Path.GetDirectoryName(StorePath));
        // log the path for debugging purposes
        CGlobals.Logger.Debug($"Credential store path: {StorePath}");
        if (File.Exists(StorePath))
        {
            var json = File.ReadAllText(StorePath);
            var dict = JsonSerializer.Deserialize<Dictionary<string, CredentialRecord>>(json)
                       ?? new Dictionary<string, CredentialRecord>();
            _cache = dict.ToDictionary(
                kvp => kvp.Key,
                kvp => (kvp.Value.Username, Convert.FromBase64String(kvp.Value.PasswordEnc ?? "")));
            // Remove any invalid entries
            foreach (var key in _cache.Keys.ToList())
            {
                var entry = _cache[key];
                if (entry.Username == null || entry.PasswordEnc == null || entry.PasswordEnc.Length == 0)
                    _cache.Remove(key);
            }
        }
        else
        {
            _cache = new Dictionary<string, (string, byte[])>();
        }
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
}
// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.IO;
using VeeamHealthCheck.Startup;

namespace VhcXTests
{
    /// <summary>
    /// Points <see cref="CredentialStore"/> at a creds.json inside a throwaway temp
    /// directory for the lifetime of the instance, so a test that calls Set/Remove
    /// never reads or writes a developer's real %APPDATA%\VeeamHealthCheck\creds.json.
    /// Dispose restores the original path and cache and deletes the temp directory.
    /// Mutates statics, so only use it from classes in the "GlobalState" collection.
    /// </summary>
    internal sealed class IsolatedCredentialStore : IDisposable
    {
        private readonly string _originalStorePath;
        private readonly string _directory;

        public IsolatedCredentialStore()
        {
            _originalStorePath = CredentialStore.StorePath;
            _directory = Path.Combine(Path.GetTempPath(), $"vhc-creds-test-{Guid.NewGuid()}");
            Directory.CreateDirectory(_directory);

            CredentialStore.StorePath = Path.Combine(_directory, "creds.json");
            CredentialStore.InitializeCache();
        }

        public string StorePath => CredentialStore.StorePath;

        public void Dispose()
        {
            CredentialStore.StorePath = _originalStorePath;
            CredentialStore.InitializeCache();

            try
            {
                if (Directory.Exists(_directory))
                {
                    Directory.Delete(_directory, recursive: true);
                }
            }
            catch
            {
                // Best effort cleanup
            }
        }
    }
}

// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System.IO;
using Microsoft.Win32;
using VeeamHealthCheck.Functions.Collection.DB;
using Xunit;

namespace VhcXTests
{
    /// <summary>
    /// Regression tests for GetVbrVersionFilePath's Mount Service fallback branch, which
    /// previously risked an uncaught NullReferenceException (missing registry key) or
    /// FileNotFoundException (stale InstallationPath value pointing at a missing DLL) instead of
    /// degrading to a clean null return.
    ///
    /// Naming convention: [Method]_[Scenario]_[Expected].
    /// </summary>
    [Collection("GlobalState")]
    public class CRegReaderTests
    {
        private const string DefaultConsolePath =
            @"C:\Program Files\Veeam\Backup and Replication\Console\Veeam.Backup.Core.dll";

        [Fact]
        public void GetVbrVersionFilePath_NoLocalConsoleAndNoMountServiceKey_ReturnsNullInsteadOfThrowing()
        {
            // This regression guard only applies on a machine without VBR/Mount Service
            // installed (true for standard CI runners). If either is present, there's nothing to
            // regression-test here - skip rather than assert against environment-dependent state.
            bool mountServiceKeyExists;
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey("Software\\Veeam\\Veeam Mount Service"))
            {
                mountServiceKeyExists = key != null;
            }

            if (File.Exists(DefaultConsolePath) || mountServiceKeyExists)
            {
                return;
            }

            var reg = new CRegReader();

            string result = reg.GetVbrVersionFilePath();

            Assert.Null(result);
        }
    }
}

// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.IO;
using VeeamHealthCheck.Functions.Collection.PSCollections;
using Xunit;

namespace VhcXTests
{
    /// <summary>
    /// Regression tests for the "collection completed but the PowerShell process still
    /// exited non-zero" tolerance (customer scenario: an orphaned standalone agent backup
    /// dirties the VBR session, so the unguarded Disconnect-VBRServer in the script's
    /// finally block throws and the process returns exit code 2 - even though every
    /// collector ran and CollectionManifest.csv was written). VbrCollectionCompleted is
    /// the pure decision helper PSInvoker.ExecutePsScript consults before treating a
    /// non-zero exit code as fatal.
    /// </summary>
    public class PSInvokerCompletionTests
    {
        [Fact]
        public void VbrCollectionCompleted_ManifestPresent_ReturnsTrue()
        {
            string manifestPath = Path.Combine(Path.GetTempPath(), $"vhc-test-manifest-{Guid.NewGuid()}.csv");
            File.WriteAllText(manifestPath, "\"Name\",\"Success\"\r\n");
            try
            {
                Assert.True(PSInvoker.VbrCollectionCompleted(stdOut: null, manifestPath));
            }
            finally
            {
                File.Delete(manifestPath);
            }
        }

        [Fact]
        public void VbrCollectionCompleted_MarkerInStdoutNoManifest_ReturnsTrue()
        {
            string manifestPath = Path.Combine(Path.GetTempPath(), $"vhc-test-manifest-{Guid.NewGuid()}.csv");
            string stdOut = "[Get-VBRConfig] Collection complete. Output: C:\\temp\\vHC\\Original\\VBR\\localhost\\20260609_150942";

            Assert.False(File.Exists(manifestPath));
            Assert.True(PSInvoker.VbrCollectionCompleted(stdOut, manifestPath));
        }

        [Fact]
        public void VbrCollectionCompleted_NoManifestNoMarker_ReturnsFalse()
        {
            string manifestPath = Path.Combine(Path.GetTempPath(), $"vhc-test-manifest-{Guid.NewGuid()}.csv");
            string stdOut = "[Get-VBRConfig] Get-VhcServer returned null - aborting. Check VBR connectivity and logs.";

            Assert.False(File.Exists(manifestPath));
            Assert.False(PSInvoker.VbrCollectionCompleted(stdOut, manifestPath));
        }

        [Fact]
        public void VbrCollectionCompleted_EmptyInputs_ReturnsFalse()
        {
            Assert.False(PSInvoker.VbrCollectionCompleted(stdOut: null, manifestPath: null));
            Assert.False(PSInvoker.VbrCollectionCompleted(stdOut: string.Empty, manifestPath: string.Empty));
        }
    }
}

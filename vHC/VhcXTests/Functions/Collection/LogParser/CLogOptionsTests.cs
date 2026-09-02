// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
//
// Regression test for PR #210 review finding #1 (top severity): installIdsByMode is a static
// dictionary that outlives a single run (e.g. a GUI retry constructs a fresh CLogOptions in
// the same process). Skipping the write when a lookup found nothing let a failed retry
// silently keep a stale ID from a previous run instead of correctly reporting "no ID this
// run". Fixed by always assigning - see CLogOptions.cs, commit 6a00b35.
//
// This relies on CVmcReader(VmcLogMode.Vbr) deterministically finding nothing on the CI
// runner: with no VBR installed, CRegReader.DefaultLogDir() returns null (registry key
// absent), which CVmcReader.GetLogDir() turns into a bogus rootless LOGLOCATION; ReadVmc()
// then throws (file not found), caught by CVmcReader.PopulateVmc()'s own try/catch, leaving
// INSTALLID null. If this ever runs somewhere VBR IS installed, the assertion below is what
// should fail - that's a real signal, not a reason to change this test.
//
// installIdsByMode is shared static state, so this test needs to run in the "GlobalState"
// collection (DisableParallelization = true) alongside everything else that touches shared
// static state (e.g. CHtmlExporterTEST) - a separate single-test collection would still run
// concurrently WITH "GlobalState" under xUnit's default cross-collection parallelism, which is
// exactly the race this test would otherwise be exposed to.
//
// These tests require Windows (WPF dependency in VeeamHealthCheck.csproj) and can only be
// compiled/run in CI (windows-latest) or a local Windows box - not on macOS.

using System.Collections.Generic;
using System.Reflection;
using VeeamHealthCheck.Functions.Collection.LogParser;
using Xunit;

namespace VeeamHealthCheck.Tests.Functions.Collection.LogParser
{
    [Collection("GlobalState")]
    public class CLogOptionsTests
    {
        private static Dictionary<VmcLogMode, string> GetInstallIdsByMode()
        {
            FieldInfo field = typeof(CLogOptions).GetField(
                "installIdsByMode",
                BindingFlags.NonPublic | BindingFlags.Static)!;
            return (Dictionary<VmcLogMode, string>)field.GetValue(null)!;
        }

        [Fact]
        public void Constructor_LookupFindsNothingOnRetry_OverwritesStaleIdInsteadOfKeepingIt()
        {
            // Arrange - seed the static dictionary as if a previous run in this process had
            // already found a real install ID for Vbr.
            Dictionary<VmcLogMode, string> dict = GetInstallIdsByMode();
            dict[VmcLogMode.Vbr] = "STALE123";

            // Act - construct a fresh CLogOptions the way a GUI retry would.
            _ = new CLogOptions(VmcLogMode.Vbr);

            // Assert - "no ID this run" must overwrite the stale value, not preserve it.
            Assert.Equal(string.Empty, CLogOptions.GetInstallId(VmcLogMode.Vbr));
        }
    }
}

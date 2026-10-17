// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
using VeeamHealthCheck.Functions.ManageServers;
using Xunit;

namespace VhcXTests
{
    // No [Collection("GlobalState")] needed: ServerListCommitter touches neither
    // CGlobals nor the filesystem - it only calls the two Func delegates it is
    // constructed with. That isolation is the whole point of the class.
    public class ServerListCommitterTests
    {
        private static CommitPlan Plan(List<string> finalServers, List<string> credentialsToDelete)
        {
            return new CommitPlan
            {
                FinalServers = finalServers,
                CredentialsToDelete = credentialsToDelete,
            };
        }

        [Fact]
        public void Execute_AllCredentialRemovalsSucceed_PersistsFinalServersUnchanged()
        {
            var plan = Plan(
                finalServers: new List<string> { "vbr01" },
                credentialsToDelete: new List<string> { "vbr02" });
            var committer = new ServerListCommitter(
                removeCredential: _ => true,
                setServers: _ => true);

            var outcome = committer.Execute(plan);

            Assert.Equal(plan.FinalServers, outcome.ServersPersisted);
            Assert.Empty(outcome.FailedCredentialRemovals);
        }

        [Fact]
        public void Execute_ACredentialRemovalFails_ReinstatesThatHostInServersPersisted()
        {
            // The property-under-test that justifies this class existing. Without the
            // reinstatement in ServerListCommitter.Execute, "vbr02" - removed from the
            // list AND whose credential deletion just failed - would be dropped from
            // ServersPersisted while its credential survives on disk: unreachable from
            // the GUI, the exact failure mode deleting credentials first exists to
            // prevent. Mutation-tested: deleting the `.Concat(...)` reinstatement in
            // ServerListCommitter.Execute makes exactly this test fail.
            //
            // Also the only fixture where a credential removal fails (failed.Count > 0)
            // while the settings save still succeeds - asserting SettingsSaved here is
            // what pins Execute reading _setServers's actual return value rather than a
            // stand-in like "failed.Count == 0" that happens to agree with it on every
            // OTHER fixture in this file (every other test's failed.Count and SettingsSaved
            // move together, so neither alone was previously proven independent).
            var plan = Plan(
                finalServers: new List<string> { "vbr01" },
                credentialsToDelete: new List<string> { "vbr02" });
            var committer = new ServerListCommitter(
                removeCredential: host => host != "vbr02",
                setServers: _ => true);

            var outcome = committer.Execute(plan);

            Assert.True(outcome.SettingsSaved);
            Assert.Contains("vbr01", outcome.ServersPersisted);
            Assert.Contains("vbr02", outcome.ServersPersisted);
            Assert.Equal(new List<string> { "vbr02" }, outcome.FailedCredentialRemovals);
        }

        [Fact]
        public void Execute_SettingsSaveFails_ReturnsSettingsSavedFalseButStillReportsFailedRemovals()
        {
            // The caller needs both pieces of information, not just one: a failed
            // settings save must not hide a failed credential removal that happened
            // in the same Execute call.
            var plan = Plan(
                finalServers: new List<string> { "vbr01" },
                credentialsToDelete: new List<string> { "vbr02" });
            var committer = new ServerListCommitter(
                removeCredential: _ => false,
                setServers: _ => false);

            var outcome = committer.Execute(plan);

            Assert.False(outcome.SettingsSaved);
            Assert.Equal(new List<string> { "vbr02" }, outcome.FailedCredentialRemovals);
        }

        [Fact]
        public void Execute_NoCredentialsToDelete_SkipsRemovalLoopAndPersistsFinalServersVerbatim()
        {
            var plan = Plan(
                finalServers: new List<string> { "vbr01" },
                credentialsToDelete: new List<string>());
            var committer = new ServerListCommitter(
                // Throwing if invoked pins that the removal loop is skipped entirely
                // when there is nothing to remove, rather than merely a no-op count.
                removeCredential: _ => throw new InvalidOperationException(
                    "removeCredential must not be called when CredentialsToDelete is empty"),
                setServers: _ => true);

            var outcome = committer.Execute(plan);

            Assert.Equal(plan.FinalServers, outcome.ServersPersisted);
        }
    }
}

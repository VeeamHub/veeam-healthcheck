// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
using System.Linq;

namespace VeeamHealthCheck.Functions.ManageServers
{
    internal sealed class CommitOutcome
    {
        public bool SettingsSaved { get; internal set; }
        public List<string> FailedCredentialRemovals { get; internal set; } = new();
        public List<string> ServersPersisted { get; internal set; } = new();
    }

    // Executes a CommitPlan against the two primitives Done depends on, both of which
    // swallow their own exceptions (CredentialStore.Remove catches and returns false;
    // CAppSettings.SetServers catches, logs, and returns false). Avalonia- and
    // CGlobals-free, same as ServerListEditor and for the same reason: this is the
    // part of Done with real failure-handling edge cases, and a dialog code-behind is
    // not where those should be proven correct.
    internal sealed class ServerListCommitter
    {
        private readonly Func<string, bool> _removeCredential;
        private readonly Func<IEnumerable<string>, bool> _setServers;

        public ServerListCommitter(
            Func<string, bool> removeCredential,
            Func<IEnumerable<string>, bool> setServers)
        {
            _removeCredential = removeCredential ?? throw new ArgumentNullException(nameof(removeCredential));
            _setServers = setServers ?? throw new ArgumentNullException(nameof(setServers));
        }

        public CommitOutcome Execute(CommitPlan plan)
        {
            var failed = new List<string>();

            foreach (var host in plan.CredentialsToDelete)
            {
                if (!_removeCredential(host))
                {
                    failed.Add(host);
                }
            }

            // FinalServers already excludes every removed row unconditionally - Commit()
            // computed it before any deletion was attempted, so it cannot know the
            // outcome. A host whose deletion just failed is reinstated here rather than
            // left orphaned: without this, a failed deletion still drops the host from
            // the persisted list while its credential survives - unreachable from the
            // GUI, the exact failure mode deleting credentials first exists to prevent.
            // Appended after FinalServers rather than restored to its original position:
            // position in the persisted list has no behavioural meaning elsewhere in the
            // codebase, so this is a deliberate choice, not an oversight.
            //
            // The Contains guard is unreachable via ServerListEditor.Commit() - it
            // partitions FinalServers and CredentialsToDelete on the same IsPendingRemoval
            // flag, so a host can never land in both. Kept anyway: CommitPlan's setters are
            // `internal set`, not enforced-immutable, so nothing stops an in-assembly
            // caller (this file's own tests included) from constructing an incoherent
            // plan where it would matter.
            var toPersist = plan.FinalServers
                .Concat(failed.Where(h => !plan.FinalServers.Contains(h, StringComparer.OrdinalIgnoreCase)))
                .ToList();

            bool saved = _setServers(toPersist);

            return new CommitOutcome
            {
                SettingsSaved = saved,
                FailedCredentialRemovals = failed,
                ServersPersisted = toPersist,
            };
        }
    }
}

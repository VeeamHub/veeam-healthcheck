// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
using System.Linq;

namespace VeeamHealthCheck.Functions.ManageServers
{
    internal enum AddResult
    {
        Added,
        Duplicate,
        Invalid,
        UndidPendingRemoval,
    }

    internal sealed class ServerRow
    {
        public string Name { get; internal set; }
        public bool HasCredentials { get; internal set; }
        public bool IsPendingRemoval { get; internal set; }
        public bool IsRemovable { get; internal set; }
        internal bool IsNewlyAdded { get; set; }
    }

    internal sealed class CommitPlan
    {
        public List<string> FinalServers { get; internal set; } = new();
        public List<string> CredentialsToDelete { get; internal set; } = new();
    }

    // Staging model for the Manage Servers dialog. Deliberately free of Avalonia and
    // CGlobals: this is the only part of Stage C with real logic and real edge cases,
    // and keeping it here is the only way any of it is testable off Windows.
    //
    // `pinned` carries localhost in from the caller rather than being hardcoded, so the
    // pinning rule is directly testable and the class never mentions localhost. Pinned
    // names are expected to ALSO appear in `initial` - that is a caller contract for
    // DISPLAY purposes only (the user needs to see the row), not a correctness
    // dependency: Add()'s own pinned check makes Add("localhost") a duplicate even if a
    // caller violates this, so persisted state never depends on the contract holding.
    internal sealed class ServerListEditor
    {
        private readonly List<ServerRow> _rows = new();
        private readonly HashSet<string> _pinned;
        private readonly Func<string, bool> _hasCredentials;

        public ServerListEditor(
            IEnumerable<string> initial,
            IEnumerable<string> pinned,
            Func<string, bool> hasCredentials)
        {
            _hasCredentials = hasCredentials;

            // Trim pinned names before building the set, and compare TRIMMED row
            // names against it below. Order matters: trimming `initial` but not
            // `pinned` would let a padded pinned entry ("  localhost  ") fail to
            // match a trimmed row name, rendering that row removable - the exact
            // bug this self-normalisation exists to prevent. This mirrors
            // CAppSettings.NormalizeServers on the settings side, so the class no
            // longer depends on caller hygiene for either list.
            _pinned = new HashSet<string>(
                (pinned ?? Enumerable.Empty<string>())
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Select(n => n.Trim()),
                StringComparer.OrdinalIgnoreCase);

            foreach (var name in (initial ?? Enumerable.Empty<string>())
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n.Trim()))
            {
                if (_rows.Any(r => r.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                _rows.Add(new ServerRow
                {
                    Name = name,
                    // Safe to call more than once during the dialog's lifetime - it is
                    // modal, so nothing can change stored credentials underneath it
                    // while open. Add() also calls this (via SafeHasCredentials) for
                    // names typed in after construction, so this is not a one-shot
                    // snapshot.
                    HasCredentials = SafeHasCredentials(name),
                    IsPendingRemoval = false,
                    IsRemovable = !_pinned.Contains(name),
                    IsNewlyAdded = false,
                });
            }
        }

        public IReadOnlyList<ServerRow> Rows => _rows;

        public int PendingChangeCount =>
            _rows.Count(r => r.IsPendingRemoval || r.IsNewlyAdded);

        public AddResult Add(string name)
        {
            var trimmed = name?.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                return AddResult.Invalid;
            }

            // A pinned name is always conceptually present - it is injected by the
            // caller rather than stored - so adding one is a duplicate even if it is
            // somehow absent from `initial`. Without this, a caller that passed
            // `pinned` without also seeding `initial` would get Added, render a
            // removable row, and have Commit silently discard it: an explicit user
            // action dropped with no feedback. Defence in depth: Task 12 as planned
            // does seed `initial` with every pinned name, so this should never be the
            // path taken in production - but persisted state must never depend on
            // that invariant holding.
            if (_pinned.Contains(trimmed))
            {
                return AddResult.Duplicate;
            }

            var existing = _rows.FirstOrDefault(
                r => r.Name.Equals(trimmed, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                // Re-adding a name that is staged for removal undoes the removal
                // rather than creating a duplicate.
                if (existing.IsPendingRemoval)
                {
                    existing.IsPendingRemoval = false;
                    return AddResult.UndidPendingRemoval;
                }

                return AddResult.Duplicate;
            }

            _rows.Add(new ServerRow
            {
                Name = trimmed,
                // Consult the predicate rather than hardcoding false: a name typed in
                // here may already have a stored credential (e.g. it was previously
                // removed from the persisted list without its credential being
                // cleaned up, or captured via a path that never echoed it back into
                // `initial`). Not load-bearing for Commit today - a newly added row
                // is dropped outright by Remove() rather than staged, so it can never
                // satisfy CredentialsToDelete's (IsPendingRemoval && HasCredentials)
                // filter in this session. Consulting the predicate keeps the row's
                // "credentials saved" marker truthful and makes HasCredentials mean
                // the same thing on every row, so no future consumer (a renderer, or
                // a later change that makes newly-added rows stageable) has to know
                // which branch built it.
                HasCredentials = SafeHasCredentials(trimmed),
                IsPendingRemoval = false,
                IsRemovable = true,
                IsNewlyAdded = true,
            });

            return AddResult.Added;
        }

        // Guards every call to the injected hasCredentials predicate. The real
        // predicate - name => CredentialStore.Get(name) != null - calls into DPAPI
        // (ProtectedData.Unprotect, uncaught in CredentialStore.Get), which can throw
        // CryptographicException if the encrypted blob can't be decrypted on this
        // machine/profile (creds.json copied to another machine, or a recreated
        // Windows user profile). Left unguarded, that throw would propagate out of
        // this constructor or Add() - both reachable from a button click in the
        // Manage Servers dialog - and leave that dialog, the one surface that could
        // remove the offending host, permanently unopenable. Defaulting to true keeps
        // the "credentials saved" marker honest for a row that does have SOMETHING on
        // disk, and if the row is later removed, routes the corrupted entry through
        // CredentialStore.Remove, which never decrypts anything and so actually
        // cleans it up.
        private bool SafeHasCredentials(string name)
        {
            if (_hasCredentials == null)
            {
                return false;
            }

            try
            {
                return _hasCredentials(name);
            }
            catch
            {
                return true;
            }
        }

        public void Remove(string name)
        {
            // Belt-and-braces, not load-bearing: every stored row.Name is already
            // trimmed (by the constructor or by Add), and Task 8 passes row.Name back
            // in here, so an untrimmed match never actually happens today. Trimming
            // anyway keeps this method's own robustness independent of that caller
            // behaviour, matching the "no longer depends on caller hygiene" principle
            // the constructor and Add already follow.
            var trimmed = name?.Trim();
            var row = _rows.FirstOrDefault(
                r => r.Name.Equals(trimmed, StringComparison.OrdinalIgnoreCase));

            if (row == null || !row.IsRemovable)
            {
                return;
            }

            // A row added in this same dialog session was never persisted, so there is
            // nothing to stage a removal against - just drop it.
            if (row.IsNewlyAdded)
            {
                _rows.Remove(row);
                return;
            }

            row.IsPendingRemoval = true;
        }

        public void UndoRemove(string name)
        {
            // Same belt-and-braces trim as Remove - not load-bearing today, kept
            // consistent with it.
            var trimmed = name?.Trim();
            var row = _rows.FirstOrDefault(
                r => r.Name.Equals(trimmed, StringComparison.OrdinalIgnoreCase));

            if (row != null)
            {
                row.IsPendingRemoval = false;
            }
        }

        public CommitPlan Commit()
        {
            return new CommitPlan
            {
                // Pinned names are excluded because this value is handed straight to
                // CAppSettings.SetServers, and an injecting machine must never persist
                // localhost.
                FinalServers = _rows
                    .Where(r => !r.IsPendingRemoval && !_pinned.Contains(r.Name))
                    .Select(r => r.Name)
                    .ToList(),

                CredentialsToDelete = _rows
                    .Where(r => r.IsPendingRemoval && r.HasCredentials)
                    .Select(r => r.Name)
                    .ToList(),
            };
        }
    }
}

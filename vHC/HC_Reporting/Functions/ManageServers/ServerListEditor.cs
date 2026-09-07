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
    // names must ALSO appear in `initial` (they are displayed rows) - that is what makes
    // Add("localhost") a plain duplicate on an injecting machine rather than a second row.
    internal sealed class ServerListEditor
    {
        private readonly List<ServerRow> _rows = new();
        private readonly HashSet<string> _pinned;

        public ServerListEditor(
            IEnumerable<string> initial,
            IEnumerable<string> pinned,
            Func<string, bool> hasCredentials)
        {
            _pinned = new HashSet<string>(pinned ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);

            foreach (var name in (initial ?? Enumerable.Empty<string>()).Where(n => !string.IsNullOrWhiteSpace(n)))
            {
                if (_rows.Any(r => r.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                _rows.Add(new ServerRow
                {
                    Name = name,
                    // Snapshotted once at construction. The dialog is modal and is
                    // disabled during a run, so nothing can change credentials
                    // underneath it while it is open.
                    HasCredentials = hasCredentials != null && hasCredentials(name),
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
                HasCredentials = false,
                IsPendingRemoval = false,
                IsRemovable = true,
                IsNewlyAdded = true,
            });

            return AddResult.Added;
        }

        public void Remove(string name)
        {
            var row = _rows.FirstOrDefault(
                r => r.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

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
            var row = _rows.FirstOrDefault(
                r => r.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

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

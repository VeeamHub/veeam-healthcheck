// <copyright file="CSessionGroupKey.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using VeeamHealthCheck.Functions.Reporting.DataTypes;

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.Job_Session_Summary
{
    /// <summary>
    /// Holds a single rollup group produced by <see cref="CSessionGroupKey.Group"/>: a stable
    /// key, the display name to render, and the sessions that were rolled up into it.
    /// </summary>
    internal sealed class CSessionGroup
    {
        public CSessionGroup(string key, string displayName)
        {
            this.Key = key;
            this.DisplayName = displayName;
            this.Sessions = new List<CJobSessionInfo>();
        }

        public string Key { get; }

        public string DisplayName { get; set; }

        public List<CJobSessionInfo> Sessions { get; }
    }

    /// <summary>
    /// Computes stable group identifiers and display names for session rollup.
    /// Children inherit the parent's PolicyTag (GUID) and PolicyName, so grouping
    /// by <see cref="Of"/> automatically merges per-machine child sessions under
    /// their parent. See ADR 0019.
    /// </summary>
    internal static class CSessionGroupKey
    {
        private const char HierarchySeparator = '\\';

        /// <summary>
        /// Returns the rollup group identifier for a session. Preference order:
        ///   1. PolicyTag - child sessions point at their parent's job GUID.
        ///   2. JobId - parents and standalone jobs use their own GUID.
        ///   3. JobName prefix - legacy CSVs without GUID columns.
        /// A PolicyTag equal to the session's own JobId is a self-reference meaning
        /// "no parent GUID link exists," not "child of itself," so it is excluded
        /// from the PolicyTag branch and falls through to the JobId branch instead.
        /// </summary>
        public static string Of(CJobSessionInfo s)
        {
            var ownId = s.JobId.GetValueOrDefault();
            if (s.PolicyTag.HasValue
                && s.PolicyTag.Value != Guid.Empty
                && s.PolicyTag.Value != ownId)
            {
                return "id:" + s.PolicyTag.Value.ToString("D");
            }

            if (ownId != Guid.Empty)
            {
                return "id:" + ownId.ToString("D");
            }

            return "name:" + (s.JobName ?? string.Empty);
        }

        /// <summary>
        /// Returns the display name for a session's group. Children carry the
        /// parent's PolicyName; parents/standalone fall back to their own JobName.
        /// </summary>
        public static string DisplayName(CJobSessionInfo s) =>
            !string.IsNullOrEmpty(s.PolicyName) ? s.PolicyName : (s.JobName ?? string.Empty);

        /// <summary>
        /// Groups sessions for rollup. GUID grouping via <see cref="Of"/> comes first;
        /// sessions whose identity is hierarchical ("&lt;Parent&gt;\&lt;child&gt;") additionally roll
        /// up on the parent prefix. VBR rejects '\' in job names, so a backslash in a
        /// session identity is always a parent/child marker. Needed because VBR reports
        /// some Backup-Copy per-object child sessions with PolicyTag == JobId -- a
        /// self-reference that carries no parent GUID at all. See ADR 0019, issue #219.
        ///
        /// A hierarchical child whose prefix matches an already-indexed parent identity
        /// always merges into that parent's group, even when the parent identity also
        /// carries non-zero data of its own in the same window. An earlier revision
        /// gated the merge on the parent identity being data-free, to guard against two
        /// *unrelated* jobs coincidentally sharing a name prefix. In practice, a single
        /// Backup Copy job routinely has both a GUID-linked child with data and a
        /// self-referencing hierarchical child with data for the same parent in the same
        /// window -- both are simply normal, healthy children of the same job -- and the
        /// guard misfired on exactly that shape, kicking the hierarchical child out into
        /// its own mangled row and reproducing issue #219's symptom for the case this
        /// pass was added to fix. See ADR 0030.
        /// </summary>
        public static List<CSessionGroup> Group(IEnumerable<CJobSessionInfo> sessions)
        {
            var ordered = (sessions ?? Enumerable.Empty<CJobSessionInfo>()).ToList();

            // Pass 1: index non-hierarchical identities (candidate parents) by their
            // rollup key, so Pass 2 can match a hierarchical child's name prefix
            // against them.
            var keyByIdentity = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var s in ordered)
            {
                var identity = DisplayName(s);
                if (string.IsNullOrEmpty(identity) || identity.IndexOf(HierarchySeparator) >= 0)
                {
                    continue;
                }

                if (!keyByIdentity.ContainsKey(identity))
                {
                    keyByIdentity[identity] = Of(s);
                }
            }

            // Pass 2: assign each session to its effective group.
            var groups = new List<CSessionGroup>();
            var byKey = new Dictionary<string, CSessionGroup>(StringComparer.Ordinal);
            foreach (var s in ordered)
            {
                var key = Of(s);
                var name = DisplayName(s);
                var sepIndex = name.IndexOf(HierarchySeparator);
                var parent = sepIndex > 0 ? name.Substring(0, sepIndex) : null;

                if (parent != null)
                {
                    if (keyByIdentity.TryGetValue(parent, out var parentKey))
                    {
                        // Always merge on a prefix match, regardless of whether the parent
                        // identity carries data of its own -- see the Group() doc comment
                        // above and ADR 0030 for why the previous data-based guard here was
                        // removed.
                        key = parentKey;
                        name = parent;
                    }
                    else
                    {
                        // No orchestrator row for this parent in the current window: anchor
                        // the children on the parent's name so they render as one row.
                        key = "name:" + parent;
                        name = parent;
                    }
                }

                if (!byKey.TryGetValue(key, out var group))
                {
                    group = new CSessionGroup(key, name);
                    byKey[key] = group;
                    groups.Add(group);
                }
                else if (string.IsNullOrEmpty(group.DisplayName) && !string.IsNullOrEmpty(name))
                {
                    group.DisplayName = name;
                }

                group.Sessions.Add(s);
            }

            foreach (var g in groups)
            {
                if (string.IsNullOrEmpty(g.DisplayName))
                {
                    g.DisplayName = g.Sessions[0].JobName ?? string.Empty;
                }
            }

            return groups;
        }
    }
}

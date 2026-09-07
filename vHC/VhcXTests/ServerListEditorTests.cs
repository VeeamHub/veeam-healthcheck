// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Linq;
using VeeamHealthCheck.Functions.ManageServers;
using Xunit;

namespace VhcXTests
{
    // No [Collection("GlobalState")] needed: ServerListEditor touches no statics,
    // no CGlobals and no filesystem. That isolation is the whole point of the class.
    public class ServerListEditorTests
    {
        private static ServerListEditor Editor(
            string[] initial = null,
            string[] pinned = null,
            string[] withCreds = null)
        {
            var creds = withCreds ?? Array.Empty<string>();
            return new ServerListEditor(
                initial ?? new[] { "localhost", "vbr01" },
                pinned ?? new[] { "localhost" },
                name => creds.Contains(name, StringComparer.OrdinalIgnoreCase));
        }

        [Fact]
        public void Rows_ReflectInitialListPinningAndCredentials()
        {
            var editor = Editor(withCreds: new[] { "vbr01" });

            Assert.Equal(new[] { "localhost", "vbr01" }, editor.Rows.Select(r => r.Name));
            Assert.False(editor.Rows[0].IsRemovable);
            Assert.True(editor.Rows[1].IsRemovable);
            Assert.False(editor.Rows[0].HasCredentials);
            Assert.True(editor.Rows[1].HasCredentials);
            Assert.Equal(0, editor.PendingChangeCount);
        }

        [Fact]
        public void Add_CoversItsFourOutcomes()
        {
            // One Fact rather than a Theory over AddResult: the enum is internal, and
            // an internal type as an [InlineData] parameter produces CS0051 on the
            // generated public test method. Do not widen AddResult to public.
            var editor = Editor();

            var added = editor.Add("vbr02");
            var duplicate = editor.Add("VBR01");
            var invalid = editor.Add("   ");
            var pinnedDuplicate = editor.Add("LOCALHOST");

            editor.Remove("vbr01");
            var undid = editor.Add("vbr01");

            Assert.Equal(AddResult.Added, added);
            Assert.Equal(AddResult.Duplicate, duplicate);
            Assert.Equal(AddResult.Invalid, invalid);
            // Pinned names are also in `initial`, which is what makes adding one a
            // plain duplicate instead of creating a second localhost row.
            Assert.Equal(AddResult.Duplicate, pinnedDuplicate);
            Assert.Equal(AddResult.UndidPendingRemoval, undid);
            Assert.False(editor.Rows.Single(r => r.Name == "vbr01").IsPendingRemoval);
        }

        [Fact]
        public void Add_TrimsWhitespace()
        {
            var editor = Editor();

            editor.Add("  vbr02  ");

            Assert.Contains("vbr02", editor.Rows.Select(r => r.Name));
        }

        [Fact]
        public void Remove_StagesWithoutDeletingTheRow()
        {
            var editor = Editor();

            editor.Remove("vbr01");

            // The row stays visible so Cancel is legible and the user can see what
            // Done is about to destroy.
            Assert.Contains("vbr01", editor.Rows.Select(r => r.Name));
            Assert.True(editor.Rows.Single(r => r.Name == "vbr01").IsPendingRemoval);
            Assert.Equal(1, editor.PendingChangeCount);
        }

        [Fact]
        public void Remove_OnPinnedRow_IsNoOp()
        {
            var editor = Editor();

            editor.Remove("localhost");

            Assert.False(editor.Rows.Single(r => r.Name == "localhost").IsPendingRemoval);
            Assert.Equal(0, editor.PendingChangeCount);
        }

        [Fact]
        public void UndoRemove_ClearsThePendingFlag()
        {
            var editor = Editor();
            editor.Remove("vbr01");

            editor.UndoRemove("vbr01");

            Assert.False(editor.Rows.Single(r => r.Name == "vbr01").IsPendingRemoval);
            Assert.Equal(0, editor.PendingChangeCount);
        }

        [Fact]
        public void Remove_OnAFreshlyAddedRow_DropsItEntirely()
        {
            var editor = Editor();
            editor.Add("vbr02");

            editor.Remove("vbr02");

            // Never persisted, so there is nothing to stage a removal against.
            Assert.DoesNotContain("vbr02", editor.Rows.Select(r => r.Name));
            Assert.Equal(0, editor.PendingChangeCount);
        }

        [Fact]
        public void PendingChangeCount_CountsAddsAndRemovals()
        {
            var editor = Editor();

            editor.Add("vbr02");
            editor.Remove("vbr01");

            Assert.Equal(2, editor.PendingChangeCount);
        }

        [Fact]
        public void Commit_ExcludesPinnedAndRemovedFromFinalServers()
        {
            var editor = Editor(
                initial: new[] { "localhost", "vbr01", "vbr02" },
                pinned: new[] { "localhost" });
            editor.Remove("vbr01");

            var plan = editor.Commit();

            // Pinned is excluded because FinalServers goes straight to SetServers and
            // an injecting machine must never persist localhost.
            Assert.Equal(new[] { "vbr02" }, plan.FinalServers);
        }

        [Fact]
        public void Commit_ListsOnlyRemovedHostsThatHaveCredentials()
        {
            var editor = Editor(
                initial: new[] { "localhost", "vbr01", "vbr02" },
                pinned: new[] { "localhost" },
                // localhost also has credentials (RunSaveCredsFlow defaults its host to
                // "localhost") but is pinned, so Remove no-ops on it and it is never
                // staged for removal. Without a credentialed row that survives, deleting
                // the IsPendingRemoval clause from Commit's filter would go unnoticed:
                // HasCredentials alone would already narrow the result to {"vbr01"}.
                withCreds: new[] { "vbr01", "localhost" });
            editor.Remove("vbr01");
            editor.Remove("vbr02");

            var plan = editor.Commit();

            Assert.Equal(new[] { "vbr01" }, plan.CredentialsToDelete);
        }

        [Fact]
        public void Commit_WithNoPinned_TreatsLocalhostAsAnOrdinaryEntry()
        {
            // The non-injecting machine: no local Veeam product, so localhost is
            // addable, removable and persistable like any other name.
            var editor = Editor(
                initial: new[] { "localhost", "vbr01" },
                pinned: Array.Empty<string>());

            Assert.True(editor.Rows.Single(r => r.Name == "localhost").IsRemovable);
            Assert.Contains("localhost", editor.Commit().FinalServers);
        }

        [Fact]
        public void Add_PinnedNameAbsentFromInitial_ReturnsDuplicateAndCreatesNoRow()
        {
            // Defence in depth against a caller violating "pinned must also appear in
            // initial". Without this check, Add would return Added, render a removable
            // row for a name that is conceptually already present, and Commit would
            // then silently discard it - an explicit user action dropped with no
            // feedback, the same failure shape as Task 3's seed burning itself.
            var editor = new ServerListEditor(
                new[] { "vbr01" },
                new[] { "localhost" },
                _ => false);

            var result = editor.Add("localhost");

            Assert.Equal(AddResult.Duplicate, result);
            Assert.DoesNotContain("localhost", editor.Rows.Select(r => r.Name));
        }

        [Fact]
        public void Constructor_WithUntrimmedInitialAndPinned_TrimsBothAndKeepsPinnedRowNotRemovable()
        {
            // Order matters here: trimming `initial` but not `pinned` (or vice versa)
            // would leave "  localhost  " and "localhost" as distinct strings under
            // OrdinalIgnoreCase comparison, so the row would come out removable - the
            // exact bug this self-normalisation exists to prevent.
            var editor = new ServerListEditor(
                new[] { "  localhost  ", "vbr01" },
                new[] { "  localhost  " },
                _ => false);

            var row = editor.Rows.Single(r => r.Name == "localhost");
            Assert.False(row.IsRemovable);
        }
    }
}

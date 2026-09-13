// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Linq;
using System.Security.Cryptography;
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
            var localhostRow = editor.Rows.Single(r => r.Name == "localhost");
            var vbr01Row = editor.Rows.Single(r => r.Name == "vbr01");
            Assert.False(localhostRow.IsRemovable);
            Assert.True(vbr01Row.IsRemovable);
            Assert.False(localhostRow.HasCredentials);
            Assert.True(vbr01Row.HasCredentials);
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
            // Duplicate here because Add's own pinned check treats a pinned name as
            // always-present, regardless of whether it also appears in `initial` (it
            // does in this fixture, for display, but that is not what makes this a
            // duplicate).
            Assert.Equal(AddResult.Duplicate, pinnedDuplicate);
            Assert.Equal(AddResult.UndidPendingRemoval, undid);
            Assert.Contains("vbr01", editor.Rows.Select(r => r.Name));
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
        public void Commit_PendingRemovalCount_CountsAllPendingRemovalsRegardlessOfCredentials()
        {
            // One credentialed removal and one non-credentialed removal, so the test
            // actually distinguishes PendingRemovalCount from CredentialsToDelete.Count
            // rather than relying on a fixture where they'd coincidentally match.
            var editor = Editor(
                initial: new[] { "localhost", "vbr01", "vbr02" },
                pinned: new[] { "localhost" },
                withCreds: new[] { "vbr01" });
            editor.Remove("vbr01");
            editor.Remove("vbr02");

            var plan = editor.Commit();

            Assert.Equal(2, plan.PendingRemovalCount);
            Assert.Equal(1, plan.CredentialsToDelete.Count);
        }

        [Fact]
        public void Commit_WithNoPinned_TreatsLocalhostAsAnOrdinaryEntry()
        {
            // The non-injecting machine: no local Veeam product, so localhost is
            // addable, removable and persistable like any other name. This must be
            // proven behaviourally (actually calling Remove), not just by inspecting
            // IsRemovable - a hardcoded "if name == localhost, no-op" inserted into
            // Remove would leave IsRemovable untouched and still pass a flag-only
            // check while reintroducing exactly the hardcoded-localhost bug the class
            // is designed to avoid (spec: a blanket filter would silently discard a
            // legitimate localhost entry on a VB365-only machine).
            var editor = Editor(
                initial: new[] { "localhost", "vbr01" },
                pinned: Array.Empty<string>());

            Assert.True(editor.Rows.Single(r => r.Name == "localhost").IsRemovable);
            Assert.Contains("localhost", editor.Commit().FinalServers);

            editor.Remove("localhost");
            Assert.True(editor.Rows.Single(r => r.Name == "localhost").IsPendingRemoval);
            Assert.DoesNotContain("localhost", editor.Commit().FinalServers);
        }

        [Fact]
        public void Add_WithNoPinned_AddsLocalhostAsAnOrdinaryEntry()
        {
            // Same behavioural proof for Add: a hardcoded "if name == localhost,
            // Duplicate" would pass every other test in this file (all of them pin
            // localhost) while reintroducing the hardcoded-localhost bug the class
            // exists to avoid on a non-injecting machine.
            var editor = Editor(initial: new[] { "vbr01" }, pinned: Array.Empty<string>());

            Assert.Equal(AddResult.Added, editor.Add("localhost"));
            Assert.Contains("localhost", editor.Rows.Select(r => r.Name));
        }

        [Fact]
        public void Constructor_WithDuplicateInitialEntries_CreatesOneRow()
        {
            // Third-line defence: CAppSettings.NormalizeServers already dedupes
            // case-insensitively upstream, so a duplicate should never actually reach
            // this constructor in production. Kept anyway so the class's own dedup
            // loop has direct coverage, and to pin that dedup happens AFTER trimming
            // (a padded near-duplicate collapses too, not just an exact repeat).
            var editor = new ServerListEditor(
                new[] { "localhost", "  LOCALHOST ", "vbr01" }, new[] { "localhost" }, _ => false);

            Assert.Equal(new[] { "localhost", "vbr01" }, editor.Rows.Select(r => r.Name));
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

        [Fact]
        public void Add_NameWithExistingCredentialsNotInInitial_ReportsHasCredentialsTrue()
        {
            // "vbr02" is NOT in the default `initial`, so this is a brand-new row -
            // but it may already have a stored credential from a previous session
            // (e.g. it was removed from the persisted list without its credential
            // being cleaned up, or captured via a path that never echoed it back into
            // `initial`). Add's new-row branch must consult the predicate rather than
            // hardcoding HasCredentials = false, or the row's "credentials saved"
            // marker would be a lie and HasCredentials would mean something different
            // depending on which branch built the row.
            var editor = Editor(withCreds: new[] { "vbr02" });

            var result = editor.Add("vbr02");

            Assert.Equal(AddResult.Added, result);
            Assert.True(editor.Rows.Single(r => r.Name == "vbr02").HasCredentials);
        }

        [Fact]
        public void Constructor_HasCredentialsPredicateThrowsCryptographicException_DefaultsToTrueAndDoesNotThrow()
        {
            // The real predicate (CredentialStore.Get) calls into DPAPI, which throws
            // exactly CryptographicException if the encrypted blob can't be decrypted
            // on this machine/profile. Left unguarded, that throw would propagate out
            // of the constructor - reachable from a button click in the Manage
            // Servers dialog - and leave the one surface that could remove the
            // offending host permanently unopenable.
            var editor = new ServerListEditor(
                new[] { "vbr01" },
                Array.Empty<string>(),
                _ => throw new CryptographicException("simulated DPAPI failure"));

            Assert.True(editor.Rows.Single(r => r.Name == "vbr01").HasCredentials);
        }

        [Fact]
        public void Add_HasCredentialsPredicateThrowsCryptographicException_DefaultsToTrueAndDoesNotThrow()
        {
            // Same guarantee as the constructor case, but for a name typed in after
            // construction via Add.
            var editor = new ServerListEditor(
                new[] { "vbr01" },
                Array.Empty<string>(),
                _ => throw new CryptographicException("simulated DPAPI failure"));

            var result = editor.Add("vbr09");

            Assert.Equal(AddResult.Added, result);
            Assert.True(editor.Rows.Single(r => r.Name == "vbr09").HasCredentials);
        }

        [Fact]
        public void Constructor_HasCredentialsPredicateThrowsUnrelatedException_PropagatesRatherThanDefaulting()
        {
            // SafeHasCredentials catches CryptographicException specifically, not
            // Exception generally: an unrelated predicate bug should fail loudly
            // rather than be silently absorbed into "assume this row has
            // credentials," which is not an inert default further downstream -
            // ServerListCommitter reinstates any host whose credential removal
            // failed, so a wrongly-true HasCredentials on a host with nothing to
            // remove would silently undo an explicit user removal.
            Assert.Throws<InvalidOperationException>(() => new ServerListEditor(
                new[] { "vbr01" },
                Array.Empty<string>(),
                _ => throw new InvalidOperationException("unrelated predicate bug")));
        }

        [Fact]
        public void Add_NullName_ReturnsInvalid()
        {
            // The existing null-conditional `name?.Trim()` already handles this; this
            // pins it explicitly since only whitespace was tested before.
            var editor = Editor();

            var result = editor.Add(null);

            Assert.Equal(AddResult.Invalid, result);
        }
    }
}

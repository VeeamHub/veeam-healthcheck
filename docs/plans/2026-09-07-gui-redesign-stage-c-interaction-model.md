# GUI Redesign Stage C: Interaction Model — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the Terms button with an inline checkbox, move server management into a staged dialog backed by an independently-persisted server list, convert the collection-period selector to segmented pills, restore the folder picker Stage B missed, and fix the remote-only startup path.

**Architecture:** The load-bearing change is storage, not UI. Today the server list is a *view* onto `CredentialStore`'s key set, which makes Add ephemeral, Remove permanent and Clear All cosmetic. Stage C persists the list in `CAppSettings` with a one-time seed and an authoritative `null`-versus-empty rule, puts all staging logic in an Avalonia-free `ServerListEditor` so it is testable off Windows, and keeps `localhost` policy in the GUI layer behind a single predicate.

**Tech Stack:** .NET 8 (`net8.0-windows7.0`), Avalonia (XAML + code-behind, no MVVM in this file), `System.Text.Json`, xUnit.

**Canonical spec:** `docs/superpowers/specs/2026-09-07-gui-redesign-stage-c-interaction-model-design.md`. Read it. Where this plan and the spec disagree, the spec wins and the discrepancy is a bug in this plan — report it rather than guessing.

---

## Standing rules for every task

Read these once; they are not repeated per task.

1. **Revert the version bump after every build or test run**, before committing:
   ```bash
   git checkout -- vHC/HC_Reporting/VeeamHealthCheck.csproj
   ```
   Every build auto-increments it via `increment_version.ps1` and it will otherwise pollute your diff.

2. **Baseline to hold:** `843 passed, 0 failed, 12 skipped`. Each task adds tests; the passed count grows, failed stays 0, skipped stays 12. Any change to *failed* or *skipped* is a stop-and-investigate.

3. **One commit per task.** Never `git commit --amend` — create a new commit instead.

4. **You cannot render the GUI here.** Avalonia crashes at native platform bootstrap in this sandbox before any application code runs. Do not try. Anything visual is verified by a human on Windows, and Task 14 collects that list.

5. **The spike is not on this branch.** It lives on `spike/gui-redesign`. Read spike files with:
   ```bash
   git show spike/gui-redesign:vHC/Spikes/GuiRedesignSpike/Views/AdHocHealthCheckView.axaml.cs
   ```

6. **Two localization files are UTF-16LE with CRLF** (`VbrLocalizationHelper.cs`, `vhcres.txt`). `cat`/`grep` on them reports "stream did not contain valid UTF-8" — that is expected. Never edit them with a tool that rewrites as UTF-8; Task 6 gives an encoding-safe procedure. `vhcres.resx` is plain UTF-8 and edits normally.

7. **Internal enums cannot be `[InlineData]` parameters.** `AddResult` and `SettingsLoadResult` are internal; an internal type as a `[Theory]`/`[InlineData]` parameter produces **CS0051** on the generated public test method. Use a `[Fact]` with several asserts, or a `[Theory]` keyed on strings that maps to the enum inside the body. **Do not widen the enum to `public`** to make a test compile.

8. **Any new test class that touches `CAppSettings` must carry `[Collection("GlobalState")]`.** `StorePath` is a shared mutable static and xUnit parallelizes test classes across threads; the ctor/`Dispose` save-restore pattern only guards sequential leakage. See `vHC/VhcXTests/GlobalStateCollection.cs`.

---

## File structure

**Created:**

| Path | Responsibility |
|---|---|
| `vHC/HC_Reporting/Functions/ManageServers/ServerListEditor.cs` | Avalonia-free, `CGlobals`-free staging model: rows, add/remove/undo, commit plan. All of Stage C's real logic. |
| `vHC/HC_Reporting/Functions/ManageServers/ManageServersDialog.axaml` | Modal dialog markup. |
| `vHC/HC_Reporting/Functions/ManageServers/ManageServersDialog.axaml.cs` | Dialog code-behind: binds `ServerListEditor` to the list, commits on Done. |
| `vHC/VhcXTests/ServerListEditorTests.cs` | Unit tests for the staging model. |
| `docs/plans/2026-09-07-gui-redesign-stage-c-verification.md` | The Windows-only verification checklist (Task 14). |

**Modified:**

| Path | Change |
|---|---|
| `vHC/HC_Reporting/Startup/CAppSettings.cs` | `Servers` property, `TryLoad` tri-state, atomic `SetServers`, `AddServer`, `LoadOrSeedServers`. |
| `vHC/HC_Reporting/VhcGui.axaml` | Server card collapse, folder-picker button, period pills, terms checkbox. |
| `vHC/HC_Reporting/VhcGui.axaml.cs` | Server plumbing, dialog launch, terms handlers, period handler, picker handler, `DisableButtons`, `SetUiText`, `SetUiSync` fix. |
| `vHC/HC_Reporting/App.axaml` | One new style for the undo affordance. |
| `vHC/HC_Reporting/Functions/CredsWindow/CredsHandler.cs:105` | `AddServer` hook after `CredentialStore.Set`. |
| `vHC/HC_Reporting/Functions/UserInteraction/AvaloniaCredentialPrompter.cs:25` | `AddServer` hook after `CredentialStore.Set`. |
| `vHC/HC_Reporting/Resources/Localization/vhcres.resx` | 23 new keys. |
| `vHC/HC_Reporting/Resources/Localization/VbrLocalizationHelper.cs` | 23 new accessors (UTF-16LE). |
| `vHC/VhcXTests/CAppSettingsTests.cs` | Tests for all new `CAppSettings` behavior. |
| `vHC/HC_Reporting/Resources/Localization/vhcres.txt` | Matching ResGen source entries (**UTF-16LE**, `Key = Value` format). |

**Ordering rationale:** Tasks 1-6 are pure logic and localization — fully verifiable on this machine, and they land the tests that catch the design's two dangerous holes. Task 7 is a style-only change and Task 8 is a self-contained new dialog. Tasks 9-13 touch the main window, pairing each XAML change with its own code-behind so every task compiles on its own; splitting XAML from its code-behind would leave the tree broken mid-task.

Task 12 is the one that cannot be subdivided. Deleting `serverListBox` breaks **four** consumers at once — `InitializeServerList`, `UpdateSelectedServersGlobal`, `monitorQuickSetupBtn_Click`, and `SetUiSync`'s `hasRemoteServers` scan — so all four move together, which is why §7's bug 1 is fixed inside Task 12 rather than Task 13. Task 13 then depends on Task 12's `_persistedServers` field.

---

## Task 1: `CAppSettings` storage core

Adds the `Servers` property, an atomic write, and a tri-state load so a *transient* read failure is not mistaken for "never seeded".

**Files:**
- Modify: `vHC/HC_Reporting/Startup/CAppSettings.cs`
- Test: `vHC/VhcXTests/CAppSettingsTests.cs`

- [ ] **Step 1: Write the failing tests**

Append inside the existing `CAppSettingsTests` class in `vHC/VhcXTests/CAppSettingsTests.cs` (it is already `[Collection("GlobalState")]` and already isolates `StorePath` — reuse that fixture, do not create a second class):

```csharp
        [Fact]
        public void Get_WhenNoFileExists_ReturnsNullServers()
        {
            var settings = CAppSettings.Get();

            // null is load-bearing: it means "never seeded" and is what triggers
            // the one-time seed. An empty list would mean "user removed everything".
            Assert.Null(settings.Servers);
        }

        [Fact]
        public void SetServers_ThenGet_RoundTripsServers()
        {
            bool ok = CAppSettings.SetServers(new[] { "vbr01", "vbr02" });

            var settings = CAppSettings.Get();

            Assert.True(ok);
            Assert.Equal(new[] { "vbr01", "vbr02" }, settings.Servers);
        }

        [Fact]
        public void SetServers_WithEmptyList_PersistsEmptyNotNull()
        {
            CAppSettings.SetServers(new[] { "vbr01" });

            CAppSettings.SetServers(System.Array.Empty<string>());

            var settings = CAppSettings.Get();
            Assert.NotNull(settings.Servers);
            Assert.Empty(settings.Servers);
        }

        [Fact]
        public void SetServers_PreservesThemePreference()
        {
            CAppSettings.Set("Dark");

            CAppSettings.SetServers(new[] { "vbr01" });

            var settings = CAppSettings.Get();
            Assert.Equal("Dark", settings.ThemePreference);
            Assert.Equal(new[] { "vbr01" }, settings.Servers);
        }

        [Fact]
        public void Set_PreservesServers()
        {
            CAppSettings.SetServers(new[] { "vbr01" });

            CAppSettings.Set("Light");

            var settings = CAppSettings.Get();
            Assert.Equal("Light", settings.ThemePreference);
            Assert.Equal(new[] { "vbr01" }, settings.Servers);
        }

        [Fact]
        public void TryLoad_DistinguishesAbsentFromUnreadable()
        {
            // Absent: no file at all.
            var absent = CAppSettings.TryLoad(out _);

            // Unreadable: file exists but is not parseable.
            Directory.CreateDirectory(Path.GetDirectoryName(CAppSettings.StorePath)!);
            File.WriteAllText(CAppSettings.StorePath, "{ not valid json ");
            var unreadable = CAppSettings.TryLoad(out _);

            // Loaded: a real file.
            CAppSettings.SetServers(new[] { "vbr01" });
            var loaded = CAppSettings.TryLoad(out var settings);

            // One Fact with several asserts rather than a Theory: SettingsLoadResult
            // is internal, and an internal enum as an [InlineData] parameter produces
            // CS0051 on the generated public test method.
            Assert.Equal(SettingsLoadResult.Absent, absent);
            Assert.Equal(SettingsLoadResult.Unreadable, unreadable);
            Assert.Equal(SettingsLoadResult.Loaded, loaded);
            Assert.Equal(new[] { "vbr01" }, settings.Servers);
        }
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
dotnet test vHC/VhcXTests/VhcXTests.csproj --filter "FullyQualifiedName~CAppSettingsTests"
git checkout -- vHC/HC_Reporting/VeeamHealthCheck.csproj
```

Expected: compile failure — `SetServers`, `TryLoad`, `SettingsLoadResult` and `AppSettings.Servers` do not exist.

- [ ] **Step 3: Implement**

In `vHC/HC_Reporting/Startup/CAppSettings.cs`, add `using System.Collections.Generic;` to the using block, then add the property to `AppSettings`:

```csharp
public class AppSettings
{
    public string ThemePreference { get; set; } = "System";

    // Deliberately null rather than an empty list, and deliberately un-annotated.
    //
    // null means "never seeded" and triggers CAppSettings.LoadOrSeedServers' one-time
    // seed from the credential store. Any non-null value - INCLUDING an empty list -
    // is authoritative and means the user's own list, even if they emptied it. Without
    // that distinction, "removed everything" and "fresh upgrade" are indistinguishable
    // and the list resurrects itself on the next launch.
    //
    // No `?` annotation: the csproj sets no <Nullable> and this file has no #nullable
    // context, so List<string>? would emit CS8632 (which NoWarn's CA-only list does
    // not cover). A plain List<string> defaulting to null deserializes identically for
    // an absent JSON property. The explicit `= null` is redundant to the compiler and
    // kept purely as documentation, because the null default carries meaning.
    public List<string> Servers { get; set; } = null;
}

// Distinguishes "there is no settings file yet" from "there is one but we could not
// read it". Get() collapses both to defaults, which is fine for a theme preference
// but NOT for Servers: treating a transient read failure as "never seeded" would
// re-seed and resurrect servers the user had removed. Internal because it is a
// detail of LoadOrSeedServers, not part of the public settings surface.
internal enum SettingsLoadResult
{
    Loaded,
    Absent,
    Unreadable,
}
```

Replace the body of `CAppSettings` from `Get()` through `Set(...)` with:

```csharp
    internal static SettingsLoadResult TryLoad(out AppSettings settings)
    {
        settings = new AppSettings();

        try
        {
            if (!File.Exists(StorePath))
            {
                return SettingsLoadResult.Absent;
            }

            var json = File.ReadAllText(StorePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                // An empty file is indistinguishable from a not-yet-written one and
                // carries no user intent, so treat it as absent rather than unreadable.
                return SettingsLoadResult.Absent;
            }

            var loaded = JsonSerializer.Deserialize<AppSettings>(json);
            if (loaded == null)
            {
                return SettingsLoadResult.Unreadable;
            }

            settings = loaded;
            return SettingsLoadResult.Loaded;
        }
        catch (Exception ex)
        {
            CGlobals.Logger.Warning($"App settings file is malformed or unreadable, using defaults. Error: {ex.Message}");
            return SettingsLoadResult.Unreadable;
        }
    }

    // Unchanged contract: never throws, always returns something usable. Callers that
    // need to tell Absent from Unreadable use TryLoad instead.
    public static AppSettings Get()
    {
        TryLoad(out var settings);
        return settings;
    }

    public static void Set(string themePreference)
    {
        var settings = Get();
        settings.ThemePreference = themePreference;
        Write(settings);
    }

    public static bool SetServers(IEnumerable<string> servers)
    {
        var settings = Get();
        settings.Servers = servers?.ToList() ?? new List<string>();
        return Write(settings);
    }

    // NOT synchronized. This and AddServer and Set(themePreference) are all
    // Get() -> mutate -> write read-modify-writes over one shared file, so a GUI commit
    // racing a CLI /savecreds AddServer, or two GUI instances, loses an update - and a
    // lost update can resurrect a just-removed server. Stage C assumes a single
    // instance. Recorded here deliberately so the resulting bug is not a mystery later.
    //
    // Writes via a temp file plus an atomic move, so an interrupted write can never
    // leave a truncated settings.json behind. That matters more than usual here:
    // a truncated file reads back as Unreadable, and an earlier design that collapsed
    // Unreadable into "never seeded" would have re-seeded and resurrected removed
    // servers. Returns false (having logged) rather than throwing, matching the
    // pre-existing swallow-and-log contract of this class.
    private static bool Write(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StorePath)!);

            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            var tempPath = StorePath + ".tmp";

            File.WriteAllText(tempPath, json);
            File.Move(tempPath, StorePath, overwrite: true);
            return true;
        }
        catch (Exception ex)
        {
            CGlobals.Logger.Error($"Failed to persist app settings: {ex.Message}");
            return false;
        }
    }
```

Add `using System.Collections.Generic;` and `using System.Linq;` to the file's using block.

- [ ] **Step 4: Run the tests to verify they pass**

```bash
dotnet test vHC/VhcXTests/VhcXTests.csproj --filter "FullyQualifiedName~CAppSettingsTests"
git checkout -- vHC/HC_Reporting/VeeamHealthCheck.csproj
```

Expected: PASS, all of them. Note `Get_WhenFileIsMalformedJson_ReturnsDefault` and `Get_WhenFileIsEmpty_ReturnsDefault` are pre-existing tests that must still pass — `Get()`'s contract is unchanged.

- [ ] **Step 5: Commit**

```bash
git add vHC/HC_Reporting/Startup/CAppSettings.cs vHC/VhcXTests/CAppSettingsTests.cs
git commit -m "feat(settings): add Servers, atomic writes and a tri-state load to CAppSettings"
```

### Corrections applied after review — Task 1 as built

**Executed: `9eb450f` (steps above) + `ec79d68` (these corrections).** The steps above are left as written for history; where they disagree with this list, **this list is what shipped and what later tasks build on.**

Two independent reviews found one substantive defect and several smaller gaps, all traceable to the steps above rather than to the implementation:

1. **`Set` now returns `bool` and respects the `Unreadable` guard.** As specified above it went through `Get()`, which collapses `Unreadable` into defaults, then wrote unconditionally — so a corrupt `settings.json` plus a theme toggle would write `Servers: null`, which the next launch cannot distinguish from "never seeded", firing the migration seed and resurrecting removed servers. A *recoverable* read failure became *permanent* via an unrelated cosmetic action. Task 3's `LoadOrSeedServers_WhenSettingsUnreadable_ReturnsEmptyAndDoesNotWrite` would have asserted a guarantee its own sibling method falsified. `Set` now calls `Load` directly, logs, and returns `false` without writing when the file is unreadable.
2. **`TryLoad` is named `Load`.** `Try*` promises "returns `bool`, never throws"; a three-valued enum reads against that. **Tasks 2 and 3 must call `Load`, not `TryLoad`.**
3. **A file whose entire content is the JSON literal `null` returns `Absent`, not `Unreadable`.** It parses cleanly and means "no content", the same as an empty file. As `Unreadable` it would leave `LoadOrSeedServers` returning empty *every* session and never writing — permanently stuck rather than retry-recoverable.
4. **`Write` uses a fresh `Guid` per call for the temp filename**, not a fixed `StorePath + ".tmp"`. A fixed name lets two concurrent writers put a partially-written file into place via `File.Move`, which quietly contradicted the comment's own "can never leave a truncated file" claim. The catch block also best-effort deletes the temp file, so a failed `Move` leaves no litter in `%APPDATA%`.
5. **Caller-facing contracts moved to `///` on `SetServers` and `Set`** (return value, not-synchronized), with temp-file mechanics left on private `Write` — matching how `CredentialStore.cs` splits caller docs from implementation rationale.
6. **Eight further tests**, beyond the six in Step 1: the empty-file and JSON-`null` → `Absent` branches, `SetServers(null)` normalising to an authoritative empty list, `Set` refusing to write over an unreadable file, no temp file surviving a successful write, and `SetServers` returning `false` without throwing when the write fails. Before these, reverting `Write` to the original one-line `File.WriteAllText` kept the entire suite green — the `bool` the spec pins as contract surface had no coverage of its interesting branch.

   A second review round then found the write-failure test threw at `Directory.CreateDirectory`, *before* `WriteAllText` ran, so the catch block's temp-file cleanup was still unexercised. Closed by `SetServers_WhenMoveFails_ReturnsFalseAndLeavesNoTempFile`, which pre-creates a *directory* at `StorePath` so only `File.Move` fails. `Write` also gained a best-effort sweep of stale `settings.json.*.tmp` files, since `Guid` names — unlike the fixed name they replaced — are not self-healing when a process is killed mid-write.

**Suite after Task 1: 857 passed, 0 failed, 12 skipped** (verified directly, not just reported). Use **857** as the baseline for Task 2, not the 843 in standing rule 2.

---

## Task 2: `CAppSettings.AddServer`

The auto-add hook's storage half. **The no-op-while-null behavior is the single most dangerous detail in Stage C** — get it wrong and existing users silently lose servers on upgrade.

**Files:**
- Modify: `vHC/HC_Reporting/Startup/CAppSettings.cs`
- Test: `vHC/VhcXTests/CAppSettingsTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
        [Fact]
        public void AddServer_WhenServersNeverSeeded_IsNoOp()
        {
            // THE upgrade-data-loss guard. If AddServer materialised the list here,
            // Servers would flip null -> non-null, non-null is authoritative, and the
            // one-time seed would never run - silently dropping every server the user
            // already had credentials for. CredentialStore.Set has already persisted
            // the credential by this point, so the eventual seed picks the host up.
            CAppSettings.AddServer("newhost");

            Assert.Null(CAppSettings.Get().Servers);
        }

        [Fact]
        public void AddServer_WhenSeeded_AppendsServer()
        {
            CAppSettings.SetServers(new[] { "vbr01" });

            CAppSettings.AddServer("vbr02");

            Assert.Equal(new[] { "vbr01", "vbr02" }, CAppSettings.Get().Servers);
        }

        [Fact]
        public void AddServer_WhenSeededEmpty_AppendsServer()
        {
            // An empty list is authoritative, not "never seeded", so AddServer applies.
            CAppSettings.SetServers(System.Array.Empty<string>());

            CAppSettings.AddServer("vbr02");

            Assert.Equal(new[] { "vbr02" }, CAppSettings.Get().Servers);
        }

        [Fact]
        public void AddServer_WithExistingNameInDifferentCase_DoesNotDuplicate()
        {
            CAppSettings.SetServers(new[] { "VBR01" });

            CAppSettings.AddServer("vbr01");

            Assert.Equal(new[] { "VBR01" }, CAppSettings.Get().Servers);
        }

        [Fact]
        public void AddServer_WithNullOrWhitespace_IsNoOp()
        {
            CAppSettings.SetServers(new[] { "vbr01" });

            CAppSettings.AddServer(null);
            CAppSettings.AddServer("   ");

            Assert.Equal(new[] { "vbr01" }, CAppSettings.Get().Servers);
        }

        [Fact]
        public void AddServer_DoesNotSpecialCaseLocalhost()
        {
            // CAppSettings deliberately knows nothing about localhost. The injection
            // policy lives in the GUI layer, and a blanket filter here would silently
            // discard a legitimate entry on a machine with no local Veeam product.
            // A stray localhost is neutralised at read time by LoadOrSeedServers.
            CAppSettings.SetServers(System.Array.Empty<string>());

            CAppSettings.AddServer("localhost");

            Assert.Equal(new[] { "localhost" }, CAppSettings.Get().Servers);
        }
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
dotnet test vHC/VhcXTests/VhcXTests.csproj --filter "FullyQualifiedName~CAppSettingsTests"
git checkout -- vHC/HC_Reporting/VeeamHealthCheck.csproj
```

Expected: compile failure — `AddServer` does not exist.

- [ ] **Step 3: Implement**

Add to `CAppSettings`:

```csharp
    // Called from the two production CredentialStore.Set call sites so a host that
    // gains credentials outside the GUI (a /savecreds run, a CLI collection) does not
    // stay invisible in the picker.
    //
    // NO-OP while Servers is null, and that is not an optimisation. null means "never
    // seeded"; writing here would flip it to authoritative and permanently suppress
    // the one-time seed, silently discarding every server the user already had. The
    // credential itself is already persisted by the caller, so the eventual seed
    // picks this host up anyway.
    //
    // Knows nothing about localhost by design - see AddServer_DoesNotSpecialCaseLocalhost.
    public static void AddServer(string server)
    {
        if (string.IsNullOrWhiteSpace(server))
        {
            return;
        }

        var settings = Get();
        if (settings.Servers == null)
        {
            return;
        }

        if (settings.Servers.Any(s => s.Equals(server, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        settings.Servers.Add(server);
        Write(settings);
    }
```

- [ ] **Step 4: Run the tests to verify they pass**

```bash
dotnet test vHC/VhcXTests/VhcXTests.csproj --filter "FullyQualifiedName~CAppSettingsTests"
git checkout -- vHC/HC_Reporting/VeeamHealthCheck.csproj
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add vHC/HC_Reporting/Startup/CAppSettings.cs vHC/VhcXTests/CAppSettingsTests.cs
git commit -m "feat(settings): add CAppSettings.AddServer, no-op until the list is seeded"
```

### Corrections applied after review — Task 2 as built

**Executed: `b940d34` (steps above) + `390a19b` (these corrections).** Where the steps disagree with this list, this list is what shipped.

1. **The duplicate check uses `string.Equals(s, trimmed, ...)`, not `s.Equals(...)`.** Step 3's sample would throw `NullReferenceException` on a `null` element in the persisted list — reachable via a hand-edited `settings.json`, since `SetServers` does `servers?.ToList()` with no per-element filtering and a `null` element round-trips through `Write` unchanged. That violates this class's never-throws contract, and Task 5 hooks `AddServer` into `AvaloniaCredentialPrompter.PromptAsync`, an `async` handler where an unobserved exception is a crash rather than a logged warning.

   Note *why* the fix belongs here rather than in Task 3: `AddServer` reads via `Get()`, so §3's `Filter` is a **different read path** and never sees the element. Fixing it there would leave the crash open while appearing to address it.

2. **`AddServer` trims its input**, matching `addServerBtn_Click` (`VhcGui.axaml.cs:665`) and `ServerListEditor.Add`. Without it, case-insensitive-but-not-trim-insensitive detection lets `AddServer(" vbr01 ")` append a near-duplicate of `vbr01`. Trim happens after the whitespace short-circuit, so `"   "` still no-ops, and the trimmed value is what gets compared and persisted.

3. **No write-back healing.** `AddServer` must not rewrite the file to strip a bad element. Writing-on-read is the pattern Task 1's correction #1 removed from `Set`, and Task 3's `Unreadable` branch forbids it outright. A `null` element is harmless once item 1 lands and invisible after `Filter`.

4. **Three further tests:** a `null` element in the persisted list is tolerated and the duplicate still detected; a whitespace-padded existing name does not duplicate; and `AddServer` leaves an unreadable `settings.json` byte-identical. That last guarantee previously held only *by transitivity* — `Get()` returns `Servers == null`, so the null guard returns before any write — a real guarantee resting on a guard that exists for another reason, with no coverage. A doc note now says any future refactor to call `Load` directly must preserve it explicitly.

**Still open, deliberately deferred to Task 3:** per-element filtering in `SetServers`, so it can reuse `Filter`'s `IsNullOrWhiteSpace` predicate instead of hand-rolling a second copy of the same rule. It needs its own test, because `SetServers(new[] { "  " })` would then persist `[]` — "the user emptied it" — which is a meaningful and irreversible statement under the null-versus-empty rule.

5. **`AddServer` calls `Load` and logs on `Unreadable`, rather than `Get()` and silence.** The transitivity argument (`Get()` yields `Servers == null`, so the null guard returns first) was verified sound, so this is observability rather than correctness — but without it a user with a corrupt `settings.json` gets the auto-add promise silently broken with nothing in the log explaining why their `/savecreds` host never appeared. Making it explicit also let the "any future refactor must preserve this" caveat be deleted, since the guarantee stopped being incidental.

6. **The comparator is `string.Equals(s?.Trim(), trimmed, ...)`.** Trimming only the *input* left the stored side exposed to the same hand-edited-file source: a persisted `["  vbr01  "]` plus `AddServer("vbr01")` found no match and appended, giving two picker rows for one host. `s?.Trim()` makes the null-safety a property of the construction rather than a separate argument, so one line subsumes both hardenings.

7. **Five further tests** across the two correction rounds: padded-*input* and padded-*stored* duplicates, a `null` element tolerated with the duplicate still detected, an unreadable file left byte-identical, and `AddServer_PreservesThemePreference` completing the per-mutator matrix Task 1 established. That last one has a subtlety worth preserving if it is ever rewritten: it must seed via `SetServers` *before* asserting, or `AddServer`'s no-op-while-null guard makes it pass vacuously without ever exercising the write path.

**Suite after Task 2: 868 passed, 0 failed, 12 skipped** (verified directly). Baseline for Task 3.

---

## Task 3: `CAppSettings.LoadOrSeedServers`

The one-time seed, plus the read-time `localhost` filter that makes the injection invariant self-healing.

**Task 3 also owns one item deferred from Task 2:** make `SetServers` drop null/whitespace elements on write, reusing `Filter`'s predicate rather than hand-rolling a second copy of the same rule — that shared predicate is the actual internal-consistency win. It needs its own test, because `SetServers(new[] { "  " })` would then persist `[]`, which under the null-versus-empty rule is the meaningful and irreversible statement "the user emptied the list" rather than "one blank entry".

**Files:**
- Modify: `vHC/HC_Reporting/Startup/CAppSettings.cs`
- Test: `vHC/VhcXTests/CAppSettingsTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
        [Fact]
        public void LoadOrSeedServers_WhenNeverSeeded_SeedsFromCredentialServersAndPersists()
        {
            var result = CAppSettings.LoadOrSeedServers(
                new[] { "vbr01", "vbr02" }, excludeLocalhost: true);

            Assert.Equal(new[] { "vbr01", "vbr02" }, result);
            Assert.Equal(new[] { "vbr01", "vbr02" }, CAppSettings.Get().Servers);
        }

        [Fact]
        public void LoadOrSeedServers_WhenAlreadySeeded_IgnoresCredentialServers()
        {
            CAppSettings.SetServers(new[] { "kept" });

            var result = CAppSettings.LoadOrSeedServers(
                new[] { "ignored" }, excludeLocalhost: true);

            Assert.Equal(new[] { "kept" }, result);
        }

        [Fact]
        public void LoadOrSeedServers_WhenSeededEmpty_StaysEmpty()
        {
            // The null-vs-empty rule, end to end: a user who removed everything must
            // not have their list rebuilt from the credential store on next launch.
            CAppSettings.SetServers(System.Array.Empty<string>());

            var result = CAppSettings.LoadOrSeedServers(
                new[] { "vbr01" }, excludeLocalhost: true);

            Assert.Empty(result);
        }

        [Fact]
        public void LoadOrSeedServers_WhenExcludingLocalhost_FiltersSeedInput()
        {
            var result = CAppSettings.LoadOrSeedServers(
                new[] { "LocalHost", "vbr01" }, excludeLocalhost: true);

            Assert.Equal(new[] { "vbr01" }, result);
            Assert.Equal(new[] { "vbr01" }, CAppSettings.Get().Servers);
        }

        [Fact]
        public void LoadOrSeedServers_WhenNotExcludingLocalhost_RetainsIt()
        {
            // A VB365-only machine has IsVbrInstalled == false, so nothing injects
            // localhost - and RunSaveCredsFlow defaults its host to "localhost", so a
            // credential for it legitimately exists. Filtering unconditionally here
            // would leave the picker blank.
            var result = CAppSettings.LoadOrSeedServers(
                new[] { "localhost" }, excludeLocalhost: false);

            Assert.Equal(new[] { "localhost" }, result);
        }

        [Fact]
        public void LoadOrSeedServers_WhenExcludingLocalhost_FiltersAlreadyPersistedList()
        {
            // The self-healing half. AddServer has no localhost special case, so
            // /savecreds against localhost on an already-seeded injecting machine
            // really does persist the name. Filtering only the seed would then let
            // injection render it a second time as a duplicate row.
            CAppSettings.SetServers(new[] { "localhost", "vbr01" });

            var result = CAppSettings.LoadOrSeedServers(
                System.Array.Empty<string>(), excludeLocalhost: true);

            Assert.Equal(new[] { "vbr01" }, result);
        }

        [Fact]
        public void LoadOrSeedServers_ToleratesNullElementsAndTrimsPaddedEntries()
        {
            // A hand-edited settings.json is the realistic route to both. The null
            // element must not throw (Filter's IsNullOrWhiteSpace clause has to run
            // first), and "  localhost  " must still be excluded when localhost is
            // injected - otherwise it renders beside the injected row as a duplicate.
            CAppSettings.SetServers(new[] { null, "  vbr01  ", "  localhost  ", "   " });

            var result = CAppSettings.LoadOrSeedServers(
                System.Array.Empty<string>(), excludeLocalhost: true);

            Assert.Equal(new[] { "vbr01" }, result);
        }

        [Fact]
        public void LoadOrSeedServers_WhenNotExcludingLocalhost_ReturnsPaddedLocalhostTrimmed()
        {
            CAppSettings.SetServers(new[] { "  localhost  " });

            var result = CAppSettings.LoadOrSeedServers(
                System.Array.Empty<string>(), excludeLocalhost: false);

            Assert.Equal(new[] { "localhost" }, result);
        }

        [Fact]
        public void LoadOrSeedServers_WhenSettingsUnreadable_ReturnsEmptyAndDoesNotWrite()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CAppSettings.StorePath)!);
            File.WriteAllText(CAppSettings.StorePath, "{ not valid json ");
            var before = File.ReadAllText(CAppSettings.StorePath);

            var result = CAppSettings.LoadOrSeedServers(
                new[] { "vbr01" }, excludeLocalhost: true);

            // Seeding over an unreadable file would turn a transient read failure into
            // a permanent resurrection of removed servers. Better to show nothing this
            // session and leave the file alone so a retry can recover.
            Assert.Empty(result);
            Assert.Equal(before, File.ReadAllText(CAppSettings.StorePath));
        }
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
dotnet test vHC/VhcXTests/VhcXTests.csproj --filter "FullyQualifiedName~CAppSettingsTests"
git checkout -- vHC/HC_Reporting/VeeamHealthCheck.csproj
```

Expected: compile failure — `LoadOrSeedServers` does not exist.

- [ ] **Step 3: Implement**

Add to `CAppSettings`:

```csharp
    private const string LocalhostName = "localhost";

    // Resolves the effective server list, seeding once from the credential store if
    // this is the first launch after upgrade.
    //
    // `credentialStoreServers` is a parameter rather than an internal
    // CredentialStore.GetAllServers() call so this stays a pure function of its inputs
    // and is directly unit-testable, matching the StorePath seam pattern this class
    // and CredentialStore already use.
    //
    // `excludeLocalhost` is the GUI's LocalhostIsInjected predicate. It filters the
    // RETURNED list on every path, not just the seed - that is what makes the
    // "localhost is injected, never persisted" invariant self-healing rather than
    // dependent on every writer behaving. AddServer has no localhost special case, so
    // a stray persisted localhost is possible; ignoring it at read time neutralises it
    // wherever it would be injected and honours it wherever it would not.
    public static List<string> LoadOrSeedServers(IEnumerable<string> credentialStoreServers, bool excludeLocalhost)
    {
        var state = Load(out var settings);

        if (state == SettingsLoadResult.Unreadable)
        {
            // Do NOT seed and do NOT write over a file we could not parse.
            CGlobals.Logger.Warning(
                "Settings file unreadable; server list unavailable this session. Not seeding, to avoid resurrecting removed servers.");
            return new List<string>();
        }

        if (settings.Servers == null)
        {
            var seeded = Filter(credentialStoreServers, excludeLocalhost);
            SetServers(seeded);
            return seeded;
        }

        return Filter(settings.Servers, excludeLocalhost);
    }

    private static List<string> Filter(IEnumerable<string> servers, bool excludeLocalhost)
    {
        if (servers == null)
        {
            return new List<string>();
        }

        // Clause order is load-bearing, twice over.
        //
        // The IsNullOrWhiteSpace Where MUST come first: the persisted list can
        // legitimately contain a null element (a hand-edited settings.json, since
        // SetServers does no per-element filtering and a null round-trips through
        // Write), and any Equals below would throw on it. LINQ chains lazily, so this
        // ordering IS the null-safety - not incidental tidiness.
        //
        // The Trim Select must precede the localhost Where, or a persisted
        // "  localhost  " survives excludeLocalhost and renders BESIDE the injected
        // localhost row - exactly the duplicate this filter exists to prevent.
        // Trimming on read also stops the picker showing a padded hostname.
        var query = servers
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim());

        if (excludeLocalhost)
        {
            query = query.Where(s => !s.Equals(LocalhostName, StringComparison.OrdinalIgnoreCase));
        }

        return query.ToList();
    }
```

- [ ] **Step 4: Run the tests to verify they pass**

```bash
dotnet test vHC/VhcXTests/VhcXTests.csproj --filter "FullyQualifiedName~CAppSettingsTests"
git checkout -- vHC/HC_Reporting/VeeamHealthCheck.csproj
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add vHC/HC_Reporting/Startup/CAppSettings.cs vHC/VhcXTests/CAppSettingsTests.cs
git commit -m "feat(settings): add LoadOrSeedServers with a read-time localhost filter"
```

---

## Task 4: `ServerListEditor`

All of Stage C's real logic, deliberately free of Avalonia and `CGlobals` so it is testable here.

**Files:**
- Create: `vHC/HC_Reporting/Functions/ManageServers/ServerListEditor.cs`
- Create: `vHC/VhcXTests/ServerListEditorTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `vHC/VhcXTests/ServerListEditorTests.cs`:

```csharp
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
                withCreds: new[] { "vbr01" });
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
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
dotnet test vHC/VhcXTests/VhcXTests.csproj --filter "FullyQualifiedName~ServerListEditorTests"
git checkout -- vHC/HC_Reporting/VeeamHealthCheck.csproj
```

Expected: compile failure — the namespace `VeeamHealthCheck.Functions.ManageServers` does not exist.

- [ ] **Step 3: Implement**

Create `vHC/HC_Reporting/Functions/ManageServers/ServerListEditor.cs`:

```csharp
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
```

- [ ] **Step 4: Run the tests to verify they pass**

```bash
dotnet test vHC/VhcXTests/VhcXTests.csproj --filter "FullyQualifiedName~ServerListEditorTests"
git checkout -- vHC/HC_Reporting/VeeamHealthCheck.csproj
```

Expected: PASS, all 11.

- [ ] **Step 5: Commit**

```bash
git add vHC/HC_Reporting/Functions/ManageServers/ServerListEditor.cs vHC/VhcXTests/ServerListEditorTests.cs
git commit -m "feat(servers): add Avalonia-free ServerListEditor staging model"
```

---

## Task 5: Wire the auto-add hooks

**Files:**
- Modify: `vHC/HC_Reporting/Functions/CredsWindow/CredsHandler.cs:105`
- Modify: `vHC/HC_Reporting/Functions/UserInteraction/AvaloniaCredentialPrompter.cs:25`

- [ ] **Step 1: Add the hook in `CredsHandler`**

In `PromptForCredentialsCli`, immediately after the existing `CredentialStore.Set(host, username, password);` line:

```csharp
                // Store credentials for future use
                CredentialStore.Set(host, username, password);
                // Keep the GUI's persisted server list in step with credentials captured
                // outside it (/savecreds, CLI collection). No-ops until the list has been
                // seeded, so it can never pre-empt the one-time upgrade seed.
                CAppSettings.AddServer(host);
                CGlobals.Logger.Info($"Credentials stored for host: {host}", false);
```

`CredsHandler.cs` already has `using VeeamHealthCheck.Startup;` (it references `CredentialStore`), so no new using is needed. Verify before assuming:

```bash
grep -n "using VeeamHealthCheck.Startup;" vHC/HC_Reporting/Functions/CredsWindow/CredsHandler.cs
```

- [ ] **Step 2: Add the hook in `AvaloniaCredentialPrompter`**

Inside `PromptAsync`, immediately after its `CredentialStore.Set(...)` line:

```csharp
                CredentialStore.Set(host, dialog.Username, dialog.Password);
                CAppSettings.AddServer(host);
                CGlobals.Logger.Debug($"Credentials stored for host: {host}");
```

This file already has `using VeeamHealthCheck.Startup;`.

- [ ] **Step 3: Confirm no hook goes inside `CredentialStore.Set`**

```bash
grep -n "CAppSettings" vHC/HC_Reporting/Startup/CredentialStore.cs || echo "correct: no hook inside CredentialStore"
```

Expected: `correct: no hook inside CredentialStore`. `Set` has ~25 call sites in `VhcXTests`, and `CredentialStoreSecurityTests` isolates `CredentialStore.StorePath` but **not** `CAppSettings.StorePath` — a hook inside `Set` would make the test suite write to the developer's real `%APPDATA%/VeeamHealthCheck/settings.json`. `CArgsParser:574`'s `SetTransient` path also gets no hook, since transient credentials are never persisted.

- [ ] **Step 4: Build and run the full suite**

```bash
dotnet build vHC/HC.sln --configuration Debug
dotnet test vHC/VhcXTests/VhcXTests.csproj
git checkout -- vHC/HC_Reporting/VeeamHealthCheck.csproj
```

Expected: 0 build errors. Tests: 0 failed, 12 skipped. The pre-existing `CredsHandlerPrompterTests` and `SilentModeTests` must still pass — neither reaches these hooks (`CredsHandlerPrompterTests` stubs the prompter with `GUIEXEC = true`, and `SilentModeTests` returns at the `CGlobals.Silent` guard in `GetCreds`), so the count should not move for those classes.

- [ ] **Step 5: Commit**

```bash
git add vHC/HC_Reporting/Functions/CredsWindow/CredsHandler.cs vHC/HC_Reporting/Functions/UserInteraction/AvaloniaCredentialPrompter.cs
git commit -m "feat(servers): auto-add hosts to the persisted list on credential capture"
```

---

## Task 6: Localization keys

23 new keys. Read standing rule 6 before touching either file.

**Files:**
- Modify: `vHC/HC_Reporting/Resources/Localization/vhcres.resx` (UTF-8)
- Modify: `vHC/HC_Reporting/Resources/Localization/VbrLocalizationHelper.cs` (**UTF-16LE + CRLF**)
- Modify: `vHC/HC_Reporting/Resources/Localization/vhcres.txt` (**UTF-16LE + CRLF**, optional but preferred)

- [ ] **Step 1: Add the keys to `vhcres.resx`**

Insert these `<data>` blocks immediately before the existing `<data name="GuiAcceptButton" ...>` block at line 15. Keys are added to the **neutral** resx only — `NeutralLanguage=en-US` resolves them to English for `fr-FR`/`ja`/`zh-cn`/`zh-tw` automatically, so translation is a later content-only edit with no code change.

```xml
  <data name="GuiServerLabel" xml:space="preserve">
    <value>Server</value>
  </data>
  <data name="GuiManageServersTooltip" xml:space="preserve">
    <value>Manage servers...</value>
  </data>
  <data name="GuiManageServersTitle" xml:space="preserve">
    <value>Manage Servers</value>
  </data>
  <data name="GuiManageServersAddWatermark" xml:space="preserve">
    <value>Add server...</value>
  </data>
  <data name="GuiManageServersAddButton" xml:space="preserve">
    <value>Add</value>
  </data>
  <data name="GuiManageServersRemoveTooltip" xml:space="preserve">
    <value>Remove server</value>
  </data>
  <data name="GuiManageServersUndoTooltip" xml:space="preserve">
    <value>Undo removal</value>
  </data>
  <data name="GuiManageServersCredsSaved" xml:space="preserve">
    <value>credentials saved</value>
  </data>
  <data name="GuiManageServersPending" xml:space="preserve">
    <value>{0} pending change(s) - nothing is saved until you click Done</value>
  </data>
  <data name="GuiManageServersNoPending" xml:space="preserve">
    <value>No pending changes</value>
  </data>
  <data name="GuiManageServersCancel" xml:space="preserve">
    <value>Cancel</value>
  </data>
  <data name="GuiManageServersDone" xml:space="preserve">
    <value>Done</value>
  </data>
  <data name="GuiManageServersConfirmTitle" xml:space="preserve">
    <value>Confirm Changes</value>
  </data>
  <data name="GuiManageServersConfirmBody" xml:space="preserve">
    <value>Removing {0} server(s). Saved credentials for {1} of them will be deleted.</value>
  </data>
  <data name="GuiManageServersSaveFailedTitle" xml:space="preserve">
    <value>Save Failed</value>
  </data>
  <data name="GuiManageServersSaveFailed" xml:space="preserve">
    <value>Could not save the server list. See the log for details.</value>
  </data>
  <data name="GuiManageServersDuplicate" xml:space="preserve">
    <value>Server '{0}' is already in the list.</value>
  </data>
  <data name="GuiManageServersInvalid" xml:space="preserve">
    <value>Please enter a server name.</value>
  </data>
  <data name="GuiBrowseFolderTooltip" xml:space="preserve">
    <value>Browse for folder...</value>
  </data>
  <data name="GuiBrowseFolderTitle" xml:space="preserve">
    <value>Select Output Directory</value>
  </data>
  <data name="GuiPeriod7" xml:space="preserve">
    <value>7 Days</value>
  </data>
  <data name="GuiPeriod30" xml:space="preserve">
    <value>30 Days</value>
  </data>
  <data name="GuiPeriod90" xml:space="preserve">
    <value>90 Days</value>
  </data>
```

- [ ] **Step 2: Add the accessors to `VbrLocalizationHelper.cs`, encoding-preserving**

The helper is UTF-16LE with CRLF and its last two lines are `}}`. Insert the new accessors before them with this script — do **not** use a text editor or an edit tool that would rewrite the file as UTF-8:

```bash
python3 - <<'PY'
import io
p = "vHC/HC_Reporting/Resources/Localization/VbrLocalizationHelper.cs"
keys = [
    "GuiServerLabel", "GuiManageServersTooltip", "GuiManageServersTitle",
    "GuiManageServersAddWatermark", "GuiManageServersAddButton",
    "GuiManageServersRemoveTooltip", "GuiManageServersUndoTooltip",
    "GuiManageServersCredsSaved", "GuiManageServersPending",
    "GuiManageServersNoPending", "GuiManageServersCancel", "GuiManageServersDone",
    "GuiManageServersConfirmTitle", "GuiManageServersConfirmBody",
    "GuiManageServersSaveFailedTitle", "GuiManageServersSaveFailed",
    "GuiManageServersDuplicate", "GuiManageServersInvalid",
    "GuiBrowseFolderTooltip", "GuiBrowseFolderTitle",
    "GuiPeriod7", "GuiPeriod30", "GuiPeriod90",
]
s = io.open(p, encoding="utf-16-le", newline="").read()
marker = "}}"
assert s.rstrip().endswith(marker), "unexpected helper tail; inspect before editing"
tail_at = s.rstrip().rfind(marker)
block = "".join('public static string %s = m4.GetString("%s");\r\n' % (k, k) for k in keys)
s = s[:tail_at] + block + s[tail_at:]
io.open(p, "w", encoding="utf-16-le", newline="").write(s)
print("inserted", len(keys), "accessors")
PY
```

- [ ] **Step 3: Verify the encoding survived**

```bash
file vHC/HC_Reporting/Resources/Localization/VbrLocalizationHelper.cs
iconv -f UTF-16LE -t UTF-8 vHC/HC_Reporting/Resources/Localization/VbrLocalizationHelper.cs | grep -c 'GuiManageServers'
```

Expected: `Unicode text, UTF-16, little-endian text, with CRLF line terminators` and a count of `17` (the key list has 17 `GuiManageServers*` entries; the other six new keys use different prefixes). **If `file` reports UTF-8, stop and `git checkout --` the file** — a UTF-8 rewrite corrupts every localized string in the application.

- [ ] **Step 4: Append the matching entries to `vhcres.txt`**

This file is the ResGen resource source, **not C#** — its format is `Key = Value` (see `GuiAcceptButton = Accept Terms` at line 9). `VbrResFileBuilder.ps1` reads it, takes `$line.Split()[0]` as the key, and *generates* the C# accessor from it. Do not append C# here. It is also UTF-16LE.

```bash
python3 - <<'PY'
import io
p = "vHC/HC_Reporting/Resources/Localization/vhcres.txt"
pairs = [
    ("GuiServerLabel", "Server"),
    ("GuiManageServersTooltip", "Manage servers..."),
    ("GuiManageServersTitle", "Manage Servers"),
    ("GuiManageServersAddWatermark", "Add server..."),
    ("GuiManageServersAddButton", "Add"),
    ("GuiManageServersRemoveTooltip", "Remove server"),
    ("GuiManageServersUndoTooltip", "Undo removal"),
    ("GuiManageServersCredsSaved", "credentials saved"),
    ("GuiManageServersPending", "{0} pending change(s) - nothing is saved until you click Done"),
    ("GuiManageServersNoPending", "No pending changes"),
    ("GuiManageServersCancel", "Cancel"),
    ("GuiManageServersDone", "Done"),
    ("GuiManageServersConfirmTitle", "Confirm Changes"),
    ("GuiManageServersConfirmBody", "Removing {0} server(s). Saved credentials for {1} of them will be deleted."),
    ("GuiManageServersSaveFailedTitle", "Save Failed"),
    ("GuiManageServersSaveFailed", "Could not save the server list. See the log for details."),
    ("GuiManageServersDuplicate", "Server '{0}' is already in the list."),
    ("GuiManageServersInvalid", "Please enter a server name."),
    ("GuiBrowseFolderTooltip", "Browse for folder..."),
    ("GuiBrowseFolderTitle", "Select Output Directory"),
    ("GuiPeriod7", "7 Days"),
    ("GuiPeriod30", "30 Days"),
    ("GuiPeriod90", "90 Days"),
]
s = io.open(p, encoding="utf-16-le", newline="").read()
if not s.endswith("\r\n"):
    s += "\r\n"
s += "".join("%s = %s\r\n" % kv for kv in pairs)
io.open(p, "w", encoding="utf-16-le", newline="").write(s)
print("appended", len(pairs), "entries")
PY
file vHC/HC_Reporting/Resources/Localization/vhcres.txt
```

Expected: still UTF-16LE.

- [ ] **Step 5: Build and verify every key resolves**

`m4.GetString()` returns **null** for a missing key — no exception, no build error, just a silently blank label. Build, then assert every new accessor is non-null:

```bash
dotnet build vHC/HC.sln --configuration Debug
git checkout -- vHC/HC_Reporting/VeeamHealthCheck.csproj
```

Expected: 0 errors. A blank-label typo cannot be caught here — Task 14 puts it on the Windows checklist. Re-read your resx names against the accessor names character for character now, while the diff is small.

- [ ] **Step 6: Commit**

```bash
git add vHC/HC_Reporting/Resources/Localization/
git commit -m "feat(l10n): add Stage C's 23 resource keys to the neutral resx"
```

---

## Task 7: The undo-affordance style

`Button.remove-server` already exists at `App.axaml:313-326` — **do not write a new one.** It was added during Stage A for exactly this, with a comment at `:309-312` saying it awaits "whichever later stage revisits that model (originally 'Stage C')". What is missing is a sibling for undo.

**Files:**
- Modify: `vHC/HC_Reporting/App.axaml`

- [ ] **Step 1: Add the style**

Immediately after the existing `ListBoxItem:pointerover Button.remove-server` style (line 324-326), before `</Application.Styles>`:

```xml
        <!-- Undo deliberately does NOT reuse Button.remove-server. That style starts at
             Opacity 0.5 and only reaches 1 via ListBoxItem:pointerover (above), which is
             right for a remove affordance but wrong here: a user scanning a staged list
             to see what Done is about to destroy must be able to read the undo control
             without hovering every row. Full opacity, accent-coloured, always legible. -->
        <Style Selector="Button.undo-server">
            <Setter Property="Background" Value="Transparent" />
            <Setter Property="BorderThickness" Value="0" />
            <Setter Property="Foreground" Value="{DynamicResource AccentBrush}" />
            <Setter Property="Padding" Value="6" />
            <Setter Property="Cursor" Value="Hand" />
        </Style>
        <Style Selector="Button.undo-server:pointerover /template/ ContentPresenter">
            <Setter Property="Background" Value="Transparent" />
        </Style>
```

- [ ] **Step 2: Build**

```bash
dotnet build vHC/HC.sln --configuration Debug
git checkout -- vHC/HC_Reporting/VeeamHealthCheck.csproj
```

Expected: 0 errors. (An invalid selector is a XAML compile error, so this does catch typos.)

- [ ] **Step 3: Commit**

```bash
git add vHC/HC_Reporting/App.axaml
git commit -m "feat(gui): add Button.undo-server style for staged-removal undo"
```

---

## Task 8: `ManageServersDialog`

**Files:**
- Create: `vHC/HC_Reporting/Functions/ManageServers/ManageServersDialog.axaml`
- Create: `vHC/HC_Reporting/Functions/ManageServers/ManageServersDialog.axaml.cs`

Follows this project's `Functions/<Name>/` per-dialog convention, as Stage B's `Functions/AboutDialog/` did — **not** the spike's `Dialogs/` folder.

- [ ] **Step 1: Create the markup**

`vHC/HC_Reporting/Functions/ManageServers/ManageServersDialog.axaml`:

```xml
<!--
Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
MIT License
-->
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        x:Class="VeeamHealthCheck.Functions.ManageServers.ManageServersDialog"
        Width="420" Height="380"
        CanResize="False"
        WindowStartupLocation="CenterOwner">
    <Grid RowDefinitions="Auto,*,Auto,Auto" Margin="20">

        <Grid Grid.Row="0" ColumnDefinitions="*,8,Auto" Margin="0,0,0,12">
            <TextBox x:Name="newServerBox" Grid.Column="0" Classes="modern" Height="32" FontSize="12" />
            <Button x:Name="addBtn" Grid.Column="2" Classes="primary"
                    Height="32" FontSize="12" Width="70" Click="addBtn_Click" />
        </Grid>

        <!-- The row list is the only Star row, so it absorbs the window's spare height
             and scrolls internally rather than pushing the footer off-screen. -->
        <Border Grid.Row="1" BorderBrush="{DynamicResource CardBorderBrush}" BorderThickness="1"
                Background="{DynamicResource CardBackgroundBrush}" CornerRadius="4">
            <ListBox x:Name="serverRows" BorderThickness="0" Padding="4"
                     SelectionMode="Single" FontSize="13" />
        </Border>

        <TextBlock x:Name="pendingText" Grid.Row="2" Classes="secondary-text"
                   Margin="0,12,0,8" TextWrapping="Wrap" />

        <Grid Grid.Row="3" ColumnDefinitions="*,Auto,8,Auto">
            <Button x:Name="cancelBtn" Grid.Column="1" Classes="secondary"
                    Height="32" FontSize="12" Padding="14,5" Click="cancelBtn_Click" />
            <Button x:Name="doneBtn" Grid.Column="3" Classes="primary"
                    Height="32" FontSize="12" Padding="14,5" Click="doneBtn_Click" />
        </Grid>
    </Grid>
</Window>
```

- [ ] **Step 2: Create the code-behind**

`vHC/HC_Reporting/Functions/ManageServers/ManageServersDialog.axaml.cs`:

```csharp
// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Resources.Localization;
using VeeamHealthCheck.Startup;

namespace VeeamHealthCheck.Functions.ManageServers
{
    // Membership only - this dialog never changes the active server selection. The
    // spike's version returned the selected server as its dialog result; this one
    // returns true if changes were committed and false otherwise, and leaves the
    // caller to repopulate its own picker. That matters because the selection is read
    // by BOTH tabs (monitorQuickSetupBtn_Click reads it too).
    public partial class ManageServersDialog : Window
    {
        private readonly ServerListEditor _editor;

        // Required by the XAML loader. The real entry point is the two-argument
        // constructor; nothing should show a dialog built this way, and leaving _editor
        // null would NRE on the first click rather than failing where the mistake was.
        public ManageServersDialog()
        {
            InitializeComponent();
        }

        public ManageServersDialog(IEnumerable<string> initial, IEnumerable<string> pinned)
            : this()
        {
            this.Title = VbrLocalizationHelper.GuiManageServersTitle;
            this.newServerBox.Watermark = VbrLocalizationHelper.GuiManageServersAddWatermark;
            this.addBtn.Content = VbrLocalizationHelper.GuiManageServersAddButton;
            this.cancelBtn.Content = VbrLocalizationHelper.GuiManageServersCancel;
            this.doneBtn.Content = VbrLocalizationHelper.GuiManageServersDone;

            _editor = new ServerListEditor(
                initial,
                pinned,
                name => CredentialStore.Get(name) != null);

            this.RenderRows();
            this.doneBtn.IsEnabled = true;
        }

        // Rebuilt wholesale on every mutation. The list is a handful of rows and this
        // avoids an ObservableCollection plus per-row change notification for state
        // that only ever changes in response to a click in this same dialog.
        private void RenderRows()
        {
            this.serverRows.Items.Clear();

            foreach (var row in _editor.Rows)
            {
                this.serverRows.Items.Add(this.BuildRow(row));
            }

            int pending = _editor.PendingChangeCount;
            this.pendingText.Text = pending == 0
                ? VbrLocalizationHelper.GuiManageServersNoPending
                : string.Format(
                    CultureInfo.CurrentCulture,
                    VbrLocalizationHelper.GuiManageServersPending,
                    pending);
        }

        private Control BuildRow(ServerRow row)
        {
            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            };

            var label = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                Text = row.HasCredentials
                    ? $"{row.Name}  ({VbrLocalizationHelper.GuiManageServersCredsSaved})"
                    : row.Name,
            };

            if (row.IsPendingRemoval)
            {
                // Staged removals stay VISIBLE and struck through rather than
                // disappearing. If rows vanished on click, a staged dialog would look
                // identical to today's immediate one and the user would have no way to
                // see what Done is about to destroy - which is what makes Cancel legible.
                label.TextDecorations = TextDecorations.Strikethrough;
                label.Opacity = 0.5;
            }

            Grid.SetColumn(label, 0);
            grid.Children.Add(label);

            if (row.IsRemovable)
            {
                var button = new Button
                {
                    Tag = row.Name,
                    VerticalAlignment = VerticalAlignment.Center,
                };

                if (row.IsPendingRemoval)
                {
                    button.Content = "↶";
                    button.Classes.Add("undo-server");
                    ToolTip.SetTip(button, VbrLocalizationHelper.GuiManageServersUndoTooltip);
                    button.Click += this.UndoRow_Click;
                }
                else
                {
                    button.Content = "✕";
                    button.Classes.Add("remove-server");
                    ToolTip.SetTip(button, VbrLocalizationHelper.GuiManageServersRemoveTooltip);
                    button.Click += this.RemoveRow_Click;
                }

                Grid.SetColumn(button, 1);
                grid.Children.Add(button);
            }

            return grid;
        }

        private void addBtn_Click(object sender, RoutedEventArgs e)
        {
            string name = this.newServerBox.Text;
            var result = _editor.Add(name);

            switch (result)
            {
                case AddResult.Invalid:
                    _ = CGlobals.Notifier.ShowErrorAsync(
                        VbrLocalizationHelper.GuiManageServersInvalid,
                        VbrLocalizationHelper.GuiManageServersTitle);
                    return;

                case AddResult.Duplicate:
                    _ = CGlobals.Notifier.ShowErrorAsync(
                        string.Format(
                            CultureInfo.CurrentCulture,
                            VbrLocalizationHelper.GuiManageServersDuplicate,
                            name?.Trim()),
                        VbrLocalizationHelper.GuiManageServersTitle);
                    this.newServerBox.Text = string.Empty;
                    return;

                default:
                    this.newServerBox.Text = string.Empty;
                    this.RenderRows();
                    return;
            }
        }

        private void RemoveRow_Click(object sender, RoutedEventArgs e)
        {
            _editor.Remove((string)((Button)sender).Tag);
            this.RenderRows();
        }

        private void UndoRow_Click(object sender, RoutedEventArgs e)
        {
            _editor.UndoRemove((string)((Button)sender).Tag);
            this.RenderRows();
        }

        // Cancel and the OS close button both discard every staged change and touch
        // nothing. Close(false) is also what an unhandled window close yields, so the
        // two paths agree without extra wiring.
        private void cancelBtn_Click(object sender, RoutedEventArgs e) => Close(false);

        private async void doneBtn_Click(object sender, RoutedEventArgs e)
        {
            var plan = _editor.Commit();

            if (plan.CredentialsToDelete.Count > 0)
            {
                // Both buttons are disabled across the await, and this is not just
                // double-click hygiene. AvaloniaUiNotifier always owns its dialogs with
                // AvaloniaHost.MainWindow (AvaloniaUiNotifier.cs:23, :32), which is
                // VhcGui - NOT this window. So the confirm below disables the main
                // window and leaves THIS dialog fully interactive: without the guard the
                // user can click Cancel while the confirm is up, Close(false) resolves
                // the caller's ShowDialog<bool> so it skips its refresh, and then this
                // suspended handler resumes and deletes credentials anyway - after a
                // cancel, with the list still showing the servers it just unlinked.
                this.doneBtn.IsEnabled = false;
                this.cancelBtn.IsEnabled = false;

                bool confirmed;
                try
                {
                    confirmed = await CGlobals.Notifier.ConfirmAsync(
                        string.Format(
                            CultureInfo.CurrentCulture,
                            VbrLocalizationHelper.GuiManageServersConfirmBody,
                            _editor.Rows.Count(r => r.IsPendingRemoval),
                            plan.CredentialsToDelete.Count),
                        VbrLocalizationHelper.GuiManageServersConfirmTitle);
                }
                finally
                {
                    this.doneBtn.IsEnabled = true;
                    this.cancelBtn.IsEnabled = true;
                }

                if (!confirmed)
                {
                    return;
                }
            }

            // Credentials FIRST, then settings - deliberately, and not the other way
            // round. Both primitives swallow their own exceptions (CAppSettings logs and
            // returns; CredentialStore.Remove catches and returns false), so ordering
            // decides which failure mode you get. Persisting the list first and then
            // failing to delete a credential leaves creds.json holding an entry for a
            // host no longer in the list - unreachable from the GUI, since it is not
            // there to remove. Deleting first leaves a failed removal still listed,
            // which the user can simply retry.
            foreach (var host in plan.CredentialsToDelete)
            {
                if (!CredentialStore.Remove(host))
                {
                    CGlobals.Logger.Error($"Failed to remove stored credentials for host: {host}");
                }
            }

            if (!CAppSettings.SetServers(plan.FinalServers))
            {
                await CGlobals.Notifier.ShowErrorAsync(
                    VbrLocalizationHelper.GuiManageServersSaveFailed,
                    VbrLocalizationHelper.GuiManageServersSaveFailedTitle);
                return;
            }

            Close(true);
        }
    }
}
```

- [ ] **Step 3: Build**

```bash
dotnet build vHC/HC.sln --configuration Debug
git checkout -- vHC/HC_Reporting/VeeamHealthCheck.csproj
```

Expected: 0 errors.

`CGlobals` lives in `VeeamHealthCheck.Shared`, **not** `VeeamHealthCheck.Common` — the latter namespace does not exist anywhere in the solution, despite `CGlobals.cs` sitting in a `Common/` folder (`vHC/HC_Reporting/Common/CGlobals.cs:12` declares `namespace VeeamHealthCheck.Shared`). `VhcGui.axaml.cs:17` and `CAppSettings.cs:6` both import `VeeamHealthCheck.Shared` for exactly this reason. Folder name and namespace diverge here; trust the namespace.

- [ ] **Step 4: Run the full suite**

```bash
dotnet test vHC/VhcXTests/VhcXTests.csproj
git checkout -- vHC/HC_Reporting/VeeamHealthCheck.csproj
```

Expected: 0 failed, 12 skipped.

- [ ] **Step 5: Commit**

```bash
git add vHC/HC_Reporting/Functions/ManageServers/
git commit -m "feat(servers): add staged ManageServersDialog"
```

---

## Task 9: Terms checkbox

XAML and code-behind together, so the tree compiles at the end of the task.

**Files:**
- Modify: `vHC/HC_Reporting/VhcGui.axaml` (bottom bar, ~line 250)
- Modify: `vHC/HC_Reporting/VhcGui.axaml.cs` (`SelectTab`, `SetUiText`, `DisableButtons`, `AcceptButton_click`)

- [ ] **Step 1: Replace the button in the bottom bar**

In `vHC/HC_Reporting/VhcGui.axaml`, replace:

```xml
                <Button x:Name="termsBtn" Grid.Column="0" Classes="secondary"
                        Height="45" Click="AcceptButton_click" />
```

with:

```xml
                <CheckBox x:Name="termsCheckBox" Grid.Column="0" Classes="modern"
                          VerticalAlignment="Center"
                          Checked="termsCheckBox_Checked" Unchecked="termsCheckBox_Unchecked" />
```

- [ ] **Step 2: Replace the handler**

In `vHC/HC_Reporting/VhcGui.axaml.cs`, replace `AcceptButton_click` (and its comment block) with:

```csharp
        // Guards the programmatic revert below. A plain bool is sufficient ONLY because
        // Avalonia raises Unchecked synchronously inside the IsChecked assignment, while
        // this flag is still set - which is also why termsCheckBox_Unchecked must not be
        // async void.
        private bool _suppressTermsHandler;

        // AcceptTerms() stays synchronous - but this handler runs directly on the UI
        // thread, and AcceptTerms() reaches the notifier's BLOCKING wrapper, which
        // deadlocks there. Task.Run moves it off the UI thread first, exactly like
        // SetUiAsync's PreRunCheck call. Do not "simplify" this away: the deadlock
        // cannot reproduce on a non-Windows machine.
        //
        // The checkbox is visibly checked while the modal is open and springs back only
        // on decline. That is intended, not a bug.
        private async void termsCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (_suppressTermsHandler)
            {
                return;
            }

            this.functions.LogUIAction("Accept");
            bool accepted = await Task.Run(() => this.functions.AcceptTerms());
            run.IsEnabled = accepted;

            if (!accepted)
            {
                _suppressTermsHandler = true;
                termsCheckBox.IsChecked = false;
                _suppressTermsHandler = false;
            }
        }

        // Deliberately NOT async void. An await before the guard check would resume the
        // continuation after _suppressTermsHandler has been reset to false, silently
        // disabling the guard. There is nothing to await here anyway.
        private void termsCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_suppressTermsHandler)
            {
                return;
            }

            run.IsEnabled = false;
        }
```

- [ ] **Step 3: Update `SelectTab`**

In `SelectTab`, replace the three `termsBtn` lines with `termsCheckBox`:

```csharp
            termsCheckBox.Opacity = isAdHoc ? 1 : 0;
            termsCheckBox.IsHitTestVisible = isAdHoc;
            termsCheckBox.Focusable = isAdHoc;
```

`SelectTab` is the only place this triple is set — the constructor calls `SelectTab(isAdHoc: true)` at `:40` precisely so it stays the single source of truth (see the comment at `:34-39`). **Keep using the Opacity/IsHitTestVisible/Focusable triple and never `IsVisible`**: the checkbox sits alone in an `Auto` column of the bottom-bar grid, and `IsVisible = false` zeroes its `DesiredSize`, collapsing that column and shifting the progress stack and Run. Stage B fixed that exact bug twice.

- [ ] **Step 4: Update `SetUiText` and `DisableButtons`**

In `SetUiText`, replace the `termsBtn` line:

```csharp
            this.termsCheckBox.Content = VbrLocalizationHelper.GuiAcceptButton;
```

`GuiAcceptButton` ("Accept Terms") is reused verbatim, so all five locales stay covered and no new string is introduced.

In `DisableButtons`, replace `termsBtn.IsEnabled = false;` with:

```csharp
            termsCheckBox.IsEnabled = false;
```

- [ ] **Step 5: Update the comments that name `termsBtn`**

Comments do not break the build, so these are silent — and one of them is load-bearing. `termsBtn` survives at `VhcGui.axaml.cs:37`, `:39`, `:95`, `:99` and `VhcGui.axaml:244`. The block at `:95-107` is the explanation of *why* `SelectTab` uses the Opacity/IsHitTestVisible/Focusable triple, which Step 3 depends on, so it needs updating rather than tolerating.

Replace every `termsBtn` in those comments with `termsCheckBox`, keeping the surrounding reasoning intact. Verify:

```bash
grep -n 'termsBtn' vHC/HC_Reporting/VhcGui.axaml.cs vHC/HC_Reporting/VhcGui.axaml
```

Expected: no output.

- [ ] **Step 6: Build and test**

```bash
dotnet build vHC/HC.sln --configuration Debug
dotnet test vHC/VhcXTests/VhcXTests.csproj
git checkout -- vHC/HC_Reporting/VeeamHealthCheck.csproj
```

Expected: 0 build errors, 0 failed tests. Any remaining `termsBtn` reference *in code* is a compile error — that part has a safety net; the comments in Step 5 do not.

- [ ] **Step 7: Commit**

```bash
git add vHC/HC_Reporting/VhcGui.axaml vHC/HC_Reporting/VhcGui.axaml.cs
git commit -m "feat(gui): replace the Terms button with a checkbox that raises AcceptTerms"
```

---

## Task 10: Collection-period segmented pills

**Files:**
- Modify: `vHC/HC_Reporting/VhcGui.axaml` (~lines 140-147)
- Modify: `vHC/HC_Reporting/VhcGui.axaml.cs` (`ComboBox_SelectionChanged` → new handler, `SetUiText`)

- [ ] **Step 1: Replace the ComboBox with pills**

In `vHC/HC_Reporting/VhcGui.axaml`, replace:

```xml
                                        <ComboBox Name="daysSelector" Classes="modern"
                                                  Width="120" SelectedIndex="0" SelectionChanged="ComboBox_SelectionChanged">
                                            <ComboBoxItem Content="7 Days" IsSelected="True" />
                                            <ComboBoxItem Content="30 Days" />
                                            <ComboBoxItem Content="90 Days" />
                                        </ComboBox>
```

with:

```xml
                                        <StackPanel Orientation="Horizontal" Spacing="0">
                                            <RadioButton x:Name="days7" GroupName="Period" Classes="segment"
                                                         Tag="7" CornerRadius="4,0,0,4" IsChecked="True"
                                                         Checked="PeriodRadio_Checked" />
                                            <RadioButton x:Name="days30" GroupName="Period" Classes="segment"
                                                         Tag="30" CornerRadius="0" Margin="-1,0,0,0"
                                                         Checked="PeriodRadio_Checked" />
                                            <RadioButton x:Name="days90" GroupName="Period" Classes="segment"
                                                         Tag="90" CornerRadius="0,4,4,0" Margin="-1,0,0,0"
                                                         Checked="PeriodRadio_Checked" />
                                        </StackPanel>
```

The `-1` left margins overlap adjacent 1px borders so the three pills read as one joined control.

- [ ] **Step 2: Replace the handler**

In `vHC/HC_Reporting/VhcGui.axaml.cs`, replace `ComboBox_SelectionChanged` (and its comment block about the `daysSelector` null guard) with:

```csharp
        // Reads sender, never the days7/days30/days90 fields. days7's IsChecked="True"
        // in XAML raises Checked DURING InitializeComponent(), when the other two named
        // fields may not be assigned yet - inspecting them to find the checked one would
        // throw a NullReferenceException at construction. This is the same class of
        // timing hazard the ComboBox handler this replaces guarded against with a null
        // check.
        //
        // This also makes SetReportDays reachable during InitializeComponent() for the
        // first time, which the old null guard suppressed. That is safe: `functions` is
        // a field initializer so it runs before the constructor body, and LogUIAction
        // only writes to the static CGlobals.mainlog. It is harmless either way, because
        // CGlobals.reportDays already defaults to 7 - so do not "fix" the ordering.
        //
        // The value comes from Tag rather than Name or Content because Content is
        // localized, and parsing a localized label as data is exactly the mistake
        // notifSeverityBox already makes.
        private void PeriodRadio_Checked(object sender, RoutedEventArgs e)
        {
            int days = (sender as RadioButton)?.Tag switch
            {
                "30" => 30,
                "90" => 90,
                _ => 7,
            };

            this.SetReportDays(days);
        }
```

`SetReportDays` itself — its `CGlobals.ReportDays` write and `LogUIAction` call — is unchanged.

- [ ] **Step 3: Set the pill labels in `SetUiText`**

Add to `SetUiText`:

```csharp
            this.days7.Content = VbrLocalizationHelper.GuiPeriod7;
            this.days30.Content = VbrLocalizationHelper.GuiPeriod30;
            this.days90.Content = VbrLocalizationHelper.GuiPeriod90;
```

- [ ] **Step 4: Confirm `RadioButton` is imported**

```bash
grep -n "using Avalonia.Controls;" vHC/HC_Reporting/VhcGui.axaml.cs
```

Expected: a match. `RadioButton` lives in `Avalonia.Controls`.

- [ ] **Step 5: Build and test**

```bash
dotnet build vHC/HC.sln --configuration Debug
dotnet test vHC/VhcXTests/VhcXTests.csproj
git checkout -- vHC/HC_Reporting/VeeamHealthCheck.csproj
```

Expected: 0 errors, 0 failed.

- [ ] **Step 6: Commit**

```bash
git add vHC/HC_Reporting/VhcGui.axaml vHC/HC_Reporting/VhcGui.axaml.cs
git commit -m "feat(gui): convert the collection-period selector to segmented pills"
```

---

## Task 11: Output-directory folder picker

Restores the control Stage B did not port. The spike's implementation is sound — **port it verbatim.**

**Files:**
- Modify: `vHC/HC_Reporting/VhcGui.axaml` (~lines 109-114)
- Modify: `vHC/HC_Reporting/VhcGui.axaml.cs` (new handler, `SetUiText`, `DisableButtons`)

- [ ] **Step 1: Read the reference implementation**

```bash
git show spike/gui-redesign:vHC/Spikes/GuiRedesignSpike/Views/AdHocHealthCheckView.axaml.cs | sed -n '42,64p'
```

All three guards you need are already there. Do not go looking for a subtlety it missed; there isn't one.

- [ ] **Step 2: Add the button to the markup**

In `vHC/HC_Reporting/VhcGui.axaml`, replace:

```xml
                                <TextBox x:Name="pathBox" Classes="modern"
                                         TextChanged="pathBox_TextChanged" Height="32" Margin="0,10,0,0" />
```

with:

```xml
                                <Grid ColumnDefinitions="*,8,Auto" Margin="0,10,0,0">
                                    <TextBox x:Name="pathBox" Grid.Column="0" Classes="modern"
                                             TextChanged="pathBox_TextChanged" Height="32" />
                                    <Button x:Name="browseFolderBtn" Grid.Column="2" Classes="secondary"
                                            Content="..." Width="32" Height="32" Padding="0" FontWeight="Bold"
                                            HorizontalContentAlignment="Center" VerticalContentAlignment="Center"
                                            Click="browseFolderBtn_Click" />
                                </Grid>
```

32px, not the spike's 36px, to match every other control height in this window.

- [ ] **Step 3: Add the handler**

In `vHC/HC_Reporting/VhcGui.axaml.cs`:

```csharp
        // First use of Avalonia's StorageProvider anywhere in this application. Ported
        // from the spike verbatim, including all three guards. In production `this` IS
        // the Window, so GetTopLevel cannot return null once the constructor has run -
        // the guard is retained anyway.
        //
        // Assigning pathBox.Text is all that is needed: the existing pathBox_TextChanged
        // handler propagates it to CGlobals.desiredPath.
        private async void browseFolderBtn_Click(object sender, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is null)
            {
                return;
            }

            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = VbrLocalizationHelper.GuiBrowseFolderTitle,
                AllowMultiple = false,
            });

            if (folders.Count == 0)
            {
                return;
            }

            var path = folders[0].TryGetLocalPath();
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            this.SetPathBoxText(path);
        }
```

Add to the using block:

```csharp
using Avalonia.Platform.Storage;
```

- [ ] **Step 4: Wire the tooltip and the disable**

Add to `SetUiText`:

```csharp
            ToolTip.SetTip(this.browseFolderBtn, VbrLocalizationHelper.GuiBrowseFolderTooltip);
```

Add to `DisableButtons`:

```csharp
            browseFolderBtn.IsEnabled = false;
```

- [ ] **Step 5: Build and test**

```bash
dotnet build vHC/HC.sln --configuration Debug
dotnet test vHC/VhcXTests/VhcXTests.csproj
git checkout -- vHC/HC_Reporting/VeeamHealthCheck.csproj
```

Expected: 0 errors, 0 failed.

- [ ] **Step 6: Commit**

```bash
git add vHC/HC_Reporting/VhcGui.axaml vHC/HC_Reporting/VhcGui.axaml.cs
git commit -m "feat(gui): restore the output-directory folder picker Stage B missed"
```

---

## Task 12: Server card and list plumbing

The largest task, and it must be atomic: deleting `serverListBox` breaks three consumers at once, so the XAML and all three call sites move together or the tree does not compile.

**Files:**
- Modify: `vHC/HC_Reporting/VhcGui.axaml` (lines 49-88)
- Modify: `vHC/HC_Reporting/VhcGui.axaml.cs` (`InitializeServerList`, `UpdateSelectedServersGlobal`, the three server handlers, `monitorQuickSetupBtn_Click`, `DisableButtons`, `SetUiText`, constructor)

- [ ] **Step 1: Collapse the server card markup**

In `vHC/HC_Reporting/VhcGui.axaml`, replace everything from `<Grid Margin="0,0,0,10" ColumnDefinitions="*,10,80">` through the closing `</Grid>` of the Remove/Clear row (the add row, the 120px ListBox `Border`, and the Remove/Clear `Grid` — lines 49-88) with:

```xml
                                    <TextBlock x:Name="serverLabel" Classes="field-label" Margin="0,0,0,8" />
                                    <Grid ColumnDefinitions="*,8,Auto">
                                        <ComboBox x:Name="serverSelector" Grid.Column="0"
                                                  Classes="modern"
                                                  Height="32" FontSize="12"
                                                  HorizontalAlignment="Stretch"
                                                  SelectionChanged="serverSelector_SelectionChanged" />
                                        <Button x:Name="manageServersBtn" Grid.Column="2" Classes="secondary"
                                                Content="&#x2699;" Width="32" Height="32" Padding="0"
                                                HorizontalContentAlignment="Center" VerticalContentAlignment="Center"
                                                Click="manageServersBtn_Click" />
                                    </Grid>
```

Leave the `<Separator>` and the `Product Type:` block that follow it untouched.

- [ ] **Step 2: Add the localhost predicate and the resolved-list field**

In `vHC/HC_Reporting/VhcGui.axaml.cs`, near the other private fields:

```csharp
        private const string LocalhostName = "localhost";

        // Resolved once in the constructor, BEFORE SetUiSync() needs it. Caching it is
        // what lets SetUiSync see the real list instead of a not-yet-populated control -
        // see the SetUiSync fix. This is the PERSISTED list: it never contains localhost
        // on an injecting machine.
        private List<string> _persistedServers = new();

        // What the picker actually shows: _persistedServers plus injected localhost.
        // Kept as a field rather than read back off serverSelector.ItemsSource, because
        // casting ItemsSource back to a concrete collection type is a runtime cast that
        // silently depends on what InitializeServerList happened to assign.
        private List<string> _displayServers = new();

        // The single predicate that owns localhost policy. Injection, pinning and the
        // seed filter all derive from it; writing those three rules independently is how
        // they drift apart and produce a blank picker on a VB365-only machine.
        //
        // IsVbrInstalled alone is not enough: it is set only for a running
        // Veeam.Backup.Service (CClientFunctions.cs:106), so it is false on a VB365-only
        // box - which can still legitimately hold a localhost credential, because
        // RunSaveCredsFlow defaults its host to "localhost".
        private static bool LocalhostIsInjected =>
            CGlobals.IsVbrInstalled || CGlobals.IsVb365;
```

Ensure `using System.Collections.Generic;` and `using System.Linq;` are present.

- [ ] **Step 3: Resolve the list inside `SetUiSync()`, immediately after `ModeCheck()`**

Two things force this into Task 12 rather than a later one, and both are easy to miss:

- **`SetUiSync` is a fourth consumer of `serverListBox`** (`VhcGui.axaml.cs:211`, the `hasRemoteServers` scan). Step 1 deletes that control, which removes its compiler-generated backing field, so this method must be fixed in the *same* task or Step 9 fails with CS0103.
- **`LocalhostIsInjected` is meaningless before `ModeCheck()` runs.** `ModeCheck()` is the only thing that sets `CGlobals.IsVbrInstalled` (`CClientFunctions.cs:106`) or `IsVb365` (`:99`) on the GUI path, and it is called from `VhcGui.axaml.cs:205` — inside `SetUiSync`. `CArgsParser.LaunchUi` never calls it. Resolving the list in the constructor *before* `SetUiSync()` would read both flags as `false` on **every** machine, including a VBR box, silently making `excludeLocalhost` always `false`.

In `SetUiSync()`, replace:

```csharp
            string modeCheckResult = this.functions.ModeCheck();

            if (modeCheckResult == "fail")
            {
                // If remote servers are configured, don't exit — let user select product type
                bool hasRemoteServers = false;
                foreach (var item in serverListBox.Items)
                {
                    if (!item.ToString().Equals("localhost", StringComparison.OrdinalIgnoreCase))
                    {
                        hasRemoteServers = true;
                        break;
                    }
                }
```

with:

```csharp
            string modeCheckResult = this.functions.ModeCheck();

            // Resolved HERE, and not one line earlier in the constructor. ModeCheck() is
            // the only thing that populates CGlobals.IsVbrInstalled / IsVb365 on the GUI
            // path, so evaluating LocalhostIsInjected before this call reads both as
            // false on every machine - which would make excludeLocalhost permanently
            // false, persist localhost into the one-time seed on injecting machines, and
            // render the read-time filter inert. The same predicate evaluates correctly
            // in manageServersBtn_Click (which runs later), so getting this wrong makes
            // two calls to one function disagree.
            //
            // Placed before the fail branch below so hasRemoteServers still sees the
            // resolved list. CredentialStore.GetAllServers() is safe at any point - a
            // static constructor initialises its cache (CredentialStore.cs:34-36).
            _persistedServers = CAppSettings.LoadOrSeedServers(
                CredentialStore.GetAllServers(),
                excludeLocalhost: LocalhostIsInjected);

            if (modeCheckResult == "fail")
            {
                // Reads the resolved list rather than a control that has not been
                // populated yet. The old scan iterated an empty serverListBox, because
                // SetUiSync() runs before InitializeServerList() - so this branch could
                // never fire, and a machine with no local Veeam but remote servers
                // configured always got the abort the branch exists to prevent.
                //
                // The localhost filter is kept rather than relying on the "localhost is
                // never persisted" invariant: on a non-injecting machine localhost IS
                // legitimately persisted, and counting it as a remote server would put a
                // local-only box into Remote Mode.
                bool hasRemoteServers = _persistedServers
                    .Any(s => !s.Equals(LocalhostName, StringComparison.OrdinalIgnoreCase));
```

Leave the constructor's `this.SetUiSync();` line exactly as it is.

- [ ] **Step 4: Rewrite `InitializeServerList`**

Replace the whole method:

```csharp
        // preserveSelection distinguishes the two callers, and the distinction is
        // load-bearing. At startup there is no selection to keep and localhost-first is
        // the right default. After the dialog commits, silently reasserting that default
        // would move a user who was sitting on vbr01 back to localhost - flipping
        // REMOTEEXEC to false and pointing the next run at the local box - even if they
        // pressed Done having changed nothing. Both tabs read that selection
        // (monitorQuickSetupBtn_Click at :846), so it must survive a repopulate.
        private void InitializeServerList(bool preserveSelection = false)
        {
            string previous = preserveSelection
                ? serverSelector.SelectedItem?.ToString()
                : null;

            // The persisted list is authoritative; GetAllServers() is consulted only by
            // LoadOrSeedServers' one-time seed and the post-commit refresh.
            var display = new List<string>();

            if (LocalhostIsInjected)
            {
                display.Add(LocalhostName);
            }

            // Case-insensitive de-dup, matching the .Distinct() this method used to
            // apply. LoadOrSeedServers already strips localhost when it is injected;
            // this is belt and braces so a stray entry can never render twice.
            foreach (var server in _persistedServers)
            {
                if (!display.Any(s => s.Equals(server, StringComparison.OrdinalIgnoreCase)))
                {
                    display.Add(server);
                }
            }

            _displayServers = display;
            serverSelector.ItemsSource = _displayServers;

            // Restore the prior selection when it survived the commit; otherwise fall
            // back to localhost-first, then first-entry - the precedence this method has
            // always used at startup.
            string keep = previous == null
                ? null
                : _displayServers.FirstOrDefault(
                    s => s.Equals(previous, StringComparison.OrdinalIgnoreCase));

            if (keep != null)
            {
                serverSelector.SelectedItem = keep;
            }
            else if (_displayServers.Any(s => s.Equals(LocalhostName, StringComparison.OrdinalIgnoreCase)))
            {
                serverSelector.SelectedItem = _displayServers.First(
                    s => s.Equals(LocalhostName, StringComparison.OrdinalIgnoreCase));
            }
            else if (_displayServers.Count > 0)
            {
                serverSelector.SelectedIndex = 0;
            }

            UpdateSelectedServersGlobal();
        }
```

- [ ] **Step 5: Repoint `UpdateSelectedServersGlobal` at the ComboBox**

Change only the control name — the `else if` / `else` fallbacks and the `REMOTEEXEC` assignment stay exactly as they are. A `ComboBox` with a default selection is rarely null where `ListBox.SelectedItem` often was, but the branches cost nothing and deleting them is how a null-deref arrives later:

```csharp
        private void UpdateSelectedServersGlobal()
        {
            if (serverSelector.SelectedItem != null)
            {
                CGlobals.VBRServerName = serverSelector.SelectedItem.ToString();
                CGlobals.REMOTEHOST = serverSelector.SelectedItem.ToString();
            }
            else if (_displayServers.Count > 0)
            {
                CGlobals.VBRServerName = _displayServers[0];
                CGlobals.REMOTEHOST = CGlobals.VBRServerName;
            }
            else
            {
                CGlobals.VBRServerName = LocalhostName;
                CGlobals.REMOTEHOST = LocalhostName;
            }

            CGlobals.REMOTEEXEC = !CGlobals.VBRServerName.Equals(LocalhostName, StringComparison.OrdinalIgnoreCase);
        }
```

- [ ] **Step 6: Replace the three server handlers with two**

Delete `addServerBtn_Click`, `removeServerBtn_Click` and `clearServersBtn_Click` entirely. `Clear All` is gone: with per-row removal and a real Cancel in the dialog it has no home. Note that `clearCredsCheckBox` is **not** a substitute — it clears credentials and now leaves the list standing, which is a different behavior, not the same one.

Replace `serverListBox_SelectionChanged` with:

```csharp
        // Guard preserved from the ListBox version: Avalonia's generated
        // InitializeComponent() can raise SelectionChanged while assigning named fields
        // as the tree is built, so serverSelector can still be null on first raise.
        private void serverSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (serverSelector == null) return;

            UpdateSelectedServersGlobal();

            if (serverSelector.SelectedItem != null)
            {
                this.functions.LogUIAction($"Selected server: {serverSelector.SelectedItem}");
            }
        }

        private async void manageServersBtn_Click(object sender, RoutedEventArgs e)
        {
            var pinned = LocalhostIsInjected
                ? new[] { LocalhostName }
                : System.Array.Empty<string>();

            // Pinned names are passed in `initial` as well - they are displayed rows,
            // and that is what makes Add("localhost") a plain duplicate rather than a
            // second row.
            var initial = _displayServers.ToList();

            var dialog = new ManageServersDialog(initial, pinned);
            bool committed = await dialog.ShowDialog<bool>(this);

            if (!committed)
            {
                return;
            }

            // Re-resolve and repopulate. Without this, a removed server would remain in
            // CGlobals.VBRServerName/REMOTEHOST and the next run would target a host the
            // user just deleted.
            _persistedServers = CAppSettings.LoadOrSeedServers(
                CredentialStore.GetAllServers(),
                excludeLocalhost: LocalhostIsInjected);

            this.InitializeServerList(preserveSelection: true);
        }
```

Add `using VeeamHealthCheck.Functions.ManageServers;` to the using block.

- [ ] **Step 7: Fix the third consumer**

In `monitorQuickSetupBtn_Click` (~line 846), the Continuous Monitoring tab reads the Ad-hoc tab's selection. Change only the control name:

```csharp
            string server = serverSelector.SelectedItem?.ToString() ?? CGlobals.VBRServerName;
```

- [ ] **Step 8: Update `DisableButtons` and `SetUiText`**

In `DisableButtons`, delete the five now-nonexistent lines (`serverTextBox`, `addServerBtn`, `removeServerBtn`, `clearServersBtn`, `serverListBox`) and add:

```csharp
            serverSelector.IsEnabled = false;
            manageServersBtn.IsEnabled = false;
```

`manageServersBtn` matters and is not cosmetic: without it, a user can open Manage Servers mid-run, delete the host the collection is targeting, and have `Done` call `CredentialStore.Remove` on it.

Add to `SetUiText`:

```csharp
            this.serverLabel.Text = VbrLocalizationHelper.GuiServerLabel;
            ToolTip.SetTip(this.manageServersBtn, VbrLocalizationHelper.GuiManageServersTooltip);
```

- [ ] **Step 9: Build and test**

```bash
dotnet build vHC/HC.sln --configuration Debug
dotnet test vHC/VhcXTests/VhcXTests.csproj
git checkout -- vHC/HC_Reporting/VeeamHealthCheck.csproj
```

Expected: 0 errors, 0 failed, 12 skipped. Any surviving reference to a deleted control is a compile error.

- [ ] **Step 10: Commit**

```bash
git add vHC/HC_Reporting/VhcGui.axaml vHC/HC_Reporting/VhcGui.axaml.cs
git commit -m "feat(gui): collapse inline server management into a picker plus dialog"
```

---

## Task 13: Finish the remote-only startup fix

Task 12 fixed bug 1 (the dead `hasRemoteServers` scan), because it had to — that scan referenced a control Task 12 deletes. Fixing bug 1 makes the Remote Mode branch reachable for the first time ever, which exposes bug 2 on it. This task closes bug 2 and updates the comment that documents both as unfixed.

**Files:**
- Modify: `vHC/HC_Reporting/VhcGui.axaml.cs` (`SetUiSync`, ~lines 190-232)

- [ ] **Step 1: Stop the title being clobbered**

Replace:

```csharp
            this.Title = modeCheckResult;
```

with:

```csharp
            // Only overwrite when ModeCheck actually succeeded. On the fail-but-remote
            // path above, modeCheckResult is the literal string "fail", and the
            // unconditional assignment used to replace the Remote Mode title with it.
            // That was invisible while bug 1 made the branch unreachable; fixing bug 1
            // makes it a user-visible window titled "fail".
            if (modeCheckResult != "fail")
            {
                this.Title = modeCheckResult;
            }
```

- [ ] **Step 2: Update the stale comment above `SetUiSync`**

The comment block at `:194-200` documents both bugs as deliberately left intact. Replace those lines with:

```csharp
        // NOTE: preserved from the real WPF file, except for two bugs this stage fixes.
        // The hasRemoteServers scan used to iterate serverListBox, which SetUiSync()
        // runs before populating, so the remote-only branch was dead; and
        // "this.Title = modeCheckResult;" used to overwrite the "Remote Mode" title
        // unconditionally with the literal "fail". Both are addressed below.
        //
        // Making this branch live also means SetUiAsync() now reaches
        // Task.Run(() => PreRunCheck()) on a machine with no local Veeam. That is a
        // no-op: both of PreRunCheck's non-admin dialog branches are gated on
        // CGlobals.IsVbr / CGlobals.IsVb365 (CClientFunctions.cs:36, :72), and both are
        // false on this path.
```

- [ ] **Step 3: Build and test**

```bash
dotnet build vHC/HC.sln --configuration Debug
dotnet test vHC/VhcXTests/VhcXTests.csproj
git checkout -- vHC/HC_Reporting/VeeamHealthCheck.csproj
```

Expected: 0 errors, 0 failed, 12 skipped.

- [ ] **Step 4: Commit**

```bash
git add vHC/HC_Reporting/VhcGui.axaml.cs
git commit -m "fix(gui): stop the Remote Mode window title being overwritten with 'fail'"
```

---

## Task 14: Final verification and handoff

**Files:**
- Create: `docs/plans/2026-09-07-gui-redesign-stage-c-verification.md`

- [ ] **Step 1: Full clean build and test**

```bash
dotnet build vHC/HC.sln --configuration Debug 2>&1 | tail -20
dotnet test vHC/VhcXTests/VhcXTests.csproj 2>&1 | tail -20
git checkout -- vHC/HC_Reporting/VeeamHealthCheck.csproj
```

Expected: `0 Error(s)`, and `Failed: 0, Skipped: 12` with `Passed` at **888** — the 843 baseline plus 45 new tests (14 in Task 1, 11 in Task 2, 9 in Task 3 including the two null/trim tests added after Task 2's review, 11 in Task 4), each count including post-review corrections.

- [ ] **Step 2: Confirm nothing stale survives**

```bash
grep -rn 'termsBtn\|serverListBox\|serverTextBox\|addServerBtn\|removeServerBtn\|clearServersBtn\|daysSelector' vHC/HC_Reporting --include="*.cs" --include="*.axaml" | grep -v /obj/
```

Expected: **no output at all.** Tasks 9, 10, 12 and 13 each update the comments that mention their own removed controls, so by this point nothing — code or comment — should still name one. Any hit is a missed comment update; go back and fix it in place rather than accepting it here.

Do not use the `|| echo "clean"` form: `grep` exits 0 when it matches, so the echo can never fire while a single stale reference survives, which makes the check silently useless.

- [ ] **Step 3: Confirm the localization encodings survived every task**

```bash
file vHC/HC_Reporting/Resources/Localization/VbrLocalizationHelper.cs vHC/HC_Reporting/Resources/Localization/vhcres.txt
```

Expected: both still `Unicode text, UTF-16, little-endian`.

- [ ] **Step 4: Confirm the tree is clean**

```bash
git status --short
```

Expected: empty. If `VeeamHealthCheck.csproj` appears, revert it.

- [ ] **Step 5: Write the human verification checklist**

Create `docs/plans/2026-09-07-gui-redesign-stage-c-verification.md` containing the list below. Nothing visual or interactive can be checked in this sandbox — Avalonia crashes at native platform bootstrap before any application code runs — so this is the handoff.

```markdown
# Stage C — Windows verification checklist

Requires a real Windows machine with VBR installed. Nothing here can be checked off-Windows.

## Terms checkbox
- [ ] Label reads "Accept Terms" (localized, not blank — a blank label means a resx/accessor key mismatch).
- [ ] Checking it raises the disclaimer modal; confirming leaves it checked and enables Run.
- [ ] Declining reverts the checkbox to unchecked and leaves Run disabled.
- [ ] Dismissing the modal via its OS close button behaves the same as declining.
- [ ] Unchecking manually disables Run again.
- [ ] No hang when checking the box (this is the deadlock guard — `Task.Run` around `AcceptTerms`).
- [ ] Switching to Continuous Monitoring and back causes no bottom-bar reflow or shifting of the progress area.

## Manage Servers
- [ ] Gear button opens the dialog; its tooltip is localized.
- [ ] Add appends a row; adding a duplicate (any casing) shows the duplicate error; adding blank shows the input error.
- [ ] Clicking remove leaves the row visible, struck through, with an undo control.
- [ ] The undo control is legible **without** hovering the row.
- [ ] The remove control appears on row hover as intended.
- [ ] Undo clears the strike-through; the pending count updates on every change.
- [ ] `localhost` has no remove control.
- [ ] Cancel discards everything; the OS close button discards everything.
- [ ] Done applies changes; the summary confirm appears **only** when a removal would delete a saved credential, and its counts are right.
- [ ] **With the summary confirm open:** it appears in front of the dialog, and Done/Cancel on the dialog behind it are visibly disabled. Then decline the confirm and check both re-enable. (The notifier owns its dialogs with the *main* window, not this one, so this dialog is not automatically inert while the confirm is up — that is what the `try`/`finally` guard covers.)
- [ ] Double-click Done rapidly on a change set that triggers the confirm: exactly one commit happens.
- [ ] The three new glyphs render as glyphs and not as tofu boxes: the gear on the Ad-hoc tab, and `✕` / `↶` on the dialog rows.
- [ ] Removing the currently-selected server, then Done: the picker falls back sensibly and a subsequent run does not target the deleted host.
- [ ] Select a **remote** server, open the dialog, press Done having changed **nothing**: the selection is still that remote server, not `localhost`. Then start a run and confirm it targets the remote host. (This is the `preserveSelection` path — a regression here is silent, and testing only the removal case above will not catch it.)
- [ ] Select a remote server, add an unrelated server, Done: selection still on the original remote server.

## Persistence
- [ ] Add a server, restart: it is still there.
- [ ] Remove a server, restart: it stays removed.
- [ ] Remove every removable server, restart: the list stays empty rather than repopulating from stored credentials.
- [ ] `/savecreds` against a new host, then launch the GUI: the host appears in the picker.
- [ ] `/savecreds` against `localhost` on a machine with a local product: exactly **one** `localhost` row, not two.
- [ ] Upgrade path: with pre-existing stored credentials and no `Servers` key in `settings.json`, the first launch shows all previously-credentialed servers.

## Period pills
- [ ] All three select correctly and the log shows the matching `Interval set to 7|30|90`.
- [ ] "7 Days" is selected on launch.
- [ ] Labels are localized, not blank.
- [ ] **Hover and press each pill, checked and unchecked, in both light and dark themes.** `RadioButton.segment` defines no `:pointerover` or `:pressed` rules while every other interactive class in `App.axaml` does, so FluentTheme is expected to paint over the checked pill. If it does, fix it by fetching the real template source for the pinned Avalonia version or copying an already-validated sibling pattern from `App.axaml` — do not guess. Stage B's `Button.tab` fix needed `Background`, `BorderBrush` **and** `Foreground` neutralized on `ContentPresenter#PART_ContentPresenter` for both states; `Background` alone was verified insufficient on real hardware.

## Folder picker
- [ ] `...` opens a folder picker with a localized title; its tooltip is localized.
- [ ] Cancelling leaves the path box untouched.
- [ ] Choosing a folder updates the path box and the actual output location of a run.

## Remote-only startup (never worked before)
- [ ] On a machine with no local Veeam but at least one persisted remote server: the window opens instead of aborting, titled "Veeam Health Check - Remote Mode" rather than "fail".
- [ ] On a machine with no local Veeam and no persisted servers: it still aborts. That case is meant to fail.

## During a run
- [ ] The server picker, gear button, `...` button and Terms checkbox are all disabled while a collection is running.

## Layout
- [ ] Both new 32px icon buttons align with their partner controls in light and dark themes.
- [ ] The Ad-hoc left column is visibly shorter than the right now. Expected and accepted — rebalancing is out of scope.
- [ ] **Judgement call:** the card is titled "VBR Server" and the new field-label under it reads "Server". The spec called for that label (the spike's equivalent card was titled "Target & Output", where it wasn't redundant). If it reads as duplication on real hardware, delete the `serverLabel` `TextBlock` and its `SetUiText` line; leave the `GuiServerLabel` resx key in place for Stage D rather than removing a shipped key.
```

- [ ] **Step 6: Commit**

```bash
git add docs/plans/2026-09-07-gui-redesign-stage-c-verification.md
git commit -m "docs(gui): add the Stage C Windows verification checklist"
```

---

## Coverage map

Every spec section, and the task that implements it:

| Spec section | Task(s) |
|---|---|
| §1 Terms acceptance | 9 |
| §2 `Servers`, `null`-vs-empty, atomic write, `TryLoad` | 1 |
| §2 `AddServer` no-op-while-null | 2 |
| §2 `LoadOrSeedServers`, read-time localhost filter | 3 |
| §2 auto-add hooks at the two `Set` call sites | 5 |
| §2 `LocalhostIsInjected` predicate, injection, pinning | 12 |
| §2 `UpdateSelectedServersGlobal` fallbacks preserved | 12 |
| §3 tab surface (picker + gear) | 12 |
| §3 `Clear All` dropped | 12 |
| §3 `ManageServersDialog`, staged rows, commit order | 8 |
| §3 `ServerListEditor` | 4 |
| §3 dialog never changes selection; post-Done repopulate | 8, 12 |
| §3 all four `serverListBox` consumers (incl. `monitorQuickSetupBtn_Click` and `SetUiSync`) | 12 |
| §3 concurrency / single-instance assumption recorded | 1 (comment on `Write`) |
| §3a `DisableButtons` | 9, 11, 12 |
| §4 period pills, `sender`/`Tag` handler | 10 |
| §5 folder picker | 11 |
| §6 localization | 6 (keys), 9-12 (`SetUiText` wiring) |
| §7 bug 1 (dead `hasRemoteServers` scan) | 12 |
| §7 bug 2 (title clobbered with "fail") + stale comment | 13 |
| App.axaml undo style | 7 |
| Testing / verification | 1-4 (unit), 14 (handoff checklist) |

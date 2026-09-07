# GUI Redesign Stage C: Interaction Model — Design

**Date:** 2026-09-07
**Branch:** `stage-c/interaction-model` (off `feature/gui-redesign-port` @ `39ab236`)
**Depends on:** Stage A (theme/style port, `48336ec`), Stage B (layout restructure, `39ab236`, PR #215)
**Followed by:** Stage D (localization sweep of pre-existing hardcoded strings)

## Context

Stages A and B were deliberately behavior-preserving: A recolored the existing layout, B restructured it. Every control kept its `x:Name` and its handler. Stage C is the first stage that changes how the GUI *behaves*, and it exists to settle the two interaction models both prior stages explicitly deferred, plus one smaller control-type question.

Two corrections to the record before the design, because both prior documents carry them:

1. **The spike has no chip.** Stage B's non-goals describe the spike's server UI as "chip + Manage Servers dialog". It is actually a stretched `ComboBox` plus a 36px gear button (`Views/AdHocHealthCheckView.axaml`), opening `Dialogs/ManageServersDialog`. The `chip` style exists in `App.axaml` from Stage A but is unused in the spike's ad-hoc view. No chip is being ported and none is being built.

2. **The spike README's framing of its own dialog bug does not transfer.** The README says to "ensure the dialog only applies changes on explicit Done/OK, and rolls them back on Cancel/close." In production that promise is not straightforwardly keepable, because the server list is not a list — it is a view onto the credential store, with three different persistence semantics:
   - `InitializeServerList()` populates from `CredentialStore.GetAllServers()`, which returns `_cache.Keys` — the credential-store key set — plus `localhost` when `CGlobals.IsVbrInstalled`.
   - `addServerBtn_Click` only calls `serverListBox.Items.Add(...)`. Nothing persists a bare server name. `CredentialStore.Set` has exactly two production call sites (`CredsHandler.PromptForCredentialsCli:105`, which also serves `/savecreds` via `CArgsParser.RunSaveCredsFlow`, and `AvaloniaCredentialPrompter.Prompt:25`), both reached only when credentials are actually captured. **An added server is therefore ephemeral** until a run against it stores credentials.
   - `removeServerBtn_Click` calls `CredentialStore.Remove(...)` — permanent, on-disk, after a per-item confirm.
   - `clearServersBtn_Click` empties the ListBox and re-adds localhost but **never purges credentials**, so the next launch resurrects the entire list from `GetAllServers()`.

   Add is ephemeral, Remove is permanent, Clear is cosmetic. Fixing the surface without fixing that asymmetry would produce a dialog whose central promise the backend cannot keep. This stage therefore changes the storage model as well as the surface.

## Goals

- Replace the Terms button + modal with an inline checkbox that still guarantees the user saw the disclaimer, without losing any existing localized string or making `AcceptTerms()` dead code.
- Give the server list an honest, independent persistence model, and move its management into a modal dialog whose Cancel actually cancels.
- Convert the collection-period selector to the segmented-pill style ported in Stage A and unused since.
- Restore the spike's output-directory folder picker, which Stage B did not port.
- Localize every string this stage authors or rewrites, rather than adding to the hardcoded-English backlog.
- Fix the two `SetUiSync()` bugs that keep the remote-only startup path from working, now that the persisted list makes the first of them tractable (§7).

## Non-goals (explicitly deferred)

- **Severity selector control type.** `notifSeverityBox` stays a `ComboBox`. Its values (`warning`, `critical`, `ok`) are not an ordered progression, so a horizontal segmented control would imply a ranking the list does not have; it sits beside a 4-item notification-type `ComboBox` that stays a `ComboBox` regardless, so pills there would create an inconsistency rather than remove one; and `GetNotifSettings()` reads its visible `Content` as the backend value, so a rewrite would have to introduce a `Tag`-based value mapping (see "Recorded, not fixed").
- **Credential entry or editing.** The Manage Servers dialog shows a read-only "credentials saved" marker and nothing more. Capture stays lazy — `CredsHandler.PromptForCredentials` → `CGlobals.CredentialPrompter` → `CredentialPromptWindow`, raised mid-run when a connection needs it. There is no proactive credential surface in the GUI today and this stage does not add one.
- **Ad-hoc column rebalancing.** Collapsing the server block frees roughly 200px in the left column, which makes the left/right height imbalance the spike README flagged (item 2) more visible, not less. Stage B's real-machine pass already accepted an internal scrollbar on the right column. Rebalancing is layout work and Stage B is closed.
- **Localization of pre-existing hardcoded strings.** `VhcGui.axaml` carries 11 hardcoded `ToolTip.Tip` attributes and 36 hardcoded `Text`/`Content` attributes against 9 resx-driven assignments in `SetUiText()`. Stage C localizes only what it authors or rewrites; the remainder is Stage D.
- **Translation of the new keys.** New keys are added to the neutral `vhcres.resx` only. `NeutralLanguage=en-US` resolves them to English for `fr-FR`/`ja`/`zh-cn`/`zh-tw` automatically, so translation is a later content-only edit with no code change.

## Design

### 1. Terms acceptance

`termsBtn` becomes `termsCheckBox` — a `CheckBox Classes="modern"` in the same `Auto` column of the bottom bar, labelled `VbrLocalizationHelper.GuiAcceptButton` ("Accept Terms") verbatim, assigned in `SetUiText()` exactly as the button's `Content` is today.

Checking it calls the existing `this.functions.AcceptTerms()`, which raises today's `CGlobals.Notifier.Confirm` dialog carrying `GuiAcceptText`. Confirm → the box stays checked and `run.IsEnabled = true`. Decline or dismiss → the box reverts to unchecked and Run stays disabled. Unchecking manually disables Run again.

This is the only one of the four models considered that adopts the spike's inline visual model while keeping all three assets: the read-guarantee (the disclaimer is still shown, not merely linked), both resx keys across all five locales, and `AcceptTerms()` live and called rather than dead.

Acceptance **does not persist** across restarts. `CAppSettings` is being extended this stage and a `TermsAccepted` flag would have been nearly free, but per-launch acceptance matches today's behavior and is the safer posture.

Three constraints, the first of which is a correctness issue this sandbox cannot catch:

- **The handler must stay `async void` and keep `await Task.Run(() => this.functions.AcceptTerms())`.** Today's `AcceptButton_click` does exactly that, and the comments at `VhcGui.axaml.cs:525` and `IUiNotifier.cs:27` explain why: `AcceptTerms()` is synchronous and reaches the notifier's *blocking* wrapper, which deadlocks if invoked on the UI thread. A checkbox-handler rewrite is precisely where that gets dropped, and the resulting deadlock will not reproduce on macOS. Keep the shape:

  **Both handlers, in full.** The guard matters most in the one the programmatic revert actually raises, so neither can be left to prose:

  ```csharp
  private async void termsCheckBox_Checked(object sender, RoutedEventArgs e)
  {
      if (_suppressTermsHandler) return;
      this.functions.LogUIAction("Accept");
      bool accepted = await Task.Run(() => this.functions.AcceptTerms());
      run.IsEnabled = accepted;
      if (!accepted)
      {
          _suppressTermsHandler = true;
          termsCheckBox.IsChecked = false;   // raises Unchecked synchronously
          _suppressTermsHandler = false;
      }
  }

  // Deliberately NOT async void. See the re-entrancy note below.
  private void termsCheckBox_Unchecked(object sender, RoutedEventArgs e)
  {
      if (_suppressTermsHandler) return;
      run.IsEnabled = false;
  }
  ```

  Note the checkbox is *visibly checked while the modal is open* and springs back only on decline. That is intended, not a bug — a reviewer should not flag it.

- **Re-entrancy.** `IsChecked = false` raises **`Unchecked`**, not `Checked` — so the guard that does the real work is the one in `termsCheckBox_Unchecked`. A plain `bool _suppressTermsHandler` suffices *only because that event is delivered synchronously*, inside the assignment, while the flag is still set. **`termsCheckBox_Unchecked` must therefore not be `async void`**, however tempting the symmetry with its sibling: an `await` before the guard check moves the continuation past the point where `_suppressTermsHandler` is reset back to `false`, and the guard silently stops working. It has nothing to await in any case.
- **`SelectTab()` must use the `Opacity` / `IsHitTestVisible` / `Focusable` triple, never `IsVisible`.** The checkbox inherits `termsBtn`'s position: alone in an `Auto` column of the bottom-bar grid. `IsVisible = false` zeroes its `DesiredSize`, collapsing that column and shifting the progress stack and Run — the exact bug Stage B fixed twice (once for `progressText`, once for `termsBtn`/`run` themselves). The constructor's existing explicit initialization of these three properties must be updated to cover the checkbox rather than the button.

### 2. Server list: storage and semantics

`AppSettings` gains one property; `CAppSettings` gains two methods following its existing `Get()` → mutate → serialize pattern:

```csharp
public List<string> Servers { get; set; } = null;   // deliberately null, see below

public static bool SetServers(IEnumerable<string> servers);   // false if the write failed
public static void AddServer(string server);   // idempotent, case-insensitive; NO-OP when Servers is null
```

**`CAppSettings` has no knowledge of `localhost`.** The injection policy lives entirely in the GUI layer (see below) and is communicated to `CAppSettings` as a parameter. Putting a `localhost` special case inside `SetServers` or `AddServer` looks like a safety net but is actively wrong: on a machine with no local Veeam product, `localhost` is a legitimate ordinary entry, and a blanket filter would silently discard it.

No `?` annotation: `VeeamHealthCheck.csproj` sets no `<Nullable>` property and the codebase has no `#nullable` directives, so `List<string>?` emits CS8632 — which `NoWarn` (CA rules only) does not suppress. A plain `List<string>` defaulting to `null` deserializes to `null` for an absent JSON property exactly the same way, so the null-versus-empty rule below is unaffected. The explicit `= null` is redundant to the compiler but kept as documentation, since the null default is load-bearing rather than incidental.

**`null` versus empty is load-bearing.** `null` — the property absent from `settings.json` — means never seeded, and triggers a one-time seed from `CredentialStore.GetAllServers()` so no existing user loses their servers on upgrade. Any non-null value, **including an empty list**, is authoritative. Without that distinction, "the user removed everything" and "fresh upgrade" are indistinguishable and the list resurrects itself, which is the bug being fixed.

**`AddServer` must be a no-op while `Servers` is `null`.** This is the single most dangerous interaction in the design and it is easy to get wrong. The obvious implementation — `Get()` → `Servers ??= new()` → add → serialize — flips `null` to non-null, and non-null is authoritative, so it *destroys the seed signal before the seed has ever run*.

The failure is silent upgrade data loss, on exactly the users the seed exists to protect: someone with three credentialed remote servers in `creds.json` who has not yet launched the new GUI (`Servers == null`) runs `/savecreds` against a fourth host. `AddServer` writes `Servers = ["newhost"]`. The next GUI launch finds a non-null list, never seeds, and **the three pre-existing servers are gone.**

No-op is the correct resolution rather than "seed first, then add": `CredentialStore.Set` has already persisted the credential by the time the hook runs, so the eventual one-time seed picks the new host up for free. It also keeps `AddServer` free of any `CredentialStore` dependency, and makes the behavior independent of whether the hook runs before or after `Set` — an ordering this spec deliberately does not constrain.

**The persisted list is authoritative, with auto-add on credential capture.** `InitializeServerList()` no longer reads `GetAllServers()` except during the one-time seed. To keep a host that gains credentials outside the GUI (a `/savecreds` run, a CLI collection) from being invisible, `CAppSettings.AddServer(host)` is called at the **two production `CredentialStore.Set` call sites** — `CredsHandler.PromptForCredentialsCli:105` and `AvaloniaCredentialPrompter.Prompt:25`.

The hook must **not** go inside `CredentialStore.Set` itself. `Set` has ~25 call sites in `VhcXTests`, and `CredentialStoreSecurityTests` redirects `CredentialStore.StorePath` but not `CAppSettings.StorePath` — a hook inside `Set` would make the test suite write to the developer's real `%APPDATA%/VeeamHealthCheck/settings.json`. `CArgsParser:574`'s `SetTransient` path correctly gets no hook, since transient credentials are never persisted.

**`localhost` is injected when a local product exists, and is an ordinary entry otherwise.** One predicate owns this, and every rule derives from it:

```csharp
// VhcGui, GUI layer only
private static bool LocalhostIsInjected =>
    CGlobals.IsVbrInstalled || CGlobals.IsVb365;
```

When `LocalhostIsInjected` is true, `localhost` is prepended to the displayed list, passed to `ServerListEditor` as **pinned** (so it has no remove affordance), and filtered out of the one-time seed — because it will be supplied by injection on every future launch, persisting it would duplicate it. When it is false, `localhost` receives no special treatment anywhere: it is not injected, not pinned, not filtered from the seed, and persists like any other name.

The three rules must derive from the one predicate rather than being written independently, and that is the whole point of naming it. `CGlobals.IsVbrInstalled` is set in exactly one place — `CClientFunctions.cs:106`, only when a `Veeam.Backup.Service` process is running — so it is **false on a VB365-only machine**. `localhost` genuinely can carry stored credentials there, since `CArgsParser.RunSaveCredsFlow` defaults its host to `"localhost"` when `REMOTEHOST` is empty (`CArgsParser.cs:562`), which means `GetAllServers()` can legitimately contain it.

An earlier draft of this spec gated injection on `IsVbrInstalled` alone while filtering the seed unconditionally. Those two rules disagree, and the disagreement is a real defect: on a VB365-only machine with a `localhost` credential, the seed strips the entry and nothing injects it back, so **the effective list is empty and the server picker renders blank.** `UpdateSelectedServersGlobal()`'s `else` branch (`VhcGui.axaml.cs:183-187`) still sets `VBRServerName = "localhost"` and `REMOTEEXEC = false`, so a run would work — but with nothing selectable in the UI. Adding `IsVb365` to the predicate and deriving the filter from it closes that; keeping them independent reopens it.

**Persisted state must never depend on this invariant holding.** Two consumers would otherwise be one bug away from targeting the wrong host, so both are written defensively:

- `ServerListEditor` receives pinned names in **both** `initial` and `pinned`, so `Add("localhost")` on an injecting machine returns `Duplicate` and cannot create a second entry. `Commit().FinalServers` excludes pinned names, so an injecting machine never persists `localhost` even if one reaches the editor by another route.
- §7's `hasRemoteServers` check filters `localhost` explicitly rather than assuming it is absent from the persisted list. On a non-injecting machine it legitimately *is* present, and treating it as a remote server would put a local-only box into Remote Mode.

`UpdateSelectedServersGlobal()`'s `else if` / `else` fallbacks and its `CGlobals.REMOTEEXEC = !VBRServerName.Equals("localhost")` assignment are preserved verbatim. A `ComboBox` with a default selection is rarely null where `ListBox.SelectedItem` often was, but the branches cost nothing and deleting them is how a null-deref arrives later.

### 3. Server management UI

**Ad-hoc tab.** The current ~200px block (textbox + Add row, 120px `ListBox`, Remove/Clear row) collapses to a `Server` field-label above a single 32px row: a stretched `ComboBox x:Name="serverSelector"` plus a 32px gear `Button x:Name="manageServersBtn"`, in a `*,8,Auto` grid. 32px rather than the spike's 36px, to match the established control height in the ported window (`serverTextBox`, `pathBox`, and the Add button are all `Height="32"`).

`serverListBox_SelectionChanged` becomes `serverSelector_SelectionChanged` with the same body, including the existing `null` guard for `SelectionChanged` raised during `InitializeComponent()`.

**`Clear All` is dropped.** With per-row removal and a real Cancel in the dialog it has no home, it was the least honest control in the old block, and `clearCredsCheckBox` still covers wholesale credential purging.

**`ManageServersDialog`** lives at `Functions/ManageServers/ManageServersDialog.axaml(.cs)`, following this project's `Functions/<Name>/` per-dialog convention (as Stage B's `Functions/AboutDialog/` did) rather than the spike's `Dialogs/` folder. It contains:

- An add row: `TextBox` with watermark + `Add` button.
- A `ListBox` of rows, each showing the server name, a read-only "credentials saved" marker where `CredentialStore.Get(server) != null`, and a remove affordance. `localhost` renders without one.
- A pending-change count line stating that nothing is saved until Done.
- A `Cancel` / `Done` footer.

**Staged removals stay visible, struck-through, with an undo affordance** rather than disappearing. This is what makes Cancel legible: if rows vanished on click, a staged dialog would look identical to today's immediate one and the user would have no way to see what Done is about to destroy.

When any removal would destroy a credential, `Done` first raises **one** summary confirm ("Removing N servers. Saved credentials for M of them will be deleted.") replacing today's per-item prompts. `Cancel` and the OS close button both discard every staged change and touch nothing.

**Commit order is credentials first, then settings** — and this is the reverse of what an earlier draft specified, for a reason. Both primitives Done depends on swallow their own exceptions:

- `CAppSettings`'s write path catches, logs, and returns (`CAppSettings.cs:58-61`).
- `CredentialStore.Remove` catches `Exception`, logs, and returns `false` (`CredentialStore.cs:315-319`).

So a naive `Done` can report success having written nothing. Worse, persisting the list *first* creates a failure mode the old immediate-commit model could not produce: settings write succeeds, a credential deletion silently fails, and `creds.json` now holds an entry for a host that is no longer in the list — **unreachable from the GUI**, because it is not in the list to remove, and clearable only via `clearCredsCheckBox`. Previously the list *was* the credential key set, so orphaning was structurally impossible.

Deleting credentials first means a failed deletion leaves the host still listed, which is recoverable by retrying. Concretely: delete credentials for each removed host, log any `Remove` returning `false`, then call `SetServers` — which returns a `bool` for this purpose — and surface a failure to the user rather than closing the dialog as though it worked.

**Durability of the null-versus-empty rule.** `CAppSettings.cs:56` uses `File.WriteAllText`, which is not atomic, and `Get()`'s catch-all (`:42-46`) turns a truncated or malformed file into defaults — meaning `Servers == null`, meaning re-seed, meaning the list resurrects. That is precisely the bug §2 exists to prevent, reachable through any interrupted write. Two changes close it:

- Write via a temp file plus `File.Move(..., overwrite: true)` so a partial write can never be observed.
- Distinguish "file absent" from "file unreadable" in `Get()`, so corruption does not silently mean "never seeded".

**Concurrency is out of scope, but must be stated.** `SetServers`, `AddServer`, and the pre-existing `Set(themePreference)` are all unsynchronized `Get()` → mutate → `WriteAllText` read-modify-writes over one shared file. A GUI `Done` racing a CLI `/savecreds` `AddServer`, or two GUI instances, loses an update — and a lost update can resurrect a just-removed host. Repeated sequential launches are fine; concurrent ones are not. Stage C assumes a single instance and does not add locking; the assumption is recorded here so nobody later treats the resulting bug as a mystery.

**The staging logic lives in a plain `ServerListEditor` class with no Avalonia dependency**, in the same folder:

```csharp
internal sealed class ServerListEditor
{
    ServerListEditor(
        IEnumerable<string> initial,
        IEnumerable<string> pinned,              // never removable, never returned by Commit
        Func<string, bool> hasCredentials);

    IReadOnlyList<ServerRow> Rows { get; }        // Name, HasCredentials, IsPendingRemoval, IsRemovable
    int PendingChangeCount { get; }
    AddResult Add(string name);                   // Added | Duplicate | Invalid | UndidPendingRemoval
    void Remove(string name);                     // stages; no-op for a pinned row
    void UndoRemove(string name);
    CommitPlan Commit();                          // FinalServers, CredentialsToDelete
}
```

`pinned` carries `localhost` in from the caller rather than the editor hardcoding it, which keeps the class free of both Avalonia and `CGlobals` and makes the pinning rule directly testable. **Pinned names must also appear in `initial`**, since they are displayed rows; that is what makes `Add("localhost")` return `Duplicate` on an injecting machine instead of creating a second entry. `Commit().FinalServers` **excludes pinned entries**, since that value is handed straight to `CAppSettings.SetServers`.

On a machine where `LocalhostIsInjected` is false (§2), the caller passes an empty `pinned`, and `localhost` is then an ordinary addable, removable, persistable entry. The editor itself never mentions `localhost`.

Adding a name that is currently staged for removal undoes the removal rather than creating a duplicate. Duplicate detection is case-insensitive, matching today's `addServerBtn_Click`. This is the only part of Stage C with real logic and real edge cases, and separating it from the dialog is the only way any of it is testable on a non-Windows machine.

**The dialog manages membership only, never the active selection.** The spike's dialog returned the selected server as its dialog result; this one does not. Selection stays with the tab's `ComboBox`. After `Done`, the caller repopulates `serverSelector` from the committed list and then re-runs `UpdateSelectedServersGlobal()`. If the previously-active server was among those removed, selection falls back to `localhost` when present and otherwise to the first entry — the same precedence `InitializeServerList()` already uses. Without this step a removed server would remain in `CGlobals.VBRServerName`/`REMOTEHOST` and a subsequent run would target a host the user just deleted.

### 3a. `DisableButtons()` must learn about the new controls

`DisableButtons()` (`VhcGui.axaml.cs:506-523`) greys the whole input surface out for the duration of a run. It currently names `serverTextBox`, `addServerBtn`, `removeServerBtn`, `clearServersBtn`, `serverListBox`, and `termsBtn` — every one of which this stage renames or deletes.

The renames and deletions are compile errors and will be caught. **The additions are silent**, so they are called out explicitly:

- Remove: `serverTextBox`, `addServerBtn`, `removeServerBtn`, `clearServersBtn`, `serverListBox` (all deleted by §3).
- Add: `serverSelector`, `manageServersBtn` (§3) and the `...` folder-picker button (§5).
- Rename: `termsBtn` → `termsCheckBox` (§1).

This is not cosmetic. Without `manageServersBtn` in the list, a user can open Manage Servers **mid-run**, delete the host the collection is currently targeting, and have `Done` call `CredentialStore.Remove` on it. It also makes `ServerListEditor`'s `hasCredentials` results — snapshotted at construction — stale, so the Done summary confirm's credential count can be wrong.

`daysSelector` is deliberately absent from today's list and the period pills stay absent too; leaving the collection period editable mid-run is pre-existing behavior and not this stage's to change.

### 4. Collection-period segmented selector

`daysSelector` (`ComboBox`) becomes three `RadioButton Classes="segment"` controls — `days7` / `days30` / `days90`, shared `GroupName`, in a horizontal `StackPanel`, using the spike's corner-radius and `Margin="-1,0,0,0"` treatment for a joined appearance. `days7` starts checked.

`ComboBox_SelectionChanged`'s `switch (daysSelector.SelectedIndex)` becomes a checked-button lookup calling the same `SetReportDays(7|30|90)`. The default-to-7 branch is preserved for the no-selection case. `SetReportDays` itself, and its `CGlobals.ReportDays` write plus `LogUIAction` call, are unchanged.

The three visible labels ("7 Days" / "30 Days" / "90 Days") are currently hardcoded `ComboBoxItem` `Content` values. Since these controls are being replaced outright, the labels come from resx (see §6).

### 5. Output-directory folder picker

Stage B did not port the spike's `PickFolderButton`, leaving `pathBox` a bare `TextBox`. It is restored here: `pathBox` moves into a `*,8,Auto` grid with a 32px `...` button, matching the gear button's treatment — which is also what makes the gear a coherent choice rather than the window's only icon-only control.

This is the **first use of Avalonia's `StorageProvider` anywhere in the application** (no production file references it today), so it needs real guards rather than the spike's optimistic version:

- `TopLevel.GetTopLevel(this)` may be null → return.
- `OpenFolderPickerAsync` returns an empty collection on cancel → return.
- `TryGetLocalPath()` returns null for non-filesystem locations → return without writing.

On success it assigns `pathBox.Text`, which propagates to `CGlobals.desiredPath` through the existing `pathBox_TextChanged` handler. No additional wiring, and no change to how the path reaches the rest of the application.

### 6. Localization

Every string this stage authors or rewrites is resx-backed. Concretely, new keys are needed for: the gear tooltip, the dialog title, the dialog's add-row watermark and Add button, the remove and undo tooltips, the credentials-saved marker, the pending-changes line, the Cancel and Done buttons, the Done summary confirm body and title, the `...` tooltip, the folder-picker dialog title, the `Server` field label, and the three period-pill labels.

Mechanics, verified rather than assumed:

- MSBuild compiles the `.resx` files directly — confirmed by `obj/.../VeeamHealthCheck.Resources.Localization.vhcres{,.fR-FR,.ja,.zh-cn,.zh-tw}.resources` and the `ja/VeeamHealthCheck.resources.dll` satellite in `bin`. The `vhcres.txt` → `ResGen.exe` → `.resources` pipeline in `VbrResFileBuilder.ps1` is dead legacy: it requires Visual Studio 2022 Professional and contains a hardcoded `A:\source\veeam-healthcheck\...` path from the original author's machine. It is not needed and must not be run.
- Adding a string is therefore two required edits and one optional one:
  1. **Required** — a `<data name="X"><value>…</value></data>` block in the neutral `vhcres.resx`. This file is plain **UTF-8** and edits normally.
  2. **Required** — one `public static string X = m4.GetString("X");` line in `VbrLocalizationHelper.cs`. This file is **UTF-16LE with CRLF**; see the hazard note below.
  3. **Optional** — the matching entry in `vhcres.txt`, so the legacy generator would stay consistent if anyone ever repairs it. Note this file is the **ResGen resource source, not C#**: its format is `Key = Value` (e.g. `GuiAcceptButton = Accept Terms` at line 9), and `VbrResFileBuilder.ps1` reads it, takes `$line.Split()[0]` as the key, and *emits* the C# line in item 2 from it. Do not append C# to this file. It is also **UTF-16LE with CRLF**, so the same encoding hazard applies.
- `NeutralLanguage=en-US` means keys present only in the neutral resx fall back to English for the other four cultures. Translation is a later content-only edit.
- Strings are applied in code-behind via `SetUiText()` (and `ToolTip.SetTip(control, …)` for tooltips), because `VbrLocalizationHelper` is an internal class and is not reachable from XAML markup — the same pattern the file already uses for its 9 existing resx-driven assignments.

### 7. Fixing the remote-only startup path

`SetUiSync()` carries two pre-existing bugs that Stage B documented in code and deliberately left intact. Stage C is rewriting the exact data flow the first one depends on, so both are fixed here.

**Bug 1 — the `hasRemoteServers` scan always sees an empty list.** `SetUiSync()` runs before `InitializeServerList()` in the constructor, so its `foreach (var item in serverListBox.Items)` iterates nothing. The branch exists so that a machine with no local Veeam install but remote servers configured continues into the UI instead of aborting; because the scan can never find anything, such a machine always falls through to `_modeCheckFailed = true` and gets exactly the failure the branch was written to prevent.

**Bug 2 — the Remote Mode title is immediately overwritten.** The `hasRemoteServers` branch sets `this.Title = "Veeam Health Check - Remote Mode"`, then execution falls through to `this.Title = modeCheckResult;` a few lines later — and on this path `modeCheckResult` is the literal string `"fail"`. Fixing bug 1 makes this branch reachable for the first time, so leaving bug 2 would ship a newly-live code path that titles the window `"fail"`.

**The fix.** The seed-and-resolve logic becomes a static method on `CAppSettings`:

```csharp
public static List<string> LoadOrSeedServers(
    IEnumerable<string> credentialStoreServers,
    bool excludeLocalhost);
```

It returns `Servers` when non-null (including when empty), and otherwise seeds from `credentialStoreServers`, persists the result, and returns it. `excludeLocalhost` is supplied by the caller as `LocalhostIsInjected` (§2) — the same predicate that drives injection and pinning — which is what keeps `CAppSettings` free of `localhost` policy and stops the two rules from drifting apart.

Taking the credential-store servers as a parameter rather than calling `CredentialStore.GetAllServers()` internally keeps it a pure function of its inputs and directly unit-testable, in line with the seam pattern `CAppSettings.StorePath` and `CredentialStore.StorePath` already use.

`VhcGui`'s constructor calls it once, **before** `SetUiSync()`, and caches the result in a `_persistedServers` field that both `SetUiSync()` and `InitializeServerList()` read.

`SetUiSync()`'s scan then becomes a filtered check. It keeps the `localhost` exclusion that the current `foreach` already has, rather than relying on §2's invariant to guarantee absence — on a non-injecting machine `localhost` is legitimately persisted, and counting it as a remote server would put a local-only box into Remote Mode:

```csharp
bool hasRemoteServers = _persistedServers
    .Any(s => !s.Equals("localhost", StringComparison.OrdinalIgnoreCase));
```

For bug 2, the trailing `this.Title = modeCheckResult;` becomes conditional so it does not clobber the Remote Mode title on the fail-but-remote path.

This ordering matters and is the reason for extracting a method rather than writing a one-liner: if `SetUiSync()` simply read `CAppSettings.Get().Servers` directly, the very first launch after upgrade would see `null` — the seed would not yet have run, because it lives in `InitializeServerList()`, which runs later. A user upgrading with remote servers already credentialed would hit the abort path exactly once, which is the worst possible time for it. Resolving the list before `SetUiSync()` removes the constructor-ordering fragility rather than working around it.

## Implementation notes / hazards

- **`VbrLocalizationHelper.cs` and `vhcres.txt` are both UTF-16LE with CRLF line endings** (PowerShell `out-file` output). An edit tool that rewrites either as UTF-8 corrupts every localized string in the application. Edits to these two files must be encoding-preserving, and the encoding must be re-verified afterwards (`file` should still report "Unicode text, UTF-16, little-endian"). `vhcres.resx` is plain UTF-8 and needs no special handling. Note that this encoding is also why `cat`/`grep` on the two UTF-16 files appears to fail with "stream did not contain valid UTF-8" — that is expected, not a sign of a damaged file.
- **`m4.GetString()` returns `null` for a missing key** — no exception, no build error. A typo'd key produces a silently blank label. Every new key needs render verification on the real-machine pass, and the resx name must be character-for-character identical to the helper field name.
- **Avalonia `/template/`-level styling is out of scope here**, but if `RadioButton.segment` or the gear button turns out to need FluentTheme neutralization, follow `feedback_avalonia_fetch_real_template_source`: fetch the real template source for the pinned Avalonia version or copy an already-validated sibling pattern from `App.axaml`. Do not guess. Stage B's `Button.tab` fix needed `Background`, `BorderBrush`, **and** `Foreground` neutralized on `ContentPresenter#PART_ContentPresenter` for both `:pointerover` and `:pressed`; `Background` alone was verified insufficient on real hardware.
- **`AddResult` is an internal enum, and `VhcXTests` reaches internals via `InternalsVisibleTo`.** An internal enum used as an `[InlineData]` parameter in a `[Theory]` produces **CS0051** (inconsistent accessibility on the generated test method). The fix is to reshape the test — a `[Fact]` with several asserts, or a `[Theory]` keyed on strings that maps to the enum inside the body — **not** to widen `AddResult` to `public`. This has been hit and resolved in this repo before; do not "fix" it by changing the enum's accessibility.
- **Every build auto-increments `vHC/HC_Reporting/VeeamHealthCheck.csproj`.** Run `git checkout -- vHC/HC_Reporting/VeeamHealthCheck.csproj` after building or testing and before committing.
- One commit per logical change; never `git commit --amend`.

## Testing / verification

Unit-testable on macOS, and therefore required as part of this stage:

- `ServerListEditor`: add, duplicate rejection (case-insensitive), stage removal, undo removal, add-undoes-pending-removal, a pinned row rejecting `Remove`, `PendingChangeCount`, `Commit()` producing the correct final list and credential-deletion set, and `Commit().FinalServers` excluding pinned entries.
- `CAppSettings`: `Servers` round-trip through `SetServers`/`Get`, `AddServer` idempotence and case-insensitivity, `AddServer` ignoring `localhost` in any casing, and the `null`-versus-empty seed rule. `CAppSettings.StorePath` already has an internal test seam for isolation.
- `CAppSettings.LoadOrSeedServers`: returns the persisted list unchanged when non-null; returns an empty list unchanged when persisted as empty (the null-versus-empty rule); seeds and persists when null; filters `localhost` in any casing out of the seed input. Pure in its parameter, so no `CredentialStore` isolation is needed — only the existing `CAppSettings.StorePath` seam.

Baseline to hold: **843 passed, 0 failed, 12 skipped** on `dotnet test vHC/VhcXTests/VhcXTests.csproj`, plus the new tests. `dotnet build vHC/HC.sln --configuration Debug` must report 0 errors.

Windows-only, handed to the user (this sandbox cannot render Avalonia at all — it crashes at native platform bootstrap before any application code runs):

- Terms checkbox: confirm enables Run; decline and dismiss both revert the checkbox and leave Run disabled; no handler re-entrancy; label renders localized.
- Tab switching with the checkbox present — no bottom-bar reflow, and no keyboard focus landing on hidden controls.
- Manage Servers: add, staged removal appearance, undo, Cancel discarding, OS-close discarding, Done applying, the summary confirm firing only when credentials would be deleted, and `localhost` having no remove affordance.
- Removing the **currently active** server via the dialog, then confirming the tab's selection falls back correctly and a subsequent run does not target the deleted host.
- Persistence: added server survives a restart; removed server stays removed; removing every server and restarting leaves the list empty rather than resurrecting it (the `null`-versus-empty rule, end to end); `/savecreds` against a new host makes it appear in the list; `/savecreds` against `localhost` does **not** add a persisted entry.
- Period pills: all three select correctly, `days7` default, correct `CGlobals.ReportDays` in the log.
- Folder picker: cancel leaves the path untouched; a chosen folder updates both the textbox and the effective output path.
- Both new icon buttons render at 32px and align with their partner controls in light and dark themes.
- **The remote-only path (§7), which has never actually worked:** on a machine with no local Veeam install but at least one persisted remote server, the window opens instead of aborting, and its title reads "Veeam Health Check - Remote Mode" rather than "fail". Also confirm the still-broken case is genuinely broken — no local Veeam *and* no persisted servers should still abort.

## Recorded, not fixed

- **`notifTypeBox` and `notifSeverityBox` round-trip backend values through their visible `Content`.** `GetNotifSettings()` reads `(notifTypeBox.SelectedItem as ComboBoxItem)?.Content?.ToString()?.ToLower()` and the same for severity. Localizing those labels in Stage D would silently break notification delivery. Stage D must add a `Tag`-based value mapping before touching them.
- ~~Two pre-existing bugs in `SetUiSync()`~~ — **now in scope, see §7.**
- **`Clear All`'s removal is a deliberate behavior deletion**, not an oversight.
- **The freed left-column space** makes the Ad-hoc height imbalance more visible. Out of scope by decision.

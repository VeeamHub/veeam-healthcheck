# GUI Redesign Stage E: Remote-GUI / VSA Cold-Start Recovery — Design

**Branch:** `stage-e/cold-start-recovery` (name TBD at plan time), off `feature/gui-redesign-port` @ `dedb71e4` (Stage D, PR #231). Verify that's still the tip before branching.

## Context

Stage C introduced "Remote Mode": on a machine with no local VBR/VB365 process detected, if
at least one remote server is already persisted in `settings.json`, the GUI still renders and
lets the user work against that server. It identified, but explicitly deferred, a harder
case: a machine with no local Veeam **and** zero persisted servers — the cold-start case on a
console-only/VSA-adjacent management box, an increasingly common real deployment pattern
since VSA appliances can't run this tool against themselves locally.

On that path today, `SetUiSync()` (`VhcGui.axaml.cs:316-384`) sets `_modeCheckFailed = true`
when `ModeCheck()` returns `"fail"` and no non-localhost server is persisted. `SetUiAsync()`
(`:386-423`), running from the window's `Loaded` handler, then shows a single OK-only error
dialog ("No Veeam Software detected on this machine...") and calls
`desktop.Shutdown()` — the window renders but is immediately torn down. There is no way to
reach `ManageServersDialog` (the "Manage Servers" button lives on the same window that's
about to close) to add a remote host, so the only way forward is relaunching from the command
line with `/remote /host=...`.

This stage closes that gap: give the user a way to add a remote server from inside the
cold-start dialog itself, and — if they do — continue into the normal Remote Mode flow in the
same launch, with no relaunch required.

## Design

### Control flow

Replace the `_modeCheckFailed` branch in `SetUiAsync()` with:

1. Show a Yes/No confirm (`IUiNotifier.ConfirmAsync`, existing primitive, no interface
   change) explaining the situation and asking whether to add a remote server now.
2. If **Yes**: open the existing `ManageServersDialog` (the same one `manageServersBtn_Click`
   already uses), owned by `this` — valid because `AvaloniaHost.MainWindow` is already
   assigned in the `VhcGui` constructor (`:120`), before `SetUiSync`/`SetUiAsync` ever run.
   `LocalhostIsInjected` is always `false` on this path (mode check only fails when neither
   `CGlobals.IsVbrInstalled` nor `CGlobals.IsVb365` is true), so `pinned` is always empty —
   same shape `manageServersBtn_Click` uses when not injecting.
3. If the dialog is committed, re-resolve `_persistedServers` via the same
   `CAppSettings.LoadOrSeedServers(CredentialStore.GetAllServers(), excludeLocalhost:
   LocalhostIsInjected)` call already used elsewhere — using the property rather than a
   literal `false`, matching every other call site's style, even though it is always `false`
   on this path.
4. Evaluate the shared `CAppSettings.HasNonLocalhostServer(_persistedServers)` predicate (see
   below). If still `false` — user declined the confirm, cancelled the dialog, or committed
   with net zero non-localhost servers — fall through to today's exact shutdown behavior
   (`desktop.Shutdown()` + `return`). No new "silent quit" path; every non-success route
   converges here.
5. If `true`: set `this.Title = "Veeam Health Check - Remote Mode"` and log, matching
   `SetUiSync`'s existing pre-persisted Remote Mode branch; call
   `InitializeServerList(preserveSelection: false)` to repopulate the server picker (it was
   populated with an empty list at construction time); then fall through into the rest of
   `SetUiAsync` unchanged (`Task.Run(PreRunCheck)`, `scrubBox`/`RescanBox` defaults, etc.) —
   the same tail every pre-persisted Remote Mode launch already executes.

`SetUiText()` still runs unconditionally at the top of `SetUiAsync()` before this branch
(Stage D's fix, `:393`) — unchanged, and sufficient, since it already runs before any of the
above.

### Shared predicate: `CAppSettings.HasNonLocalhostServer`

`SetUiSync()` currently has its own inline `_persistedServers.Any(s => !s.Equals("localhost",
...))` check (`:350-351`). Stage E's recovery branch needs the identical check. Rather than
duplicate it, add:

```csharp
public static bool HasNonLocalhostServer(IEnumerable<string> servers) =>
    servers.Any(s => !string.Equals(s, "localhost", StringComparison.OrdinalIgnoreCase));
```

to `CAppSettings` (`Startup/CAppSettings.cs`) — the existing Avalonia-free, tested home for
server-list logic (`NormalizeServers`, `LoadOrSeedServers`, etc.). Update `SetUiSync()`'s
inline check to call it too, so both call sites share one implementation. This is the only
piece of Stage E's logic that's directly unit-testable in this sandbox; the surrounding
Avalonia code-behind has zero coverage here (per prior stages' `reference_avalonia_gui_cannot_render_in_sandbox`).

### Dialog content and new resx keys

`NotifierDialog` (the Avalonia backing for `ConfirmAsync`/`ShowErrorAsync`) has fixed,
hardcoded-English Yes/No/OK buttons — not resx-backed, and out of scope for this stage (an
unrelated, pre-existing gap; changing it would be scope creep). Only the message and title we
pass in are ours to localize.

The current cold-start message and title (`VhcGui.axaml.cs:397-402`) are raw C# string
literals today — not resx keys, despite Stage D's localization sweep. (Likely missed because
it's a local variable inside a method body, not a XAML/`Content` assignment, which is what
Stage D's scan appears to have targeted.) Stage E resx-backs them as part of rewriting the
copy.

New keys, added with English content to all four locale files, matching Stage D's established
parity pattern (English content only, no new translation):

- `GuiNoVeeamDetectedTitle` = `"Veeam Software Not Detected"` (unchanged from today)
- `GuiNoVeeamDetectedMessage` =
  `"No Veeam Software detected on this machine.\n\nThis tool requires Veeam Backup & Replication (VBR) or Veeam Backup for Microsoft 365 (VB365) to be installed locally, or a remote server to connect to.\n\nWould you like to add a remote server now?\n\nAlternatively, close this window and run from the command line with: VeeamHealthCheck.exe /remote /host=your-vbr-server"`

Call site becomes:

```csharp
bool wantsToAddServer = await CGlobals.Notifier.ConfirmAsync(
    VbrLocalizationHelper.GuiNoVeeamDetectedMessage,
    VbrLocalizationHelper.GuiNoVeeamDetectedTitle);
```

matching the existing `VbrLocalizationHelper.GuiXxx` static-property pattern.

The pre-existing `"Veeam Health Check - Remote Mode"` title string stays a raw literal —
untouched, pre-existing behavior; localizing it now would be unrelated scope creep for a
stage that isn't primarily a localization sweep.

Implementation note (verify, don't assume): confirm the existing Stage D resx guard test
picks up new keys generically (by scanning key parity across locale files) rather than via a
hardcoded key list, so it doesn't need editing to cover `GuiNoVeeamDetectedTitle`/
`GuiNoVeeamDetectedMessage`.

### Non-goal, re-examined and confirmed still a non-goal

Stage C's spec deferred "no proactive credential-entry UI" and flagged (in this repo's
memory, not a written spec) that closing the cold-start gap "would force revisiting" that
non-goal, since the lazy `CredentialPromptWindow` also seemed unreachable before the window
renders. On inspection this turns out not to be true: `AvaloniaCredentialPrompter.PromptAsync`
and `ManageServersDialog` both only need `AvaloniaHost.MainWindow` as their `ShowDialog` owner,
and that's assigned in the `VhcGui` constructor (`:120`) — before `SetUiSync`/`SetUiAsync` run
at all. The existing lazy credential-prompt flow requires no changes; Windows verification
scenario 5 below confirms this empirically rather than trusting the reasoning alone.

## Testing plan

**Unit tests** (new, in `CAppSettingsTests.cs`, Avalonia-free): `HasNonLocalhostServer` —
empty list → `false`; localhost-only (mixed casing) → `false`; a real host present → `true`;
mixed localhost + real host → `true`.

**Windows verification** (real hardware, folding in Stage D's still-owed checklist per the
prior handoff, since both need the same kind of machine access):

1. Cold start: no local Veeam, no persisted servers, no stored credentials → confirm dialog
   shows the new message/title. **Decline** → shuts down exactly as today.
2. Same start, **accept** → `ManageServersDialog` opens; add a host, commit → window enters
   Remote Mode (title updates, picker shows the new host), Run becomes usable, `PreRunCheck`
   runs without deadlocking.
3. Accept, then cancel the dialog with nothing added → falls through to shutdown.
4. Accept, add then remove a host before committing (net zero) → falls through to shutdown.
5. After adding a host via this path and starting a run, confirm `CredentialPromptWindow`
   still appears at the expected point — empirically verifies the non-goal re-examination
   above.
6. A non-English locale pass, confirming the two new resx keys resolve cleanly (no broken
   lookup or missing-key fallback) in at least one of `fR-FR`/`ja`/`zh-cn`/`zh-tw`. This can
   piggyback on Stage D's still-open "any non-English rendering pass at all" item rather than
   being a separate trip.

Stage D's remaining checklist items not superseded by the above (the notif type/severity
correctness-trap fix under a non-English locale, and the French SOBR locale-file spot-check)
are unrelated to Stage E and stay tracked separately — not silently dropped, not folded in
here.

## Out of scope

- Any change to `NotifierDialog`'s own button localization.
- Localizing the pre-existing `"Veeam Health Check - Remote Mode"` title string.
- Approach 2 from brainstorming (removing the blocking dialog entirely in favor of an
  always-rendered degraded main window with Run-button gating) — a legitimate future
  enhancement, not required to close this specific reachability gap.
- Any change to `ManageServersDialog`, `ServerListEditor`, or `ServerListCommitter` — reused
  as-is.

## Redactions

None needed — no credentials, API keys, or PII in this design conversation.

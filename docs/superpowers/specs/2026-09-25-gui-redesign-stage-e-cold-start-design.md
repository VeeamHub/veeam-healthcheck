# GUI Redesign Stage E: Remote-GUI / VSA Cold-Start Recovery — Design

**Branch:** `stage-e/cold-start-recovery`, off `feature/gui-redesign-port` @ `dedb71e4` (Stage D, PR #231). Already created as a worktree at `.claude/worktrees/gui-redesign-stage-e`.

**Revision note:** this spec was independently reviewed by a fresh agent before implementation
planning started. The review verified every code claim against the actual current files
rather than trusting the first draft, and found several real errors, which this revision
corrects. See inline notes for what changed.

## Context

Stage C introduced "Remote Mode": on a machine with no local VBR/VB365 process detected, if
at least one remote server is already persisted in `settings.json`, the GUI still renders and
lets the user work against that server. It identified, but explicitly deferred, a harder
case: a machine with no local Veeam **and** zero persisted servers — the cold-start case on a
console-only/VSA-adjacent management box, an increasingly common real deployment pattern
since VSA appliances can't run this tool against themselves locally.

On that path today, `SetUiSync()` (`VhcGui.axaml.cs:316-384`) sets `_modeCheckFailed = true`
when `ModeCheck()` returns `"fail"` and `_persistedServers` contains **no entry other than
`"localhost"`** — i.e. it's empty, or it only contains a stray `"localhost"` entry (possible if
the credential store or settings.json retained one from an earlier state, e.g. a machine that
used to have local Veeam installed, or a `/savecreds` run against the default host). This is
the precise trigger, not simply "zero persisted servers" — the distinction matters for the
recovery flow below. `SetUiAsync()` (`:386-404`), running from the window's `Loaded` handler, then shows a single OK-only error
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

0. Immediately on entering the branch, before showing any dialog: call `run.IsEnabled = false;
   this.hideProgressBar();` — the same two calls `SetUiSync()`'s success tail already makes
   (`:382-383`), but which the early `return` in the fail branch (`:360-361`) currently skips
   entirely. Today that's invisible because the window shuts down within a frame or two of
   showing the OK dialog. Once the confirm + `ManageServersDialog` interaction below can take
   real, human-paced time, the window behind them would otherwise sit at its XAML defaults —
   Run enabled, progress bar spinning — for as long as the user takes to respond. Calling these
   here is safe regardless of which way the branch below resolves.
1. Show a Yes/No confirm (`IUiNotifier.ConfirmAsync`, existing primitive, no interface
   change) explaining the situation and asking whether to add a remote server now.
2. If **Yes**: open the existing `ManageServersDialog` with
   `new ManageServersDialog(initial: _displayServers.ToList(), pinned: Array.Empty<string>())`
   — the exact same `initial`/`pinned` shape `manageServersBtn_Click` (`:1013-1032`) uses when
   not injecting localhost, and deliberately spelled out here rather than left implicit:
   passing anything other than `_displayServers.ToList()` as `initial` would make
   `ManageServersDialog`'s commit overwrite `settings.json`'s server list with an incomplete
   one, silently dropping whatever was already persisted (including a stray `"localhost"` —
   see Context above). `ShowDialog<bool>(this)` is valid because `AvaloniaHost.MainWindow`
   is already assigned in the `VhcGui` constructor (`:120`), before `SetUiSync`/`SetUiAsync`
   ever run. `LocalhostIsInjected` is always `false` on this path (`ModeCheck()`'s fail
   condition, `CClientFunctions.cs:126`, is `!CGlobals.IsVb365 && !CGlobals.IsVbr`, and
   `IsVbrInstalled` is set alongside `IsVbr` at `:105-106`, so `LocalhostIsInjected` —
   `IsVbrInstalled || IsVb365` — is false whenever the fail branch is reached), so `pinned` is
   always empty.
3. If the dialog is committed (`ShowDialog<bool>` returns `true` — this happens on every Done
   click, including one where nothing changed; Cancel and the window close button both return
   `false` and write nothing), re-resolve `_persistedServers` via the same
   `CAppSettings.LoadOrSeedServers(CredentialStore.GetAllServers(), excludeLocalhost:
   LocalhostIsInjected)` call already used elsewhere.
4. Evaluate the shared `CAppSettings.HasNonLocalhostServer(_persistedServers)` predicate (see
   below). If still `false` — user declined the confirm, cancelled the dialog, or committed
   with net zero non-localhost servers — fall through to today's exact shutdown behavior
   (`desktop.Shutdown()` + `return`). No new "silent quit" path; every non-success route
   converges here.
5. If `true`: set `this.Title = "Veeam Health Check - Remote Mode"` and log, matching
   `SetUiSync`'s existing pre-persisted Remote Mode branch; call
   `InitializeServerList(preserveSelection: false)` to repopulate the server picker (it was
   populated with an empty list at construction time). `InitializeServerList`'s own fallback
   selects `"localhost"` first when present (`:256-260`) — correct for its other two call
   sites, but wrong here: reaching this point means `HasNonLocalhostServer` is `true`, and
   selecting a lingering `"localhost"` entry over the remote host the user just added would
   set `REMOTEEXEC = false` (`:287`) and point the run straight back at the local box that has
   no Veeam installed, defeating the entire point of this recovery path. Immediately after
   `InitializeServerList`, explicitly select a non-localhost entry instead:
   ```csharp
   var remoteServer = _displayServers.FirstOrDefault(
       s => !s.Equals(LocalhostName, StringComparison.OrdinalIgnoreCase));
   serverSelector.SelectedItem = remoteServer;
   UpdateSelectedServersGlobal();
   ```
   (`remoteServer` cannot be `null` here — `HasNonLocalhostServer(_persistedServers)` was just
   confirmed `true`, and `_displayServers` is built from `_persistedServers` with
   `LocalhostIsInjected` false, so no injected-localhost row exists to interfere.) Then fall
   through into the rest of `SetUiAsync` unchanged (`Task.Run(PreRunCheck)`,
   `scrubBox`/`RescanBox` defaults, etc.) — the same tail every pre-persisted Remote Mode
   launch already executes.

`SetUiText()` still runs unconditionally at the top of `SetUiAsync()` before this branch
(Stage D's fix, `:393`) — unchanged, and sufficient, since it already runs before any of the
above.

### Shared predicate: `CAppSettings.HasNonLocalhostServer`

`SetUiSync()` currently has its own inline `_persistedServers.Any(s => !s.Equals("localhost",
...))` check (`:350-351`). Stage E's recovery branch needs the identical check. Rather than
duplicate it, add:

```csharp
public static bool HasNonLocalhostServer(IEnumerable<string> servers) =>
    servers.Any(s => !string.Equals(s, LocalhostName, StringComparison.OrdinalIgnoreCase));
```

to `CAppSettings` (`Startup/CAppSettings.cs`), reusing the class's existing private
`LocalhostName` const (`:54`) rather than a second literal `"localhost"`. This joins
`NormalizeServers`/`LoadOrSeedServers`/etc. as the Avalonia-free home for server-list logic in
this class — `NormalizeServers` itself is private and only covered indirectly today (via
`LoadOrSeedServers`'s public tests in `CAppSettingsTests.cs`), and `HasNonLocalhostServer`
should be public and given its own direct tests rather than relying on indirect coverage the
way `NormalizeServers` does. Update `SetUiSync()`'s inline check to call it too, so both call
sites share one implementation. This is the only piece of Stage E's logic that's directly
unit-testable in this sandbox; the surrounding Avalonia code-behind has zero coverage here —
this sandbox cannot render Avalonia at all, confirmed empirically on every prior stage of this
arc.

### Dialog content and new resx keys

`NotifierDialog` (the Avalonia backing for `ConfirmAsync`/`ShowErrorAsync`) has fixed,
hardcoded-English Yes/No/OK buttons — not resx-backed, and out of scope for this stage (an
unrelated, pre-existing gap; changing it would be scope creep). Only the message and title we
pass in are ours to localize.

The current cold-start message and title (`VhcGui.axaml.cs:397-404`) are raw C# string
literals today — not resx keys, despite Stage D's localization sweep. (Likely missed because
it's a local variable inside a method body, not a XAML/`Content` assignment, which is what
Stage D's scan appears to have targeted.) Stage E resx-backs them as part of rewriting the
copy.

New keys, added with English content to all four locale files, matching Stage D's established
parity pattern (English content only, no new translation):

- `GuiNoVeeamDetectedTitle` = `Veeam Software Not Detected` (unchanged from today)
- `GuiNoVeeamDetectedMessage`, as a **literal multi-line value**, not a C#-style `\n`-escaped
  one — confirmed by reading `vhcres.resx`'s existing `GuiAcceptText` entry, whose
  `<value xml:space="preserve">` holds real line breaks, not the two characters `\` `n`. A
  resx value containing the literal text `\n` would render as a visible backslash-n in the
  dialog, not a line break. The value:
  ```
  No Veeam Software detected on this machine.

  This tool requires Veeam Backup & Replication (VBR) or Veeam Backup for Microsoft 365
  (VB365) to be installed locally, or a remote server to connect to.

  Would you like to add a remote server now?

  Alternatively, close this window and run from the command line with:
  VeeamHealthCheck.exe /remote /host=your-vbr-server
  ```

Every file this touches, matching Stage D's precedent of listing them explicitly rather than
leaving "all locale files" implicit (all under `Resources/Localization/`):
- `vhcres.resx` (neutral/English source)
- `vhcres.fR-FR.resx`, `vhcres.ja.resx`, `vhcres.zh-cn.resx`, `vhcres.zh-tw.resx` (satellites —
  English content per the parity pattern, real line breaks as above, `xml:space="preserve"`)
- `vhcres.txt` — UTF-16LE/CRLF, edit via an encoding-preserving script and verify with `file
  <path>` afterward, per this arc's standing hygiene rule. Unlike the `.resx` files, `\n`
  *is* the correct escape here (confirmed against the existing `GuiAcceptText` entry in this
  file) — `VbrResFileBuilder.ps1`'s `ResGen.exe` pipeline expands it on the rare occasion
  someone runs it by hand. That pipeline is inert for the actual build (Stage D's finding),
  but Stage D still kept this file in sync with a warning comment, and Stage E should too.
- `VbrLocalizationHelper.cs` — also UTF-16LE/CRLF, same hygiene rule — add the two new
  `public static string` fields (not properties: every existing entry, e.g.
  `GuiRescanHosts`/`GuiPdfUnavailableTooltip` at `:10`/`:626`, is a plain
  `public static string X = m4.GetString("X");` field initializer, not a property)
- `untranslated-keys.txt` — convention only (no code consumer; nothing in `vHC/` or `.github/`
  reads it), but Stage D added entries here for its new keys, so Stage E should add the
  2-keys-×-4-cultures entries here too, for consistency

Call site becomes:

```csharp
bool wantsToAddServer = await CGlobals.Notifier.ConfirmAsync(
    VbrLocalizationHelper.GuiNoVeeamDetectedMessage,
    VbrLocalizationHelper.GuiNoVeeamDetectedTitle);
```

matching the existing `VbrLocalizationHelper.GuiXxx` static-field pattern.

The pre-existing `"Veeam Health Check - Remote Mode"` title string stays a raw literal —
untouched, pre-existing behavior; localizing it now would be unrelated scope creep for a
stage that isn't primarily a localization sweep.

Implementation note (verified): the existing Stage D resx guard tests
(`VbrLocalizationHelperTests.cs`) are generic — `AllStaticStrings_ResolveNonNullAndNonEmpty`
reflects over every public static string field, and `EverySatellite_HasNoOrphansAndNoUnexpectedMissingKeys`
diffs full key sets against the neutral resource rather than checking a hardcoded list. No
test file needs editing to cover the two new keys — but the satellites must actually receive
them, or the second test fails.

### Non-goal, re-examined and confirmed still a non-goal

Stage C's spec (`docs/superpowers/specs/2026-09-07-gui-redesign-stage-c-interaction-model-design.md:40`)
deferred "no proactive credential surface in the GUI," on the assumption that closing the
cold-start gap would force revisiting that non-goal, since the lazy `CredentialPromptWindow`
also seemed unreachable before the window renders. On inspection this turns out not to be
true: `AvaloniaCredentialPrompter.PromptAsync` and `ManageServersDialog` both only need
`AvaloniaHost.MainWindow` as their `ShowDialog` owner, and that's assigned in the `VhcGui`
constructor (`:120`) — before `SetUiSync`/`SetUiAsync` run at all. The existing lazy
credential-prompt flow requires no changes; Windows verification scenario 7 below confirms
this empirically rather than trusting the reasoning alone.

## Testing plan

**Unit tests** (new, in `CAppSettingsTests.cs`, Avalonia-free): `HasNonLocalhostServer` —
empty list → `false`; localhost-only (mixed casing) → `false`; a real host present → `true`;
mixed localhost + real host → `true`.

**Windows verification** (real hardware, folding in Stage D's still-owed checklist per the
prior handoff, since both need the same kind of machine access):

1. Cold start: no local Veeam, no persisted servers, no stored credentials → confirm dialog
   shows the new message/title, Run disabled and progress bar hidden behind it (point 0 in
   Control flow). **Decline** → shuts down exactly as today; nothing written to
   `settings.json`.
2. Same start, **accept** → `ManageServersDialog` opens; add a host, commit → window enters
   Remote Mode (title updates, picker shows and selects the new host — not `"localhost"`,
   verifying the selection fix in Control flow point 5), Run becomes usable, `PreRunCheck`
   runs without deadlocking.
3. Accept, then **Cancel** the dialog with nothing added → falls through to shutdown; nothing
   written to `settings.json` (`ShowDialog<bool>` returns `false`, `SetServers` is never
   called).
4. Accept, then press **Done with nothing changed** (no host added) → falls through to
   shutdown, same as decline/cancel — but unlike scenario 3, this *does* write
   `"Servers": []` to `settings.json` (`ManageServersDialog`'s Done path always calls
   `CAppSettings.SetServers` on a successful commit, regardless of whether anything changed).
   Worth confirming this doesn't wrongly convert a previously-seeded state into a permanent
   "user emptied it" signal in a way that surprises on the next launch — pre-existing
   `ManageServersDialog` behavior, not something Stage E introduces, but this is the first
   path that reaches it without the user explicitly having opened Manage Servers on purpose.
5. Accept, add then remove a host before committing (net zero) → same outcome and same
   `"Servers": []` write as scenario 4.
6. Cold start where `_persistedServers` already contains a stray `"localhost"` (e.g. seeded
   from a `/savecreds` run against the default host, or a credential left over from when this
   machine had local Veeam installed) → same as scenario 1's dialog appears (the trigger is
   "no non-localhost server", not "zero servers" — see Context); accepting and adding a real
   host must still select and target that host, not `"localhost"`.
7. After adding a host via scenario 2 or 6 and starting a run, confirm `CredentialPromptWindow`
   still appears at the expected point — empirically verifies the non-goal re-examination
   above.
8. A non-English locale pass, confirming the two new resx keys resolve cleanly (no broken
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

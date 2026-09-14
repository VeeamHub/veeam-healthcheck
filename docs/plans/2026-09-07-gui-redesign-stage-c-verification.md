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
- [ ] Check the box, and before the disclaimer modal resolves, click **Import**: once the modal is answered (either way), `run.IsEnabled` is not silently flipped back on and the checkbox is not silently re-enabled — the in-flight accept flow must recognize it's now stale and do nothing observable. (Task 9's `_guiLockedForRun` race fix — reachable specifically via Import, which does not itself wait on terms acceptance.)

## Manage Servers
- [ ] Gear button opens the dialog; its tooltip is localized.
- [ ] Add appends a row; adding a duplicate (any casing) shows the duplicate error; adding blank shows the input error.
- [ ] Clicking remove leaves the row visible, struck through, with an undo control.
- [ ] The undo control is legible **without** hovering the row.
- [ ] **Hover the undo control specifically.** `Button.undo-server:pointerover /template/ ContentPresenter` (`App.axaml`, Task 7) resets `Background` but not `Foreground` — matching the pre-existing `Button.link` pattern it was modeled on, but unverified on real hardware. If FluentTheme's default hover foreground competes with `AccentBrush` and the glyph looks off-color or low-contrast on hover, add a `Foreground` setter to that rule the same way Stage B's `Button.tab` fix needed one.
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
- [ ] Corrupt or otherwise make undecryptable a stored credential's DPAPI blob (e.g. edit the encrypted file on disk, or move the profile to another machine), then open the dialog: that row still shows the "has credentials" marker (defaults to true on `CryptographicException` rather than silently reporting no credentials) — then Remove it and confirm it's actually cleaned up (`CredentialStore.Remove` never decrypts, so cleanup itself does not throw).
- [ ] Make a credential removal genuinely fail (e.g. revoke write permission on the credential file mid-session), then Done: the "Removal Incomplete" notice names the specific reinstated host, and that host is still present in the picker afterward — not silently dropped.
- [ ] Make the settings save itself fail (e.g. revoke write permission on `settings.json`) and press Done: an error is shown, Done becomes **permanently** disabled for the rest of the dialog's life, and Cancel + reopening the dialog is the only way to retry (a fresh dialog re-queries credentials per row).
- [ ] With the summary confirm open, click the dialog's own OS title-bar close button (not Cancel): it does nothing while the confirm is in flight — the close is blocked, not merely visually disabled. Answer the confirm, then confirm the OS close button works normally afterward.

## Persistence
- [ ] Add a server, restart: it is still there.
- [ ] Remove a server, restart: it stays removed.
- [ ] Remove every removable server, restart: the list stays empty rather than repopulating from stored credentials.
- [ ] `/savecreds` against a new host, then launch the GUI: the host appears in the picker.
- [ ] `/savecreds` against `localhost` on a machine with a local product: exactly **one** `localhost` row, not two.
- [ ] Upgrade path: with pre-existing stored credentials and no `Servers` key in `settings.json`, the first launch shows all previously-credentialed servers.

## Period pills
- [ ] All three select correctly and the log shows the matching `Interval set to 7|30|90`.
- [ ] "7 Days" is selected on launch **when no `/days:N` argument is given**. (This is the no-override case only — see the CLI-override item below, which selects a different pill on purpose.)
- [ ] Launch with `/days:30`: the 30 pill (not 7) is selected on open, and the log reads `Interval set to 30`, not `Interval set to 7`. Repeat for `/days:90`. (Task 10's CLI-stomping regression fix — the pills' `IsChecked="True"` on the 7-day option fires synchronously during construction and would otherwise silently overwrite the CLI value.)
- [ ] Launch with `/days:12` (or any value with no matching pill): no pill appears selected, and the log reads `Interval set to 12` — not `7`, and not silently defaulting without a log entry.
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

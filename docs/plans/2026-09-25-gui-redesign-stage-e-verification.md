# Stage E — Windows verification checklist

This sandbox cannot render Avalonia (native platform bootstrap crash). Everything below
needs a real Windows machine, and most items specifically need a machine (or a VM) with
**no local VBR/VB365 installed** to reach the cold-start path at all.

## Core recovery flow (highest priority — verify first)

- [ ] Cold start: no local Veeam, no persisted servers, no stored credentials in
      CredentialStore. Confirm dialog shows the new message/title. Confirm Run is
      disabled and the progress bar is not spinning behind the dialog (this used to be
      harmless because the app shut down within a frame or two — Stage E is what makes
      it visible for the first time).
- [ ] **Decline** the confirm → app shuts down exactly as before this stage. Confirm
      nothing was written to `settings.json`.
- [ ] Same cold start, **accept** the confirm → `ManageServersDialog` opens. Add a host,
      click Done → window enters Remote Mode: title changes to "Veeam Health Check -
      Remote Mode", the server picker shows and **selects** the new host (not
      "localhost"), Run becomes enabled, and a run completes without deadlocking on
      `PreRunCheck`.
- [ ] Accept, then **Cancel** the `ManageServersDialog` with nothing added → falls
      through to shutdown, same as declining. Confirm nothing was written to
      `settings.json`.
- [ ] Accept, then press **Done with nothing changed** (no host added, none removed) →
      falls through to shutdown, same outward behavior as Cancel — but confirm
      `settings.json` now has an explicit `"Servers": []`, unlike the Cancel case. This
      is pre-existing `ManageServersDialog` behavior, not new to this stage, but this is
      the first path that reaches it without the user having deliberately opened Manage
      Servers.
- [ ] Accept, add a host, then remove it again before clicking Done (net zero) → same
      outcome and same `"Servers": []` write as the item above.
- [ ] Seed `settings.json` (or the credential store) with a stray `"localhost"` entry
      before cold start (e.g. via a `/savecreds` run against the default host on a
      machine with no local Veeam, or by hand-editing `settings.json`). Confirm the
      cold-start dialog still appears (the trigger is "no *non-localhost* server", not
      "zero servers"). Accept, add a real host, commit — confirm the picker selects and
      targets the real host, **not** `"localhost"`, and that a run actually reaches the
      remote server rather than failing against the local, Veeam-less box.
- [ ] **Quit and relaunch** after the item above (still no local Veeam, `settings.json`
      now has `["localhost", "<the host you added>"]`). Confirm the picker still selects
      and targets the real host on this second launch too, not `"localhost"` — this
      exercises `SetUiSync`'s pre-existing "already has remote servers" branch, which
      this stage's own recovery path never runs on a second launch (`_modeCheckFailed`
      stays `false`), so `InitializeServerList`'s own fallback ordering is what has to
      get this right, not `SetUiAsync`'s explicit override. A code-quality review during
      implementation (Task 4) found and fixed a real regression here — this item exists
      specifically to catch it on real hardware if the fix regresses.
- [ ] After successfully adding a host via any of the above and starting a run, confirm
      the lazy credential prompt (`CredentialPromptWindow`) still appears at the
      expected point if that host has no stored credentials yet.

## Localization

- [ ] With Windows display language set to French, Japanese, Simplified Chinese, or
      Traditional Chinese, trigger the cold-start dialog and confirm the message and
      title render (English content is expected and correct — these two keys are not
      translated yet, same policy as every other Stage D/E key added this way) rather
      than falling back to a raw resx key name or throwing.

## Recorded, not fixed (informational only — no action needed)

- `NotifierDialog`'s Yes/No/OK button labels are hardcoded English, not resx-backed.
  Pre-existing, unrelated to this stage, deliberately out of scope (see the spec's "Out
  of scope" section).
- The `"Veeam Health Check - Remote Mode"` title string stays a raw C# literal,
  unlocalized — pre-existing, deliberately out of scope for the same reason.
- Still owed from Stage D, unrelated to this stage's changes: the notif type/severity
  correctness-trap fix under a non-English locale, and the French SOBR locale-file
  spot-check (`docs/plans/2026-09-15-gui-redesign-stage-d-verification.md`).

# GUI Redesign Stage D: Localization Sweep — Design

**Branch:** `stage-d/localization`, off `feature/gui-redesign-port` @ `a5e9d74a` (Stage C, PR #223).

## Context

Stages A–C ported and restructured the Avalonia GUI's theme, layout, and interaction model.
Throughout, localization coverage was tracked but not swept: Stage C's own spec quantified
the gap as "11 hardcoded `ToolTip.Tip` attributes and 36 hardcoded `Text`/`Content` attributes
against 9 resx-driven assignments in `SetUiText()`," explicitly deferred as a Stage D non-goal.

That number is stale by definition — it predates Stage C's own real-hardware-fix commits and
was never recounted. This spec recounts the gap against current `HEAD` and scopes the sweep
to close it, plus a correctness trap discovered during recounting that must be fixed as part
of it.

**The gap, recounted at `a5e9d74a`:**

- `SetUiText()` now has **13** resx-driven `Content`/`Text` assignments (up from 9) + 2
  resx-driven tooltips = 15 total resx-driven, but it also has **2 hardcoded strings still
  sitting inside it**: `pdfCheckBox.Content = "Export PDF"` and
  `clearCredsCheckBox.Content = "Clear Saved Credentials"` (`VhcGui.axaml.cs:450,452`).
- XAML-declared: **10** hardcoded `ToolTip.Tip`, **~32** hardcoded `Text`/`Content` attributes
  in `VhcGui.axaml` (down slightly from Stage C's 11/36 — a couple of strings were touched by
  Stage C's hardware fixes without being resx-backed).
- **Not counted by Stage C's spec at all:** **15** hardcoded `Text`/`Content` assignments in
  code-behind (`VhcGui.axaml.cs`) — dynamic status/progress messages such as
  `monitorStatusText.Text = "Setup failed — check log"` and
  `progressText.Text = $"Collection complete — {failed.Count} collector warning(s)"`. These
  are ~13 distinct strings once duplicated sites are deduplicated (see Design §3).

**The correctness trap** (found while recounting, not previously flagged as urgent): three
sites in `VhcGui.axaml.cs` read a `ComboBoxItem`'s currently-*displayed* `Content` text as a
backend protocol value:

- `GetNotifSettings()` (`VhcGui.axaml.cs:1071,1073`):
  `(notifTypeBox.SelectedItem as ComboBoxItem)?.Content?.ToString()?.ToLower() ?? "ntfy"` and
  the equivalent read of `notifSeverityBox` for `minSeverity`.
- `notifTypeBox_SelectionChanged` (`VhcGui.axaml.cs:1170`): the same pattern.

If Stage D localizes `notifTypeBox`/`notifSeverityBox` labels without fixing this first,
notification delivery breaks silently in any non-English locale — the app would send a
notification type or severity using translated text as a literal protocol value.

The fix pattern already exists once in this exact file. `PeriodRadio_Checked`
(`VhcGui.axaml.cs:936`) reads `(sender as RadioButton)?.Tag`, not `Content`, precisely
because — per the comment already there — "Content is localized, and parsing a localized
label as data is exactly the mistake `notifSeverityBox` already makes." Stage D applies that
same `Tag`-based pattern to the two notif boxes.

**The localization pipeline is not what it looks like.** The Localization folder contains a
hand-run script (`VbrResFileBuilder.ps1`) that launches Visual Studio, runs the legacy
`ResGen.exe` against `.txt` files, and regenerates `VbrLocalizationHelper.cs` and loose
`.resources` binaries — all against a hardcoded path (`A:\source\veeam-healthcheck\...`) that
doesn't match any known dev machine. This looked like it might be load-bearing and
Windows/VS-only. It isn't: a plain cross-platform `dotnet build` in this repo already
auto-compiles `vhcres.*.resx` into proper culture satellite assemblies via the SDK's default
`.resx` → `EmbeddedResource` convention — confirmed by inspecting
`bin/Debug/net8.0-windows7.0/{fR-FR,ja}/VeeamHealthCheck.resources.dll`, both freshly built by
a plain `dotnet build vHC/HC.sln`. `VbrResFileBuilder.ps1`, `ResGen.exe`, and the checked-in
loose `.resources` files predate the SDK-style project migration and are dead weight, not the
real pipeline. This means every part of this sweep — including translated-locale edits — is
an ordinary cross-platform resx edit, buildable and testable in a non-Windows sandbox.

**Translations already exist but are effectively unreachable today.** Four satellite resx
files exist with real content: `vhcres.fR-FR.resx`, `vhcres.ja.resx`, `vhcres.zh-cn.resx`,
`vhcres.zh-tw.resx`. But nothing in the codebase ever sets `CultureInfo.CurrentUICulture` —
there is no in-app language picker. `ResourceManager.GetString()` resolves against whatever
the OS's UI culture happens to be, so these translations only ever surface on a machine whose
Windows display language already matches one of the four. Compounding this,
`VbrLocalizationHelper`'s fields are `public static string X = m4.GetString(...)` —
plain static-field initializers, evaluated exactly once at class load. Even with a picker,
nothing today could re-localize at runtime without a larger refactor. That refactor is not
Stage D (see Non-goals).

Stage C's keys were also never backfilled into the four satellite locales — they exist only
in the neutral `vhcres.resx`. Measured key counts: neutral `vhcres.resx` has 598 `<data>`
entries; `vhcres.fR-FR.resx` has 551 (47 missing); `vhcres.ja.resx`, `vhcres.zh-cn.resx`, and
`vhcres.zh-tw.resx` each have 560 (38 missing each).

## Goals

- Fix the `notifTypeBox`/`notifSeverityBox` `Content`-as-data correctness trap.
- Resx-back every remaining hardcoded, translatable UI string in `VhcGui.axaml` and its
  code-behind `VhcGui.axaml.cs`, including the two hardcoded strings already sitting inside
  `SetUiText()`.
- Backfill the resulting new keys — Stage D's own plus Stage C's already-known gap — into all
  four existing satellite locales with best-effort translated content.
- Add a mechanical guard against silent null/empty localization failures, since this sandbox
  cannot render Avalonia to catch a blank label visually.

## Non-goals (explicitly deferred)

- **Icon glyphs and brand names.** `&#x2699;` (a gear glyph used as button content) and the
  notif-type brand names (`Teams`, `Slack`, `PagerDuty`, `ntfy`) are not translatable language
  content. They stay hardcoded in XAML/`Tag` values. Documented here explicitly so a future
  recount does not re-flag them as remaining debt.
- **A locale picker / runtime language switching.** Blocked by `VbrLocalizationHelper`'s
  static-field-initializer pattern, which resolves every string exactly once at class load.
  Introducing runtime switching means converting those fields to properties (or another
  indirection) and adding UI to select a culture — real, separate scope. The hazard this
  creates is *latent, not live*: nothing today sets `CurrentUICulture`, so there is no code
  path that could hit ordering problems from touching `VbrLocalizationHelper` before culture
  is set. Do not let this scope-creep into Stage D.
- **Removing the vestigial `ResGen.exe` pipeline.** `VbrResFileBuilder.ps1` and the loose
  `.resources` files are confirmed dead weight (see Context), but cleaning them up is a
  separate, low-risk chore, not part of this sweep.
- **Translation quality assurance.** New and backfilled non-English strings are best-effort —
  produced without a certified translator in the loop. They should be flagged for
  native-speaker review before shipping, not treated as final copy.
- **Localizing `status.Summary`'s own content.** `monitorLastRunText.Text = $"Last run:
  {status.Timestamp:g} — {status.Summary}"` gets its wrapper localized (`"Last run: {0} —
  {1}"`), but `status.Summary` itself is sourced from elsewhere in the codebase and stays
  English. Localizing that payload is out of scope here.

## Design

### 1. Fix the notif-box correctness trap (must land before either box's `Content` is touched)

Add `Tag` attributes to every `ComboBoxItem` under `notifTypeBox` and `notifSeverityBox` in
`VhcGui.axaml`, carrying the existing English/protocol values verbatim (`"ntfy"`, `"Teams"`,
`"Slack"`, `"PagerDuty"` for type; `"ok"`, `"warning"`, `"critical"` for severity — matching
current `Content` values exactly, so behavior is provably unchanged). `Content` stays
hardcoded English at this point — it gets resx-backed in §2, after this fix lands and is
verified independently.

Repoint all three read sites at `Tag` instead of `Content`:

- `GetNotifSettings()` (`VhcGui.axaml.cs:1071`): `notifType` reads
  `(notifTypeBox.SelectedItem as ComboBoxItem)?.Tag?.ToString()?.ToLower() ?? "ntfy"`.
- `GetNotifSettings()` (`VhcGui.axaml.cs:1073`): `minSeverity` reads
  `(notifSeverityBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "warning"`.
- `notifTypeBox_SelectionChanged` (`VhcGui.axaml.cs:1170`): same `Tag` read.

This is a pure correctness fix with no visible behavior change — same values, different
source attribute. It is provably safe to land and commit standalone, ahead of any
localization work touching these two controls.

### 2. XAML sweep

Resx-back every hardcoded `Text`, `Content`, and `ToolTip.Tip` attribute in `VhcGui.axaml`
except the documented non-goals (icon glyph, notif-type brand names — which after §1 are
`Tag`-sourced and can keep hardcoded `Content`). This includes the ProductType combobox
items (`"Auto-detect"`, `"VBR (Backup & Replication)"`, `"VB365 (Backup for Microsoft 365)"`,
`"Both"`) — confirmed safe to localize freely, since `productTypeSelector_SelectionChanged`
already reads `SelectedIndex`, never `Content` (`VhcGui.axaml.cs:1013-1024`).

New keys follow the existing `Gui`-prefixed naming convention in `VbrLocalizationHelper.cs`
(e.g. `GuiNotifTypeHeader`, `GuiMinSeverityLabel`, `GuiProductTypeAuto`).

### 3. Code-behind sweep

Resx-back the hardcoded `Text`/`Content` assignments in `VhcGui.axaml.cs`, including the two
already inside `SetUiText()` (`pdfCheckBox.Content`, `clearCredsCheckBox.Content`) and the
monitor-status/progress messages. Two of these strings appear at two call sites each and
must map to **one shared resx key**, not be duplicated:

- `"Setup failed — check log"` — `VhcGui.axaml.cs:1115` and `:1142`.
- `"Available — not set up"` — `VhcGui.axaml.cs:1046` and `:1193`.

Interpolated strings become format-string resx entries with numbered placeholders:

- `$"Running ({version})"` → `"Running ({0})"`, formatted with `version`.
- `$"Last run: {status.Timestamp:g} — {status.Summary}"` → `"Last run: {0} — {1}"`, formatted
  with `status.Timestamp.ToString("g")` and `status.Summary` (the latter stays English content
  — see Non-goals).
- `$"Collection complete — {failed.Count} collector warning(s)"` → `"Collection complete —
  {0} collector warning(s)"`. The English-only `"(s)"` pluralization dodge is kept as-is;
  resx has no plural-rule support and this string has exactly one call site, so introducing
  proper plural handling here is not worth the complexity. Each locale's translated string
  can phrase pluralization however is natural in that language.

### 4. Backfill existing locales

Add every new key from §1–§3, plus Stage C's already-known un-backfilled keys, to
`vhcres.fR-FR.resx`, `vhcres.ja.resx`, `vhcres.zh-cn.resx`, and `vhcres.zh-tw.resx`, with
best-effort translated values. Verify via `dotnet build` that each locale's satellite
resources DLL is regenerated and grows to match the neutral resx's key count.

### 5. Guard test

Add a `VhcXTests` fact that reflects over every `public static string` field on
`VbrLocalizationHelper` and asserts each one is non-null and non-empty under the neutral
(build/test-default) culture. This is cheap, runs cross-platform, and is the only check that
would catch a typo'd resx key before it ships as a blank label — this sandbox cannot render
Avalonia to catch that visually, and a build/compile-time check would not catch it either,
since `ResourceManager.GetString()` returns `null` silently on a missing key rather than
throwing.

## Implementation notes / hazards

- **`GetString()` returns `null` on a missing key; it does not throw.** A single mismatch
  between a `VbrLocalizationHelper` property name and its resx key produces an empty
  `Content`/`Text` — no exception, no build error. The guard test in §5 is the only
  backstop; treat every new key addition as needing that test to pass before considering the
  task done.
- **UTF-16LE encoding-preserving edits.** `VbrLocalizationHelper.cs` and `vhcres.txt` are
  UTF-16LE with CRLF and must be edited via encoding-preserving scripts, never a plain
  text-editing tool. Verify with `file <path>` after every edit (must still say "Unicode
  text, UTF-16, little-endian") — but note `file` only confirms the encoding, not that
  specific characters survived the round-trip. Several of the code-behind strings contain
  em-dashes (`—`); diff the actual rendered value after edits, not just the encoding.
  `vhcres.resx` and the four satellite `.resx` files are plain UTF-8 — no special handling
  needed.
- **Casing mismatch, harmless.** `vhcres.fR-FR.resx` (mixed-case culture segment) vs. a
  differently-cased `vhcres.FR-FR.resources` binary sitting alongside it — .NET culture
  lookup is case-insensitive, so this does not affect resolution. Noted here so a future
  recount does not flag it as a bug.
- **Sites vs. keys.** When counting "how much debt is left" at any point in this sweep, count
  distinct keys, not call sites — §3's two duplicated strings mean 15 code-behind sites map
  to 13 keys.
- **`VeeamHealthCheck.csproj` auto-increments on every build/test.** Always
  `git checkout --` that exact file before committing — never anything else.

## Testing / verification

- `dotnet test vHC/VhcXTests/VhcXTests.csproj` — includes the new guard test (§5); must pass
  cross-platform.
- `dotnet build vHC/HC.sln -c Debug` — confirms satellite resource DLLs regenerate for all
  four locales with the new key counts, verifiable in this sandbox via
  `bin/Debug/net8.0-windows7.0/{fR-FR,ja,zh-CN,zh-tw}/VeeamHealthCheck.resources.dll`.
- Manual, Windows-only (this sandbox cannot render Avalonia): confirm no blank labels/tooltips
  render in the main window and the monitor-status panel, in both the neutral culture and at
  least one of the four backfilled locales (set via OS display language). Confirm the notif
  dropdowns still send the correct protocol values after §1's fix (send a real ntfy/Teams
  notification and check the payload).

## Recorded, not fixed

- The static-field-initializer pattern in `VbrLocalizationHelper` blocks any future
  runtime-culture-switching feature (a locale picker) without a refactor. See Non-goals.
- The vestigial `ResGen.exe`/`VbrResFileBuilder.ps1` pipeline remains in the repo, unused by
  the actual build. A future chore could remove it, but it is inert and not a functional risk
  as-is.

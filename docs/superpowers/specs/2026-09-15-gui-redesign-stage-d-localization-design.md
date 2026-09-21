# GUI Redesign Stage D: Localization Sweep — Design

**Branch:** `stage-d/localization`, off `feature/gui-redesign-port` @ `a5e9d74a` (Stage C, PR #223).

**Revision note:** this spec was independently reviewed by a fresh agent before implementation
planning started. The review re-derived every count from the code directly (rather than
trusting the first draft) and found several load-bearing numeric/design errors, which this
revision corrects. See "Corrections applied after review" at the end of each affected section.

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
- XAML-declared: **10** hardcoded `ToolTip.Tip` attributes in `VhcGui.axaml`, plus **1 more**
  set from code-behind (`ToolTip.SetTip(pdfCheckBox, "PDF Export not available when both
  VB365 & VBR are detected on the same machine.")`, `VhcGui.axaml.cs:373`) — **11 total**.
  **~32** hardcoded `Text`/`Content` attributes in `VhcGui.axaml` (down slightly from Stage
  C's 36 — a couple of strings were touched by Stage C's hardware fixes without being
  resx-backed).
- **Not counted by Stage C's spec at all:** **16** hardcoded `Text`/`Content` assignments in
  code-behind (`VhcGui.axaml.cs`) — dynamic status/progress messages such as
  `monitorStatusText.Text = "Setup failed — check log"` and
  `progressText.Text = $"Collection complete — {failed.Count} collector warning(s)"`. Two
  strings each appear at two call sites (`"Setup failed — check log"` at lines 1115/1142,
  `"Available — not set up"` at lines 1046/1193), so this is **14 distinct keys**, not 16.
- **Also not counted:** `ThemeLabelFor()` (`VhcGui.axaml.cs:163`) returns hardcoded
  `"🌙 Dark"` / `"☀ Light"` / `"🖥 System"`, assigned to `ThemeToggleButton.Content` at two
  call sites (lines 114, 153).

**The correctness trap** (found while recounting, not previously flagged as urgent): three
sites in `VhcGui.axaml.cs` read a `ComboBoxItem`'s currently-*displayed* `Content` text as a
backend value:

- `GetNotifSettings()` (`VhcGui.axaml.cs:1071,1073`):
  `(notifTypeBox.SelectedItem as ComboBoxItem)?.Content?.ToString()?.ToLower() ?? "ntfy"` and
  the equivalent read of `notifSeverityBox` for `minSeverity`.
- `notifTypeBox_SelectionChanged` (`VhcGui.axaml.cs:1170`): the same pattern, feeding a
  (currently dead — see Recorded, not fixed) URL-hint switch.

**The severity read is the dangerous half, not the type read.**
`CVhcMonitorIntegration.GenerateConfig` (`CVhcMonitorIntegration.cs:93`) defensively
lowercases and falls back the notif *type* to `"ntfy"` if it doesn't match a known webhook
type — a localized type label degrades to the default, silently wrong but not corrupting
config. `minSeverity`, however, is written **raw**, unvalidated, straight into the generated
YAML: `sb.AppendLine($"    min_severity: \"{minSeverity}\"");` (`CVhcMonitorIntegration.cs:104`).
Localizing `notifSeverityBox`'s `Content` without this fix emits `min_severity: "Warnung"`
into a live monitor config file.

The fix pattern already exists once in this exact file. `PeriodRadio_Checked`
(`VhcGui.axaml.cs:936`) reads `(sender as RadioButton)?.Tag`, not `Content`, precisely
because — per the comment already there — "Content is localized, and parsing a localized
label as data is exactly the mistake `notifSeverityBox` already makes." Stage D applies that
same `Tag`-based pattern to the two notif boxes. (Positively confirmed during review: no
other control in `VhcGui.axaml.cs` or any dialog code-behind reads a displayed label back out
as data — this bug class is fully diagnosed and limited to these two boxes.)

**The localization pipeline is not what it looks like — for VBR strings.** The Localization
folder contains a hand-run script (`VbrResFileBuilder.ps1`) that launches Visual Studio, runs
the legacy `ResGen.exe` against `.txt` files, and regenerates `VbrLocalizationHelper.cs` and
loose `.resources` binaries — all against a hardcoded path (`A:\source\veeam-healthcheck\...`)
that doesn't match any known dev machine. This looked like it might be load-bearing and
Windows/VS-only. For the VBR-side strings, it isn't: a plain cross-platform `dotnet build` in
this repo already auto-compiles `vhcres.*.resx` into proper culture satellite assemblies via
the SDK's default `.resx` → `EmbeddedResource` convention — confirmed twice independently by
deleting the built artifacts and rebuilding from clean, both times producing
`bin/Debug/net8.0-windows7.0/{fR-FR,ja,zh-CN,zh-tw}/VeeamHealthCheck.resources.dll`. No
explicit `EmbeddedResource Include` exists for `vhcres*.resx` in the csproj, so this is the
SDK's implicit glob, not the legacy script.

**This does not extend to VB365.** `VeeamHealthCheck.csproj:156` explicitly embeds
`Resources\Localization\VB365\vb365_vhcres.resources`, and `Vb365ResourceHandler.cs` loads it
by that exact manifest name via `ResourceManager`. That file **is** load-bearing and is
produced by `Vb365ResFileBuilder.ps1`/`ResGen.exe` — untouched by this stage, which only
touches the VBR-side `VhcGui.axaml`/`VbrLocalizationHelper.cs` strings. Only the VBR-side
loose `.resources` files in `Resources/Localization/` (not `Resources/Localization/VB365/`)
are confirmed dead weight — no explicit `Include`, and the SDK's implicit glob is `**/*.resx`,
not `*.resources`.

**Translations already exist but are effectively unreachable today.** Four satellite resx
files exist with real content: `vhcres.fR-FR.resx`, `vhcres.ja.resx`, `vhcres.zh-cn.resx`,
`vhcres.zh-tw.resx`. But nothing in the codebase ever sets `CultureInfo.CurrentUICulture` —
there is no in-app language picker. `ResourceManager.GetString()` resolves against whatever
the OS's UI culture happens to be, so these translations only ever surface on a machine whose
Windows display language already matches one of the four. Compounding this,
`VbrLocalizationHelper`'s fields are `public static string X = m4.GetString(...)` —
plain static-field initializers, evaluated exactly once at class load (598 fields, 598
`GetString` calls, verified 1:1). Even with a picker, nothing today could re-localize at
runtime without a larger refactor. That refactor is not Stage D (see Non-goals).

**A consequence worth stating plainly, since the original draft of this spec didn't:** given
the above, any *new* translated content Stage D might add to the four locales would be exactly
as unreachable as the existing content — verifiable only by manually switching a Windows
machine's display language. That fact directly shaped the decision in Design §4 below.

**Existing translations already have a real, unrelated bug — worth fixing regardless of
anything else in this stage.** Diffing key sets (not just counts) between the neutral resx and
each locale found:

| file | `<data>` entries | keys present in neutral but missing here | keys present here but *not* in neutral (orphans) |
|---|---|---|---|
| `vhcres.resx` (neutral) | 598 | — | — |
| `vhcres.fR-FR.resx` | 551 | **64** | **17** |
| `vhcres.ja.resx` | 560 | **39** | 1 |
| `vhcres.zh-cn.resx` | 560 | **39** | 1 |
| `vhcres.zh-tw.resx` | 560 | **39** | 1 |

(The naive count subtraction — 598−551=47, 598−560=38 — undercounts because it doesn't
account for orphans; the real missing-key numbers are higher, confirmed by an actual key-set
diff.) The single orphan common to ja/zh-cn/zh-tw is `HtmlIntroLine3`, renamed to
`HtmlIntroLine3Anon`/`HtmlIntroLine3Original` in the neutral resx after those locales were
translated. fR-FR's 17 orphans include the same issue plus 16 more where a human translator
appears to have translated the *key name* instead of leaving it alone and translating only the
value — e.g. neutral `SbrTitle` / fR-FR `'Titre Sbr'`, neutral `SbrExt8`...`SbrExt14` / fR-FR
`'SbrExt 8'`...`'SbrExt 14'`, neutral `v365NavValue0`/`1` / fR-FR `'v365NavValeur0'`/`1`. Every
one of these is a real, already-written French translation that silently falls back to English
today because `GetString()` looks up by the neutral key name and finds nothing under the
mangled one. Fixing this needs no translator — just renaming 17 existing `<data name="...">`
keys in `vhcres.fR-FR.resx` and one in each of `vhcres.ja/zh-cn/zh-tw.resx` to match neutral.

## Goals

- Fix the `notifTypeBox`/`notifSeverityBox` `Content`-as-data correctness trap.
- Resx-back every hardcoded `Text`, `Content`, and `ToolTip.Tip` attribute assignment (XAML or
  code-behind) that Stage C left behind in `VhcGui.axaml`/`VhcGui.axaml.cs`, excluding the
  documented non-goals.
- Fix the 17+1+1+1 pre-existing orphaned/mistranslated keys in the four locale files —
  independent of any new translation work, since these are real existing translations that
  are silently dead.
- Add every new resx key this sweep creates to all four locale files (parity, not
  translation — see Design §4) plus a mechanical test that would catch either kind of gap.

**Corrections applied after review:** the original draft's Goal statement ("every remaining
hardcoded, translatable UI string ... and code-behind") was broader than what Design §3 (below)
actually covered — Design never mentioned the ~13 dialog message bodies/titles elsewhere in
`VhcGui.axaml.cs` (e.g. `"Veeam Software Not Detected"`, `"Health Check Failed"`,
`"Credentials Required"`, `"Monitor Not Found"`, and their message bodies). Rather than
silently absorb that additional surface into this pass, it's called out explicitly as a
non-goal below — it's a comparable amount of additional work to what's already scoped and
deserves its own sign-off rather than scope-creeping in in a revision.

## Non-goals (explicitly deferred)

- **Icon glyphs and punctuation-only content.** The gear glyph (`&#x2699;`), the ellipsis
  Content on `browseFolderBtn` (XAML), and the emoji prefixes in `ThemeLabelFor()`'s three
  return values are not translatable language content and stay hardcoded. (The *word* half of
  `ThemeLabelFor()`'s strings — "Dark"/"Light"/"System" — is in scope; see Design §2.)
- **Notif-type brand names.** `Teams`, `Slack`, `PagerDuty`, `ntfy` on `notifTypeBox` items are
  proper nouns and stay hardcoded `Content` (backed by matching `Tag` values after the Design
  §1 fix). This does **not** extend to `notifSeverityBox` — its labels (`ok`/`warning`/
  `critical`) are ordinary words and are resx-backed like everything else, while their `Tag`
  values stay the fixed English protocol strings.
- **Dialog message bodies and titles** elsewhere in `VhcGui.axaml.cs` (error dialogs, the
  "Health Check Failed"/"Credentials Required"/"Monitor Not Found" family). Comparable-sized
  additional surface to what's already scoped here; left for a follow-up pass rather than
  silently folded in.
- **A locale picker / runtime language switching.** Blocked by `VbrLocalizationHelper`'s
  static-field-initializer pattern, which resolves every string exactly once at class load.
  Introducing runtime switching means converting those fields to properties (or another
  indirection) and adding UI to select a culture — real, separate scope. The hazard this
  creates is *latent, not live*: nothing today sets `CurrentUICulture`, so there is no code
  path that could hit ordering problems from touching `VbrLocalizationHelper` before culture
  is set. Do not let this scope-creep into Stage D.
- **Removing the vestigial VBR-side `ResGen.exe` pipeline** (`VbrResFileBuilder.ps1` and the
  loose `.resources` files under `Resources/Localization/`, *not* the VB365 ones — those are
  load-bearing, see Context). Confirmed dead weight, but cleanup is a separate, low-risk chore.
- **Producing new translated (non-English) content for this sweep's new keys.** See Design §4
  for the reasoning and the chosen alternative (parity + allowlist, no machine translation).
- **Fixing `notifUrlBox.Tag`'s dead-code write** (`VhcGui.axaml.cs:1171`) or wiring the URL
  hint to Avalonia's `Watermark` property so it actually renders. Real bug, discovered as a
  side effect of Design §1, but unrelated to localization — see Recorded, not fixed.
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
  `(notifTypeBox.SelectedItem as ComboBoxItem)?.Tag?.ToString()?.ToLower() ?? "ntfy"` — keep
  the existing `.ToLower()`, since `GenerateConfig`'s webhook-type switch is itself
  case-normalized and this preserves current behavior exactly.
- `GetNotifSettings()` (`VhcGui.axaml.cs:1073`): `minSeverity` reads
  `(notifSeverityBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "warning"`.
- `notifTypeBox_SelectionChanged` (`VhcGui.axaml.cs:1170`): read
  `(notifTypeBox.SelectedItem as ComboBoxItem)?.Tag?.ToString()` **without** `.ToLower()` —
  the switch immediately after this read compares case-sensitively against `"Teams"`,
  `"Slack"`, `"PagerDuty"` (exact `Tag` casing). Applying `.ToLower()` here the way §1's other
  two sites do would make every arm of that switch fall through to its default case. This is
  called out explicitly because it's an easy copy-paste mistake between the three sites.

This is a pure correctness fix with no visible behavior change — same values, different
source attribute. It is provably safe to land and commit standalone, ahead of any
localization work touching these two controls.

**Corrections applied after review:** original draft said all three sites get "the same `Tag`
read" without qualification, which would have propagated the `.ToLower()` bug above into an
implementation. Now stated per-site.

### 2. XAML sweep

Resx-back every hardcoded `Text`, `Content`, and `ToolTip.Tip` attribute in `VhcGui.axaml`
except the documented non-goals (icon glyph, ellipsis, notif-type brand names — which after
§1 are `Tag`-sourced and can keep hardcoded `Content`). This includes:

- The ProductType combobox items (`"Auto-detect"`, `"VBR (Backup & Replication)"`,
  `"VB365 (Backup for Microsoft 365)"`, `"Both"`) — confirmed safe to localize freely, since
  `productTypeSelector_SelectionChanged` already reads `SelectedIndex`, never `Content`
  (`VhcGui.axaml.cs:1013-1024`).
- `notifSeverityBox`'s labels (`ok`/`warning`/`critical`) — resx-backed `Content`, fixed
  English `Tag` (see Non-goals).
- The code-behind-set tooltip on `pdfCheckBox` (`VhcGui.axaml.cs:373`) — the 11th tooltip.

New keys follow the existing `Gui`-prefixed naming convention in `VbrLocalizationHelper.cs`
(e.g. `GuiNotifTypeHeader`, `GuiMinSeverityLabel`, `GuiProductTypeAuto`).

**`ThemeLabelFor()` needs restructuring, not a straight resx swap.** It currently returns one
hardcoded string per variant (`"🌙 Dark"`, `"☀ Light"`, `"🖥 System"`). Split the decorative
emoji (stays hardcoded, non-goal) from the word (`Dark`/`Light`/`System`, resx-backed):
`$"{emoji} {VbrLocalizationHelper.GuiThemeDark}"` etc.

**Corrections applied after review:** original draft's XAML sweep count (10 tooltips) missed
the code-behind-set tooltip at line 373 (now 11 total) and didn't mention `ThemeLabelFor()` or
the `browseFolderBtn` ellipsis at all.

### 3. Code-behind sweep

Resx-back the hardcoded `Text`/`Content` assignments in `VhcGui.axaml.cs`, including the two
already inside `SetUiText()` (`pdfCheckBox.Content`, `clearCredsCheckBox.Content`) and the
monitor-status/progress messages. Two of these strings appear at two call sites each and
must map to **one shared resx key**, not be duplicated:

- `"Setup failed — check log"` — `VhcGui.axaml.cs:1115` and `:1142`.
- `"Available — not set up"` — `VhcGui.axaml.cs:1046` and `:1193`.

16 total hardcoded sites, 14 distinct keys once these two are deduplicated.

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

**Corrections applied after review:** original draft said 15 sites / ~13 keys; exact
enumeration found 16 sites / 14 keys (`ThemeLabelFor`'s 2 call sites were miscounted in, not
as part of this list — they're handled separately in §2 since they're a function return value,
not a direct assignment).

### 4. Existing-locale key parity — not translation

Fix the pre-existing orphan bug first, independent of anything else: rename the 17 mismatched
keys in `vhcres.fR-FR.resx` and the 1 in each of `vhcres.ja.resx`/`vhcres.zh-cn.resx`/
`vhcres.zh-tw.resx` to match their neutral counterparts exactly (see Context table). This
recovers real, already-written human translations. No translator needed; do this
unconditionally regardless of the rest of Stage D.

For every *new* key this sweep adds (§1's `Tag`-adjacent `Content` values, §2, §3): add the
key to all four locale resx files, but do **not** invent translated content. Two reasons,
both from Context: these strings are unreachable by any real user today (no culture-switching
mechanism exists), and three independent signals in this exact file show unaudited
translation is actively harmful here — mistranslating an operational string like `"Setup
failed — check log"` misdirects a customer's troubleshooting, `min_severity` values flow
raw into a config file, and the existing resx files already contain 18 silently-broken
translations from a previous unaudited pass (see Context). Add each new key to the locale
files with its neutral (English) value and mark it in a checked-in allowlist (e.g.
`docs/localization-pending-translation.txt` or a resx comment convention — pick one
consistent form when implementing) as "known untranslated, English fallback intentional."
A translator gets one clean, minimal diff to work from later — filling in real values and
shrinking the allowlist — rather than a spec that shipped guessed text under the product name.

**Corrections applied after review:** original draft called for "best-effort translated
content" in all four locales for every new key. Reversed after independent review flagged the
translation-quality risk and the unreachability problem; see the question this was raised back
to the user, who chose the parity-only approach.

### 5. Guard test

The `ResourceManager.GetString()` call underlying every `VbrLocalizationHelper` field returns
`null` silently on a missing key rather than throwing — no exception, no build error. This
sweep needs two independent checks, not one, because they catch different failure classes:

1. **Neutral-key coverage.** Reflect over every `public static string` field on
   `VbrLocalizationHelper` and assert each resolves non-null/non-empty under the neutral
   (build/test-default) culture. This catches a typo'd resx key in the neutral resx — the
   most common mistake this sweep will make while adding ~45 new keys.
2. **Per-satellite parity.** Check (1) alone is not sufficient: if a key is missing from a
   *satellite* locale but present in neutral, `ResourceManager.GetString()` silently falls
   back to the neutral (English) value and check (1) still passes — exactly the class of bug
   that produced the 18 dead translations in Context, and it would not have caught them. Use
   `resourceManager.GetResourceSet(culture, createIfNotExists: true, tryParents: false)` per
   locale — `tryParents: false` returns only that culture's own resource set, with no neutral
   fallback — and diff its key set against the neutral resx's key set. Hard-fail on any key
   present in a satellite but not in neutral (the orphan bug from Context) and on any key
   missing from a satellite that isn't in the Design §4 allowlist. This exercises the actual
   shipped satellite DLL (confirmed reachable in test output at
   `VhcXTests/bin/Debug/net8.0-windows7.0/{fR-FR,ja,zh-CN,zh-tw}/VeeamHealthCheck.resources.dll`),
   is culture-independent, and directly replaces the broken "counts should match" criterion
   the original draft proposed.

Both checks are reflection/resource-API based, run cross-platform, and don't depend on
`CurrentUICulture` or on `VbrLocalizationHelper`'s static-initializer-once behavior (they
inspect resource sets directly rather than reading the static fields for anything but check 1).

**Corrections applied after review:** original draft proposed only check (1) and a
"satellite DLL key count should match neutral" build-verification step in Testing/verification
— both insufficient/incorrect, since satellite fallback masks missing keys under count-based
or single-culture-field checks. This section now specifies the `tryParents: false` mechanism
needed to actually test satellite content, and spec self-review recommends spiking that API
call for ~10 minutes at implementation time before committing to its exact shape in a test.

## Implementation notes / hazards

- **`GetString()` returns `null` on a missing key; it does not throw.** See Design §5 — this
  is why two separate checks are needed, not one.
- **UTF-16LE encoding-preserving edits.** `VbrLocalizationHelper.cs` and `vhcres.txt` are
  UTF-16LE with CRLF and must be edited via encoding-preserving scripts, never a plain
  text-editing tool. Verify with `file <path>` after every edit (must still say "Unicode
  text, UTF-16, little-endian") — but note `file` only confirms the encoding, not that
  specific characters survived the round-trip. Several of the code-behind strings contain
  em-dashes (`—`); diff the actual rendered value after edits, not just the encoding.
  `vhcres.resx` and the four satellite `.resx` files are plain UTF-8 — no special handling
  needed.
- **Culture-name casing is harmless; satellite *directory* casing is not, cross-platform.**
  `vhcres.fR-FR.resx` (mixed-case culture segment in the filename) resolves fine because .NET
  culture-name lookup is case-insensitive — confirmed, the build produces a working
  `fR-FR/VeeamHealthCheck.resources.dll`. But that's a filesystem directory name, and
  filesystem case-sensitivity is platform-dependent (fine on Windows/macOS, would silently
  miss on a case-sensitive Linux deployment). Academic today; becomes load-bearing the moment
  a test or deployment step probes a specific directory casing. Worth a one-line normalization
  to `vhcres.fr-FR.resx` while other renames are already happening in Design §4, but not a
  blocker.
- **Sites vs. keys.** When counting "how much debt is left" at any point in this sweep, count
  distinct keys, not call sites — Design §3's two duplicated strings mean 16 code-behind sites
  map to 14 keys.
- **`VeeamHealthCheck.csproj` auto-increments on every build/test.** Always
  `git checkout --` that exact file before committing — never anything else.

## Testing / verification

- `dotnet test vHC/VhcXTests/VhcXTests.csproj` — includes both new guard-test checks (Design
  §5); must pass cross-platform.
- `dotnet build vHC/HC.sln -c Debug` — confirms satellite resource DLLs regenerate for all
  four locales, verifiable in this sandbox via
  `bin/Debug/net8.0-windows7.0/{fR-FR,ja,zh-CN,zh-tw}/VeeamHealthCheck.resources.dll`. Note:
  the build emits a non-fatal `BuildCopy.sh` SMB-mount warning that doesn't affect these DLLs
  — assert the DLL files exist directly rather than trusting the build's overall exit code.
- Manual, Windows-only (this sandbox cannot render Avalonia): confirm no blank labels/tooltips
  render in the main window and the monitor-status panel. Confirm the notif dropdowns still
  send the correct protocol values after Design §1's fix (send a real ntfy/Teams notification
  and check the payload, and specifically check the generated monitor YAML's `min_severity`
  value is the English protocol string, not a translated label).

## Recorded, not fixed

- The static-field-initializer pattern in `VbrLocalizationHelper` blocks any future
  runtime-culture-switching feature (a locale picker) without a refactor. See Non-goals.
- The vestigial VBR-side `ResGen.exe`/`VbrResFileBuilder.ps1` pipeline remains in the repo,
  unused by the actual build. A future chore could remove it, but it is inert and not a
  functional risk as-is. (The VB365-side equivalent is *not* vestigial — see Context — and is
  untouched by this stage either way.)
- **`notifUrlBox.Tag` is dead code.** `notifTypeBox_SelectionChanged` (`VhcGui.axaml.cs:1171`)
  writes a URL-hint example string into `notifUrlBox.Tag`, but nothing reads `Tag` on that
  control — no binding, no style, no other code path. Avalonia's actual placeholder-text
  property is `Watermark` (used correctly elsewhere, e.g. `ManageServersDialog.axaml.cs:71`),
  so this hint has never rendered. Discovered as a side effect of Design §1 (which touches
  this exact method to fix the correctness trap) but is an unrelated, pre-existing bug — left
  unfixed here to avoid scope creep. Worth a follow-up: swap `.Tag =` for `.Watermark =` on
  `notifUrlBox` once this is picked up.

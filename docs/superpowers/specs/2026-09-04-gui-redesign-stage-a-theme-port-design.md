# GUI Redesign Port — Stage A: Theme/Style Port — Design Spec

**Date:** 2026-09-04 (revised after user source-verified review)
**Branch:** `stage-a/theme-port` (base: `feature/gui-redesign-port`, base: `feature/gui-modernization`)
**Status:** Approved for implementation planning

## Revision note

This spec was reviewed once against the actual current source (both the
real `VhcGui.axaml`/`.axaml.cs` on `feature/gui-modernization` and the
spike's current `App.axaml`) and found to contain several stale/incomplete
claims that would have produced real bugs (invisible text, a deleted style
being ported, silently-unstyled buttons). All findings from that review are
folded into this revision — see the "Complete literal disposition" section,
which replaces an earlier hand-picked, incomplete list with an exhaustive
one derived from grepping every hardcoded color literal in the file.

## Problem

`vHC/Spikes/GuiRedesignSpike/` (branch `spike/gui-redesign`) validated a
two-column, card-based, tabbed redesign for the real `VhcGui.axaml`, and its
`App.axaml` theme dictionaries + style vocabulary are recommended for direct
reuse (see the spike's README, "Carrying this forward"). The real
`VhcGui.axaml` on `feature/gui-modernization` still uses its original,
mechanically-ported-from-WPF styling: hardcoded hex colors, no theme
support, local `Button.modern`/`Border.groupbox` classes defined inline in
`Window.Styles`.

This stage ports the spike's visual language — colors, theme dictionaries,
button/card/text style classes — into the real app **without** restructuring
the existing layout. The two-column/groupbox structure, control positions,
and all event-handler wiring stay exactly as they are today. The deliberate
exceptions are: a new theme-mode toggle button, and theming the runtime
status-color logic in code-behind (both explicitly requested by the user
during review — see below).

Layout restructuring into the spike's card/tab design is Stage B's job (not
this one).

## Non-goals

- No layout/structure changes to `VhcGui.axaml`'s Grid, columns, or control
  positions, other than the one new toggle button described below
- No rewiring of any existing event handler
- No terms-acceptance or server-management interaction changes (Stage C)
- No localization of new strings (Stage D handles all new/changed strings
  from every stage in one pass — see "Deferred" below for why this is safe)
- No `Border.chip` — it does not exist to port. It was deliberately removed
  from the spike in commit `1e04b14` ("replace localhost pill with a
  ComboBox for quick server switching... Removed the now-unused
  Border.chip style from App.axaml"). The spike's current `App.axaml` has
  zero chip-related styles; only historical README prose still describes
  the now-replaced chip design. Nothing to port, nothing to defer.
- No wiring of `segment`/`remove-server` styles to actual controls — they
  are ported (styles exist in `App.axaml`) but have no consumers until
  Stage B rebuilds the layout
- No `Button.tab` styling — meaningless without Stage B's tab structure

## Visual reference

A browser mockup comparing the real app's actual current controls (Export
Options card, VBR Server list, Terms/Start buttons, plus the new toggle),
recolored with the spike's exact palette in Light and Dark, was reviewed and
approved by the user before writing this spec. Palette values below are
taken directly from that approved mockup and from `vHC/Spikes/GuiRedesignSpike/App.axaml`.

## `App.axaml` changes

### Theme resources

Add `Application.Resources` with:

- Flat (non-nested) `SystemAccentColor` + 6 shade keys (`Dark1`/`Dark2`/`Dark3`/`Light1`/`Light2`/`Light3`),
  copied verbatim from the spike. Must stay flat, not nested per-theme in
  `ThemeDictionaries` — nesting breaks FluentTheme's native controls
  (CheckBox, ListBox selection, etc.) from re-resolving colors on a runtime
  theme switch (confirmed the hard way during the spike).
- `ResourceDictionary.ThemeDictionaries` with `Light` and `Dark` keys, each
  defining: `PageBackgroundBrush`, `CardBackgroundBrush`, `CardBorderBrush`,
  `PrimaryTextBrush`, `SecondaryTextBrush`, `AccentBrush`, `AccentHoverBrush`,
  `AccentPressedBrush`, `OnAccentTextBrush`, `SegmentBackgroundBrush`,
  `SegmentSelectedBrush`, `ListSelectedTintBrush` — all copied verbatim from
  the spike's `App.axaml`.
- `CautionBackgroundBrush` / `CautionBorderBrush` per theme, for the KB 2462
  warning callout (the spike has no equivalent — its design doesn't include
  a warning callout at all). Light theme keeps the existing `#FFF8E1` /
  `#FFC107` amber pairing (`CautionBackgroundBrush="#FFF8E1"`,
  `CautionBorderBrush="#FFC107"`). Dark theme uses a muted, desaturated
  amber against the dark page background (`#0F172A`) so the callout reads
  as "amber warning" without being a jarring bright patch:
  `CautionBackgroundBrush="#332B14"`, `CautionBorderBrush="#8A6D1F"`.
- **New (added during review): flat (non-nested) status brushes**, used by
  runtime status-color logic in `VhcGui.axaml.cs` (see "Status color
  theming" below): `StatusNeutralBrush="#999999"`,
  `StatusWarningBrush="#F0AD4E"`, `StatusSuccessBrush="#5CB85C"`,
  `StatusErrorBrush="#D9534F"`. Flat, not per-theme, because these are the
  exact values already shipping today and are mid-tone enough to stay
  legible against both the light and dark page backgrounds unchanged — the
  same reasoning as `SystemAccentColor`'s flat placement. This isn't a
  new color decision, it's the existing 4 runtime colors given names.

### Application-level styles

Add `Application.Styles` (after `<FluentTheme />`), ported from the spike
with the following classes:

- `Window` — `Background="{DynamicResource PageBackgroundBrush}"`,
  `FontFamily="Segoe UI,-apple-system,sans-serif"`. **Differs from the
  spike:** the spike's stack led with `Inter`, which isn't a guaranteed
  system font and isn't bundled with the app; its own README flagged this
  as an open issue. Segoe UI is guaranteed on the Windows target, so it
  leads here.
- `Button.primary` / `Button.link`, including `:pointerover`, `:pressed`,
  and `:disabled` states, copied verbatim from the spike.
- `Button.secondary`, including `:pointerover`, copied from the spike,
  **plus a new `:disabled` state the spike doesn't have.** The spike's
  `Button.secondary` was never exercised in a disabled state, so it has no
  `:disabled` override at all. The real app needs one:
  `monitorQuickSetupBtn`/`monitorVhcSetupBtn`/`monitorRunBtn` all ship
  `IsEnabled="False"` in markup and are disabled from the moment the app
  opens. Add `Button.secondary:disabled /template/ ContentPresenter` with
  `Background="#66808080"` (matching the same translucent-gray value the
  spike already uses for `Button.primary:disabled`) and
  `TextElement.Foreground="{DynamicResource SecondaryTextBrush}"`.
- `Border.card`, `TextBlock.card-title`, `TextBlock.field-label`,
  `TextBlock.secondary-text` — verbatim from the spike.
- `TextBox.modern`, `ComboBox.modern`, `CheckBox.modern` — verbatim from the
  spike (these already exist as local styles in the real `VhcGui.axaml`
  under the same class names; moving them to `App.axaml` makes them
  reusable and matches the spike's architecture).
- `RadioButton.segment` (base + `:checked` + the `Ellipse`/`ContentPresenter`
  template overrides) — verbatim from the spike, including the exact
  Avalonia-11.3.20-template-sourced fixes documented in the spike's README
  (bullet-hiding, full-pill background fill, content re-spanning for
  centering). No consumer yet in the real window.
- `ListBoxItem:selected` / `:selected:pointerover` / `:selected:pressed`
  overrides — verbatim from the spike. Applies to the real `serverListBox`
  **only if its wrapping Border's hardcoded `Background="White"` is also
  removed** — see "Complete literal disposition" below. Porting this style
  alone, without touching the wrapping Border, would make the selected
  server's text (styled to `PrimaryTextBrush`, i.e. near-white in Dark
  theme) invisible against that hardcoded white background. This was
  caught in review before implementation.
- `Button.remove-server` — verbatim from the spike. No consumer yet in the
  real window.
- **New (added during review):** `Separator` — a single global style,
  `Background="{DynamicResource CardBorderBrush}"`, since the three
  `Separator` instances in `VhcGui.axaml` currently hardcode
  `Background="#EEEEEE"` and the spike has no separator style to copy (it
  doesn't use `Separator` at all).

`Button.tab` / `Button.tab.tab-active` are **not** ported in this stage.

## `VhcGui.axaml` changes

### Removed

- The entire local `<Window.Styles>` block (`Button.modern`,
  `Button.secondary`, `CheckBox.modern`, `Border.groupbox`, `TextBox.modern`,
  `ComboBox.modern`) — superseded by the new `App.axaml` styles.
- `Background="#F5F5F5"` on the `Window` element — superseded by the
  `Window` style's `PageBackgroundBrush`.

### Class renames (mechanical, no structural change)

| Element | Old | New |
|---|---|---|
| `run` button (Start Health Check) | `Classes="modern"` | `Classes="primary"` |
| `addServerBtn` | `Classes="modern"` | `Classes="primary"` |
| `monitorQuickSetupBtn` | `Classes="modern"` | `Classes="primary"` |
| 4× section `Border` (Options, Output Directory, VBR Server, Continuous Monitoring) | `Classes="groupbox"` | `Classes="card"` |
| Instructions panel `Border` (left column, top) | ad-hoc (`Background="White"`, `CornerRadius="6"`, `BoxShadow`) | `Classes="card"` |

`addServerBtn` and `monitorQuickSetupBtn` were missed in the first pass of
this spec — both currently render with the same accent-green look as the
Start button via `Classes="modern"`, and `Button.modern` has no `App.axaml`
equivalent, so without an explicit mapping both would silently fall back to
FluentTheme's unstyled default button. Mapped to `primary` (not
`secondary`) to preserve their current visual weight exactly — the user
confirmed this is the faithful "same look, new name" choice, not a
downgrade.

The Instructions panel's `Border` (currently ad-hoc: `Background="White"
Padding="20" Margin="0,0,0,15" CornerRadius="6" BoxShadow="0 2 10 0
#4CCCCCCC"`) is folded into `Classes="card"` too — it serves the identical
"card-like info panel" purpose as the four groupbox panels, just was never
using that shared style. `Margin="0,0,0,15"` is kept as a local override
(the `card` style doesn't set margin); `BoxShadow` is dropped (the spike's
`Border.card` doesn't use shadows — accepted as part of adopting the
spike's flatter visual language).

`Classes="secondary"` on `termsBtn`, `removeServerBtn`, `clearServersBtn`,
`importButton` is unchanged (name already matches; only the style
*definition* moves and changes, and gains the new `:disabled` state above).

### Complete literal disposition (mechanical, not hand-picked)

The first pass of this spec enumerated "which TextBlocks get which class"
by hand and missed several hardcoded literals as a result — most critically,
it claimed the `ListBoxItem:selected` fix "applies immediately to the real
`serverListBox`" while missing that the listbox sits inside a
`Border Background="White"`, which would make the selected item's text
invisible in Dark theme. To close that class of gap, every
`Background="..."` / `Foreground="..."` / `BorderBrush="..."` hex or
`"White"` literal in `VhcGui.axaml` (found via
`grep -noE '(Background|Foreground|BorderBrush)="#[0-9A-Fa-f]{3,8}"|(Background|Foreground|BorderBrush)="White"'`)
is dispositioned below. **Converting an element to a class requires
removing its local `Foreground`/`FontSize`/`FontWeight` attribute, not just
adding the `Classes` attribute** — Avalonia gives a locally-set property
value priority over a `Style` setter targeting the same property, so a
class added alongside an unchanged local literal has no visible effect.
This applies equally to inline `Run` elements, which do not inherit a
class from their parent `TextBlock` and must have their own local
`Foreground` either removed (to inherit the parent's resolved color) or
set explicitly.

| Line(s) | Element | Literal | Disposition |
|---|---|---|---|
| 14 | `Window` | `Background="#F5F5F5"` | Removed (superseded by `Window` style) |
| 90 | Instructions `Border` | `Background="White"` | Removed; `Classes="card"` |
| 91 | Instructions `Border` | `BoxShadow="...#4CCCCCCC"` | Removed (dropped with `card` adoption) |
| 93 | `InsHeader` `TextBlock` | `Foreground="#00B233"` | `Classes="card-title"`, but with explicit local `Foreground="{DynamicResource AccentBrush}"` override — this is the window's brand-green title; card-title's default `PrimaryTextBrush` would silently drop that brand color, so it's kept explicit instead. Also keeps explicit local `FontSize="18" FontWeight="Bold"` (card-title's default 15px/SemiBold is sized for the four section headers, not the window's single largest heading) |
| 96 | line1–6 wrapper `TextBlock` | `Foreground="#555555"` | Removed; `Classes="secondary-text"` |
| 107 | Caution `Border` | `Background="#FFF8E1"` `BorderBrush="#FFC107"` | → `{DynamicResource CautionBackgroundBrush}` / `CautionBorderBrush` |
| 110 | `Cav1Part1` `TextBlock` | `Foreground="#333333"` | → explicit `{DynamicResource PrimaryTextBrush}` (normal-emphasis callout body text; no dedicated class for this role) |
| 120 | `kbLink` `HyperlinkButton` | `Foreground="#0066CC"` | → `{DynamicResource AccentBrush}` (aligns with `Button.link`'s existing accent-as-link convention rather than carrying over an unrelated blue that belongs to neither palette) |
| 123 | `Cav2` `TextBlock` | `Foreground="#333333"` | → explicit `{DynamicResource PrimaryTextBrush}`, same reasoning as 110 |
| 126 | Cav3/Cav4 wrapper `TextBlock` | `Foreground="#666666"` | Removed; `Classes="secondary-text"` (local `FontSize="11"` kept as an intentional fine-print override) |
| 130 (×2) | Inline `Run`s ("Credential Storage:" label, file-path text) | `Foreground="#555555"` | Removed entirely (no replacement) — inherits the parent `TextBlock`'s now-themed `secondary-text` color. This is the fix for the contrast bug flagged in review: these local `Run`-level overrides sit inside the caution box, which after this stage has a dark amber background — the old hardcoded `#555555` computes to ~2:1 contrast there |
| 140 | `OptHdr` `TextBlock` | `Foreground="#333333"` | Removed (and local `FontSize="16"`); `Classes="card-title"` |
| 145 | "Export Options" label | `Foreground="#555555"` | Removed (and `FontSize`/`FontWeight`); `Classes="field-label"` |
| 156, 176, 244 | 3× `Separator` | `Background="#EEEEEE"` | Removed; covered by the new global `Separator` style |
| 159 | "Data Collection" label | `Foreground="#555555"` | Removed; `Classes="field-label"` |
| 162 | "Collection Period:" label | `Foreground="#333333"` | Removed; `Classes="field-label"` |
| 179 | "Security & Privacy" label | `Foreground="#555555"` | Removed; `Classes="field-label"` |
| 193 | `outPath` `TextBlock` | `Foreground="#333333"` | Removed (and `FontSize`); `Classes="card-title"` |
| 201 | "VBR Server" `TextBlock` | `Foreground="#333333"` | Removed (and `FontSize`); `Classes="card-title"` |
| 217 | Server-listbox wrapping `Border` | `BorderBrush="#CCCCCC"` | → `{DynamicResource CardBorderBrush}` |
| 218 | Server-listbox wrapping `Border` | `Background="White"` | → `{DynamicResource CardBackgroundBrush}` — **the fix for the critical bug flagged in review**: without this, the `ListBoxItem:selected` style's `PrimaryTextBrush` text (near-white in Dark theme) would be invisible against this hardcoded white background, in the one control the earlier spec draft claimed was already handled |
| 247 | "Product Type:" label | `Foreground="#555555"` | Removed; `Classes="field-label"` |
| 266 | "Continuous Monitoring" `TextBlock` | `Foreground="#333333"` | Removed (and `FontSize`); `Classes="card-title"` |
| 270 | "Status: " inline label | `Foreground="#555555"` | Removed; `Classes="secondary-text"` |
| 272 | `monitorStatusText` (XAML default) | `Foreground="#999999"` | → `{DynamicResource StatusNeutralBrush}` (see "Status color theming" below) |
| 275 | `monitorLastRunText` | `Foreground="#777777"` | Removed; `Classes="secondary-text"` |
| 279 | "Alert Notifications" label | `Foreground="#555555"` | Removed; `Classes="field-label"` |
| 295 | "Min severity:" label | `Foreground="#555555"` | Removed; `Classes="field-label"` |
| 347 | Footer `Border` | `Background="White"` `BorderBrush="#DDDDDD"` | → `{DynamicResource CardBackgroundBrush}` / `CardBorderBrush` |
| 353 | `pBar` track | `Background="#E8E8E8"` | → `{DynamicResource CardBorderBrush}` |
| 354 | `pBar` fill | `Foreground="#00B233"` | → `{DynamicResource AccentBrush}` |
| 359 | `progressText` (XAML default) | `Foreground="#666666"` | Removed; `Classes="secondary-text"` (its `FontSize="12"` already matches the class, no override needed) |

### New: theme toggle button

The one purely-structural change in this stage (beyond the status-color
code changes below). Wrap the existing `Grid.Row="0"` `ScrollViewer` in a
`Panel` (no other change to its contents), and add a sibling `Button`:

- `Classes="secondary"`, `HorizontalAlignment="Right"`,
  `VerticalAlignment="Top"`, small margin offset from the window edge, so it
  floats above the existing scrollable content without taking up layout
  space in the two-column grid.
- Content cycles through three labels on click: `"🖥 System"` →
  `"🌙 Dark"` → `"☀ Light"` → back to `"🖥 System"`.
- Clicking sets `Application.Current.RequestedThemeVariant` to `null`
  (System/Default), `ThemeVariant.Dark`, or `ThemeVariant.Light`
  respectively, and calls `CAppSettings.Set(...)` to persist the choice.
- On startup, `VhcGui`'s constructor (or `Loaded` handler) reads
  `CAppSettings.Get().ThemePreference`, applies it to
  `RequestedThemeVariant` before the window is shown, and sets the button's
  initial label to match.

## Status color theming (added during review)

`monitorStatusText.Foreground` and `progressText.Foreground` are set at
runtime via `new SolidColorBrush(Color.FromRgb(...))` in
`VhcGui.axaml.cs`, completely bypassing XAML styles. Confirmed 8 call
sites, 4 distinct colors:

| Color | Meaning | Call sites (line numbers, pre-implementation) |
|---|---|---|
| `#999999` | Neutral/not-bundled | `monitorStatusText`: 727 |
| `#F0AD4E` | Warning/not-set-up | `monitorStatusText`: 735, 882 · `progressText`: 396 |
| `#5CB85C` | Success/running | `monitorStatusText`: 743 · `progressText`: 404 |
| `#D9534F` | Error/setup-failed | `monitorStatusText`: 804, 831 |

Per user decision, this stage fixes these now rather than deferring:
replace each `new SolidColorBrush(Color.FromRgb(...))` with a lookup
against the corresponding flat resource added to `App.axaml`
(`StatusNeutralBrush`/`StatusWarningBrush`/`StatusSuccessBrush`/`StatusErrorBrush`,
values unchanged from today — see "Theme resources" above), e.g.
`(IBrush)this.FindResource("StatusWarningBrush")!`. This is a values-preserving
refactor (same 4 colors, same 8 call sites, same semantics) — no new status
logic, no new colors, just named-resource lookup instead of inline
construction, so a future stage can retune status colors per-theme in one
place instead of 8.

## New: `CAppSettings` (theme persistence)

No generic settings/preferences mechanism exists in this codebase today —
confirmed by inspecting `vHC/HC_Reporting/Startup/` and
`vHC/HC_Reporting/Common/`. The only precedent for persisting anything to
disk is `CredentialStore` (`vHC/HC_Reporting/Startup/CredentialStore.cs`),
which writes JSON to `%AppData%\VeeamHealthCheck\creds.json` using
`Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)` +
`System.Text.Json`.

Add a new static class, `CAppSettings`
(`vHC/HC_Reporting/Startup/CAppSettings.cs`), following that same pattern
at a smaller scale:

- Path: `%AppData%\VeeamHealthCheck\settings.json` (sibling to `creds.json`,
  same `ApplicationData` folder).
- Payload: a single-property DTO, `AppSettings { string ThemePreference }`,
  default `"System"`. No other settings are added — this is scoped to the
  one preference this stage needs, not a general-purpose settings system.
- `public static AppSettings Get()` — reads and deserializes the file if it
  exists; returns the default (`"System"`) if the file is missing or
  unreadable (no exception should bubble up from a missing/corrupt settings
  file — fall back silently to default).
- `public static void Set(string themePreference)` — reads-merges-writes
  the file (mirroring `CredentialStore.PersistCacheToDisk()`'s
  read-merge-write approach), `JsonSerializer.Serialize(..., WriteIndented = true)`.
- Internal `StorePath` settable for tests, mirroring `CredentialStore`'s own
  test seam.

## Testing

- Unit tests for `CAppSettings` in `VhcXTests`: round-trip (`Set` then
  `Get` returns the same value), default-fallback when the file doesn't
  exist, default-fallback when the file is malformed/corrupt. Follow the
  same `StorePath`-injection test pattern already established in
  `vHC/VhcXTests/CredentialStoreSecurityTests.cs`.
- No automated test coverage for pure XAML/styling changes — verified
  manually (see below).
- Manual verification: `dotnet build vHC/HC.sln`, then `dotnet run` the real
  `VeeamHealthCheck` project (this repo builds and runs cross-platform now
  per the WPF→Avalonia migration) and click through, **in both Light and
  Dark, via all three toggle states**:
  - The caution box and its "Credential Storage" fine print (the specific
    contrast bug found in review)
  - The server list, including selecting an item (the specific invisible-text
    bug found in review)
  - The three Continuous Monitoring buttons in their default disabled state
    (`monitorQuickSetupBtn`/`monitorVhcSetupBtn`/`monitorRunBtn`)
  - The footer progress bar area, in both idle and (if feasible to trigger)
    warning/success states
  - `addServerBtn` and the Start button, confirming both still read as the
    same accent-green "primary" weight
  - Button hover/pressed states generally
  
  The spike's own history is direct evidence that static code review
  misses real visual defects (contrast failures, template quirks, invisible
  text) that only show up when the app actually runs — this spec itself was
  caught making exactly that kind of claim ("applies immediately") before
  a source-level review, not even a running instance, already disproved it.

## Deferred to later stages

- **Stage B:** wiring `segment`/`remove-server` classes to actual controls;
  the card/tab layout restructure; `Button.tab` styling.
- **Stage C:** terms-acceptance and server-management interaction changes.
- **Stage D:** localizing the toggle button's three label strings
  (`"🖥 System"` / `"🌙 Dark"` / `"☀ Light"`) into the 5 supported locales.
  Safe to defer because all four stages are squash-merged together onto
  `feature/gui-modernization` before that reaches `dev` — Stage D will have
  already fixed these strings before the combined commit lands anywhere
  visible downstream.

## Deliverable

Implementation on branch `stage-a/theme-port`, squash-merged into the
interim branch `feature/gui-redesign-port` once complete, per the branching
strategy already agreed for this port (see the session handoff doc this
stage was kicked off from).

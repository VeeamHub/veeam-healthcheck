# GUI Redesign Stage B: Layout Restructure — Design

**Date:** 2026-09-04
**Branch:** `stage-b/layout-restructure` (off `feature/gui-redesign-port` @ `48336ec`)
**Depends on:** Stage A (theme/style port, merged into `feature/gui-redesign-port` as `48336ec`)
**Followed by:** Stage C (Terms-acceptance and server-management interaction model changes), Stage D (localization)

## Context

Stage A ported the redesign spike's (`spike/gui-redesign`) theme dictionaries and style vocabulary into the real `VhcGui.axaml`, recoloring the existing layout in place. It deliberately did not touch structure. Real-machine verification of Stage A surfaced two deferred findings, both of which this stage exists to fix:

1. The window still doesn't fit a 1080p display without scrolling — the original motivating problem for the whole redesign.
2. The theme-toggle button visually overlaps nearby page content at that resolution.

The spike (`vHC/Spikes/GuiRedesignSpike/`) already answered the layout-fit question for a lighter, placeholder-content version of this screen: a fixed-size window, a header row, a two-tab strip (Ad-hoc Health Check / Continuous Monitoring), a two-column card grid per tab inside a `ScrollViewer`, and a bottom action bar in a fixed `Grid` row that is a sibling of the scrollable area, not nested inside it — so it can never be pushed off-screen.

This stage ports that structure into the real `VhcGui.axaml`, keeping every existing control, event handler, and interaction model exactly as it is today. It is a layout restructure, not a behavior change.

## Goals

- Fix the two deferred Stage A findings above.
- Adopt the spike's overall structure: fixed-size window, header row, tab strip, two-column per-tab card grid, fixed non-scrolling bottom bar.
- Preserve every existing control name, event handler wiring, and interaction model. If a control moves, it keeps its `x:Name` and click handler; nothing is rewritten to behave differently.

## Non-goals (explicitly deferred)

- **Terms acceptance flow.** `termsBtn` → `AcceptButton_click` → `CClientFunctions.AcceptTerms()` (which raises a `CGlobals.Notifier.Confirm` dialog) is untouched. Whether to adopt the spike's inline-checkbox model is Stage C's decision.
- **Server management model.** The existing inline textbox + Add button + listbox + Remove/Clear buttons stay exactly as they are. The spike's chip + "Manage Servers" dialog pattern (and its known Add/Remove commit-on-click-instead-of-on-Done bug) is Stage C's decision and does not apply here.
- **Selector control types.** The 7/30/90-day period selector (`daysSelector`) and the warning/critical/ok severity selector (`notifSeverityBox`) stay `ComboBox`. The spike uses segmented `RadioButton.segment` pills for both, and that style is already ported into the theme from Stage A, but swapping control types means rewriting `SelectedIndex`-based code-behind logic into checked-`RadioButton` lookups — a small interaction-model change, not a layout change. Not this stage.
- **Localization.** Every new or changed string introduced by this stage (tab labels, header button text, About dialog body) is hardcoded English for now, same as the spike. Stage D adds resx entries across all 5 locales in its own pass. Pre-existing hardcoded strings already in `VhcGui.axaml` (e.g. `"VBR Server"`, `"Continuous Monitoring"` card titles, which are not currently resx-driven) are left exactly as they are — Stage B does not expand or shrink the localization gap, just carries it forward unchanged.
- **Cosmetic polish.** The spike README's `:pressed`-state styling gaps and Inter-font fallback issue are not layout concerns and are not addressed here.
- **Zero scrolling, unconditionally.** The real content (Options + Output Directory + VBR Server cards) is roughly twice the vertical weight of the spike's placeholder content. The hard requirement is that the bottom bar (Terms/progress/Start) never scrolls — guaranteed structurally by living outside the `ScrollViewer`. The tab body's own content is likely to need internal scrolling as the *normal* case, not just near the 860×580 minimum — rough budget: `Border.card` padding is 16 (32px/card vertically, `App.axaml:160`), `termsBtn`/`run` are `Height="45"` (`VhcGui.axaml:257-259`), and header+tabs+bottom-bar chrome likely eats somewhere around 200px of the default `Height="620"`, leaving roughly 400px for the scrollable body — against this design's own ≈422-470px column estimates. Those figures are hand-calculated, not measured in a running instance (same caveat the spike's own README places on its pixel figures), so treat this as "expect scrolling at default size too" rather than a precise number, and confirm on the real-machine verification pass rather than treating a visible scrollbar at default size as a regression.

## Design

### Window

`VhcGui.axaml`'s `Window` element changes from:

```xml
MinHeight="500" MinWidth="900"
Width="950"
SizeToContent="Height"
```

to (matching the spike):

```xml
MinHeight="580" MinWidth="860"
Width="900" Height="620"
```

`SizeToContent="Height"` is removed. This is the actual fix for "doesn't fit 1080p": today the window grows to whatever height its content demands, with nothing capping it to the screen. A fixed size with an internal `ScrollViewer` means the window itself can never exceed a size that fits any 1080p screen.

`CanResize="True"` and `WindowStartupLocation="CenterScreen"` are unchanged.

### Top-level structure

The current `Grid RowDefinitions="*,Auto"` (content + progress-bar-only bottom strip) becomes `Grid RowDefinitions="Auto,Auto,*,Auto"`:

1. **Header row (new).** Title text, then (left to right, matching the spike's actual order) the relocated `importButton`, a divider `Border`, the existing `ThemeToggleButton` moved here from its current floating `HorizontalAlignment="Right" VerticalAlignment="Top"` position over the content, and a new `aboutButton`. Moving the theme toggle into the header row is what fixes the overlap finding — it becomes a normal flow element instead of an absolutely-positioned overlay.

   `importButton.Content` is already the plain string `"Import"` (`VbrLocalizationHelper.GuiImportButton`) — unlike the spike's `ImportLinkButton`, which hardcodes a folder emoji (`"📂 Import previous report..."`), the real button has no emoji today and none is being added.

   `importButton` is unconditionally disabled and `Width="0"` in every shipped build: `SetUiSync()` always calls `SetImportRelease()` (`VhcGui.axaml.cs:153,233-237`), which sets `IsEnabled = false` and `Width = 0` — it never touches `IsVisible`, which stays `true` throughout. `SetImportDebug()` (`VhcGui.axaml.cs:239-243`), which would enable it, has no call sites anywhere in the repo — dead code. Relocating the button changes none of that; it stays exactly as disabled/width-zero as it is today.

   Because the button hides via `Width="0"` rather than `IsVisible="false"`, a divider bound to `importButton.IsVisible` would never actually track its effective visibility — `IsVisible` never changes. Instead, add a same-named divider control and set its `IsVisible` alongside the button in the same two methods: `SetImportRelease()` sets `importDivider.IsVisible = false` next to `importButton.Width = 0`, and `SetImportDebug()` sets `importDivider.IsVisible = true` next to `importButton.Width = 100`. This keeps the divider's visibility a direct, explicit code-behind assignment rather than a binding that silently wouldn't fire.
2. **Tab strip row (new).** Two buttons, `"Ad-hoc Health Check"` and `"Continuous Monitoring"`, using the already-ported `tab` / `tab-active` style classes, mirroring the spike's `AdHocTabButton` / `MonitoringTabButton`.
3. **Tab content row.** A `ScrollViewer` wrapping a `Grid` with two panels, toggled via `IsVisible` (matching the spike's `views:AdHocHealthCheckView` / `views:ContinuousMonitoringView` visibility toggle, adapted to this project's existing single-file structure rather than separate `UserControl`s — see Implementation notes).
4. **Bottom bar row (new, fixed).** `Grid ColumnDefinitions="Auto,*,Auto"`: `termsBtn` — progress stack (`pBar` + `progressText`) — `run` button. Same handlers (`AcceptButton_click`, `run_Click`), just relocated out of the scrollable area.

   **Required code-behind change, not just relocation:** today `hideProgressBar()`/`showProgressBar()` (`VhcGui.axaml.cs:287-306`) toggle `pBar.Opacity` but `progressText.IsVisible` — deliberately, per the existing comment, to mirror WPF's `Visibility.Hidden` (space reserved) vs. `Visibility.Collapsed` (space removed). That distinction is harmless today because `progressText` sits alone in its own full-width row. Once it shares a column with nothing else to collapse against in the `Auto,*,Auto` bottom bar, `IsVisible=false` would shrink that column's content and shift `termsBtn`/`run` vertically every time a run starts or ends — the same reflow bug the spike avoided by giving both `ProgressBarControl` and `ProgressText` `Opacity="0"` in its bottom bar (`MainWindow.axaml`), never `IsVisible`. Stage B must change both `progressText.IsVisible = false/true` assignments in `hideProgressBar()`/`showProgressBar()` to `progressText.Opacity = 0/1`, matching `pBar`'s existing convention. This is scoped narrowly to the two lines needed to prevent a regression introduced by the relocation itself — it does not change what `progressText` displays or when, only how it hides.

### Ad-hoc Health Check tab — card mapping

Two columns (`ColumnDefinitions="*,20,*"`, matching the spike's spacing):

- **Left column:** VBR Server card (unchanged: server textbox + Add button, listbox, Remove Selected/Clear All buttons, separator, product type combo) stacked above the Output Directory card (unchanged: path textbox). Combined height ≈ 470px.
- **Right column:** the Options card, unchanged as a single card — Export Options (pdf/explorer/html checkboxes), separator, Data Collection (period `ComboBox` + rescan checkbox), separator, Security & Privacy (scrub + clear-creds checkboxes). Height ≈ 422px.

This split was chosen over grouping Server alone against Output+Options combined (≈359px vs. ≈533px, a 174px imbalance) specifically to keep the two columns close in height (≈470 vs. ≈422, a 48px imbalance) — approved via mockup during brainstorming.

### Continuous Monitoring tab — card mapping

Two columns, splitting the existing single card's content rather than regrouping across cards like the Ad-hoc tab does — but it is a real reordering, not a pure 1:1 split: the three action buttons move from after the notification fields (today's order) to sit with the status text instead, so they land together in the left column. Splitting one card into two also requires two new card titles; unlike the Ad-hoc tab (which regroups existing whole cards and needs no new titles), these are new hardcoded English strings, deferred to Stage D like every other new string in this stage — using the spike's own naming since it already fits the content split:

- **Left column,** titled `"Monitor Status"`: status text + last-run text + the three action buttons (`monitorQuickSetupBtn`, `monitorVhcSetupBtn`, `monitorRunBtn`), unchanged.
- **Right column,** titled `"Alert Notifications"`: notification type combo + URL textbox + severity `ComboBox` (kept as `ComboBox`, per Non-goals above), unchanged.

The tab strip's own `"Continuous Monitoring"` label continues to identify this whole screen, same as the spike.

### About / Disclaimer dialog (new)

The Instructions & Warnings content currently always-visible on the main screen (`InsHeader`, `line1`–`line6`, the KB2462 caution box with `kbLink`, `Cav1Part1`/`Cav2`/`Cav3`/`Cav4`, and the credential-storage disclaimer) moves into a new modal dialog, ported near-verbatim from the spike's `AboutDisclaimerDialog.axaml`: a `ScrollViewer` over a `StackPanel` of text blocks, a `HyperlinkButton` for the KB2462 link, and a `Close` button (`IsCancel="True"`).

This content actually comes from three different sources today, and the dialog must preserve that split exactly rather than treating it as one uniform "existing resx" story:

- `InsHeader`, `line1`–`line6`, `Cav1Part1`, `Cav2` are resx-backed via `VbrLocalizationHelper.GuiInstHeader`/`GuiInstLine1`–`GuiInstLine6`/`GuiInstCaveat1`/`GuiInstCaveat2` (`VhcGui.axaml.cs:249-257`). Only `Caveat1`/`Caveat2` exist — there is no `GuiInstCaveat3`/`GuiInstCaveat4` in `VbrLocalizationHelper` or any of the 5 `vhcres*.resx` files.
- `Cav3` and `Cav4` are hardcoded C# string literals set directly in `SetUiText()` (`VhcGui.axaml.cs:258-259`), not resx-backed.
- The credential-storage paragraph is hardcoded inline in `VhcGui.axaml` as bare `<Run>` elements (including a `FontFamily="Consolas"` run for the file path) with no `x:Name` at all — it isn't set from code-behind or resx.

Stage B ports all three exactly as they exist — the resx calls, the two hardcoded C# literals, and the inline XAML `Run`s — into the new dialog's XAML/code-behind unchanged. It does not add `GuiInstCaveat3`/`GuiInstCaveat4` resx keys or otherwise resx-back the hardcoded pieces; doing so would expand the localization gap this stage's own non-goals say to leave alone. Stage D is where that gap, if ever closed, gets closed.

The caution-colored box styling (background/border/link brushes) that highlights this text today is **not** carried into the dialog. Inside an About/Disclaimer dialog it becomes plain `secondary-text`, matching the spike's neutral treatment — an About dialog is informational, not an active inline warning, and this was confirmed with the user as an accepted reduction in prominence (the content is now one click away instead of always-visible).

This is the one new interactive surface this stage adds (a button + a modal window). It is display-only — no new business logic, no new state, just localized static text and a `Close` button — consistent with "no new behavior" since nothing about *what the tool does* changes, only where users go to read it.

## Implementation notes

- The spike splits Ad-hoc/Continuous Monitoring into separate `UserControl` files (`Views/AdHocHealthCheckView.axaml`, `Views/ContinuousMonitoringView.axaml`) referenced from `MainWindow.axaml`. The real `VhcGui.axaml`/`.axaml.cs` is a single window with all controls as direct fields on the `VhcGui` partial class (referenced throughout `VhcGui.axaml.cs` by field name, e.g. `pdfCheckBox`, `serverListBox`). Splitting into separate `UserControl`s here would require either exposing every field publicly across the boundary or duplicating logic — out of proportion to a layout restructure. Instead, both tab panels stay inline in `VhcGui.axaml` as sibling `Grid`s inside the tab-content row, toggled via `IsVisible`, keeping every existing field reference in `VhcGui.axaml.cs` untouched.
- The About dialog, being new and self-contained, follows the spike's separate-window pattern (`Dialogs/AboutDisclaimerDialog.axaml` + `.axaml.cs`), matching how this project already structures dialogs (e.g. `CredentialPromptWindow`).

## Testing / verification

Same process as Stage A: this design → task-by-task implementation plan → `superpowers:subagent-driven-development` (fresh implementer + spec-compliance review + code-quality review per task, fix-and-re-review loops) → a final whole-branch review → squash-merge into `feature/gui-redesign-port`.

This sandbox cannot render the Avalonia GUI at all (confirmed during Stage A — crashes during Avalonia's native platform bootstrap before any app code runs). Manual visual verification — confirming the two-column layout at 1080p, the fixed bottom bar staying reachable while scrolling tab content, the header no longer overlapping, and the About dialog opening/closing correctly — hands off to the user on a real Windows machine, same as Stage A.

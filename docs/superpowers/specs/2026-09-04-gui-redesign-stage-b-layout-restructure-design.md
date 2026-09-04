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
- **Zero scrolling, unconditionally.** The real content (Options + Output Directory + VBR Server cards) is roughly twice the vertical weight of the spike's placeholder content. The hard requirement is that the bottom bar (Terms/progress/Start) never scrolls — guaranteed structurally by living outside the `ScrollViewer`. The tab body's own content may still need to scroll internally at or near minimum window size; that is accepted as correct behavior, not a bug, since it's strictly better than today's unbounded-window failure mode.

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

1. **Header row (new).** Title text, then (left to right) the relocated `importButton` (plain `"Import"` text — no folder emoji; full-color emoji glyphs ignore `Foreground`/theme brushes entirely, a gotcha already hit during Stage A), a new `aboutButton`, and the existing `ThemeToggleButton` moved here from its current floating `HorizontalAlignment="Right" VerticalAlignment="Top"` position over the content. Moving it into the header row is what fixes the overlap finding — it becomes a normal flow element instead of an absolutely-positioned overlay.
2. **Tab strip row (new).** Two buttons, `"Ad-hoc Health Check"` and `"Continuous Monitoring"`, using the already-ported `tab` / `tab-active` style classes, mirroring the spike's `AdHocTabButton` / `MonitoringTabButton`.
3. **Tab content row.** A `ScrollViewer` wrapping a `Grid` with two panels, toggled via `IsVisible` (matching the spike's `views:AdHocHealthCheckView` / `views:ContinuousMonitoringView` visibility toggle, adapted to this project's existing single-file structure rather than separate `UserControl`s — see Implementation notes).
4. **Bottom bar row (new, fixed).** `Grid ColumnDefinitions="Auto,*,Auto"`: `termsBtn` — progress stack (`pBar` + `progressText`, same opacity-toggle convention as today) — `run` button. Same handlers (`AcceptButton_click`, `run_Click`), just relocated out of the scrollable area.

### Ad-hoc Health Check tab — card mapping

Two columns (`ColumnDefinitions="*,20,*"`, matching the spike's spacing):

- **Left column:** VBR Server card (unchanged: server textbox + Add button, listbox, Remove Selected/Clear All buttons, separator, product type combo) stacked above the Output Directory card (unchanged: path textbox). Combined height ≈ 470px.
- **Right column:** the Options card, unchanged as a single card — Export Options (pdf/explorer/html checkboxes), separator, Data Collection (period `ComboBox` + rescan checkbox), separator, Security & Privacy (scrub + clear-creds checkboxes). Height ≈ 422px.

This split was chosen over grouping Server alone against Output+Options combined (≈359px vs. ≈533px, a 174px imbalance) specifically to keep the two columns close in height (≈470 vs. ≈422, a 48px imbalance) — approved via mockup during brainstorming.

### Continuous Monitoring tab — card mapping

Two columns, a clean 1:1 split of the existing single card's content — no regrouping needed. Splitting one card into two requires two new card titles; unlike the Ad-hoc tab (which regroups existing whole cards and needs no new titles), these are new hardcoded English strings, deferred to Stage D like every other new string in this stage — using the spike's own naming since it already fits the content split:

- **Left column,** titled `"Monitor Status"`: status text + last-run text + the three action buttons (`monitorQuickSetupBtn`, `monitorVhcSetupBtn`, `monitorRunBtn`), unchanged.
- **Right column,** titled `"Alert Notifications"`: notification type combo + URL textbox + severity `ComboBox` (kept as `ComboBox`, per Non-goals above), unchanged.

The tab strip's own `"Continuous Monitoring"` label continues to identify this whole screen, same as the spike.

### About / Disclaimer dialog (new)

The Instructions & Warnings content currently always-visible on the main screen (`InsHeader`, `line1`–`line6`, the KB2462 caution box with `kbLink`, `Cav1Part1`/`Cav2`/`Cav3`/`Cav4`, and the credential-storage disclaimer) moves into a new modal dialog, ported near-verbatim from the spike's `AboutDisclaimerDialog.axaml`: a `ScrollViewer` over a `StackPanel` of text blocks, a `HyperlinkButton` for the KB2462 link, and a `Close` button (`IsCancel="True"`).

Content is wired to the same existing resx strings (`VbrLocalizationHelper.GuiInstHeader`, `GuiInstLine1`–`GuiInstLine6`, `GuiInstCaveat1`–`GuiInstCaveat4`) rather than the spike's hardcoded English — no string content changes, only presentation location.

The caution-colored box styling (background/border/link brushes) that highlights this text today is **not** carried into the dialog. Inside an About/Disclaimer dialog it becomes plain `secondary-text`, matching the spike's neutral treatment — an About dialog is informational, not an active inline warning, and this was confirmed with the user as an accepted reduction in prominence (the content is now one click away instead of always-visible).

This is the one new interactive surface this stage adds (a button + a modal window). It is display-only — no new business logic, no new state, just localized static text and a `Close` button — consistent with "no new behavior" since nothing about *what the tool does* changes, only where users go to read it.

## Implementation notes

- The spike splits Ad-hoc/Continuous Monitoring into separate `UserControl` files (`Views/AdHocHealthCheckView.axaml`, `Views/ContinuousMonitoringView.axaml`) referenced from `MainWindow.axaml`. The real `VhcGui.axaml`/`.axaml.cs` is a single window with all controls as direct fields on the `VhcGui` partial class (referenced throughout `VhcGui.axaml.cs` by field name, e.g. `pdfCheckBox`, `serverListBox`). Splitting into separate `UserControl`s here would require either exposing every field publicly across the boundary or duplicating logic — out of proportion to a layout restructure. Instead, both tab panels stay inline in `VhcGui.axaml` as sibling `Grid`s inside the tab-content row, toggled via `IsVisible`, keeping every existing field reference in `VhcGui.axaml.cs` untouched.
- The About dialog, being new and self-contained, follows the spike's separate-window pattern (`Dialogs/AboutDisclaimerDialog.axaml` + `.axaml.cs`), matching how this project already structures dialogs (e.g. `CredentialPromptWindow`).

## Testing / verification

Same process as Stage A: this design → task-by-task implementation plan → `superpowers:subagent-driven-development` (fresh implementer + spec-compliance review + code-quality review per task, fix-and-re-review loops) → a final whole-branch review → squash-merge into `feature/gui-redesign-port`.

This sandbox cannot render the Avalonia GUI at all (confirmed during Stage A — crashes during Avalonia's native platform bootstrap before any app code runs). Manual visual verification — confirming the two-column layout at 1080p, the fixed bottom bar staying reachable while scrolling tab content, the header no longer overlapping, and the About dialog opening/closing correctly — hands off to the user on a real Windows machine, same as Stage A.

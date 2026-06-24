# GUI HandleThirdState sets Scrub=false — tri-state scrub checkbox can silently disable anonymization

**Category:** startup-cli
**Severity:** Low
**Type:** Bug
**File(s):** `vHC/HC_Reporting/VhcGui.xaml.cs:142,337-365`

## Summary
The scrub checkbox is wired to three handlers: `HandleCheck` (→ Scrub=true), `HandleUnchecked` (→ Scrub=false), and `HandleThirdState` (→ Scrub=false). A three-state checkbox has an indeterminate middle state; `SetUi` sets `scrubBox.IsChecked = true` at startup, but if the control is `IsThreeState` and a user click cycles it into the indeterminate state, `HandleThirdState` fires and silently turns scrubbing OFF without a clear visual "unchecked" cue.

## Evidence
```csharp
scrubBox.IsChecked = true;          // SetUi default
...
private void HandleThirdState(object sender, RoutedEventArgs e)
{
    this.functions.LogUIAction("Scrub 3rd state = false");
    CGlobals.Scrub = false;          // indeterminate => anonymization disabled
}
```

## Impact
Anonymization is a privacy-protective default for this tool (scrubbed mode anonymizes IPs/servernames/credentials). If the checkbox is three-state and a user lands on the indeterminate state thinking data is still scrubbed, the report is generated with full sensitive detail. Whether this is reachable depends on the XAML `IsThreeState` setting (not in this file), so severity is Low pending that confirmation — but the handler unconditionally disables a privacy control on an ambiguous UI state.

## Suggested Fix
Confirm `scrubBox.IsThreeState` in the XAML. If three-state is not intended, remove `IsThreeState`/`Indeterminate` handler. If it is intended, make the indeterminate state's meaning explicit (and ideally default-safe: treat indeterminate as scrub=true), with a clear label.

## Labels
bug, wpf, privacy, scrub, ui-state, low

# notifType lowercased with culture-sensitive ToLower() (Turkish-I) for control-flow switch

**Category:** analysis-monitor
**Severity:** Low
**Type:** Bug
**File(s):** `vHC/HC_Reporting/Functions/Monitor/CVhcMonitorIntegration.cs:93-99`

## Summary
The webhook type is selected by lowercasing `notifType` with the culture-sensitive `string.ToLower()` and matching against ASCII literals. On a Turkish (`tr-TR`) or Azeri locale, `"I"`/`"İ"` casing differs, so a value like `"PAGERDUTY"` would not lowercase to the expected `"pagerduty"` and would silently fall through to the `ntfy` default.

## Evidence
```csharp
// CVhcMonitorIntegration.cs:93-99
string webhookType = (notifType?.ToLower()) switch
{
    "teams" => "teams",
    "slack" => "slack",
    "pagerduty" => "pagerduty",
    _ => "ntfy"          // wrong webhook type chosen under tr-TR for "I"-containing inputs
};
```

## Impact
On affected locales the user's chosen notification backend can be silently replaced with `ntfy`, sending alerts to the wrong (or no) destination. Hard to diagnose because it only reproduces under specific cultures.

## Suggested Fix
Use `notifType?.ToLowerInvariant()` (or `string.Equals(..., StringComparison.OrdinalIgnoreCase)`). This is a real correctness bug for control flow, not just the suppressed CA1305/CA1307 style flag.

## Labels
bug, culture, globalization, monitor

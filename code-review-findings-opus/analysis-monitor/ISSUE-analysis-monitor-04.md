# min_severity written to YAML without escaping

**Category:** analysis-monitor
**Severity:** Low
**Type:** Bug
**File(s):** `vHC/HC_Reporting/Functions/Monitor/CVhcMonitorIntegration.cs:104`, `:68-69`

## Summary
`minSeverity` is interpolated into a YAML double-quoted scalar without passing through `EscapeYaml` (unlike `notifUrl` on the adjacent line). It originates from `GetNotifSettings()` in the GUI and is a defaulted method parameter. If it ever carries a `"` or `\`, it corrupts the generated YAML.

## Evidence
```csharp
// CVhcMonitorIntegration.cs:101-104
sb.AppendLine("  webhook:");
sb.AppendLine($"    type: \"{webhookType}\"");
sb.AppendLine($"    url: \"{EscapeYaml(notifUrl)}\"");      // escaped
sb.AppendLine($"    min_severity: \"{minSeverity}\"");      // NOT escaped
```

## Impact
Low today because `minSeverity` is typically a controlled enum-like value ("warning"/"error"). But the inconsistency is a latent bug: any future code path that lets the value contain a quote/backslash breaks config generation, and it reads as an oversight next to the escaped `notifUrl`.

## Suggested Fix
Wrap with `EscapeYaml(minSeverity)` for consistency, or (preferred) move config emission to a real YAML serializer per ISSUE-03. Optionally validate `minSeverity` against the known set and fall back to `"warning"`.

## Labels
bug, yaml, escaping, consistency, monitor

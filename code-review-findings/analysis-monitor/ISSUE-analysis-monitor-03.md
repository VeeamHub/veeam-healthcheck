---
title: "Harden EscapeYaml against newlines/control characters and escape vbrServer in URL (YAML injection)"
severity: Medium
labels: [security, bug]
domain: analysis-monitor
files:
  - vHC/HC_Reporting/Functions/Monitor/CVhcMonitorIntegration.cs:297
  - vHC/HC_Reporting/Functions/Monitor/CVhcMonitorIntegration.cs:82
confidence: High
---

## Summary

`EscapeYaml()` only escapes backslash and double-quote. A username or password containing a newline (or CR) terminates the double-quoted YAML scalar mid-value and turns the remainder of the secret into raw YAML lines — corrupting the config or injecting arbitrary keys (e.g. overriding `url:` or `webhook:`). Separately, `vbrServer` is interpolated into the `url:` line with no escaping at all.

## Impact

- Passwords legitimately containing `\n`, `\r`, or other control characters break the generated `vhc-monitor.yaml`, producing a monitor that fails to parse its config or — worse — silently runs with attacker-influenced settings (a crafted "server name" from a config screen can inject YAML keys such as a different `url`/`verify_ssl`).
- This is the monitor's credential path, so a parse failure means scheduled monitoring silently does nothing every 5 minutes.

## Evidence

`vHC/HC_Reporting/Functions/Monitor/CVhcMonitorIntegration.cs:297-301` —
```csharp
private static string EscapeYaml(string value)
{
    if (string.IsNullOrEmpty(value)) return value;
    return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
```
YAML double-quoted scalars cannot contain a literal newline as part of a single-line value; `\n` must be written as the escape sequence `\n`. This function passes raw newlines through.

`vHC/HC_Reporting/Functions/Monitor/CVhcMonitorIntegration.cs:82` — no escaping at all:
```csharp
sb.AppendLine($"    url: \"https://{vbrServer}:9419\"");
```

## Suggested fix

Extend `EscapeYaml` to escape control characters (`\n` → `\\n`, `\r` → `\\r`, `\t` → `\\t`, and other C0 chars via `\xNN`), and apply it (or a hostname-validation check) to `vbrServer` as well. Alternatively use a YAML serializer (YamlDotNet) instead of string concatenation for the config.

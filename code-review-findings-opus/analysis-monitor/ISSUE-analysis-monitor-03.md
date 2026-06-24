# EscapeYaml is insufficient for YAML double-quoted scalars; passwords/URLs can corrupt config or inject keys

**Category:** analysis-monitor
**Severity:** Medium
**Type:** Bug
**File(s):** `vHC/HC_Reporting/Functions/Monitor/CVhcMonitorIntegration.cs:297-301`, used at `:83-84`, `:103`

## Summary
`EscapeYaml` only escapes backslash and double-quote. It is used to embed `username`, `password`, and the user-supplied `notifUrl` into YAML double-quoted scalars. YAML double-quoted scalars interpret C-style escape sequences, so a value containing a literal backslash sequence such as `\n`, `\t`, or `\u` (legitimately possible in a password) is mis-decoded by the monitor's YAML parser into a newline/tab/unicode char rather than the literal characters. A newline in particular can terminate the scalar early and let the remainder of the value be parsed as new YAML keys.

## Evidence
```csharp
// CVhcMonitorIntegration.cs:297-301
private static string EscapeYaml(string value)
{
    if (string.IsNullOrEmpty(value)) return value;
    return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
```
Consider a password ending in a backslash followed by content, e.g. the literal 4 characters `a\n9`. After `Replace("\\","\\\\")` this becomes `a\\n9` — but in a YAML double-quoted scalar `\\` decodes to a single `\` and the value round-trips correctly only for that case. The real failure is a raw newline/CR inside the value (passwords can contain them via paste, and `notifUrl` is free-form `notifUrlBox.Text`): a literal `\n` byte is not escaped at all, so:
```yaml
    password: "secret
injected_key: value"
```
breaks the scalar and injects YAML structure.

## Impact
- A password or webhook URL containing a newline/CR (or certain backslash sequences) silently produces a wrong credential ("password incorrect" failures that are hard to diagnose) or a malformed config that fails to load.
- In the worst case the trailing portion of an attacker-influenced value is parsed as additional YAML keys, altering monitor behavior (e.g., flipping `verify_ssl`, redirecting `url`).

## Suggested Fix
- Use a real YAML serializer (e.g., YamlDotNet) to emit the config instead of hand-building strings; let the library handle quoting/escaping.
- If hand-rolling must remain, escape the full set of double-quoted YAML escapes: `\`, `"`, newline (`\n`), CR (`\r`), tab (`\t`), and control chars — or emit values as single-quoted scalars (which only require doubling `'`) to avoid escape-sequence interpretation entirely.

## Labels
bug, yaml, escaping, injection, monitor

# EscapeForPowerShellDoubleQuotes does not neutralize newlines; relies on caller always quoting

**Category:** collection-security
**Severity:** Low
**Type:** Security
**File(s):** `vHC/HC_Reporting/Functions/Collection/Security/CredentialHelper.cs:41-69`

## Summary
`EscapeForPowerShellDoubleQuotes` escapes `"`, `\`, `$`, and `` ` ``, which is correct for breakout *within a double-quoted literal*. It does not handle CR/LF or other control characters, and — more importantly — its safety is entirely contingent on every caller wrapping the result in actual double quotes. The escaping function and the quoting are separated across files, so a future caller that forgets the surrounding quotes (as already happened in ISSUE-01) silently loses all protection. There is no defensive contract.

## Evidence
```csharp
foreach (char c in value)
{
    switch (c)
    {
        case '"':  sb.Append("\\\""); break;
        case '\\': sb.Append("\\\\"); break;
        case '$':  sb.Append("`$");   break;
        case '`':  sb.Append("``");   break;
        default:   sb.Append(c);      break;   // CR, LF, NUL, ; , spaces all pass through
    }
}
```
A value such as `host\n-Command;calc` passes through with the newline/semicolon intact. Inside a correctly double-quoted PowerShell string literal that is inert (PS permits multiline strings), but if the caller omits the quotes the newline/`;`/space become argument or statement separators. The companion `ContainsProblematicCharacters` (line 110-118) *does* flag these characters but is advisory only and not consulted by the escaping path.

## Impact
The escaping is correct only under the implicit invariant "the result is always placed inside `\"...\"`". That invariant is unenforced and was already violated (see ISSUE-01). Newlines/NUL in a hostname or username are not rejected.

## Suggested Fix
- Document and, where possible, enforce the quoting contract — e.g. provide a helper that returns the *fully quoted* token (`"\"" + escaped + "\""`) so callers cannot forget the quotes.
- Reject or strip CR/LF/NUL in credential/host fields at the boundary (the `/credfile=` loader already rejects `\n`/`\r` in usernames at CArgsParser.cs:531 — apply the same to interactive/host inputs).

## Labels
security, escaping, powershell, hardening

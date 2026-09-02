# TestMfa reads StandardError twice (ReadToEnd then ReadLine loop) — error parsing loop never runs

**Category:** collection-data
**Severity:** Medium
**Type:** Bug
**File(s):** `vHC/HC_Reporting/Functions/Collection/PSCollections/PSInvoker.cs:297-324`

## Summary
`TestMfa` first drains the entire stderr stream with `ReadToEnd()` into `stdErr`, then immediately tries to iterate the *same* stream again with `res.StandardError.ReadLine()` in a `while` loop. The stream is already at EOF, so `ReadLine()` returns `null` on the first call and the error-classification loop body never executes. The MFA-detection logic in this method is therefore dead.

## Evidence
```csharp
// PSInvoker.cs:298 — stream fully consumed here
string stdErr = CCollections.StripAnsiCodes(res.StandardError.ReadToEnd());
...
// PSInvoker.cs:308 — stream already at EOF, loop never enters
while ((errString = res.StandardError.ReadLine()) != null)
{
    var errResults = this.ParseErrors(errString);
    if (!errResults.Success) { ...; return mfaFound; }
    errorarray.Add(errString);
}
```
Additionally `mfaFound` is initialized to `true` and only ever set to `true`, so `TestMfa` returns `true` (= "MFA failure / stop") in essentially all code paths regardless of the actual result.

## Impact
The PowerShell-5 MFA fallback (`RunLocalMfaCheck` → `TestMfa`) cannot actually detect MFA vs. success: the parse loop is unreachable and the return value is hard-coded to `true`. This defeats the purpose of the fallback and can incorrectly abort collection (or incorrectly continue) on PS5 systems.

## Suggested Fix
Parse `stdErr` that was already captured by `ReadToEnd()` (split on newlines, as `ExecutePsScript` correctly does at `PSInvoker.cs:439`) instead of re-reading the stream. Compute `mfaFound` from actual parse results rather than a constant.

## Labels
bug, stream-double-read, mfa, collection

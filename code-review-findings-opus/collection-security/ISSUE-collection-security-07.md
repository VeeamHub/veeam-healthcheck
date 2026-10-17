# Dead/insecure RunImpersonated and CImpersonation accumulate password in managed string char-by-char

**Category:** collection-security
**Severity:** Low
**Type:** Security
**File(s):** `vHC/HC_Reporting/Functions/Collection/Security/CSecurityInit.cs:45-123`, `vHC/HC_Reporting/Functions/Collection/CImpersonation.cs:50-100`

## Summary
Both `CSecurityInit.RunImpersonated` and `CImpersonation.SafeAccessTokenHandle` read an interactive password by appending each `ConsoleKeyInfo.KeyChar` onto a managed `string` (`password += key.KeyChar`), then pass that plaintext to the native `LogonUser` P/Invoke. The string concatenation creates many short-lived immutable string copies of the growing password on the heap, none of which can be zeroed. `RunImpersonated` is unreferenced dead code carrying the same pattern.

## Evidence
`CSecurityInit.cs:68-79`:
```csharp
string password = null;
while (true)
{
    var key = System.Console.ReadKey(true);
    if (key.Key == ConsoleKey.Enter) break;
    password += key.KeyChar;          // line 78 — new heap string each keystroke
}
...
bool returnValue = LogonUser(userName, domainName, password, ...);   // line 89 — plaintext to P/Invoke
```
Identical pattern in `CImpersonation.cs:67-90`. `RunImpersonated` is private and only invoked from a commented-out call (`CSecurityInit.cs:39 // RunImpersonated();`), i.e. dead code, yet still compiled and a copy-paste source.

There is also no input masking here (unlike `CredsHandler.ReadPasswordMasked`), and the prompts print the password-reading logic but `Console.ReadKey(true)` at least suppresses echo.

## Impact
- Password plaintext fragmented across many un-zeroable heap strings during entry; lingers until GC.
- `LogonUser` receives a managed `string` (cannot be pinned/zeroed) rather than a `SecureString`/`IntPtr`.
- Dead `RunImpersonated` is an attractive nuisance that may be reactivated and duplicates the weakness.

## Suggested Fix
- If these impersonation paths are still needed, accumulate into a `char[]`/`SecureString`, marshal to an unmanaged buffer for `LogonUser` (the `LogonUser` overload accepts an `IntPtr`/`SecureString`-derived buffer), and zero it in a `finally`.
- Delete `CSecurityInit.RunImpersonated` if it is genuinely dead (it is only referenced by a commented-out call).
- Reuse `CredsHandler.ReadPasswordMasked` instead of bespoke key loops.

## Labels
security, dead-code, memory-hygiene, impersonation

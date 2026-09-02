# CImpersonation: password captured but LogonUser/console flow is broken and credential left in managed string

**Category:** collection-data
**Severity:** Medium
**Type:** Bug
**File(s):** `vHC/HC_Reporting/Functions/Collection/CImpersonation.cs:50-100`

## Summary
`SafeAccessTokenHandle()` interactively reads a username and a hand-rolled password loop from the console, but the prompts are mis-wired and the password is held in a mutable managed `string`. `Console.WriteLine(..., false)` is called with a bogus second `bool` argument (WriteLine has no such overload that suppresses newline — it binds to `WriteLine(string, object)` formatting), and there is no `Console.Write` prompt before `Console.ReadLine()`/the key loop, so the prompt text and input handling are incorrect. The password accumulates via `password += key.KeyChar` into an immutable `string`, which cannot be zeroed and lingers in the managed heap.

## Evidence
```csharp
// CImpersonation.cs:56 — second arg 'false' is treated as a format object, not newline suppression
Console.WriteLine(String.Format("Enter the login of a user on {0} ...", domainName), false);
string userName = Console.ReadLine();

Console.WriteLine(String.Format("Enter the password for {0}: ", userName), false);  // line 59

string password = null;
while (true)
{
    var key = System.Console.ReadKey(true);
    if (key.Key == ConsoleKey.Enter) break;
    password += key.KeyChar;     // CImpersonation.cs:77 — immutable string, cannot be cleared
}
```
`LogonUser` is then called with this string (`CImpersonation.cs:88`); on success the token is used to `RunImpersonated` the whole collection.

## Impact
The interactive impersonation path is fragile (prompt/format misuse means the wrong overload is invoked and the UX is broken), and the password remains recoverable in a managed `string` for the process lifetime — it is never overwritten. Backspace and paste are unhandled. If this code path is reachable in production it both behaves incorrectly and weakens credential hygiene; if it is dead/legacy it should be removed.

## Suggested Fix
Use `Console.Write` for prompts; collect the password into a `SecureString` (or `char[]` that is cleared in a `finally`) and marshal to the unmanaged `LogonUser` password parameter via `Marshal.SecureStringToCoTaskMemUnicode`/`ZeroFreeCoTaskMemUnicode`. Handle Backspace. If the impersonation feature is unused, delete it.

## Labels
bug, credential-hygiene, console-io, collection

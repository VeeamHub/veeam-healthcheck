---
title: "CImpersonation leaks the LogonUser access token and holds the password in a managed string"
severity: Medium
labels: [security, reliability]
domain: collection-security
files:
  - vHC/HC_Reporting/Functions/Collection/CImpersonation.cs:50
  - vHC/HC_Reporting/Functions/Collection/CImpersonation.cs:88
  - vHC/HC_Reporting/Functions/Collection/Security/CSecurityInit.cs:89
confidence: High
---

## Summary
`CImpersonation.SafeAccessTokenHandle()` calls `LogonUser` to obtain a primary
access token and returns it to `RunCollection`, which passes it to
`WindowsIdentity.RunImpersonated`. The `SafeAccessTokenHandle` is `IDisposable`
but is never disposed (`using`) — the token handle leaks for the process
lifetime. The password is also accumulated into an immutable `string`, which
cannot be zeroed and lingers on the managed heap until GC. This path is reachable:
`CClientFunctions.StartCollections` (CClientFunctions.cs:308) constructs and runs
`CImpersonation` whenever `REMOTEEXEC && RunSecReport`.

## Impact
A live primary-token handle held longer than needed widens the window for token
theft/impersonation by anything sharing the process, and is a handle leak across
repeated runs. The plaintext password sitting in a non-clearable `string`
increases the chance it is captured in a memory dump or swapped to disk. The same
pattern exists in the (currently unreferenced) `CSecurityInit.RunImpersonated`,
which will reintroduce the leak if that method is ever wired up.

## Evidence
`vHC/HC_Reporting/Functions/Collection/CImpersonation.cs:88` and caller:
```csharp
bool returnValue = LogonUser(userName, domainName, password,
    LOGON32_LOGON_INTERACTIVE, LOGON32_PROVIDER_DEFAULT,
    out safeAccessTokenHandle);
...
return safeAccessTokenHandle;          // handed back, never disposed
```
`CImpersonation.cs:31`:
```csharp
SafeAccessTokenHandle phToken = this.SafeAccessTokenHandle();
WindowsIdentity.RunImpersonated(phToken, () => { ... });
// phToken is never Dispose()d
```
Password collected into an immutable string (CImpersonation.cs:67-78):
```csharp
string password = null;
while (true) { ... password += key.KeyChar; }   // cannot be cleared
```

## Suggested fix
Wrap the token in `using`:
```csharp
using SafeAccessTokenHandle phToken = this.SafeAccessTokenHandle();
WindowsIdentity.RunImpersonated(phToken, () => { ... });
```
Collect the password into a `SecureString` (the existing
`CredentialHelper.ConvertToSecureString` / a char buffer) and pass it via the
`SecureString` LogonUser overload or zero the buffer in a `finally`. Apply the
same fix to the dead `CSecurityInit.RunImpersonated` or delete it so it cannot be
revived with the defect.

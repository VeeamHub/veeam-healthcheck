# /silent with no credential source is never enforced — falls back to interactive prompt

**Category:** startup-cli
**Severity:** High
**Type:** Bug
**File(s):** `vHC/HC_Reporting/Startup/CArgsParser.cs:285-318`, `vHC/HC_Reporting/Common/CMessages.cs:83`

## Summary
The help text and the in-code comment both promise that `/silent` alone with no credential source exits 2 ("creds missing"). `ValidateSilentArgs()` only enforces the `/silent + /savecreds` conflict; the "silent + no creds" case is explicitly deferred to "CredsHandler / CCollections when a null credential is returned." But the credential-required gate in `CClientFunctions.GetVbrVersion()` → `EnsureCredentialsAvailable()` only fires when `PowerShellVersion == 7 && REMOTEEXEC`. For local VBR or PS5 remote, no credential check happens, and for the GUI-less CLI path the code in `EnsureCredentialsAvailable()` merely logs a warning and continues. Nothing on the silent path translates "no stored cred + /silent" into the documented exit 2 before collection prompts.

## Evidence
Comment claiming the contract is enforced elsewhere:
```csharp
// The "/silent + no creds source" check is intentionally NOT done
// here ... That exit-2 path is enforced later by CredsHandler / CCollections
// when a null credential is returned in silent mode.
```
But `EnsureCredentialsAvailable()` (the only startup-layer cred check) never consults `CGlobals.Silent` and never exits:
```csharp
if (!CGlobals.GUIEXEC)
{
    this.LOG.Warning(... "No stored credentials found for PowerShell 7 connection.", false);
    this.LOG.Warning(... "Add the /run parameter ...", false);   // warns, then returns
}
```
and it is gated by `if (CGlobals.PowerShellVersion == 7 && CGlobals.REMOTEEXEC)` in `GetVbrVersion()`, so it does not run for local/PS5 silent collections at all.

## Impact
A `/silent` unattended run lacking credentials can hang on an interactive `Get-Credential`/console prompt (the exact failure mode `/silent` exists to prevent) instead of failing fast with exit 2. In Task Scheduler this manifests as a job that never completes. The documented exit-code contract (help menu, line 83) is not honored. This finding is in-scope to startup-cli because the gap is in `EnsureCredentialsAvailable()`/`GetVbrVersion()`; the downstream enforcement in CCollections should be confirmed separately.

## Suggested Fix
Add an explicit silent-mode gate in `EnsureCredentialsAvailable()` (and ensure it is reached for all collection modes, not only PS7+remote):
```csharp
if (storedCreds == null && string.IsNullOrEmpty(CGlobals.CredFilePath) && CGlobals.Silent)
    SilentExit.ExitSilent(SilentExit.CredsMissing,
        $"No credential source for host '{host}' in silent mode.");
```
Verify the same guard exists at every interactive-prompt site (CredsHandler) so silent mode can never reach a prompt.

## Labels
bug, silent-mode, credentials, unattended, high

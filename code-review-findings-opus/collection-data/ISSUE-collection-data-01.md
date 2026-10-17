# PowerShell argument injection in LogCollectionInfo / ServerDumpInfo (unescaped server & path, UseShellExecute=true)

**Category:** collection-data
**Severity:** High
**Type:** Security
**File(s):** `vHC/HC_Reporting/Functions/Collection/PSCollections/PSInvoker.cs:623-643`, `vHC/HC_Reporting/Functions/Collection/PSCollections/PSInvoker.cs:645-674`

## Summary
Unlike the VBR/VB365 collection paths (which all route the server name through `CredentialHelper.EscapeForPowerShellDoubleQuotes(...)`), the hotfix-detection log-collection paths interpolate `server` and `path` **raw** into the PowerShell argument string and launch with `UseShellExecute = true`. The `server` value originates from `serverlist.txt` (produced by a server-dump script) and `CGlobals.REMOTEHOST` (a CLI argument, see `CArgsParser.cs:259` `CGlobals.REMOTEHOST = providedHost`). A server name containing PowerShell metacharacters or quote-breaking content is injected verbatim into the command line.

## Evidence
`LogCollectionInfo` — `server` and `path` are not escaped and not even quoted:
```csharp
// PSInvoker.cs:626
argString = $"-NoProfile -ExecutionPolicy unrestricted -file {scriptLocation} -Server {server} -ReportPath {path}";
...
return new ProcessStartInfo()
{
    FileName = "powershell.exe",
    Arguments = argString,
    UseShellExecute = true,            // PSInvoker.cs:639
    ...
};
```
`ServerDumpInfo` — `server` comes straight from `CGlobals.REMOTEHOST` with no escaping:
```csharp
// PSInvoker.cs:656
argString = $"-NoProfile -ExecutionPolicy unrestricted -file \"{scriptLocation}\" -Server {server}";
...
UseShellExecute = true,                // PSInvoker.cs:669
```
Caller chain confirming `server` is data, not a constant — `CHotfixDetector.cs:156-159`:
```csharp
foreach(string server in this.ServerList())   // ServerList() reads serverlist.txt line by line
{
    ps.RunVbrLogCollect(this.path, server);
}
```
Contrast with the safe pattern used everywhere else, e.g. `PSInvoker.cs:511`:
```csharp
string escapedServer = CredentialHelper.EscapeForPowerShellDoubleQuotes(CGlobals.REMOTEHOST);
```

## Impact
A crafted server name (e.g. one containing spaces, `;`, `&`, or an unbalanced quote) breaks out of the intended `-Server` argument. Because `-ExecutionPolicy unrestricted` is used and `UseShellExecute = true`, this enables argument/command injection running under the (often elevated) account executing vHC. Even absent a malicious actor, a server name with a space silently corrupts the arguments and the collection fails opaquely. The other collection paths already escape — these two were missed, making this an inconsistency bug as well as a security hole.

## Suggested Fix
Escape and quote both fields consistently with the rest of the class, and prefer `UseShellExecute = false`:
```csharp
string escapedServer = CredentialHelper.EscapeForPowerShellDoubleQuotes(server);
string escapedPath   = CredentialHelper.EscapeForPowerShellDoubleQuotes(path);
argString = $"-NoProfile -ExecutionPolicy Bypass -file \"{scriptLocation}\" " +
            $"-Server \"{escapedServer}\" -ReportPath \"{escapedPath}\"";
```
Apply the same to `ServerDumpInfo`. Validate `server` against an allowed hostname/FQDN/IP pattern before use.

## Labels
security, powershell-injection, input-validation, collection

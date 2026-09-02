---
title: "REMOTEHOST is used unsanitized as a filesystem path component (output-dir traversal)"
severity: Low
labels: [security, reliability]
domain: collection-security
files:
  - vHC/HC_Reporting/Startup/CVariables.cs:84
  - vHC/HC_Reporting/Startup/CVariables.cs:92
confidence: Medium
---

## Summary
`CVariables.GetVbrDirWithTimestamp` builds the collection output directory by
calling `Path.Combine(basePath, serverName, timestamp)` where `serverName` is
`CGlobals.REMOTEHOST` (or `VBRServerName`) used verbatim. Neither value is
validated against path traversal or invalid path characters before becoming a
directory name that the collectors write CSVs into.

## Impact
A host string containing `..\` or an absolute-path fragment redirects where
collected (unscrubbed) configuration CSVs are written — potentially outside the
intended `C:\temp\vHC\Original\VBR\` tree, overwriting unrelated files or planting
data in an attacker-chosen location. `REMOTEHOST` is operator-supplied via
`/server:` and the GUI, and can be seeded from a managed-server list, so it is not
guaranteed to be a clean hostname. Severity is Low because exploitation requires
controlling the host argument, but it is unsanitized data crossing into the
filesystem on a real write path.

## Evidence
`vHC/HC_Reporting/Startup/CVariables.cs:84`:
```csharp
string serverName = !string.IsNullOrEmpty(CGlobals.REMOTEHOST)
    ? CGlobals.REMOTEHOST
    : (string.IsNullOrEmpty(CGlobals.VBRServerName) ? "localhost" : CGlobals.VBRServerName);
...
string fullPath = Path.Combine(basePath, serverName, timestamp);   // serverName unvalidated
```

## Suggested fix
Sanitize the host before using it as a path segment: reject or replace
`Path.GetInvalidFileNameChars()` (which includes `\`, `/`, `:`) and explicitly
strip `..`. For example, map the host to a safe slug
(`Regex.Replace(host, "[^A-Za-z0-9._-]", "_")`) and assert the resolved
`fullPath` stays under `basePath` via `Path.GetFullPath` prefix check before
writing.

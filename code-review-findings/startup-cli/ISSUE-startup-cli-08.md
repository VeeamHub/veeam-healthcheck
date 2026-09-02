---
title: "/path= argument matching is case-fragile, unanchored, and logs the wrong variable"
severity: Medium
labels: [bug, maintainability]
domain: startup-cli
files:
  - vHC/HC_Reporting/Startup/CArgsParser.cs:229
confidence: High
---

## Summary
The `/path=` hotfix-path argument is matched by two separate case-sensitive regexes (`"/path=.*"` and `"/PATH=.*"`), so mixed-case forms like `/Path=C:\x` silently fall through and are ignored. The regexes are also unanchored (any argument *containing* the substring `/path=` matches). And both branches log `targetDir` (the `/outdir` value, default `C:\temp\vHC`) instead of the parsed `_hfdPath`, so the log lies about which path was set.

## Impact
`VeeamHealthCheck.exe /hotfix /Path=D:\logs` drops the path argument without any warning; the user then gets the interactive `Console.ReadLine()` prompt (which hangs an unattended run). When it does match, the "HFD path:" log line shows the output dir, not the hotfix path, sending troubleshooting in the wrong direction.

## Evidence
`vHC/HC_Reporting/Startup/CArgsParser.cs:229-236`:

```csharp
case var match when new Regex("/path=.*").IsMatch(a):
    _hfdPath = this.ParsePath(a);
    CGlobals.Logger.Info("HFD path: " + targetDir);   // wrong variable
    break;
case var match when new Regex("/PATH=.*").IsMatch(a):
    _hfdPath = this.ParsePath(a);
    CGlobals.Logger.Info("HFD path: " + targetDir);   // wrong variable
    break;
```

Contrast with the adjacent `/outdir=` and `/host=` cases, which correctly use `RegexOptions.IgnoreCase` in a single case.

## Suggested fix
Collapse to one case-insensitive, anchored match and log the parsed value:

```csharp
case var _ when Regex.IsMatch(a, "^/path=.+", RegexOptions.IgnoreCase):
    _hfdPath = this.ParsePath(a);
    CGlobals.Logger.Info("HFD path: " + _hfdPath);
    break;
```

(Better yet, use the simple `a.StartsWith("/path=", StringComparison.OrdinalIgnoreCase)` pattern already used for `/credfile=` — constructing a `Regex` per argument per case is wasteful.)

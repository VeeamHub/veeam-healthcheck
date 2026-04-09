# VBR Collection Layer

This directory contains the PowerShell scripts that collect Veeam Backup &
Replication (VBR) configuration data and write it to CSV files consumed by the
C# report compiler.

---

## Invocation flow

```
C# invoker (PSInvoker.cs / PowerShell7Executor.cs)
    └── Get-VBRConfig.ps1          (canonical entry point)
            ├── VbrConfig.json             (static configuration)
            └── vHC-VbrConfig\             (collector module)
                    ├── Initialize-VhcModule
                    ├── Invoke-VhcCollector  (wraps each collector)
                    └── Get-Vhc*, Invoke-Vhc*  (individual collectors)
```

The C# invoker always calls `Get-VBRConfig.ps1`, which is the single canonical
entry point. It reads configuration, imports the module, connects to VBR, runs
all collectors in dependency order, exports the collection manifest, and
disconnects.

---

## Files

| File | Purpose |
|------|---------|
| `Get-VBRConfig.ps1` | Canonical entry point. Reads `VbrConfig.json`, imports the `vHC-VbrConfig` module, connects to VBR, runs all collectors via `Invoke-VhcCollector`, exports the collection manifest, and disconnects. |
| `VbrConfig.json` | Static configuration for the collection run. Contains `LogLevel`, `DefaultOutputPath`, resource sizing `Thresholds` used by the concurrency analysis, and the `SecurityComplianceRuleNames` mapping (rule key → human-readable label). Edit this file to adjust thresholds or add new compliance rule mappings without touching script logic. |
| `Get-NasInfo.ps1` | Legacy standalone NAS information collector. Not part of the main health-check pipeline. |
| `vHC-VbrConfig\` | PowerShell module containing all individual collector functions. See [`vHC-VbrConfig/README.md`](vHC-VbrConfig/README.md) for full documentation. |

---

## Running manually

```powershell
# Minimal (integrated auth, default output path)
& ".\Get-VBRConfig.ps1" -VBRServer myserver -VBRVersion 12

# With credentials and explicit output path
& ".\Get-VBRConfig.ps1" -VBRServer myserver -VBRVersion 12 `
    -User admin -PasswordBase64 <base64-encoded-password> `
    -ReportPath "C:\temp\vHC\Original\VBR\myserver\20260101_120000"
```

CSV output is written to `<ReportPath>\<VBRServer>_<FileName>.csv`.

---

## Key design notes

**PowerShell version compatibility**
`Get-VBRConfig.ps1` and all module files target PowerShell 5.1
(`#Requires -Version 5.1`) and are also compatible with PowerShell 7+. VBR
versions below 13 require PS 5.1 (enforced at runtime).
The Veeam PS snapin is used under PS 5.1; the `Veeam.Backup.PowerShell` module
is used under PS 7+.

**Encoding requirements**
PS 5.1 reads `.ps1` files as Windows-1252 by default. Non-ASCII characters
(em dashes, arrows, smart quotes, etc.) in string literals cause a parse error.
A UTF-8 BOM is itself non-ASCII and will also trigger the encoding test
(`AllVbrScripts_ShouldContainOnlyAsciiCharacters`). All scripts in this
directory and in `vHC-VbrConfig\` must contain **only ASCII characters** and
must **not include a UTF-8 BOM**.

**`$ErrorActionPreference = 'Stop'` and `Set-StrictMode -Version Latest`**
Both are set in `Get-VBRConfig.ps1` and inherited by module functions. This
means:
- All non-terminating errors become terminating (caught by collector try/catch blocks).
- Accessing `.Count` on a single-object `Where-Object` result (not an array) throws
  `PropertyNotFoundStrict`. Wrap such results in `@()` to force array semantics.
- Calling a method on `$null` throws immediately rather than silently continuing.

**`[Parameter()]` and CmdletBinding**
Any function with at least one `[Parameter()]` attribute implicitly gets
`[CmdletBinding()]`, which injects all PowerShell common parameters (`-Verbose`,
`-Confirm`, `-WhatIf`, etc.) into `$PSBoundParameters`. Splatting
`@PSBoundParameters` to a target that lacks `[CmdletBinding()]` will throw
`NamedParameterNotFound`.

**Collection manifest (ADR 0007)**
After each run, `Get-VBRConfig.ps1` writes `{VBRServer}_CollectionManifest.csv`
alongside the other CSVs. Schema: `Name, Success, DurationSeconds, Error, Timestamp`.
Failures are recorded from two sources:
- Catastrophic failures caught by `Invoke-VhcCollector` (the collector did not
  complete at all).
- Partial failures logged by individual collectors via `Add-VhciModuleError` and
  read back with `Get-VhcModuleErrors` before the manifest is written.

The C# layer reads this manifest via `CCsvValidator.LoadManifest()` and surfaces
failures in the HTML report (`DataCollectionSummaryTable()`), CLI warnings, and
GUI amber status indicators.

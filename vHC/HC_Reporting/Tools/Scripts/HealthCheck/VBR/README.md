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

`-Password` and `-PasswordBase64` are for manual runs only: anything on a command line is visible to other
local processes and to process-creation auditing. vHC itself supplies the password in the
`VHC_CRED_PASSWORD` environment variable, which the script reads and removes at startup, and you can do the
same by hand: `$env:VHC_CRED_PASSWORD = '<password>'`, then run the script with `-User` only.

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

## Maintenance / Diagnostic Scripts

These scripts are **not part of the VHC report pipeline** — they are developer and
maintainer tools that talk directly to the VBR REST API (port 9419, OAuth2).
They require PowerShell 7 and the VBR REST API to be enabled on the target server.

| Script | Purpose |
|--------|---------|
| `Check-CorruptionGuard-REST.ps1` | Reports Storage-level Corruption Guard settings for all jobs. Useful for verifying `backupHealth` field shapes before adding REST-based collection to the main pipeline. |
| `Check-SecurityComplianceDrift-REST.ps1` | Fetches live compliance results from a lab VBR, diffs rule types against `VbrConfig.json.SecurityComplianceRuleNames`, and reports any unmapped rules (i.e. new rules added since the last JSON update). Run this against your lab whenever a new VBR version is installed to detect label drift before it reaches customer reports. |

### Check-SecurityComplianceDrift-REST.ps1 — quick start

```powershell
# Set once per shell session (add to your profile for convenience)
$env:VBR_LAB_PRIMARY  = "vbr-v13.corp.example.com"   # VPN-on host
$env:VBR_LAB_FALLBACK = "vbr-v13-dmz.example.com"    # VPN-off reachable host
$env:VBR_LAB_USER     = "veeamadmin"
$env:VBR_LAB_PASSWORD = "hunter2"                    # gitignored; do not commit

# Run (probes primary first, falls back automatically based on reachability)
.\Check-SecurityComplianceDrift-REST.ps1

# Trigger a fresh scan before checking (non-destructive, waits up to 5 min)
.\Check-SecurityComplianceDrift-REST.ps1 -RunScan

# Export a JSON report for sharing or CI comparison
.\Check-SecurityComplianceDrift-REST.ps1 -Json -OutFile drift.json
```

**Exit codes:** `0` = no drift | `1` = drift detected | `2` = no lab reachable | `3` = auth/API error

**When drift is found:** look up the rule's console label in the Veeam Help Center
Security Analyzer documentation, add the entry to `VbrConfig.json.SecurityComplianceRuleNames`,
and bump `SecurityComplianceRulesValidatedForVbrVersion` to the current VBR version.

---

## Platform Identification

Starting with VBR 12.1, the health check collector can identify the hypervisor
platform for **External Infrastructure** (plug-in) backup jobs and propagate that
string into `_Servers.csv` and `_Jobs.csv` as a `Platform` column.

### Requirements

- VBR **12.1** or later (major version integer >= 12). Older versions return an
  empty platform map and the `Platform` column is written as empty string.
- The `Get-VBRBackupSession` cmdlet must be available (it is absent on standalone
  agents without the full VBR console snap-in).

### Canonical platform strings

The following 8 strings are the **only** canonical values produced by the VBR
`Platform.ToHumanReadable()` API. Case and spacing are significant.

| Platform string     | Product                               |
|---------------------|---------------------------------------|
| `Proxmox VE`        | Proxmox Virtual Environment           |
| `Nutanix AHV`       | Nutanix Acropolis Hypervisor          |
| `HPE Morpheus VME`  | HPE Morpheus Virtual Machine Engine   |
| `SC HyperCore`      | Scale Computing HyperCore             |
| `XCP-ng`            | XCP-ng hypervisor (Xen-based)         |
| `Sangfor HCI`       | Sangfor Hyper-Converged Infrastructure|
| `RHV`               | Red Hat Virtualization                |
| `Kasten`            | Kasten K10 Kubernetes data protection |

### How it works

1. `Get-VhciPlatformMap` (exported from the `vHC-VbrConfig` module) is called once in `Get-VBRConfig.ps1`
   before the server and job collectors. It returns a `[hashtable]` mapping
   lowercase server hostname → canonical platform string.
2. `Get-VhcServer` adds a `Platform` calculated property to each `_Servers.csv`
   row using `$script:PlatformMap`.
3. `Get-VhcJob` does the same for `_Jobs.csv` rows.
4. On the C# side, `CServerCsvInfos` and `CJobCsvInfos` read the `Platform`
   column (optional, backward-compatible). The field is propagated through
   `CServerTypeInfos` → `CManagedServer` and rendered in the Managed Server
   HTML table and the Job Info HTML table.

### Backward compatibility

Files produced by older collectors (without the `Platform` column) parse
cleanly because `[Optional]` is applied to the field in both CSV handler
classes.

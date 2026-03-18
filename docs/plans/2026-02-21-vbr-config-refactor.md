# Refactor VBR Config Monolith Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Decompose the 2,188-line `Get-VBRConfig.ps1` into a modular PowerShell module (`.psm1`) and a clean Orchestrator script, preserving all CSV output files and their exact schemas.

> **⚠️ Before starting Task 1:** Do a final count of `Export-VhcCsv` calls in the source `Get-VBRConfig.ps1` and confirm the exact number of CSV outputs. The plan references **59** throughout, but review identified a possible count of **60**. Update the collector inventory table and all references to match the confirmed count before any implementation begins.

**Architecture:** Orchestrator-Collector pattern with a Common Library module. State is passed via explicit parameters. Data flows through the PowerShell pipeline to minimise memory usage.

**Tech Stack:** PowerShell 5.1/7+, JSON for configuration, Pester (for unit testing if available).

---

## Reference: Complete Collector Inventory

The script produces **59 CSV files** (confirm count before implementation — see note above) across **16 logical collector groups**. All must be preserved in the refactor. Implementation tasks below are grouped to reflect real dependencies.

| Collector Function | Source Lines | CSV Output(s) |
|---|---|---|
| `Get-VhcUserRoles` | 193 | `_UserRoles.csv` |
| `Get-VhcServer` | 274–289 | `_Servers.csv` |
| `Get-VhcMajorVersion` *(orchestrator only, no CSV)* | 199–234 | — |
| `Get-VhcConcurrencyData` → sub-collectors below | 374–858 | *(see sub-collectors)* |
| &nbsp;&nbsp;↳ `Get-VhcGpProxy` | 447–490 | `_NasProxy.csv` |
| &nbsp;&nbsp;↳ `Get-VhcViHvProxy` | 492–553 | `_Proxies.csv`, `_HvProxy.csv` |
| &nbsp;&nbsp;↳ `Get-VhcCdpProxy` | 555–602 | `_CdpProxy.csv` |
| &nbsp;&nbsp;↳ `Get-VhcRepoGateway` | 604–689 | `_RepositoryServers.csv`, `_Gateways.csv` |
| `Invoke-VhcConcurrencyAnalysis` | 719–823 | `_AllServersRequirementsComparison.csv` |
| `Get-VhcEntraId` | 864–906 | `_entraTenants.csv`, `_entraLogJob.csv`, `_entraTenantJob.csv` |
| `Get-VhcCapacityTier` | 908–934 | `_capTier.csv` |
| `Get-VhcArchiveTier` | 936–951 | `_archTier.csv` |
| `Get-VhcTrafficRules` | 953–970 | `_trafficRules.csv` |
| `Get-VhcRegistrySettings` | 972–1003 | `_regkeys.csv` *(skipped if `$RemoteExecution -eq $true`)* |
| `Get-VhcRepository` | 1005–1089 | `_Repositories.csv`, `_SOBRs.csv`, `_SOBRExtents.csv` |
| `Get-VhcJob` *(main VBR jobs with restore points)* | 1090–1587 | `_Jobs.csv`, `_configBackup.csv` |
| &nbsp;&nbsp;↳ `Get-VhcAgentJob` | 1117–1145 | `_AgentBackupJob.csv`, `_EndpointJob.csv` |
| &nbsp;&nbsp;↳ `Get-VhcCatalystJob` | 1102–1122 | `_catCopyjob.csv`, `_catalystJob.csv` |
| &nbsp;&nbsp;↳ `Get-VhcTapeInfrastructure` | 1155–1212 | `_TapeJobs.csv`, `_TapeServers.csv`, `_TapeLibraries.csv`, `_TapeMediaPools.csv`, `_TapeVaults.csv` |
| &nbsp;&nbsp;↳ `Get-VhcNasJob` | 1213–1349 | `_nasBackup.csv`, `_nasBCJ.csv` |
| &nbsp;&nbsp;↳ `Get-VhcPluginAndCdpJob` | 1309–1349 | `_pluginjobs.csv`, `_cdpjobs.csv`, `_vcdjobs.csv` |
| &nbsp;&nbsp;↳ `Get-VhcReplication` | 1350–1385 | `_ReplicaJobs.csv`, `_Replicas.csv`, `_FailoverPlans.csv` |
| &nbsp;&nbsp;↳ `Get-VhcSureBackup` | 1147–1153, 1386–1409 | `_SureBackupJob.csv`, `_SureBackupAppGroups.csv`, `_SureBackupVirtualLabs.csv` |
| &nbsp;&nbsp;↳ `Get-VhcCloudConnect` | 1411–1433 | `_CloudGateways.csv`, `_CloudTenants.csv` |
| &nbsp;&nbsp;↳ `Get-VhcCredentialsAndNotifications` | 1435–1457 | `_EmailNotification.csv`, `_Credentials.csv` |
| `Get-VhcWanAccelerator` | 1606–1622 | `_WanAcc.csv` |
| `Get-VhcLicense` | 1627–1654 | `_LicInfo.csv` |
| `Get-VhcMalwareDetection` *(v12+ only)* | 1662–1672 | `_malware_settings.csv`, `_malware_infectedobject.csv`, `_malware_events.csv`, `_malware_exclusions.csv` |
| `Get-VhcSecurityCompliance` *(v12+ only)* | 1677–1991 | `_SecurityCompliance.csv` |
| `Get-VhcProtectedWorkloads` | 2000–2099 | `_PhysProtected.csv`, `_PhysNotProtected.csv`, `_HvProtected.csv`, `_HvUnprotected.csv`, `_ViProtected.csv`, `_ViUnprotected.csv` |
| `Get-VhcVbrInfo` *(full DB/version/MFA info)* | 2109–2176 | `_vbrinfo.csv` |

---

## Architectural Notes

### Cross-Collector State Dependencies

These dependencies **must be respected** in the orchestrator's execution order:

1. **`$VServers`** — Returned by `Get-VhcServer`. Must be collected first and passed as a parameter to `Get-VhcConcurrencyData`, `Get-VhcGpProxy`, `Get-VhcViHvProxy`, `Get-VhcCdpProxy`, and `Get-VhcRepoGateway`, all of which call `.GetPhysicalHost()` on server objects.

2. **`$hostRoles` hashtable** — Built incrementally *across* four sub-collectors (`Get-VhcGpProxy`, `Get-VhcViHvProxy`, `Get-VhcCdpProxy`, `Get-VhcRepoGateway`). The cleanest solution is to make `Get-VhcConcurrencyData` a single parent function that owns the `$hostRoles` hashtable and calls the four sub-collectors internally, then passes the completed hashtable to `Invoke-VhcConcurrencyAnalysis`.

3. **`$RepositoryDetails`** (ID→Name map) — Built during `Get-VhcRepository`. Must be passed to `Get-VhcJob` so it can resolve `TargetRepositoryId` to a human-readable name in `_Jobs.csv`.

4. **`$VBRVersion`** (major integer) — Returned by `Get-VhcMajorVersion`. Used by `Get-VhcRepository` (SOBR extent property differs ≥v12), `Get-VhcSecurityCompliance` (v12 vs v13 API split), `Get-VhcMalwareDetection` (v12+ only), and `Get-VhcVbrInfo` (registry vs API paths).

### Output Filename Format

`Export-VhcCsv` writes files as `"$ReportPath\$VBRServer$FileName"` (line 63 of source), meaning every output file is **prefixed with the server name** at runtime (e.g., `myserver_Jobs.csv`). The `_X.csv` names throughout this document are the `$FileName` suffix only. This prefix must be preserved in the refactored version — do not change the output path construction.

### `Export-VhcCsv` Scope Fix

`Export-VhcCsv` currently relies on `$ReportPath` and `$VBRServer` from script scope (lines 57–67). When promoted to the module, it must accept these explicitly:
```powershell
function Export-VhcCsv {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory)] [string] $FileName,
        [Parameter(Mandatory)] [string] $ReportPath,
        [Parameter(Mandatory)] [string] $VBRServer,
        [Parameter(ValueFromPipeline)] $InputObject
    )
    ...
}
```
The `Invoke-VhcCollector` wrapper can handle injecting `$ReportPath` and `$VBRServer` so individual collector calls remain clean.

### CSV Column Order — Use `[pscustomobject][ordered]@{}`

`Export-Csv` column order is determined by the property order of the object piped into it. To guarantee stable, deterministic column order across all collectors and PowerShell versions, **all object construction in this module must use `[pscustomobject][ordered]@{}`**, not plain `[pscustomobject]@{}`.

Plain hashtables are unordered in PowerShell 5.1 — relying on accidental insertion order will silently reorder CSV columns. The `[ordered]` modifier creates an `OrderedDictionary` which preserves insertion order through the `[pscustomobject]` cast:

```powershell
# Correct — column order is guaranteed
[pscustomobject][ordered]@{
    Name   = $job.Name
    Status = $job.LastResult
    RepoId = $job.Info.TargetRepositoryId
}

# Incorrect — column order is non-deterministic in PS 5.1
[pscustomobject]@{
    Name   = $job.Name
    Status = $job.LastResult
    RepoId = $job.Info.TargetRepositoryId
}
```

When extracting each collector, match the property order to the original `Select-Object` column sequence in `Get-VBRConfig.ps1` to ensure CSV output is identical. This is a prerequisite for the CSV parity verification in Task 10.

### `VbrConfig.json` vs Existing `CollectorConfig.json`

The existing script already loads `CollectorConfig.json` from `$global:SETTINGS.OutputPath` (lines 71–82) and merges it into `$global:SETTINGS`. The new `VbrConfig.json` **replaces `CollectorConfig.json` for this VBR script only** — note that VB365 and other scripts in the repo have their own settings loading and must not be affected. It should contain:
- All hardcoded concurrency thresholds (currently scattered at lines 299–332): proxy RAM/CPU, repo RAM/CPU, CDP proxy, Backup Server per-version, and **SQL server thresholds** (`$SQLRAMReq = 2`, `$SQLCPUReq = 1` at lines 330–332)
- Default output path and log level
- `ReportingIntervalDays` (currently `$ReportInterval` parameter default = 14)
- The Security & Compliance `$RuleTypes` rule-name mapping (currently hardcoded at lines 1761–1816) — moving this to JSON avoids code changes when new compliance rules are added

`$global:SETTINGS` should be eliminated in favour of a typed config object injected via parameter splatting.

### `Initialize-VhcModule` — Module-Level State Injection

"State is passed via explicit parameters" applies to collector function signatures. `Initialize-VhcModule` is not a contradiction: it is the one-time call that sets module-level `$script:` variables (`$script:ReportPath`, `$script:VBRServer`, `$script:LogLevel`, `$script:ReportInterval`) which `Export-VhcCsv` and `Write-LogFile` read internally. Collectors themselves still receive domain-specific inputs as explicit parameters (e.g., `-VServers`, `-Config`, `-VBRVersion`). The module-level variables are infra concerns that do not need to propagate through every function signature.

### Prerequisite Failure Cascade Strategy

`Invoke-VhcCollector` must not re-throw, but the Orchestrator must handle critical prerequisite failures explicitly:
- If `Get-VhcServer` fails (returns `$null`), the Orchestrator must **abort** and not call `Get-VhcConcurrencyData` with a null `$VServers`. Log the failure and exit.
- If `Get-VhcRepository` fails (returns `$null` `$RepositoryDetails`), `Get-VhcJob` must tolerate a null map (repo names will simply be blank in `_Jobs.csv`).
- All other collectors are independent and a failure produces an empty/partial CSV but does not block subsequent collectors.

### Known Code Issues to Address During Refactor

The following bugs exist in the source script and should be fixed (not preserved) during the refactor:

1. **Undefined variables in concurrency calculation**: `$CDPProxyOSCPUReq` and `$CDPProxyOSRAMReq` are referenced at lines 733 and 737 but never defined anywhere in the script. These should be defined in `VbrConfig.json` alongside the other CDP proxy thresholds (or defaulted to 0 with a comment).

2. **`Write-LogFile` `ValidateSet` violation**: `Write-LogFile` has `ValidateSet("Main", "Errors", "NoResult")` on the `$LogName` parameter (line 88) but is called with `"Warnings"` at lines 1251 and 1522, which is not in the set. This causes a silent runtime error in strict mode. During the refactor, either add `"Warnings"` to the `ValidateSet` or rename the callsites.

3. **Dead "general collection" stub**: Lines 237–255 collect `$pg = Get-VBRProtectionGroup` and `$pc = Get-VBRDiscoveredComputer` but neither variable is used again (`$phys` in the protected workloads section is a separate call). Remove in Task 10 cleanup.

4. **`AddTypeInfo` dead function**: Defined at lines 2183–2186 but never called. Remove in Task 10 cleanup.

### C# Invoker Integration

The following C# files reference `Get-VBRConfig.ps1` by path and must be updated once `VBR-Orchestrator.ps1` is the canonical entry point:
- `vHC/HC_Reporting/Functions/Collection/PSCollections/PSInvoker.cs`
- `vHC/HC_Reporting/Functions/Collection/PSCollections/PowerShell7Executor.cs`

Check `vHC/VhcXTests/` for any integration tests that invoke the script by name and update accordingly. This is a post-refactor step but must not be forgotten.

### Parameters the Orchestrator Must Carry Forward

| Parameter | Type | Notes |
|---|---|---|
| `$VBRServer` | string | Mandatory |
| `$VBRVersion` | int | Overridden at runtime by `Get-VhcMajorVersion` |
| `$User` | string | Optional credential |
| `$Password` | string | Plain-text password fallback. **Retained for backward compatibility with manual CLI invocations only** — the C# invoker exclusively uses `$PasswordBase64`. Add a deprecation comment in the orchestrator parameter block. |
| `$PasswordBase64` | string | Base64-encoded password; decoded inline |
| `$RemoteExecution` | bool | Gates registry collection in `Get-VhcRegistrySettings` |
| `$ReportPath` | string | Defaults to `C:\temp\vHC\Original\VBR\$VBRServer\$timestamp` |
| `$ReportInterval` | int | Days lookback for NAS session queries; default 14 |
| `$RescanHosts` | switch | Triggers `Rescan-VBREntity -AllHosts -Wait` before collection |

### Veeam Module Loading

The PS5.1 PSSnapIn fallback logic (lines 126–143) must move to the Orchestrator's initialisation section. It is not collector logic and does not belong in the module.

---

## Module File Structure

All collector functions live in individual `.ps1` files — one function per file — following the standard split-file PSModule pattern. The `.psm1` root file contains only the dot-sourcing loop and `Export-ModuleMember` call; no function definitions live there. This keeps each unit of logic independently navigable and reviewable, consistent with the DRY/SOLID/KISS objectives of the refactor.

**Public/** contains the 20 functions exported to and called directly by `VBR-Orchestrator.ps1`. **Private/** contains the 19 internal sub-collectors and helpers that are only accessible within the module.

```
vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/
├── Get-NasInfo.ps1                                    (existing – unchanged)
├── Get-VBRConfig.ps1                                  (existing – becomes backward-compat shim, Task 11)
├── Get-VeeamSessionReport.ps1                         (existing – unchanged)
├── Get-VeeamSessionReportVersion13.ps1                (existing – unchanged)
├── VBR-Orchestrator.ps1                               (new – entry point, Task 1)
├── VbrConfig.json                                     (new – thresholds and config, Task 1)
└── vHC-VbrConfig/                                     (new – PowerShell module folder, Task 1)
    ├── vHC-VbrConfig.psd1                             (new – module manifest, Task 1)
    ├── vHC-VbrConfig.psm1                             (new – dot-sources Public/ and Private/, Task 1)
    ├── Public/                                        (functions exported to VBR-Orchestrator.ps1)
    │   ├── Initialize-VhcModule.ps1                   (Task 2)
    │   ├── Invoke-VhcCollector.ps1                    (Task 2)
    │   ├── Get-VhcMajorVersion.ps1                    (Task 3)
    │   ├── Get-VhcVbrInfo.ps1                         (Task 3)
    │   ├── Get-VhcUserRoles.ps1                       (Task 4)
    │   ├── Get-VhcServer.ps1                          (Task 4)
    │   ├── Get-VhcConcurrencyData.ps1                 (Task 5)
    │   ├── Invoke-VhcConcurrencyAnalysis.ps1          (Task 5)
    │   ├── Get-VhcEntraId.ps1                         (Task 6)
    │   ├── Get-VhcCapacityTier.ps1                    (Task 6)
    │   ├── Get-VhcArchiveTier.ps1                     (Task 6)
    │   ├── Get-VhcTrafficRules.ps1                    (Task 6)
    │   ├── Get-VhcRegistrySettings.ps1                (Task 6)
    │   ├── Get-VhcRepository.ps1                      (Task 6)
    │   ├── Get-VhcJob.ps1                             (Task 7)
    │   ├── Get-VhcMalwareDetection.ps1                (Task 8)
    │   ├── Get-VhcSecurityCompliance.ps1              (Task 8)
    │   ├── Get-VhcProtectedWorkloads.ps1              (Task 8)
    │   ├── Get-VhcWanAccelerator.ps1                  (Task 9)
    │   └── Get-VhcLicense.ps1                         (Task 9)
    └── Private/                                       (internal sub-collectors and helpers)
        ├── Export-VhcCsv.ps1                          (Task 2)
        ├── Write-LogFile.ps1                          (Task 2)
        ├── ConvertToGB.ps1                            (Task 5)
        ├── EnsureNonNegative.ps1                      (Task 5)
        ├── Get-SqlSName.ps1                           (Task 5)
        ├── SafeValue.ps1                              (Task 5)
        ├── Get-VhcGpProxy.ps1                         (Task 5)
        ├── Get-VhcViHvProxy.ps1                       (Task 5)
        ├── Get-VhcCdpProxy.ps1                        (Task 5)
        ├── Get-VhcRepoGateway.ps1                     (Task 5)
        ├── Get-VhcAgentJob.ps1                        (Task 7)
        ├── Get-VhcCatalystJob.ps1                     (Task 7)
        ├── Get-VhcTapeInfrastructure.ps1              (Task 7)
        ├── Get-VhcNasJob.ps1                          (Task 7)
        ├── Get-VhcPluginAndCdpJob.ps1                 (Task 7)
        ├── Get-VhcReplication.ps1                     (Task 7)
        ├── Get-VhcSureBackup.ps1                      (Task 7)
        ├── Get-VhcCloudConnect.ps1                    (Task 7)
        └── Get-VhcCredentialsAndNotifications.ps1     (Task 7)
```

---

## Implementation Tasks

### Task 1: Module and Orchestrator Scaffolding

**Files:**
- Create: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/VBR-Orchestrator.ps1`
- Create: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/VbrConfig.json`
- Create: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/` *(module folder)*
- Create: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/vHC-VbrConfig.psm1`
- Create: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/vHC-VbrConfig.psd1`

**Step 1: Create `VbrConfig.json`**
Populate with all concurrency thresholds from lines 299–332 (including `SqlRAMMin`/`SqlCPUMin` at lines 330–332 and `CdpProxyOSCPU`/`CdpProxyOSRAM` which are currently undefined bugs — set to `0`) plus `LogLevel`, `DefaultOutputPath`, `ReportingIntervalDays`, and the `SecurityComplianceRuleNames` mapping (the `$RuleTypes` hashtable from lines 1761–1816). Example structure:
```json
{
  "ConfigVersion": 1,
  "LogLevel": "INFO",
  "DefaultOutputPath": "C:\\temp\\vHC\\Original\\VBR",
  "ReportingIntervalDays": 14,
  "Thresholds": {
    "VpProxyRAMPerTask": 1,
    "VpProxyCPUPerTask": 0.5,
    "VpProxyOSCPU": 2,
    "VpProxyOSRAM": 2,
    "GpProxyRAMPerTask": 4,
    "GpProxyCPUPerTask": 2,
    "GpProxyOSCPU": 2,
    "GpProxyOSRAM": 4,
    "RepoGwRAMPerTask": 1,
    "RepoGwCPUPerTask": 0.5,
    "RepoOSCPU": 1,
    "RepoOSRAM": 4,
    "CdpProxyRAM": 8,
    "CdpProxyCPU": 4,
    "CdpProxyOSCPU": 0,
    "CdpProxyOSRAM": 0,
    "BackupServerCPU_v12": 4,
    "BackupServerRAM_v12": 8,
    "BackupServerCPU_v13": 8,
    "BackupServerRAM_v13": 16,
    "SqlRAMMin": 2,
    "SqlCPUMin": 1
  }
}
```

**Step 2: Create module folder structure**

Create the `vHC-VbrConfig/` directory with empty `Public/` and `Private/` subfolders. Create the two module root files:

`vHC-VbrConfig/vHC-VbrConfig.psm1` — dot-sourcing root only; no function definitions:
```powershell
#Requires -Version 5.1

$Public  = @(Get-ChildItem -Path "$PSScriptRoot\Public\*.ps1"  -ErrorAction SilentlyContinue)
$Private = @(Get-ChildItem -Path "$PSScriptRoot\Private\*.ps1" -ErrorAction SilentlyContinue)

foreach ($import in ($Public + $Private)) {
    try {
        . $import.FullName
    } catch {
        throw "Failed to import function from $($import.FullName): $_"
    }
}

Export-ModuleMember -Function $Public.BaseName
```

> **Note:** Import failures must `throw` (not `Write-Error` and continue). A failed dot-source creates a partially-loaded module that fails later in confusing ways. Fail fast and loud.

`vHC-VbrConfig/vHC-VbrConfig.psd1` — module manifest:
```powershell
@{
    ModuleVersion     = '1.0.0'
    RootModule        = 'vHC-VbrConfig.psm1'
    PowerShellVersion = '5.1'
    Description       = 'VBR configuration collector module for Veeam Health Check'
    Author            = 'Veeam Health Check'
    FunctionsToExport = '*'  # Lock down to explicit list in Task 9 once all Public functions exist
}
```

> **`FunctionsToExport = '*'` during development.** Do NOT use `@()` here — an empty array prevents all exports regardless of `Export-ModuleMember`, which would silently break every verification step in Tasks 2–8. Replace `'*'` with the full explicit list of 20 Public function names in Task 9.

**Step 3: Create `VBR-Orchestrator.ps1`** with the full parameter block (all 8 parameters listed above), `VbrConfig.json` loading, and module import using the subfolder manifest path. Include PS5.1/PS7+ Veeam module loading (from lines 126–143). The module import must reference the manifest by path:
```powershell
Import-Module "$PSScriptRoot\vHC-VbrConfig\vHC-VbrConfig.psd1" -Force
```

After loading `VbrConfig.json`, validate that the required top-level keys are present (`ConfigVersion`, `Thresholds`, `SecurityComplianceRuleNames`). Fail fast with a descriptive error if any are missing — do not let a malformed config produce silent null-reference failures mid-run:
```powershell
foreach ($key in @('ConfigVersion', 'Thresholds', 'SecurityComplianceRuleNames')) {
    if ($null -eq $config.$key) { throw "VbrConfig.json is missing required key: '$key'" }
}
```

Also add a PS edition guard for VBR version compatibility: if the detected VBR version is below 13 and running under PowerShell Core (`$PSVersionTable.PSEdition -eq 'Core'`), fail fast with a clear message directing the user to rerun under Windows PowerShell 5.1.

**Step 4: Verification**

Test module loading in isolation (does not require a live Veeam installation):
```powershell
pwsh -c "Import-Module ./vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/vHC-VbrConfig.psd1; Get-Module vHC-VbrConfig"
```
Expected: Module loads with 0 exported commands (no Public functions yet). No errors.

Full orchestrator invocation can only be tested once a Veeam environment is available.

**Step 5: Commit**
```bash
git add vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/
git commit -m "feat: initial module scaffold for VBR config refactor"
```

---

### Task 2: Implement Common Utilities (Logging & Export)

**Files:**
- Create: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Initialize-VhcModule.ps1`
- Create: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Invoke-VhcCollector.ps1`
- Create: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Private/Export-VhcCsv.ps1`
- Create: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Private/Write-LogFile.ps1`

**Step 1: Promote `Write-LogFile` to module**
Extract from lines 84–118. Preserve the `LogLevel` enum and `$global:SETTINGS.loglevel` filter, replacing the global with a module-scoped `$script:LogLevel` set by the Orchestrator via `Initialize-VhcModule`.

**Step 2: Promote and fix `Export-VhcCsv`**

> **⚠️ CRITICAL — Step 2a: Rename `$input` to `$InputObject`.** The existing function uses `$input` as a parameter name. `$input` is a PowerShell automatic variable; using it as a named parameter works by accident in the monolith but will produce unpredictable behaviour (empty or corrupt CSV files) in a module context. This rename must happen first, before any other changes in this step.

Extract from lines 57–67. Add explicit `$ReportPath` and `$VBRServer` parameters so the function has no script-scope dependency. Use `process {}` block to correctly handle pipeline input. Add a null-guard at the top of the function body to catch uninitialised module state:
```powershell
if (-not $script:ReportPath -or -not $script:VBRServer) {
    throw "Initialize-VhcModule must be called before Export-VhcCsv"
}
```

**Step 3: Implement `Invoke-VhcCollector` wrapper**
Create a function that accepts `-Name` (string) and `-Action` (scriptblock). It must:
- Log `[Name] Starting...` with a timestamp
- Execute the scriptblock inside `try/catch`
- Log `[Name] Completed in Xs` on success
- Log `[Name] FAILED: <message>` on error without re-throwing (so one collector failure does not abort the run)

**Return-value contract:** `Invoke-VhcCollector` must **return the scriptblock's output unchanged** on success, and `$null` on error.

**Which collectors bypass the wrapper:** Collectors that must return values used by downstream collectors (`Get-VhcServer` → `$VServers`, `Get-VhcConcurrencyData` → `$hostRoles`, `Get-VhcRepository` → `$RepositoryDetails`) should be called **directly** by the orchestrator, not via `Invoke-VhcCollector`. The wrapper is for fire-and-forget collectors. This split must be documented in the orchestrator with a comment above each call.

**Collector run summary:** Maintain a `$collectorResults` list in the orchestrator. After each `Invoke-VhcCollector` call, append a `[PSCustomObject]@{Name; Success; Duration; Error}` result (returned by the wrapper). Log a summary table at the end of the run so operators can see pass/fail per collector at a glance.

**Step 4: Implement `Initialize-VhcModule`**
An exported function called once by the Orchestrator to inject `$ReportPath`, `$VBRServer`, `$LogLevel`, and `$ReportInterval` into module-level script variables. This avoids threading these values through every collector call.

**Step 5: Verification**
Run a temporary test script that imports the module, calls `Initialize-VhcModule`, then calls `Invoke-VhcCollector` with a dummy scriptblock.
Expected: Log file is created with timestamps; CSV contains dummy data.

**Step 6: Commit**
```bash
git add vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/
git commit -m "feat: add common logging, export, and collector wrapper to module"
```

---

### Task 3: Version Detection

**Files:**
- Create: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcMajorVersion.ps1`
- Create: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcVbrInfo.ps1`
- Modify: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/VBR-Orchestrator.ps1`

**Step 1: Create `Get-VhcMajorVersion`**
Consolidate the two-path version detection from lines 199–234 into a single function returning an `[int]` major version. This function is called at the start of the orchestrator to set `$script:VBRVersion` for all version-gated collectors.
- For v13+: use `(Get-VBRBackupServerInfo).Build.Major`
- For v10–12: read `Veeam.Backup.Core.dll` `ProductVersion` from the registry `CorePath`

**Step 2: Create `Get-VhcVbrInfo`**
Extract the full detailed version/DB/MFA collection from lines 2109–2176 into a separate function. This outputs to `_vbrinfo.csv` and is the **last** collector to run (it reads many registry paths that should not block early collectors if they fail).
- Handles the MFA global setting via `[Veeam.Backup.Core.SBackupOptions]::get_GlobalMFA()` in a try/catch (pre-v12 will fail gracefully).
- Handles both PostgreSQL and MS SQL registry paths.

**Step 3: Integrate into Orchestrator**
Call `Get-VhcMajorVersion` immediately after connecting and store the result. Pass `$VBRVersion` via splatting to all collectors that branch on it.

**Step 4: Commit**
```bash
git add vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/
git add vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/VBR-Orchestrator.ps1
git commit -m "feat: extract version detection into Get-VhcMajorVersion and Get-VhcVbrInfo"
```

---

### Task 4: Server and User Role Collectors

**Files:**
- Create: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcUserRoles.ps1`
- Create: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcServer.ps1`
- Modify: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/VBR-Orchestrator.ps1`

**Step 1: Create `Get-VhcUserRoles`**
Extract line 193. Wraps `Get-VBRUserRoleAssignment | Export-VhcCsv`. Simple pass-through.

**Step 2: Create `Get-VhcServer`**
Extract lines 274–289. Returns the `$Servers` array to the Orchestrator (do not only export — the return value is needed by concurrency collectors). Also pipes to `Export-VhcCsv -FileName '_Servers.csv'`.
> **Note:** The Orchestrator must store the return value: `$VServers = Get-VhcServer`

**Step 3: Commit**
```bash
git add vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/
git add vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/VBR-Orchestrator.ps1
git commit -m "feat: extract UserRoles and Server collectors"
```

---

### Task 5: Concurrency Data Collection and Analysis

**Files:**
- Create: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcConcurrencyData.ps1`
- Create: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Invoke-VhcConcurrencyAnalysis.ps1`
- Create: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Private/Get-VhcGpProxy.ps1`
- Create: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Private/Get-VhcViHvProxy.ps1`
- Create: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Private/Get-VhcCdpProxy.ps1`
- Create: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Private/Get-VhcRepoGateway.ps1`
- Create: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Private/ConvertToGB.ps1`
- Create: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Private/EnsureNonNegative.ps1`
- Create: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Private/Get-SqlSName.ps1`
- Create: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Private/SafeValue.ps1`
- Modify: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/VBR-Orchestrator.ps1`

This task covers lines 374–858. The data gathering and analysis are split into two functions, but they are implemented together as they share the `$hostRoles` hashtable.

**Step 1: Create `Get-VhcConcurrencyData`**
This parent function accepts `-VServers`, `-Config` (the thresholds object from `VbrConfig.json`), `-VBRServer`, and `-VBRVersion`. It owns the `$hostRoles` hashtable and calls four internal helper functions in sequence:
- `Get-VhcGpProxy` (lines 447–490): GP/NAS proxies → `_NasProxy.csv`
- `Get-VhcViHvProxy` (lines 492–553): VMware and Hyper-V proxies → `_Proxies.csv`, `_HvProxy.csv`
- `Get-VhcCdpProxy` (lines 555–602): CDP proxies → `_CdpProxy.csv`
- `Get-VhcRepoGateway` (lines 604–689): Repositories and gateway servers → `_RepositoryServers.csv`, `_Gateways.csv`

Each helper appends its role data to the shared `$hostRoles` hashtable. After all four run, `Get-VhcConcurrencyData` adds BackupServer and SQL Server roles (lines 691–717) and returns the completed `$hostRoles` to the Orchestrator.

Helper functions (`Get-SqlSName`, `ConverttoGB`, `EnsureNonNegative`, `SafeValue`) must be promoted to private module functions.

**Step 2: Create `Invoke-VhcConcurrencyAnalysis`**
Accepts `-HostRoles`, `-Config`, `-VBRVersion`, and `-BackupServerName`. Implements the requirements calculation loop (lines 719–823). Returns `$RequirementsComparison` and exports `_AllServersRequirementsComparison.csv`.

> **⚠️ Before implementing this step:** Explicitly categorise the following known behavioral discrepancies as either *compatibility-preserving* (output must match old script exactly) or *intentional fix* (output change is acceptable and documented):
> 1. **Backup server name check:** `if ($serverName -contains $BackupServerName)` applies `-contains` to a string, which is likely wrong (should be `-eq`). Fixing this may change which rows appear in `_AllServersRequirementsComparison.csv`.
> 2. **CDP OS overhead omission:** `$CdpProxyOSCPU`/`$CdpProxyOSRAM` are computed but never added into `RequiredCores`/`RequiredRAM`. Adding them (now that they're defined in `VbrConfig.json`) changes the output. Decide whether to preserve the bug or fix it.
>
> Any intentional output changes must be noted in the commit message and signed off before Task 10 final integration.

**Step 3: Orchestrator wiring**
```powershell
$hostRoles = Get-VhcConcurrencyData -VServers $VServers -Config $config -VBRServer $VBRServer -VBRVersion $VBRVersion
Invoke-VhcConcurrencyAnalysis -HostRoles $hostRoles -Config $config -VBRVersion $VBRVersion -BackupServerName $VBRServer
```

**Step 4: Handle `$RescanHosts`**
The `$RescanHosts` switch from the Orchestrator parameters must be checked before `Get-VhcConcurrencyData` runs and `Rescan-VBREntity -AllHosts -Wait` called if set (lines 359–372).

**Step 5: Commit**
```bash
git add vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/
git add vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/VBR-Orchestrator.ps1
git commit -m "feat: extract concurrency data collection and analysis into module functions"
```

---

### Task 6: Infrastructure Collectors (Repos, EntraID, Tiers, Traffic, Registry)

**Files:**
- Create: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcEntraId.ps1`
- Create: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcCapacityTier.ps1`
- Create: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcArchiveTier.ps1`
- Create: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcTrafficRules.ps1`
- Create: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcRegistrySettings.ps1`
- Create: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcRepository.ps1`
- Modify: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/VBR-Orchestrator.ps1`

**Step 1: Create `Get-VhcRepository`**
Extract lines 1005–1089. Handles standard repos, SOBRs, and SOBR extents.
- Returns `$RepositoryDetails` (ID→Name map) to the Orchestrator for use by `Get-VhcJob`.
- The SOBR extent property path differs by version: `$VBRVersion -ge 12` adds the `gatewayHosts` computed property (line 1044). Accept `-VBRVersion` parameter to branch.
- Exports: `_Repositories.csv`, `_SOBRs.csv`, `_SOBRExtents.csv`

**Step 2: Create `Get-VhcEntraId`**
Extract lines 864–906. Exports `_entraTenants.csv`, `_entraLogJob.csv`, `_entraTenantJob.csv`. Wrap in a single try/catch — Entra ID cmdlets will throw on environments without an Entra tenant.

**Step 3: Create `Get-VhcCapacityTier`**
Extract lines 908–934. Note the `Type -eq 6` (DataCloudVault) immutability workaround (line 917–921) — this must be preserved verbatim. Exports `_capTier.csv`.

**Step 4: Create `Get-VhcArchiveTier`**
Extract lines 936–951. Exports `_archTier.csv`.

**Step 5: Create `Get-VhcTrafficRules`**
Extract lines 953–970. Simple pass-through. Exports `_trafficRules.csv`.

**Step 6: Create `Get-VhcRegistrySettings`**
Extract lines 972–1003. Must accept `-RemoteExecution` parameter and return early (with a log message) if `$RemoteExecution -eq $true`. Exports `_regkeys.csv`.

**Step 7: Commit**
```bash
git add vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/
git add vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/VBR-Orchestrator.ps1
git commit -m "feat: extract infrastructure collectors (Repo, EntraID, tiers, traffic, registry)"
```

---

### Task 7: Job Collection

**Files:**
- Create: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.ps1`
- Create: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Private/Get-VhcAgentJob.ps1`
- Create: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Private/Get-VhcCatalystJob.ps1`
- Create: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Private/Get-VhcTapeInfrastructure.ps1`
- Create: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Private/Get-VhcNasJob.ps1`
- Create: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Private/Get-VhcPluginAndCdpJob.ps1`
- Create: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Private/Get-VhcReplication.ps1`
- Create: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Private/Get-VhcSureBackup.ps1`
- Create: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Private/Get-VhcCloudConnect.ps1`
- Create: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Private/Get-VhcCredentialsAndNotifications.ps1`
- Modify: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/VBR-Orchestrator.ps1`

This is the largest and most complex block (lines 1090–1587, ~500 lines). It should be decomposed into `Get-VhcJob` (the main VBR job loop) plus sub-functions for each distinct job type family.

**Step 1: Create sub-function `Get-VhcAgentJob`** (lines 1117–1130)
Collects `Get-VBRComputerBackupJob` and `Get-VBREPJob`. Exports `_AgentBackupJob.csv`, `_EndpointJob.csv`.

**Step 2: Create sub-function `Get-VhcCatalystJob`** (lines 1102–1122)
Collects `Get-VBRCatalystCopyJob` and `Get-VBRCatalystJob`. Exports `_catCopyjob.csv`, `_catalystJob.csv`.

**Step 3: Create sub-function `Get-VhcTapeInfrastructure`** (lines 1155–1212)
Collects tape jobs, servers, libraries, media pools, and vaults. Exports `_TapeJobs.csv`, `_TapeServers.csv`, `_TapeLibraries.csv`, `_TapeMediaPools.csv`, `_TapeVaults.csv`.

**Step 4: Create sub-function `Get-VhcNasJob`** (lines 1213–1349)
Collects NAS backup jobs and NAS backup copy jobs. Note on the session lookup: the code queries sessions per NAS job name (one `Get-VBRBackupSession -Name $nasJob.Name` call per job), then builds a `$nasSessionLookup` hashtable keyed by `$session.JobId.ToString()`. The hashtable optimisation avoids re-iterating the session list when resolving each job, but does not eliminate the per-job session query. Preserve this pattern. Exports `_nasBackup.csv`, `_nasBCJ.csv`.

**Step 5: Create sub-function `Get-VhcPluginAndCdpJob`** (lines 1309–1349)
Collects Plugin jobs, CDP policies, and VCD replica jobs. Exports `_pluginjobs.csv`, `_cdpjobs.csv`, `_vcdjobs.csv`.

**Step 6: Create sub-function `Get-VhcReplication`** (lines 1350–1385)
Collects replica jobs, replicas, and failover plans. Exports `_ReplicaJobs.csv`, `_Replicas.csv`, `_FailoverPlans.csv`.

**Step 7: Create sub-function `Get-VhcSureBackup`** (lines 1147–1153 and 1386–1409)
Collects SureBackup jobs (lines 1147–1153), application groups, and virtual labs (lines 1386–1409). Note: `_SureBackupJob.csv` is exported at line 1153 inside the main jobs block; the app groups and virtual labs are collected later at 1386–1409. Group all three into this single sub-function. Exports `_SureBackupJob.csv`, `_SureBackupAppGroups.csv`, `_SureBackupVirtualLabs.csv`.

**Step 8: Create sub-function `Get-VhcCloudConnect`** (lines 1411–1433)
Collects Cloud Connect gateways and tenants. Exports `_CloudGateways.csv`, `_CloudTenants.csv`.

**Step 9: Create sub-function `Get-VhcCredentialsAndNotifications`** (lines 1435–1457)
Collects email notification settings and credentials (name/username only — Veeam never exposes passwords). Exports `_EmailNotification.csv`, `_Credentials.csv`.

**Step 10: Create `Get-VhcJob`** (lines 1090–1102, 1483–1587)
The main VBR job function. Calls all sub-functions above, then runs the main `Get-VBRJob` loop with restore point size calculation. Accepts `-RepositoryDetails` for repo name resolution. Must preserve the `$CalculatedOriginalSize` fallback chain (ApproxSize → IncludedSize). Exports `_Jobs.csv`, `_configBackup.csv`.

**Step 11: Commit**
```bash
git add vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/
git add vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/VBR-Orchestrator.ps1
git commit -m "feat: extract modular Job collection with all job type sub-functions"
```

---

### Task 8: Security, Malware, and Protected Workloads

**Files:**
- Create: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcMalwareDetection.ps1`
- Create: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcSecurityCompliance.ps1`
- Create: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcProtectedWorkloads.ps1`
- Modify: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/VBR-Orchestrator.ps1`

**Step 1: Create `Get-VhcMalwareDetection`** (lines 1662–1672)
Simple wrapper around four cmdlets. Must be gated on `$VBRVersion -ge 12`. Exports `_malware_settings.csv`, `_malware_infectedobject.csv`, `_malware_events.csv`, `_malware_exclusions.csv`.

**Step 2: Create `Get-VhcSecurityCompliance`** (lines 1677–1991)
This is the most complex single block. Accept `-VBRVersion` parameter. Key implementation details to preserve:
- Uses `Start-VBRSecurityComplianceAnalyzer` to trigger a fresh scan
- Polls for results every 3 seconds up to 45 seconds (not a fixed sleep)
- **v12**: reads results via `[Veeam.Backup.DBManager.CDBManager]::Instance.BestPractices.GetAll()` (direct .NET type access)
- **v13+**: reads results via `Get-VBRSecurityComplianceAnalyzerResults` cmdlet
- Maps raw rule type strings to human-readable names via the `$RuleTypes` hashtable (40+ entries, lines 1761–1816) — this hashtable should be moved to `VbrConfig.json`
- Maps status strings via `$StatusObj` hashtable (lines 1817–1822)
- **Unknown rule type fallback:** If a rule's `Type` is not present in the `SecurityComplianceRuleNames` mapping, do **not** drop the row. Output it with the raw `Type` string as the best-practice name and the raw `Status` string as-is. This preserves visibility of new compliance rules even when the JSON mapping is stale.
- Must be gated on `$VBRVersion -gt 11`
- Exports `_SecurityCompliance.csv`

**Step 3: Create `Get-VhcProtectedWorkloads`** (lines 2000–2099)
Handles VMware, Hyper-V, and physical workloads. Each sub-type has its own inner try/catch so a failure on one platform does not skip the others. Preserve the `GetLastOibs($true)` → `GetLastOibs()` fallback pattern (lines 2011–2017). Exports 6 CSV files.

**Step 4: Commit**
```bash
git add vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/
git add vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/VBR-Orchestrator.ps1
git commit -m "feat: extract Security, Malware, and Protected Workloads collectors"
```

---

### Task 9: Remaining Collectors (WAN, License, VBR Info)

**Files:**
- Create: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcWanAccelerator.ps1`
- Create: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcLicense.ps1`
- Modify: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/VBR-Orchestrator.ps1`

**Step 1: Create `Get-VhcWanAccelerator`** (lines 1606–1622)
Simple pass-through collector. Exports `_WanAcc.csv`.

**Step 2: Create `Get-VhcLicense`** (lines 1627–1654)
Extracts structured license summary including socket, instance, and capacity licence data. Exports `_LicInfo.csv`.

**Step 3: Integrate `Get-VhcVbrInfo`** (from Task 3 Step 2)
Confirm it runs last in the orchestrator sequence and handles the MFA global setting and both PostgreSQL/MS SQL registry paths gracefully.

**Step 4: Commit**
```bash
git add vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/
git add vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/VBR-Orchestrator.ps1
git commit -m "feat: extract WAN Accelerator, License, and VBR Info collectors"
```

---

### Task 10: Cleanup & Final Integration

**Files:**
- Modify: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/VBR-Orchestrator.ps1`

**Step 1: Finalize Orchestrator execution order**
The correct sequence must be:
1. Load config, validate required keys, init module
2. Load Veeam module/snapin *(PS edition guard: if VBR < v13 and running under `pwsh`, fail fast with a message to rerun under Windows PowerShell 5.1)*
3. Connect to VBR server (with Base64 credential handling)

Wrap steps 3–23 in a `try/finally` block so `Disconnect-VBRServer` (step 23) is always called, even if a prerequisite collector aborts the run early:

```powershell
try {
    # 3. Connect-VBRServer ...
    # 4-22. Collectors ...
} finally {
    Disconnect-VBRServer -Confirm:$false -ErrorAction SilentlyContinue
}
```

The inner collector sequence:

4. `Get-VhcMajorVersion` → sets `$VBRVersion`
5. Rescan hosts if `$RescanHosts`
6. `Get-VhcUserRoles`
7. `Get-VhcServer` → stores `$VServers` *(direct call — return value required)*
8. `Get-VhcConcurrencyData` → stores `$hostRoles` *(direct call — return value required)*
9. `Invoke-VhcConcurrencyAnalysis`
10. `Get-VhcEntraId`
11. `Get-VhcCapacityTier`
12. `Get-VhcArchiveTier`
13. `Get-VhcTrafficRules`
14. `Get-VhcRegistrySettings`
15. `Get-VhcRepository` → stores `$RepositoryDetails` *(direct call — return value required)*
16. `Get-VhcJob` (passing `$RepositoryDetails`)
17. `Get-VhcWanAccelerator`
18. `Get-VhcLicense`
19. `Get-VhcMalwareDetection` (v12+ only)
20. `Get-VhcSecurityCompliance` (v12+ only)
21. `Get-VhcProtectedWorkloads`
22. `Get-VhcVbrInfo`
23. `Disconnect-VBRServer` *(in `finally` block)*

**Step 2: Remove dead code**
Remove the `AddTypeInfo` function defined at lines 2183–2186 (defined but never called). Remove commented-out collection stubs and the template block at lines 259–272.

**Step 3: CSV schema parity verification**

> **This is the most critical correctness check of the entire refactor.** Before switching the C# invokers and before final commit, run both the original `Get-VBRConfig.ps1` and the new `VBR-Orchestrator.ps1` against the same VBR environment and diff the outputs:
> - All expected CSV file names are present in the new output
> - CSV column headers match exactly (including column order — see *CSV Column Order* note in Architectural Notes; all collectors must use `[pscustomobject][ordered]@{}`)
> - Row counts match for stable, deterministic collectors
>
> Re-enable or adapt `validate-csv-schemas.yml` for this verification. At minimum, add a unit-level check that constructs a known `[PSCustomObject]` with specific properties, pipes it through `Export-VhcCsv`, and asserts the resulting CSV header row is an exact string match.

**Step 4: Performance check**
Run the new orchestrator and compare memory usage against the original on a representative dataset.

**Step 5: Final commit**
```bash
git commit -am "chore: finalize orchestrator sequence, remove dead code, complete integration"
```

---

### Task 11: Update Project Packaging (VeeamHealthCheck.csproj)

**Files:**
- Modify: `vHC/HC_Reporting/VeeamHealthCheck.csproj`
- Modify: `vHC/HC_Reporting/Functions/Collection/PSCollections/PSInvoker.cs`
- Modify: `vHC/HC_Reporting/Functions/Collection/PSCollections/PowerShell7Executor.cs`
- Check: `vHC/VhcXTests/` (integration tests that reference the script by name)

PowerShell scripts are distributed alongside the EXE via `<Content Include>` entries in the csproj (with `CopyToOutputDirectory=PreserveNewest`). SDK-style csproj auto-detects all `.ps1`, `.psm1`, `.json`, and `.psd1` files as `<None>` items unless explicitly overridden, so each new file requires both a `<None Remove>` entry and a `<Content Include>` entry.

**Step 1: Add packaging entries for new files**

The module uses 39 individual `.ps1` files across `Public/` and `Private/` subfolders. Rather than listing each file explicitly (~42 `<None Remove>` + `<Content Include>` pairs, needing updates with every new function file), use glob patterns to cover the module folder tree.

Add to the `<None Remove>` ItemGroup (alongside the existing `Get-VBRConfig.ps1` entry):
```xml
<None Remove="Tools\Scripts\HealthCheck\VBR\VBR-Orchestrator.ps1" />
<None Remove="Tools\Scripts\HealthCheck\VBR\VbrConfig.json" />
<None Remove="Tools\Scripts\HealthCheck\VBR\vHC-VbrConfig\**\*" />
```

Add to the `<Content Include>` ItemGroup (alongside the existing `Get-VBRConfig.ps1` entry):
```xml
<Content Include="Tools\Scripts\HealthCheck\VBR\VBR-Orchestrator.ps1">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
</Content>
<Content Include="Tools\Scripts\HealthCheck\VBR\VbrConfig.json">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
</Content>
<Content Include="Tools\Scripts\HealthCheck\VBR\vHC-VbrConfig\**\*">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
</Content>
```

The `vHC-VbrConfig\**\*` glob covers `vHC-VbrConfig.psm1`, `vHC-VbrConfig.psd1`, all `Public/*.ps1`, and all `Private/*.ps1` in one entry — no further csproj changes required as new function files are added during implementation.

**Step 2: Update C# invokers**

> **⚠️ Delay this step until CSV parity has been confirmed (Task 10, Step 3).** The C# application will call `VBR-Orchestrator.ps1` once this change is made. Only switch after the new orchestrator has been verified to produce identical CSV output to the original script. Until then, `Get-VBRConfig.ps1` (shim) acts as the safe fallback entry point.

Both files below reference `Get-VBRConfig.ps1` by path and must be updated to point to `VBR-Orchestrator.ps1`:

- `vHC/HC_Reporting/Functions/Collection/PSCollections/PSInvoker.cs` line 30:
  ```csharp
  // Change:
  private readonly string vbrConfigScript = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Tools\Scripts\HealthCheck\VBR\Get-VBRConfig.ps1");
  // To:
  private readonly string vbrConfigScript = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Tools\Scripts\HealthCheck\VBR\VBR-Orchestrator.ps1");
  ```

- `vHC/HC_Reporting/Functions/Collection/PSCollections/PowerShell7Executor.cs` line 24:
  ```csharp
  // Change:
  private readonly string vbrConfigScript = Environment.CurrentDirectory + @"\Tools\Scripts\HealthCheck\VBR\Get-VBRConfig.ps1";
  // To:
  private readonly string vbrConfigScript = Environment.CurrentDirectory + @"\Tools\Scripts\HealthCheck\VBR\VBR-Orchestrator.ps1";
  ```

  > **Note:** `PowerShell7Executor.cs` uses `Environment.CurrentDirectory` which changes with the working directory in service contexts and is unreliable. `PSInvoker.cs` correctly uses `AppDomain.CurrentDomain.BaseDirectory`. Consider harmonising `PowerShell7Executor.cs` to use `AppDomain.CurrentDomain.BaseDirectory` at the same time as this path update.

**Step 3: Check integration tests**

Search `vHC/VhcXTests/` for any test that references `Get-VBRConfig.ps1` by string and update to `VBR-Orchestrator.ps1`. If any test invokes the script as a file path (e.g., `-File Get-VBRConfig.ps1`), update accordingly.

**Step 4: Decide on `Get-VBRConfig.ps1` retention**

Three options:
1. **Delete it** — clean, but breaks any external tooling or manual use that calls it by name.
2. **Replace with a shim** — a 3-line wrapper that calls `VBR-Orchestrator.ps1 @PSBoundParameters` for backward compatibility.
3. **Keep both** — safest short-term, but creates maintenance confusion.

Recommendation: replace with a shim until the next major version, then remove.

**Step 5: Build and verify**

```bash
dotnet publish vHC/HC_Reporting/VeeamHealthCheck.csproj -c Release
ls publish/out/Tools/Scripts/HealthCheck/VBR/
```

Expected: `VBR-Orchestrator.ps1`, `VbrConfig.json`, and `Get-VBRConfig.ps1` (shim) present at root level; `vHC-VbrConfig/` subfolder present containing `vHC-VbrConfig.psm1`, `vHC-VbrConfig.psd1`, and all `Public/` and `Private/` function files.

**Step 6: Commit**
```bash
git add vHC/HC_Reporting/VeeamHealthCheck.csproj \
        vHC/HC_Reporting/Functions/Collection/PSCollections/PSInvoker.cs \
        vHC/HC_Reporting/Functions/Collection/PSCollections/PowerShell7Executor.cs
git commit -m "build: add new VBR module files to csproj packaging and update C# invokers"
```

---

### Task 12: Update CI/CD Workflows

**Files:**
- Modify: `.github/workflows/ps51-syntax-validation.yml`

The `manual-release.yml` and `ci-cd.yaml` workflows do not need changes — the release workflow archives everything in `publish/out/` (populated by csproj), and the integration test workflow runs the EXE which picks up scripts automatically.

The `validate-csv-schemas.yml` workflow is currently disabled; since the refactor preserves all CSV schemas exactly, it will not need changes when re-enabled.

**Step 1: Replace hardcoded script lists with glob collection**

The `$ps51Scripts` array appears **twice** in this workflow file (line 38–44 for static analysis, line 108–112 for runtime parse). With 39 individual function files across `Public/` and `Private/` subfolders, hardcoding each path is impractical. Replace both arrays with glob-based discovery:

Replace the static-analysis array (first occurrence, ~line 38):
```powershell
$ps51Scripts = @(Get-ChildItem -Path @(
    "vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/*.ps1",
    "vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/*.ps1",
    "vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Private/*.ps1",
    "vHC/HC_Reporting/Tools/Scripts/HotfixDetection/*.ps1"
) -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName)
```

Replace the runtime-parse array (second occurrence, ~line 108) — same but without HotfixDetection (the runtime step uses `powershell` / PS5.1 shell and is scoped to VBR scripts only):
```powershell
$ps51Scripts = @(Get-ChildItem -Path @(
    "vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/*.ps1",
    "vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/*.ps1",
    "vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Private/*.ps1"
) -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName)
```

The `.psm1` root file contains the dot-source loop and `#Requires -Version 5.1` — any PS7-only syntax introduced there will silently break the Windows PowerShell 5.1 path if not validated. **Include it explicitly in both arrays** by adding `"vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/vHC-VbrConfig.psm1"` as a separate entry. This is not optional.

> **Also add a workflow trigger path:** Update the `on: push: paths:` filter in `ps51-syntax-validation.yml` to include `**/*.psm1` alongside `**/*.ps1`. Otherwise, changes to the module root file will not trigger the workflow at all.

> **Alternative — broader glob:** Replace all hardcoded arrays with:
> ```powershell
> $ps51Scripts = Get-ChildItem "vHC/HC_Reporting/Tools/Scripts/**/*.ps1","vHC/HC_Reporting/Tools/Scripts/**/*.psm1" |
>     Select-Object -ExpandProperty FullName
> ```
> This eliminates the need to update the workflow whenever a new file is added.

**Step 2: Update the job summary text**

Update the "Scripts Validated" section in the `Write job summary` step (~line 176) to reflect the new dynamic discovery:
```yaml
- ``VBR-Orchestrator.ps1`` - Main VBR configuration orchestrator (entry point)
- ``vHC-VbrConfig/Public/*.ps1`` - 20 exported collector functions
- ``vHC-VbrConfig/Private/*.ps1`` - 19 internal sub-collectors and helpers
- ``Get-VBRConfig.ps1`` - Legacy shim (until removed)
- ``Get-VeeamSessionReport.ps1`` - Session reporting
- ``Get-NasInfo.ps1`` - NAS information collection
```

**Step 3: Commit**
```bash
git add .github/workflows/ps51-syntax-validation.yml
git commit -m "ci: add VBR-Orchestrator.ps1 and vHC-VbrConfig.psm1 to PS5.1 syntax validation"
```

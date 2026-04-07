# Private Function Naming + $hostRoles Refactor Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Rename all private module functions from `Vhc` prefix to `Vhci` (Vhc-Internal) to make the public/private boundary obvious, fix `ConvertToGB` to `ConvertTo-GB` (standard verb-noun convention), and eliminate the shared-mutation `$hostRoles` pattern (ADR 0006 violation).

**Architecture:** Three independent but sequentially ordered changes. The naming rename is purely mechanical (file rename + function definition + call sites). The `$hostRoles` refactor removes the `[hashtable] $HostRoles` parameter from 4 sub-collectors, replaces the mutation side-effect with an explicit return value, and moves the merge loop into `Get-VhcConcurrencyData` while preserving each sub-collector's existing fault-isolation.

**Tech Stack:** PowerShell 5.1/7-compatible ASCII-only module scripts (`Public/` + `Private/`), existing C# integration/syntax tests for the PS layer in `vHC/VhcXTests` (Windows-only), plus grep-based rename verification.

---

## Background

### Module structure

```
vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/
├── vHC-VbrConfig.psm1       # Dot-sources Public/*.ps1 + Private/*.ps1; exports $Public.BaseName only
├── vHC-VbrConfig.psd1       # Manifest
├── Public/                  # 25 files — all exported, all use Get-Vhc* / Invoke-Vhc* naming
└── Private/                 # 25 files total; 22 rename targets are currently indistinguishable from Public by name
```

The orchestrator is `Get-VBRConfig.ps1` (one level up), which imports the module and calls public functions via `Invoke-VhcCollector`.

### Why Vhci?

`Vhci` = "Vhc-Internal". A developer reading `Get-VhciHostHardware` knows immediately it is not part of the public API without having to check the manifest or file location.

### Why $hostRoles matters

`Get-VhcConcurrencyData` creates a `$hostRoles = @{}` hashtable and passes it to 4 sub-collectors. Each sub-collector mutates the hashtable in-place (via `Add-VhcHostRoleEntry`), violating ADR 0006 (no shared business state via side-effects). The fix is: sub-collectors return role-entry descriptors; `Get-VhcConcurrencyData` owns the merge.

---

## Functions Being Renamed

### Private functions (22 total)

| Old Name | New Name | Old File | New File |
|---|---|---|---|
| `Add-VhcHostRoleEntry` | `Add-VhciHostRoleEntry` | `Add-VhcHostRoleEntry.ps1` | `Add-VhciHostRoleEntry.ps1` |
| `Add-VhcModuleError` | `Add-VhciModuleError` | `Add-VhcModuleError.ps1` | `Add-VhciModuleError.ps1` |
| `ConvertToGB` | `ConvertTo-GB` | `ConvertToGB.ps1` | `ConvertTo-GB.ps1` |
| `Export-VhcCsv` | `Export-VhciCsv` | `Export-VhcCsv.ps1` | `Export-VhciCsv.ps1` |
| `Get-VhcAgentJob` | `Get-VhciAgentJob` | `Get-VhcAgentJob.ps1` | `Get-VhciAgentJob.ps1` |
| `Get-VhcCatalystJob` | `Get-VhciCatalystJob` | `Get-VhcCatalystJob.ps1` | `Get-VhciCatalystJob.ps1` |
| `Get-VhcCdpProxy` | `Get-VhciCdpProxy` | `Get-VhcCdpProxy.ps1` | `Get-VhciCdpProxy.ps1` |
| `Get-VhcCloudConnect` | `Get-VhciCloudConnect` | `Get-VhcCloudConnect.ps1` | `Get-VhciCloudConnect.ps1` |
| `Get-VhcComplianceResults` | `Get-VhciComplianceResults` | `Get-VhcComplianceResults.ps1` | `Get-VhciComplianceResults.ps1` |
| `Get-VhcCredentialsAndNotifications` | `Get-VhciCredentialsAndNotifications` | `Get-VhcCredentialsAndNotifications.ps1` | `Get-VhciCredentialsAndNotifications.ps1` |
| `Get-VhcGpProxy` | `Get-VhciGpProxy` | `Get-VhcGpProxy.ps1` | `Get-VhciGpProxy.ps1` |
| `Get-VhcHostHardware` | `Get-VhciHostHardware` | `Get-VhcHostHardware.ps1` | `Get-VhciHostHardware.ps1` |
| `Get-VhcNasJob` | `Get-VhciNasJob` | `Get-VhcNasJob.ps1` | `Get-VhciNasJob.ps1` |
| `Get-VhcPluginAndCdpJob` | `Get-VhciPluginAndCdpJob` | `Get-VhcPluginAndCdpJob.ps1` | `Get-VhciPluginAndCdpJob.ps1` |
| `Get-VhcReplication` | `Get-VhciReplication` | `Get-VhcReplication.ps1` | `Get-VhciReplication.ps1` |
| `Get-VhcRepoGateway` | `Get-VhciRepoGateway` | `Get-VhcRepoGateway.ps1` | `Get-VhciRepoGateway.ps1` |
| `Get-VhcServerOsOverhead` | `Get-VhciServerOsOverhead` | `Get-VhcServerOsOverhead.ps1` | `Get-VhciServerOsOverhead.ps1` |
| `Get-VhcSessionLogWithTimeout` | `Get-VhciSessionLogWithTimeout` | `Get-VhcSessionLogWithTimeout.ps1` | `Get-VhciSessionLogWithTimeout.ps1` |
| `Get-VhcSureBackup` | `Get-VhciSureBackup` | `Get-VhcSureBackup.ps1` | `Get-VhciSureBackup.ps1` |
| `Get-VhcTapeInfrastructure` | `Get-VhciTapeInfrastructure` | `Get-VhcTapeInfrastructure.ps1` | `Get-VhciTapeInfrastructure.ps1` |
| `Get-VhcViHvProxy` | `Get-VhciViHvProxy` | `Get-VhcViHvProxy.ps1` | `Get-VhciViHvProxy.ps1` |
| `Invoke-VhcJobSubCollectors` | `Invoke-VhciJobSubCollectors` | `Invoke-VhcJobSubCollectors.ps1` | `Invoke-VhciJobSubCollectors.ps1` |

**Not renamed** (no `Vhc` prefix, no convention conflict): `SafeValue`, `EnsureNonNegative`, `Get-SqlSName`

---

## Task 1: Rename High-Volume Utilities (`Export-VhciCsv`, `Add-VhciModuleError`)

These two functions have the most call sites (62 and 30 respectively). Do them first so subsequent tasks don't mix old and new names.

**Files:**
- Rename: `Private/Export-VhcCsv.ps1` → `Private/Export-VhciCsv.ps1`
- Rename: `Private/Add-VhcModuleError.ps1` → `Private/Add-VhciModuleError.ps1`
- Modify: every `Public/*.ps1` and `Private/Invoke-VhcJobSubCollectors.ps1` (call sites)

**Step 1: Rename the files**

```bash
cd vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig
git mv Private/Export-VhcCsv.ps1 Private/Export-VhciCsv.ps1
git mv Private/Add-VhcModuleError.ps1 Private/Add-VhciModuleError.ps1
```

**Step 2: Update function definitions in the renamed files**

In `Private/Export-VhciCsv.ps1`: change `function Export-VhcCsv` → `function Export-VhciCsv`

In `Private/Add-VhciModuleError.ps1`: change `function Add-VhcModuleError` → `function Add-VhciModuleError`

**Step 3: Update all call sites for `Export-VhciCsv`**

Find all callers:
```bash
grep -rn "Export-VhcCsv" Public/ Private/
```

Expected: 62 matches across `Public/*.ps1` and `Private/*.ps1`.

Replace every `Export-VhcCsv` with `Export-VhciCsv` in each file found. Use Edit tool per file.

**Step 4: Update all call sites for `Add-VhciModuleError`**

Find all callers:
```bash
grep -rn "Add-VhcModuleError" Public/ Private/
```

Expected: 30 matches across `Public/*.ps1`, `Private/Invoke-VhcJobSubCollectors.ps1`, and the helper definition itself.

Replace every `Add-VhcModuleError` with `Add-VhciModuleError` in each file found.

**Step 5: Verify no old names remain**

```bash
grep -rn "Export-VhcCsv\|Add-VhcModuleError" Public/ Private/
```

Expected: zero output.

**Step 6: Commit**

```bash
git add -A
git commit -m "refactor(private): rename Export-VhcCsv and Add-VhcModuleError to Vhci prefix"
```

---

## Task 2: Rename Concurrency Sub-Collectors

These 7 rename targets are also involved in the `$hostRoles` refactor (Task 5). Rename them now so Task 5 works with the new names.

**Files:**
- Rename: `Private/Add-VhcHostRoleEntry.ps1` → `Private/Add-VhciHostRoleEntry.ps1`
- Rename: `Private/Get-VhcGpProxy.ps1` → `Private/Get-VhciGpProxy.ps1`
- Rename: `Private/Get-VhcViHvProxy.ps1` → `Private/Get-VhciViHvProxy.ps1`
- Rename: `Private/Get-VhcCdpProxy.ps1` → `Private/Get-VhciCdpProxy.ps1`
- Rename: `Private/Get-VhcRepoGateway.ps1` → `Private/Get-VhciRepoGateway.ps1`
- Rename: `Private/Get-VhcHostHardware.ps1` → `Private/Get-VhciHostHardware.ps1`
- Rename: `Private/Get-VhcServerOsOverhead.ps1` → `Private/Get-VhciServerOsOverhead.ps1`
- Modify: `Public/Get-VhcConcurrencyData.ps1` (caller of all proxy/gateway functions)
- Modify: `Public/Invoke-VhcConcurrencyAnalysis.ps1` (caller of `Get-VhciServerOsOverhead`)
- Modify: `Private/Get-VhciGpProxy.ps1`, `Get-VhciViHvProxy.ps1`, `Get-VhciCdpProxy.ps1`, `Get-VhciRepoGateway.ps1` (callers of `Get-VhciHostHardware` and `Add-VhciHostRoleEntry`)

**Step 1: Rename all 7 files**

```bash
cd vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig
git mv Private/Add-VhcHostRoleEntry.ps1   Private/Add-VhciHostRoleEntry.ps1
git mv Private/Get-VhcGpProxy.ps1         Private/Get-VhciGpProxy.ps1
git mv Private/Get-VhcViHvProxy.ps1       Private/Get-VhciViHvProxy.ps1
git mv Private/Get-VhcCdpProxy.ps1        Private/Get-VhciCdpProxy.ps1
git mv Private/Get-VhcRepoGateway.ps1     Private/Get-VhciRepoGateway.ps1
git mv Private/Get-VhcHostHardware.ps1    Private/Get-VhciHostHardware.ps1
git mv Private/Get-VhcServerOsOverhead.ps1 Private/Get-VhciServerOsOverhead.ps1
```

**Step 2: Update function definitions in all 7 renamed files**

In each renamed file, change `function Get-Vhc<X>` → `function Get-Vhci<X>` (or `Add-VhcHostRoleEntry` → `Add-VhciHostRoleEntry`).

**Step 3: Update call sites inside the Private sub-collectors**

All four renamed collectors call `Get-VhciHostHardware`, and all four still call `Add-VhciHostRoleEntry` until Task 5 completes. Be explicit about every site:

- `Get-VhciGpProxy.ps1`: 1 `Get-VhciHostHardware` call, 1 `Add-VhciHostRoleEntry` call
- `Get-VhciViHvProxy.ps1`: 1 fallback `Get-VhciHostHardware` call, 1 `Add-VhciHostRoleEntry` call
- `Get-VhciCdpProxy.ps1`: 1 `Get-VhciHostHardware` call, 1 `Add-VhciHostRoleEntry` call
- `Get-VhciRepoGateway.ps1`: 2 `Get-VhciHostHardware` calls, 2 `Add-VhciHostRoleEntry` calls

```bash
grep -n "Get-VhcHostHardware\|Add-VhcHostRoleEntry" Private/Get-Vhci*Proxy.ps1 Private/Get-VhciRepoGateway.ps1
```

Expected: zero output.

**Step 4: Update callers in Public/**

`Get-VhcConcurrencyData.ps1` calls: `Get-VhcGpProxy`, `Get-VhcViHvProxy`, `Get-VhcCdpProxy`, `Get-VhcRepoGateway`

`Invoke-VhcConcurrencyAnalysis.ps1` calls: `Get-VhcServerOsOverhead`

Find and replace in both files. Also check for `Add-VhcHostRoleEntry` in case it's called directly from Public (it isn't, but verify):

```bash
grep -rn "Get-VhcGpProxy\|Get-VhcViHvProxy\|Get-VhcCdpProxy\|Get-VhcRepoGateway\|Get-VhcHostHardware\|Get-VhcServerOsOverhead\|Add-VhcHostRoleEntry" Public/ Private/
```

Update every match.

**Step 5: Verify**

```bash
grep -rn "Get-VhcGpProxy\|Get-VhcViHvProxy\|Get-VhcCdpProxy\|Get-VhcRepoGateway\|Get-VhcHostHardware\|Get-VhcServerOsOverhead\|Add-VhcHostRoleEntry" Public/ Private/
```

Expected: zero output.

**Step 6: Commit**

```bash
git add -A
git commit -m "refactor(private): rename concurrency sub-collectors and hardware helpers to Vhci prefix"
```

---

## Task 3: Rename Job Sub-Collectors

**Files:**
- Rename: `Private/Invoke-VhcJobSubCollectors.ps1` → `Private/Invoke-VhciJobSubCollectors.ps1`
- Rename: `Private/Get-VhcAgentJob.ps1` → `Private/Get-VhciAgentJob.ps1`
- Rename: `Private/Get-VhcCatalystJob.ps1` → `Private/Get-VhciCatalystJob.ps1`
- Rename: `Private/Get-VhcNasJob.ps1` → `Private/Get-VhciNasJob.ps1`
- Rename: `Private/Get-VhcPluginAndCdpJob.ps1` → `Private/Get-VhciPluginAndCdpJob.ps1`
- Rename: `Private/Get-VhcReplication.ps1` → `Private/Get-VhciReplication.ps1`
- Rename: `Private/Get-VhcCloudConnect.ps1` → `Private/Get-VhciCloudConnect.ps1`
- Rename: `Private/Get-VhcSureBackup.ps1` → `Private/Get-VhciSureBackup.ps1`
- Rename: `Private/Get-VhcTapeInfrastructure.ps1` → `Private/Get-VhciTapeInfrastructure.ps1`
- Rename: `Private/Get-VhcCredentialsAndNotifications.ps1` → `Private/Get-VhciCredentialsAndNotifications.ps1`
- Modify: `Public/Get-VhcJob.ps1` — calls `Invoke-VhcJobSubCollectors`
- Modify: `Private/Invoke-VhciJobSubCollectors.ps1` — calls all 9 sub-collectors

**Step 1: Rename all 10 files**

```bash
cd vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig
git mv Private/Invoke-VhcJobSubCollectors.ps1          Private/Invoke-VhciJobSubCollectors.ps1
git mv Private/Get-VhcAgentJob.ps1                     Private/Get-VhciAgentJob.ps1
git mv Private/Get-VhcCatalystJob.ps1                  Private/Get-VhciCatalystJob.ps1
git mv Private/Get-VhcNasJob.ps1                       Private/Get-VhciNasJob.ps1
git mv Private/Get-VhcPluginAndCdpJob.ps1              Private/Get-VhciPluginAndCdpJob.ps1
git mv Private/Get-VhcReplication.ps1                  Private/Get-VhciReplication.ps1
git mv Private/Get-VhcCloudConnect.ps1                 Private/Get-VhciCloudConnect.ps1
git mv Private/Get-VhcSureBackup.ps1                   Private/Get-VhciSureBackup.ps1
git mv Private/Get-VhcTapeInfrastructure.ps1           Private/Get-VhciTapeInfrastructure.ps1
git mv Private/Get-VhcCredentialsAndNotifications.ps1  Private/Get-VhciCredentialsAndNotifications.ps1
```

**Step 2: Update function definitions in all 10 renamed files**

Each file contains a single `function Get-Vhc<X>` or `function Invoke-VhcJobSubCollectors` definition. Update each.

**Step 3: Update call sites in `Invoke-VhciJobSubCollectors.ps1`**

This file calls all 9 sub-collectors by name. Replace all old `Get-Vhc*` names with `Get-Vhci*` equivalents.

```bash
grep -n "Get-Vhc" Private/Invoke-VhciJobSubCollectors.ps1
```

Expected: ~9 lines, each a call to a sub-collector. Update all.

**Step 4: Update `Public/Get-VhcJob.ps1`**

```bash
grep -n "Invoke-VhcJobSubCollectors" Public/Get-VhcJob.ps1
```

Replace `Invoke-VhcJobSubCollectors` → `Invoke-VhciJobSubCollectors`.

**Step 5: Verify**

```bash
grep -rn "Invoke-VhcJobSubCollectors\b\|Get-VhcAgentJob\|Get-VhcCatalystJob\|Get-VhcNasJob\|Get-VhcPluginAndCdpJob\|Get-VhcReplication\|Get-VhcCloudConnect\|Get-VhcSureBackup\|Get-VhcTapeInfrastructure\|Get-VhcCredentialsAndNotifications" Public/ Private/
```

Expected: zero output.

**Step 6: Commit**

```bash
git add -A
git commit -m "refactor(private): rename job sub-collectors to Vhci prefix"
```

---

## Task 4: Rename Remaining Private Functions + Fix `ConvertToGB`

**Files:**
- Rename: `Private/Get-VhcComplianceResults.ps1` → `Private/Get-VhciComplianceResults.ps1`
- Rename: `Private/Get-VhcSessionLogWithTimeout.ps1` → `Private/Get-VhciSessionLogWithTimeout.ps1`
- Rename: `Private/ConvertToGB.ps1` → `Private/ConvertTo-GB.ps1`
- Modify: `Public/Get-VhcSecurityCompliance.ps1` (calls `Get-VhcComplianceResults`)
- Modify: `Public/Get-VhcSessionReport.ps1` (calls `Get-VhcSessionLogWithTimeout`)
- Modify: `Private/Get-VhciHostHardware.ps1` and `Private/Get-VhciViHvProxy.ps1` (both call `ConvertToGB`)

**Step 1: Rename the 3 files**

```bash
cd vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig
git mv Private/Get-VhcComplianceResults.ps1    Private/Get-VhciComplianceResults.ps1
git mv Private/Get-VhcSessionLogWithTimeout.ps1 Private/Get-VhciSessionLogWithTimeout.ps1
git mv Private/ConvertToGB.ps1                  Private/ConvertTo-GB.ps1
```

**Step 2: Update function definitions**

- `Get-VhciComplianceResults.ps1`: `function Get-VhcComplianceResults` → `function Get-VhciComplianceResults`
- `Get-VhciSessionLogWithTimeout.ps1`: `function Get-VhcSessionLogWithTimeout` → `function Get-VhciSessionLogWithTimeout`
- `ConvertTo-GB.ps1`: `function ConvertToGB` → `function ConvertTo-GB`

**Step 3: Update callers of `ConvertTo-GB`**

```bash
grep -rn "ConvertToGB" Private/
```

Expected: hits in `Get-VhciHostHardware.ps1` and `Get-VhciViHvProxy.ps1`. Replace `ConvertToGB` → `ConvertTo-GB` in both.

**Step 4: Update callers of `Get-VhciComplianceResults`**

```bash
grep -rn "Get-VhcComplianceResults" Public/
```

Expected: 2 hits in `Get-VhcSecurityCompliance.ps1`. Replace with `Get-VhciComplianceResults`.

**Step 5: Update callers of `Get-VhciSessionLogWithTimeout`**

```bash
grep -rn "Get-VhcSessionLogWithTimeout" Public/
```

Expected: 1 hit in `Get-VhcSessionReport.ps1`. Replace with `Get-VhciSessionLogWithTimeout`.

**Step 6: Final naming verification for the remaining Task 4 rename targets**

```bash
grep -rn "\bGet-VhcComplianceResults\b\|\bGet-VhcSessionLogWithTimeout\b\|\bConvertToGB\b" Public/ Private/
```

Expected: zero output.

> Do **not** run a repo-wide stale-name grep yet; `VhcModuleUnitTests.cs` and `README.md` are updated in Task 6.

**Step 7: Commit**

```bash
git add -A
git commit -m "refactor(private): rename remaining private functions to Vhci prefix; ConvertToGB -> ConvertTo-GB"
```

---

## Task 5: Refactor `$hostRoles` Shared Mutation (ADR 0006)

This is the structural change. Four sub-collectors currently take a `[hashtable] $HostRoles` parameter and mutate it via `Add-VhciHostRoleEntry`. After this task they return data instead, and `Get-VhcConcurrencyData` owns the merge.

**Files:**
- Modify: `Private/Get-VhciGpProxy.ps1`
- Modify: `Private/Get-VhciViHvProxy.ps1`
- Modify: `Private/Get-VhciCdpProxy.ps1`
- Modify: `Private/Get-VhciRepoGateway.ps1`
- Modify: `Public/Get-VhcConcurrencyData.ps1`

**`Add-VhciHostRoleEntry` is NOT changed** — it still works the same way, but is now called only from `Get-VhcConcurrencyData`.

### Null / failure handling contract

Each sub-collector keeps its existing `try/catch` and logs failures locally. Preserve that behavior, but make the return contract explicit: on success return an array of descriptors; on failure return `@()` after logging. `Get-VhcConcurrencyData` should still normalize each collector result with `@(...)` and filter `$null` before the merge, because `@($null)` produces a single null element and `$entry.HostName` would then throw.

### Role-entry descriptor schema

Each sub-collector will return an array of these objects (one per server role entry it would have added):

```powershell
[PSCustomObject]@{
    HostName     = [string]   # e.g. 'proxy-server-01'
    RoleName     = [string]   # e.g. 'Proxy', 'GPProxy', 'CDPProxy', 'Repository', 'Gateway'
    EntryName    = [string]   # e.g. proxy object name or repo name
    TaskCount    = [int]      # task slots contributed
    TaskCountKey = [string]   # e.g. 'TotalVpProxyTasks'
    Cores        = [int]      # physical cores (0 if unknown)
    RAM          = [int]      # physical RAM in GB (0 if unknown)
}
```

### Step 1: Refactor `Get-VhciGpProxy.ps1`

**Remove** the `[Parameter(Mandatory)] [hashtable] $HostRoles` parameter from the `param()` block.

**Replace** the `Add-VhciHostRoleEntry` call (which mutated `$HostRoles`) with building a `$roleEntry` descriptor. Collect all descriptors in a list and **return** them at the end of the function (after the existing CSV export call).

Before (current pattern inside the foreach loop):
```powershell
Add-VhciHostRoleEntry -HostRoles $HostRoles -HostName $GPProxy.Server.Name `
    -RoleName 'GPProxy' -EntryName $GPProxy.Server.Name `
    -TaskCount $NrofGPProxyTasks -TaskCountKey 'TotalGPProxyTasks' `
    -Cores $GPProxyCores -RAM $GPProxyRAM
```

After (replacement inside the foreach loop):
```powershell
$roleEntries.Add([PSCustomObject]@{
    HostName     = $GPProxy.Server.Name
    RoleName     = 'GPProxy'
    EntryName    = $GPProxy.Server.Name
    TaskCount    = $NrofGPProxyTasks
    TaskCountKey = 'TotalGPProxyTasks'
    Cores        = $GPProxyCores
    RAM          = $GPProxyRAM
})
```

Add before the `try {}` block (preferred) or at least before the first possible return:
```powershell
$roleEntries = [System.Collections.Generic.List[PSCustomObject]]::new()
```

Add after the CSV export call (at end of the `try {}` block):
```powershell
return $roleEntries.ToArray()
```

Add in the `catch {}` block after the existing logging:
```powershell
return @()
```

### Step 2: Refactor `Get-VhciViHvProxy.ps1`

Same pattern as Step 1. This function calls `Add-VhciHostRoleEntry` once per proxy (with RoleName `'Proxy'` and TaskCountKey `'TotalVpProxyTasks'`).

Remove `[hashtable] $HostRoles` from params. Replace `Add-VhciHostRoleEntry` call with building a descriptor. Return the array on success and `@()` on failure.

### Step 3: Refactor `Get-VhciCdpProxy.ps1`

Same pattern. RoleName `'CDPProxy'`, TaskCountKey `'TotalCDPProxyTasks'`, TaskCount hardcoded `1`. Return the array on success and `@()` on failure.

### Step 4: Refactor `Get-VhciRepoGateway.ps1`

This function has **two** `Add-VhciHostRoleEntry` call sites — one for gateways and one for repositories. Both become descriptors added to the same `$roleEntries` list. Return the array at the end on success and `@()` on failure.

### Step 5: Refactor `Get-VhcConcurrencyData.ps1`

**Remove** `$HostRoles` from all 4 sub-collector call sites.

**Capture** return values instead:

```powershell
# Before (current):
Get-VhciGpProxy    -GPProxies $GPProxies -VServers $VServers -HostRoles $hostRoles
Get-VhciViHvProxy  -VMwareProxies $VMwareProxies -HyperVProxies $HyperVProxies -VServers $VServers -HostRoles $hostRoles
Get-VhciCdpProxy   -CDPProxies $CDPProxies -VServers $VServers -HostRoles $hostRoles
Get-VhciRepoGateway -Repositories $VBRRepositories -VServers $VServers -HostRoles $hostRoles

# After:
$gpEntries   = @(Get-VhciGpProxy     -GPProxies $GPProxies -VServers $VServers)
$viHvEntries = @(Get-VhciViHvProxy   -VMwareProxies $VMwareProxies -HyperVProxies $HyperVProxies -VServers $VServers)
$cdpEntries  = @(Get-VhciCdpProxy    -CDPProxies $CDPProxies -VServers $VServers)
$repoEntries = @(Get-VhciRepoGateway -Repositories $VBRRepositories -VServers $VServers)
```

**Move** the `$hostRoles = @{}` initialisation to after the 4 sub-collector calls. Then add a merge loop:

```powershell
$hostRoles = @{}
$allEntries = (@($gpEntries) + @($viHvEntries) + @($cdpEntries) + @($repoEntries)) |
    Where-Object { $null -ne $_ }
foreach ($entry in $allEntries) {
    Add-VhciHostRoleEntry -HostRoles $hostRoles `
        -HostName $entry.HostName `
        -RoleName $entry.RoleName `
        -EntryName $entry.EntryName `
        -TaskCount $entry.TaskCount `
        -TaskCountKey $entry.TaskCountKey `
        -Cores $entry.Cores `
        -RAM $entry.RAM
}
```

> **Note:** The BackupServer and SQLServer role-appending logic that follows (which reads `$hostRoles` to append extra roles for already-present hosts) stays unchanged — it runs after the merge.

### Step 6: Verify descriptor schema completeness before the merge

Confirm that all four refactored files now build descriptor objects with the same property set and that `Get-VhciRepoGateway.ps1` emits both Gateway and Repository roles:

```bash
grep -n "HostName\|RoleName\|EntryName\|TaskCount\|TaskCountKey\|Cores\|RAM" \
    Private/Get-VhciGpProxy.ps1 Private/Get-VhciViHvProxy.ps1 \
    Private/Get-VhciCdpProxy.ps1 Private/Get-VhciRepoGateway.ps1
grep -n "RoleName.*Gateway\|RoleName.*Repository" Private/Get-VhciRepoGateway.ps1
```

Expected: each file shows descriptor construction, and `Get-VhciRepoGateway.ps1` shows both role names.

### Step 7: Verify no `$HostRoles` parameter remains in the sub-collectors

```bash
grep -n "\$HostRoles" Private/Get-VhciGpProxy.ps1 Private/Get-VhciViHvProxy.ps1 Private/Get-VhciCdpProxy.ps1 Private/Get-VhciRepoGateway.ps1
```

Expected: zero output.

### Step 8: Verify `Add-VhciHostRoleEntry` is now called only from `Get-VhcConcurrencyData`

```bash
grep -rn "Add-VhciHostRoleEntry" Public/ Private/ | \
    grep -v "^Public/Get-VhcConcurrencyData\.ps1:" | \
    grep -v "^Private/Add-VhciHostRoleEntry\.ps1:"
```

Expected: zero output.

### Step 9: Commit

```bash
git add -A
git commit -m "refactor(concurrency): remove shared \$hostRoles mutation; sub-collectors return role-entry data (ADR 0006)"
```

---

## Task 6: Update dependent tests and module documentation

The repository already has Windows-only C# integration/syntax tests for the PowerShell layer, and the module README documents the private helper surface. Update both so the rename/refactor leaves no stale private helper references behind.

**Files:**
- Modify: `vHC/VhcXTests/Integration/VhcModuleUnitTests.cs`
- Modify: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/README.md`

### Step 1: Update `VhcModuleUnitTests.cs`

- Change private script paths from `Export-VhcCsv.ps1` → `Export-VhciCsv.ps1`
- Change embedded PowerShell snippets from `Export-VhcCsv` → `Export-VhciCsv`
- Change `Get-VhcSessionLogWithTimeout.ps1` → `Get-VhciSessionLogWithTimeout.ps1`
- Leave public function names such as `Get-VhcSessionReport` and `Invoke-VhcCollector` unchanged

### Step 2: Update `README.md`

- Rename all private helper references in the **Private functions** and **Utility helpers** sections to their `Vhci` / `ConvertTo-GB` names
- Update the concurrency sub-collector descriptions so they say the helpers return role-entry descriptors merged by `Get-VhcConcurrencyData`, rather than mutating `$hostRoles` directly
- Leave public function names untouched

### Step 3: Verify no stale private helper names remain in tests/docs

```bash
grep -rn "Export-VhcCsv\.ps1\|Export-VhcCsv\b\|Get-VhcSessionLogWithTimeout\.ps1" \
    vHC/VhcXTests/Integration/VhcModuleUnitTests.cs \
    vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/README.md

grep -rn "\bGet-VhcViHvProxy\b\|\bGet-VhcGpProxy\b\|\bGet-VhcCdpProxy\b\|\bGet-VhcRepoGateway\b\|\bInvoke-VhcJobSubCollectors\b\|\bConvertToGB\b" \
    vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/README.md
```

Expected: zero output.

### Step 4: Run the existing PowerShell-layer tests on Windows

```bash
dotnet test vHC/VhcXTests/VhcXTests.csproj
```

If you are not on Windows, note that this suite cannot be executed locally and rely on the grep checks plus syntax validation already covered by CI / Windows execution.

### Step 5: Commit

```bash
git add -A
git commit -m "test(docs): update renamed private helper references in tests and README"
```

---

## Final Verification

Run all grep checks below from `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig` — every grep command should produce zero output:

```bash
cd vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig

# No old Vhc-prefixed private names remain anywhere
grep -rn "\bExport-VhcCsv\b\|\bAdd-VhcModuleError\b\|\bAdd-VhcHostRoleEntry\b" Public/ Private/
grep -rn "\bGet-VhcGpProxy\b\|\bGet-VhcViHvProxy\b\|\bGet-VhcCdpProxy\b\|\bGet-VhcRepoGateway\b" Public/ Private/
grep -rn "\bGet-VhcHostHardware\b\|\bGet-VhcServerOsOverhead\b\|\bInvoke-VhcJobSubCollectors\b" Public/ Private/
grep -rn "\bGet-VhcAgentJob\b\|\bGet-VhcCatalystJob\b\|\bGet-VhcNasJob\b\|\bGet-VhcSureBackup\b" Public/ Private/
grep -rn "\bGet-VhcTapeInfrastructure\b\|\bGet-VhcCloudConnect\b\|\bGet-VhcReplication\b" Public/ Private/
grep -rn "\bGet-VhcPluginAndCdpJob\b\|\bGet-VhcCredentialsAndNotifications\b" Public/ Private/
grep -rn "\bGet-VhcComplianceResults\b\|\bGet-VhcSessionLogWithTimeout\b" Public/ Private/
grep -rn "\bConvertToGB\b" .

# No $HostRoles parameter left in sub-collectors
grep -n "\$HostRoles" Private/Get-VhciGpProxy.ps1 Private/Get-VhciViHvProxy.ps1 \
    Private/Get-VhciCdpProxy.ps1 Private/Get-VhciRepoGateway.ps1

# Add-VhciHostRoleEntry called only from Get-VhcConcurrencyData (definition excluded)
grep -rn "Add-VhciHostRoleEntry" Public/ Private/ | \
    grep -v "^Public/Get-VhcConcurrencyData\.ps1:" | \
    grep -v "^Private/Add-VhciHostRoleEntry\.ps1:"
```

Confirm all 22 Private files now have `Vhci` (or non-Vhc) names:
```bash
ls Private/ | grep -E "^(Get|Add|Export|Invoke|Convert)-Vhc[^i]"
```

Expected: zero output.

Then, from the repository root on Windows, rerun the existing suite:

```bash
dotnet test vHC/VhcXTests/VhcXTests.csproj
```

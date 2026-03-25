# VBR Config DRY/SOLID Refactor Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Address the KISS, DRY, and SOLID violations identified in the post-refactor assessment of `Get-VBRConfig.ps1` and the `vHC-VbrConfig` module, while preserving observable output everywhere except for the intentional output changes listed below.

**Architecture:** Extract four new Private helper functions (`Add-VhcModuleError`, `Add-VhcHostRoleEntry`, `Get-VhcHostHardware`, `Get-VhcServerOsOverhead`) to eliminate duplicated patterns; DRY two large functions by extracting private helpers and reusing shared column definitions; split `Get-VhcJob`'s dual responsibility; write one new ADR and amend one existing ADR to document the design decisions.

**Tech Stack:** PowerShell 5.1 (PS5.1-compatible syntax only — no ternary `?:`, no `??`, no `??=`, no `&&`, no `||`), pure ASCII, one function per `.ps1` file, `#Requires -Version 5.1` header on all new files, C# xUnit for structural tests (`dotnet test`, Windows only per Validation prerequisites), and targeted behavioral checks for the three intentional output changes below.

---

## Intentional Output Changes

These are the only allowed output changes. Any other CSV diff is a regression and must be investigated before commit.

| Change | Affected output | Example before | Example after |
|--------|------------------|----------------|---------------|
| CDP totals fix | `_AllServersRequirementsComparison.csv` -> `Concurrent Tasks` | A CDP-only host exported `0` because `TotalTasks` was never incremented | The same host exports `1` when one CDP proxy is present |
| Gateway rounding | `_Gateways.csv` and `_AllServersRequirementsComparison.csv` -> `Concurrent Tasks` | 5 repository tasks across 2 gateways exported `2.5` | Both gateway rows export `3` via `[Math]::Ceiling()` |
| Gateway names | `_AllServersRequirementsComparison.csv` -> `Names` | `Server=gw01, Names=gw01/Repo-B` | `Server=gw01, Names=Repo-A/Repo-B` |

When validating refactor output, only the specific rows/columns above may change.

---

## Validation prerequisites

- **Windows required for `dotnet` validation.** `CLAUDE.md` notes that tests require Windows because the suite depends on WPF/.NET Windows.
- Before any `dotnet test --no-build` command in this plan, run:
  ```bash
  dotnet restore vHC/HC.sln
  dotnet build vHC/HC.sln --configuration Debug
  ```
- The task-local structural test commands below are smoke checks only. They do not replace the full Windows test suite or the targeted output verification for the three intentional diffs above.

---

## ADR Impact Summary

| ADR | Action | Reason |
|-----|--------|--------|
| **New ADR 0008** | Create | Documents the `Add-VhcHostRoleEntry` pattern and the canonical shape of a host-role entry. This is an architectural decision (mutable accumulator vs. return-value composition) that affects how all four proxy/repo sub-collectors interact with the shared host-role map. |
| **ADR 0007 Amendment** | Amend | ADR 0007 says "write a failure record to `$script:ModuleErrors`" but does not standardise the call. Introducing `Add-VhcModuleError` changes what "writing a failure record" looks like in code — the amendment documents this canonical form and its module-scope requirement. |

---

## Constraints (read before touching any file)

- **PS5.1 only.** No ternary `$x ? $a : $b`, no `??`, no `??=`, no `&&`, no `||`.
  Use `if (...) { ... } else { ... }` for all conditionals.
- **Pure ASCII.** No em dashes, curly quotes, or any character > U+007F.
  The C# test `AllVbrScripts_ShouldContainOnlyAsciiCharacters` will catch violations.
- **One function per file.** Each new `.ps1` file exports exactly one function.
- **Private helpers need no psd1 change.** The psm1 auto-discovers `Private/*.ps1`;
  only new *Public* functions require a psd1 `FunctionsToExport` entry.
- **Minimal behavioral change.** Exactly three intentional output changes are allowed:
  1. CDP `TotalTasks` was never incremented in the original (bug) — now fixed by the helper.
  2. Gateway task-count division is now rounded up via `[Math]::Ceiling()` rather than
     accumulated as a float — conservative fix for capacity planning.
  3. Gateway `Names` now records repository names consistently instead of the gateway
     server name on first insert.
  All other CSV fields/rows must remain unchanged.
  The existing C# test suite is a required structural gate, but it is not sufficient by
  itself for these intentional diffs — use the targeted output checks in this plan as well.

---

## Task 1: Write ADR 0008 — Host-Role Accumulation Contract

**Files:**
- Create: `docs/adr/0008-host-role-accumulation-via-helper.md`

**Step 1: Write the ADR**

Create `docs/adr/0008-host-role-accumulation-via-helper.md` with this exact content:

```markdown
# ADR 0008: Host-Role Accumulation via Add-VhcHostRoleEntry Helper

* **Status:** Accepted
* **Date:** 2026-03-05
* **Decider:** Nam
* **Consulted:** GitHub Copilot

## Context and Problem Statement

`Get-VhcConcurrencyData` orchestrates four private sub-collectors
(Get-VhcGpProxy, Get-VhcViHvProxy, Get-VhcCdpProxy, Get-VhcRepoGateway)
that all mutate a shared `$hostRoles` hashtable. Each sub-collector
contained an identical "upsert + task-count increment" block:

```
if (-not $HostRoles.ContainsKey($key)) {
    $HostRoles[$key] = [ordered]@{ Roles = @($role); Names = @($name);
                                   TotalTasks = 0; Cores = $c; RAM = $r;
                                   TotalXTasks = 0 }
} else {
    $HostRoles[$key].Roles += $role
    $HostRoles[$key].Names += $name
}
$HostRoles[$key].TotalXTasks += $n
$HostRoles[$key].TotalTasks  += $n
```

This pattern appeared verbatim four times with only the role name,
task-count key name, and hardware values varying. Any change to the
canonical entry shape required touching four files.

## Decision Drivers

- DRY: four copies of the same 10-line block.
- Correctness: inconsistent handling of multi-role servers (some branches
  did not guard the role-specific task-count key for pre-existing entries
  that gain a new role).
- Testability: a pure hashtable-mutation helper has no VBR dependencies
  and can be unit-tested without a live environment.

## Considered Options

### Option A - Return-value composition (no shared hashtable)
Each sub-collector returns a partial hashtable; the orchestrator merges
them. Eliminates mutable shared state.

**Rejected:** Would require changing the public function signature of
`Get-VhcConcurrencyData` (ADR 0006 break) and adds merge complexity that
exceeds the problem being solved.

### Option B - Single `Add-VhcHostRoleEntry` private helper (chosen)
Extract the upsert pattern into a private function with a `$TaskCountKey`
parameter. The shared hashtable remains the accumulation mechanism;
the helper encapsulates the entry-shape and increment logic.

## Decision

Option B. `Add-VhcHostRoleEntry` is the canonical way to add or update
a server entry in the `$HostRoles` map. All four sub-collectors call this
helper instead of duplicating the upsert block.

### Canonical host-role entry shape

```
[ordered]@{
    Roles       = [string[]]  # role labels appended per call
    Names       = [string[]]  # display names appended per call
    TotalTasks  = [int]       # aggregate across all roles
    Cores       = [int]       # set on first creation only
    RAM         = [int]       # set on first creation only
    <TaskCountKey> = [int]    # role-specific counter, e.g. TotalVpProxyTasks
}
```

`Cores` and `RAM` are set only when the entry is first created. If a
host has multiple roles, Cores/RAM reflect the values from whichever
role was discovered first; subsequent roles do not overwrite them because
the hardware belongs to the host, not to the role.

### Task-count key names (by role)

| Role       | TaskCountKey        |
|------------|---------------------|
| Proxy      | TotalVpProxyTasks   |
| GPProxy    | TotalGPProxyTasks   |
| CDPProxy   | TotalCDPProxyTasks  |
| Repository | TotalRepoTasks      |
| Gateway    | TotalGWTasks        |

## Consequences

* **Good:** Single definition of entry shape - changes propagate to all roles.
* **Good:** Multi-role key-guard is always applied consistently.
* **Good:** Helper is purely testable without VBR.
* **Neutral:** Mutable hashtable pattern is unchanged - ADR 0006 compliance
  is unaffected (shared state is infrastructure accumulation, not business
  data exchange between collectors).
```

**Step 2: Commit**

```bash
git add docs/adr/0008-host-role-accumulation-via-helper.md
git commit -m "docs(adr): add ADR 0008 - host-role accumulation via helper

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

## Task 2: Extract `Get-VhcHostHardware` Private Helper

Fixes the DRY violation where `GetPhysicalHost().HardwareInfo.CoresCount/PhysicalRAMTotal`
is called with inconsistent null-safety and fallback behaviour across four sub-collectors.
After this task `Get-VhcGpProxy`, `Get-VhcCdpProxy`, and `Get-VhcRepoGateway` all have
consistent null + exception fallback matching `Get-VhcViHvProxy`.

**Files:**
- Create: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Private/Get-VhcHostHardware.ps1`
- Modify: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Private/Get-VhcGpProxy.ps1`
- Modify: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Private/Get-VhcCdpProxy.ps1`
- Modify: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Private/Get-VhcRepoGateway.ps1`
- Modify: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Private/Get-VhcViHvProxy.ps1`

**Step 1: Create the helper**

`vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Private/Get-VhcHostHardware.ps1`:

```powershell
#Requires -Version 5.1

function Get-VhcHostHardware {
    <#
    .Synopsis
        Returns CPU core count and RAM in GB for a VBR server object.
        Returns @{Cores=0; RAM=0} if the server is null or if GetPhysicalHost()
        throws (e.g. offline/unreachable host). Logs a WARNING in that case.
    .Parameter Server
        A VBR server object (e.g. from Get-VBRServer). May be $null.
    .Outputs
        [hashtable] @{ Cores = [int]; RAM = [int] }
    #>
    param (
        [Parameter(Mandatory = $false)] $Server
    )

    if ($null -eq $Server) {
        Write-LogFile "Server lookup returned null - defaulting to 0 cores / 0 RAM." -LogLevel "WARNING"
        return @{ Cores = 0; RAM = 0 }
    }

    try {
        $physHost = $Server.GetPhysicalHost()
        return @{
            Cores = $physHost.HardwareInfo.CoresCount
            RAM   = ConvertToGB($physHost.HardwareInfo.PhysicalRAMTotal)
        }
    } catch {
        Write-LogFile "Hardware info unavailable for '$($Server.Name)' - defaulting to 0 cores / 0 RAM." -LogLevel "WARNING"
        return @{ Cores = 0; RAM = 0 }
    }
}
```

**Step 2: Write a test script**

The function is pure PS (no VBR dependency). Verify manually before committing:

```powershell
# Run from the module root or any PS7/5.1 session:
. ./Private/ConvertToGB.ps1
. ./Private/Get-VhcHostHardware.ps1

# Simulate Write-LogFile (not available when dot-sourced in isolation)
function Write-LogFile { param($m, $LogLevel) Write-Host "[LOG] $m" }

# Test 1: null server -> zeros
$hw = Get-VhcHostHardware -Server $null
if ($hw.Cores -ne 0 -or $hw.RAM -ne 0) { throw "FAIL: null server must return zeros" }
Write-Host "PASS: null server returns zeros"

# Test 2: server whose GetPhysicalHost() throws -> zeros + warning
$badServer = [pscustomobject]@{ Name = 'test' }
$badServer | Add-Member -MemberType ScriptMethod -Name GetPhysicalHost -Value { throw "unreachable" }
$hw = Get-VhcHostHardware -Server $badServer
if ($hw.Cores -ne 0 -or $hw.RAM -ne 0) { throw "FAIL: exception must return zeros" }
Write-Host "PASS: exception returns zeros"
```

Run: `pwsh -NoProfile -File <script>`  
Expected: both PASS lines printed, exit 0.

**Step 3: Update Get-VhcGpProxy**

Replace:
```powershell
$Serv         = $VServers | Where-Object { $_.Name -eq $GPProxy.Server.Name }
$GPProxyCores = $Serv.GetPhysicalHost().HardwareInfo.CoresCount
$GPProxyRAM   = ConvertToGB($Serv.GetPhysicalHost().HardwareInfo.PhysicalRAMTotal)
```
With:
```powershell
$hw           = Get-VhcHostHardware ($VServers | Where-Object { $_.Name -eq $GPProxy.Server.Name })
$GPProxyCores = $hw.Cores
$GPProxyRAM   = $hw.RAM
```

**Step 4: Update Get-VhcCdpProxy**

Replace:
```powershell
$CDPServer     = $VServers | Where-Object { $_.Id -eq $CDPProxy.ServerId }
$CDPProxyCores = $CDPServer.GetPhysicalHost().HardwareInfo.CoresCount
$CDPProxyRAM   = ConvertToGB($CDPServer.GetPhysicalHost().HardwareInfo.PhysicalRAMTotal)
```
With:
```powershell
$CDPServer     = $VServers | Where-Object { $_.Id -eq $CDPProxy.ServerId }
$hw            = Get-VhcHostHardware $CDPServer
$CDPProxyCores = $hw.Cores
$CDPProxyRAM   = $hw.RAM
```

**Step 5: Update Get-VhcRepoGateway — gateway branch**

Replace both inline `if ($null -ne $Server)` hardware lookups with `Get-VhcHostHardware`:

Gateway branch — replace:
```powershell
$Server  = $VServers | Where-Object { $_.Name -eq $gatewayServer.Name }
$GWCores = if ($null -ne $Server) { $Server.GetPhysicalHost().HardwareInfo.CoresCount } else { 0 }
$GWRAM   = if ($null -ne $Server) { ConvertToGB($Server.GetPhysicalHost().HardwareInfo.PhysicalRAMTotal) } else { 0 }
```
With:
```powershell
$Server  = $VServers | Where-Object { $_.Name -eq $gatewayServer.Name }
$hw      = Get-VhcHostHardware $Server
$GWCores = $hw.Cores
$GWRAM   = $hw.RAM
```

Repo branch — replace:
```powershell
$Server    = $VServers | Where-Object { $_.Name -eq $Repository.Host.Name }
$RepoCores = if ($null -ne $Server) { $Server.GetPhysicalHost().HardwareInfo.CoresCount } else { 0 }
$RepoRAM   = if ($null -ne $Server) { ConvertToGB($Server.GetPhysicalHost().HardwareInfo.PhysicalRAMTotal) } else { 0 }
```
With:
```powershell
$Server    = $VServers | Where-Object { $_.Name -eq $Repository.Host.Name }
$hw        = Get-VhcHostHardware $Server
$RepoCores = $hw.Cores
$RepoRAM   = $hw.RAM
```

**Step 6: Update Get-VhcViHvProxy — fallback VServers branch only**

The *primary* `$Proxy.GetPhysicalHost()` call is unique to this function (direct proxy lookup)
and should stay. Only the *fallback* branch needs the helper:

Replace in the `catch` block:
```powershell
$Server     = $VServers | Where-Object { $_.Name -eq $Proxy.Name }
if ($null -ne $Server) {
    $ProxyCores = $Server.GetPhysicalHost().HardwareInfo.CoresCount
    $ProxyRAM   = ConvertToGB($Server.GetPhysicalHost().HardwareInfo.PhysicalRAMTotal)
} else {
    Write-LogFile "Hardware info unavailable for proxy '$($Proxy.Name)' - defaulting to 0 cores / 0 RAM." -LogLevel "WARNING"
    $ProxyCores = 0
    $ProxyRAM   = 0
}
```
With:
```powershell
$hw         = Get-VhcHostHardware ($VServers | Where-Object { $_.Name -eq $Proxy.Name })
$ProxyCores = $hw.Cores
$ProxyRAM   = $hw.RAM
```

(The helper already logs the WARNING when null, so the explicit message in the old `else` is replaced.)

**Step 7: Run structural tests**

```bash
cd /home/localadmin/git/github/comnam90/veeam-healthcheck
dotnet test vHC/VhcXTests/VhcXTests.csproj \
  --filter "AllVbrScripts_ShouldContainOnlyAsciiCharacters|AllPs51Scripts_ShouldNotContain" \
  --no-build 2>&1 | tail -20
```
Expected: all targeted tests pass.

**Step 8: Commit**

```bash
git add vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Private/Get-VhcHostHardware.ps1 \
        vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Private/Get-VhcGpProxy.ps1 \
        vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Private/Get-VhcCdpProxy.ps1 \
        vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Private/Get-VhcRepoGateway.ps1 \
        vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Private/Get-VhcViHvProxy.ps1
git commit -m "refactor(concurrency): extract Get-VhcHostHardware for consistent hardware lookup

Eliminates repeated GetPhysicalHost() calls with inconsistent null/exception
handling across Get-VhcGpProxy, Get-VhcCdpProxy, Get-VhcRepoGateway,
Get-VhcViHvProxy. All four now use a single helper with uniform null-safety
and WARNING fallback. No behavioral change to CSV output.

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

## Task 3: Extract `Add-VhcHostRoleEntry` Private Helper

Eliminates the four-way DRY violation in the HostRoles accumulation pattern.
After this task each sub-collector replaces its 10-line upsert block with a
single `Add-VhcHostRoleEntry` call. See ADR 0008.

**Files:**
- Create: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Private/Add-VhcHostRoleEntry.ps1`
- Modify: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Private/Get-VhcGpProxy.ps1`
- Modify: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Private/Get-VhcViHvProxy.ps1`
- Modify: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Private/Get-VhcCdpProxy.ps1`
- Modify: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Private/Get-VhcRepoGateway.ps1`

**Step 1: Create the helper**

`vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Private/Add-VhcHostRoleEntry.ps1`:

```powershell
#Requires -Version 5.1

function Add-VhcHostRoleEntry {
    <#
    .Synopsis
        Upserts a server entry in the shared HostRoles hashtable and increments the
        role-specific and aggregate task counters. See ADR 0008.
    .Parameter HostRoles
        Shared hashtable keyed by server name. Modified in-place.
    .Parameter HostName
        Server hostname used as the hashtable key.
    .Parameter RoleName
        Role label (e.g. 'Proxy', 'GPProxy', 'CDPProxy', 'Repository', 'Gateway').
    .Parameter EntryName
        Display name for this specific entry (proxy name, repo name, etc.).
    .Parameter TaskCount
        Number of concurrent tasks this entry contributes.
    .Parameter TaskCountKey
        Name of the role-specific task counter key in the entry hashtable
        (e.g. 'TotalVpProxyTasks', 'TotalGPProxyTasks'). See ADR 0008 for the
        full mapping table.
    .Parameter Cores
        Physical CPU core count. Used only when creating a new entry; ignored
        for existing entries (hardware belongs to the host, not the role).
    .Parameter RAM
        Physical RAM in GB. Used only when creating a new entry.
    #>
    [CmdletBinding()]
    param (
        [Parameter(Mandatory)] [hashtable] $HostRoles,
        [Parameter(Mandatory)] [string]    $HostName,
        [Parameter(Mandatory)] [string]    $RoleName,
        [Parameter(Mandatory)] [string]    $EntryName,
        [Parameter(Mandatory)] [int]       $TaskCount,
        [Parameter(Mandatory)] [string]    $TaskCountKey,
        [Parameter(Mandatory = $false)] [int] $Cores = 0,
        [Parameter(Mandatory = $false)] [int] $RAM   = 0
    )

    if (-not $HostRoles.ContainsKey($HostName)) {
        $HostRoles[$HostName] = [ordered]@{
            Roles      = @($RoleName)
            Names      = @($EntryName)
            TotalTasks = 0
            Cores      = $Cores
            RAM        = $RAM
        }
        $HostRoles[$HostName][$TaskCountKey] = 0
    } else {
        $HostRoles[$HostName].Roles += $RoleName
        $HostRoles[$HostName].Names += $EntryName
        # Ensure the role-specific counter exists for hosts gaining a new role
        if (-not $HostRoles[$HostName].ContainsKey($TaskCountKey)) {
            $HostRoles[$HostName][$TaskCountKey] = 0
        }
    }

    $HostRoles[$HostName][$TaskCountKey] += $TaskCount
    $HostRoles[$HostName].TotalTasks     += $TaskCount
}
```

**Step 2: Write and run a test script**

```powershell
# Run from Private/ directory with pwsh
. ./Add-VhcHostRoleEntry.ps1

$h = @{}

# Test 1: new entry is created correctly
Add-VhcHostRoleEntry -HostRoles $h -HostName 'srv1' -RoleName 'Proxy' `
    -EntryName 'Proxy-1' -TaskCount 4 -TaskCountKey 'TotalVpProxyTasks' `
    -Cores 8 -RAM 32
if ($h['srv1'].TotalTasks -ne 4 -or $h['srv1'].TotalVpProxyTasks -ne 4 -or
    $h['srv1'].Cores -ne 8 -or $h['srv1'].Roles.Count -ne 1) { throw "FAIL: Test 1" }
Write-Host "PASS: new entry created"

# Test 2: second role appended; Cores/RAM unchanged
Add-VhcHostRoleEntry -HostRoles $h -HostName 'srv1' -RoleName 'Repository' `
    -EntryName 'Repo-1' -TaskCount 2 -TaskCountKey 'TotalRepoTasks' -Cores 99 -RAM 99
if ($h['srv1'].TotalTasks -ne 6 -or $h['srv1'].TotalRepoTasks -ne 2 -or
    $h['srv1'].Cores -ne 8 -or $h['srv1'].Roles.Count -ne 2) { throw "FAIL: Test 2" }
Write-Host "PASS: second role appended, Cores/RAM unchanged"

# Test 3: same role again (second proxy on same host)
Add-VhcHostRoleEntry -HostRoles $h -HostName 'srv1' -RoleName 'Proxy' `
    -EntryName 'Proxy-2' -TaskCount 4 -TaskCountKey 'TotalVpProxyTasks'
if ($h['srv1'].TotalVpProxyTasks -ne 8 -or $h['srv1'].TotalTasks -ne 10) { throw "FAIL: Test 3" }
Write-Host "PASS: same role second proxy accumulates correctly"
```

Run: `pwsh -NoProfile -File <script>`  
Expected: three PASS lines, exit 0.

**Step 3: Update Get-VhcGpProxy — replace upsert block**

Remove this entire block:
```powershell
if (-not $HostRoles.ContainsKey($GPProxy.Server.Name)) {
    $HostRoles[$GPProxy.Server.Name] = [ordered]@{
        Roles              = @('GPProxy')
        Names              = @($GPProxy.Server.Name)
        TotalTasks         = 0
        Cores              = $GPProxyCores
        RAM                = $GPProxyRAM
        Task               = $NrofGPProxyTasks
        TotalGPProxyTasks  = 0
    }
} else {
    $HostRoles[$GPProxy.Server.Name].Roles += 'GPProxy'
    $HostRoles[$GPProxy.Server.Name].Names += $GPProxy.Server.Name
}
$HostRoles[$GPProxy.Server.Name].TotalGPProxyTasks += $NrofGPProxyTasks
$HostRoles[$GPProxy.Server.Name].TotalTasks        += $NrofGPProxyTasks
```

Replace with:
```powershell
Add-VhcHostRoleEntry -HostRoles $HostRoles -HostName $GPProxy.Server.Name `
    -RoleName 'GPProxy' -EntryName $GPProxy.Server.Name `
    -TaskCount $NrofGPProxyTasks -TaskCountKey 'TotalGPProxyTasks' `
    -Cores $GPProxyCores -RAM $GPProxyRAM
```

Note: the `Task = $NrofGPProxyTasks` key in the original new-entry block is not
used by `Invoke-VhcConcurrencyAnalysis` (which reads `TotalGPProxyTasks`). It is
an unused legacy key and is dropped here intentionally.

**Step 4: Update Get-VhcViHvProxy — replace upsert block**

Remove:
```powershell
if (-not $HostRoles.ContainsKey($Proxy.Host.Name)) {
    $HostRoles[$Proxy.Host.Name] = [ordered]@{
        Roles             = @('Proxy')
        Names             = @($Proxy.Name)
        TotalTasks        = 0
        Cores             = $ProxyCores
        RAM               = $ProxyRAM
        TotalVpProxyTasks = 0
    }
} else {
    $HostRoles[$Proxy.Host.Name].Roles += 'Proxy'
    $HostRoles[$Proxy.Host.Name].Names += $Proxy.Name
}
$HostRoles[$Proxy.Host.Name].TotalVpProxyTasks += $NrofProxyTasks
$HostRoles[$Proxy.Host.Name].TotalTasks        += $NrofProxyTasks
```

Replace with:
```powershell
Add-VhcHostRoleEntry -HostRoles $HostRoles -HostName $Proxy.Host.Name `
    -RoleName 'Proxy' -EntryName $Proxy.Name `
    -TaskCount $NrofProxyTasks -TaskCountKey 'TotalVpProxyTasks' `
    -Cores $ProxyCores -RAM $ProxyRAM
```

**Step 5: Update Get-VhcCdpProxy — replace upsert block**

Remove:
```powershell
if (-not $HostRoles.ContainsKey($CDPServer.Name)) {
    $HostRoles[$CDPServer.Name] = [ordered]@{
        Roles              = @('CDPProxy')
        Names              = @($CDPProxy.Name)
        TotalTasks         = 0
        Cores              = $CDPProxyCores
        RAM                = $CDPProxyRAM
        TotalCDPProxyTasks = 0
    }
} else {
    $HostRoles[$CDPServer.Name].Roles += 'CDPProxy'
    $HostRoles[$CDPServer.Name].Names += $CDPProxy.Name
}
$HostRoles[$CDPServer.Name].TotalCDPProxyTasks += 1
```

Replace with:
```powershell
Add-VhcHostRoleEntry -HostRoles $HostRoles -HostName $CDPServer.Name `
    -RoleName 'CDPProxy' -EntryName $CDPProxy.Name `
    -TaskCount 1 -TaskCountKey 'TotalCDPProxyTasks' `
    -Cores $CDPProxyCores -RAM $CDPProxyRAM
```

Note: the original code did not update `TotalTasks` for CDP proxies (the `+= 1` only
updated `TotalCDPProxyTasks`). The helper *does* update `TotalTasks`. Verify that
`Invoke-VhcConcurrencyAnalysis` uses `server.Value.TotalTasks` for the `Concurrent Tasks`
column — if so, this is a bug-fix (CDP proxies were not contributing to the host's
`TotalTasks`). This is Intentional Output Change #1. During validation, confirm the
effect is limited to CDP-backed rows in `_AllServersRequirementsComparison.csv`
(example: a CDP-only host can move from `Concurrent Tasks = 0` to `1` after the fix).
Note this intentional diff in the commit message.

**Step 6: Update Get-VhcRepoGateway — gateway upsert block**

Remove:
```powershell
$GWDetails = [pscustomobject][ordered]@{
    'Repository Name'  = $Repository.Name
    'Gateway Server'   = $gatewayServer.Name
    'Gateway Cores'    = $GWCores
    'Gateway RAM (GB)' = $GWRAM
    'Concurrent Tasks' = $NrofRepositoryTasks / $NrofgatewayServers
}
$GWData.Add($GWDetails)

if (-not $HostRoles.ContainsKey($gatewayServer.Name)) {
    $HostRoles[$gatewayServer.Name] = [ordered]@{
        Roles         = @('Gateway')
        Names         = @($gatewayServer.Name)
        TotalTasks    = 0
        Cores         = $GWCores
        RAM           = $GWRAM
        TotalGWTasks  = 0
    }
} else {
    $HostRoles[$gatewayServer.Name].Roles += 'Gateway'
    $HostRoles[$gatewayServer.Name].Names += $Repository.Name
}
$HostRoles[$gatewayServer.Name].TotalGWTasks += $NrofRepositoryTasks / $NrofgatewayServers
$HostRoles[$gatewayServer.Name].TotalTasks   += $NrofRepositoryTasks / $NrofgatewayServers
```

Replace with:
```powershell
$gwTaskCount = [int][Math]::Ceiling($NrofRepositoryTasks / $NrofgatewayServers)
$GWDetails = [pscustomobject][ordered]@{
    'Repository Name'  = $Repository.Name
    'Gateway Server'   = $gatewayServer.Name
    'Gateway Cores'    = $GWCores
    'Gateway RAM (GB)' = $GWRAM
    'Concurrent Tasks' = $gwTaskCount
}
$GWData.Add($GWDetails)

Add-VhcHostRoleEntry -HostRoles $HostRoles -HostName $gatewayServer.Name `
    -RoleName 'Gateway' -EntryName $Repository.Name `
    -TaskCount $gwTaskCount -TaskCountKey 'TotalGWTasks' `
    -Cores $GWCores -RAM $GWRAM
```

Note on `$gwTaskCount`: the original accumulated a raw float
(`$NrofRepositoryTasks / $NrofgatewayServers`). `[Math]::Ceiling()` replaces this
with conservative integer rounding — 5 tasks across 2 gateways becomes 3, not 2.5.
This intentional fix is applied consistently to both `_Gateways.csv` (the
`'Concurrent Tasks'` column in `$GWDetails`) and the HostRoles accumulation,
so the CSV and the concurrency analysis always agree on the task count per gateway.
This is Intentional Output Change #2. During validation, confirm a concrete example:
5 repository tasks shared by 2 gateways must now produce `3` in both
`_Gateways.csv` and `_AllServersRequirementsComparison.csv`.

Note on `$EntryName`: the original `if`-branch (new entry) used `$gatewayServer.Name`
while the `else`-branch (existing entry) used `$Repository.Name`. The if-branch value
was a bug — it made the Names column in the HTML report redundantly show the gateway
server name, which is already in the Server column. Gateway repos (SMB, NFS, S3, etc.)
have no meaningful `Host.Name` of their own; their associated names are the repositories
they serve. Passing `$Repository.Name` to both branches fixes this and produces a
consistent, informative Names column ("which repos does this gateway serve?").
This is Intentional Output Change #3. During validation, confirm a gateway row changes
from something like `Server=gw01, Names=gw01/Repo-B` to
`Server=gw01, Names=Repo-A/Repo-B`, with no unrelated column changes.

**Step 7: Update Get-VhcRepoGateway — repository upsert block**

Remove:
```powershell
if (-not $HostRoles.ContainsKey($Repository.Host.Name)) {
    $HostRoles[$Repository.Host.Name] = [ordered]@{
        Roles           = @('Repository')
        Names           = @($Repository.Name)
        TotalTasks      = 0
        Cores           = $RepoCores
        RAM             = $RepoRAM
        TotalRepoTasks  = 0
    }
} else {
    $HostRoles[$Repository.Host.Name].Roles += 'Repository'
    $HostRoles[$Repository.Host.Name].Names += $Repository.Name
}
$HostRoles[$Repository.Host.Name].TotalRepoTasks += $NrofRepositoryTasks
$HostRoles[$Repository.Host.Name].TotalTasks     += $NrofRepositoryTasks
```

Replace with:
```powershell
Add-VhcHostRoleEntry -HostRoles $HostRoles -HostName $Repository.Host.Name `
    -RoleName 'Repository' -EntryName $Repository.Name `
    -TaskCount $NrofRepositoryTasks -TaskCountKey 'TotalRepoTasks' `
    -Cores $RepoCores -RAM $RepoRAM
```

**Step 8: Run structural tests**

```bash
dotnet test vHC/VhcXTests/VhcXTests.csproj \
  --filter "AllVbrScripts_ShouldContainOnlyAsciiCharacters|AllPs51Scripts_ShouldNotContain" \
  --no-build 2>&1 | tail -20
```

**Step 9: Commit**

```bash
git add vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Private/Add-VhcHostRoleEntry.ps1 \
        vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Private/Get-VhcGpProxy.ps1 \
        vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Private/Get-VhcViHvProxy.ps1 \
        vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Private/Get-VhcCdpProxy.ps1 \
        vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Private/Get-VhcRepoGateway.ps1
git commit -m "refactor(concurrency): extract Add-VhcHostRoleEntry (ADR 0008)

Eliminates 4-way DRY violation in HostRoles upsert pattern across
Get-VhcGpProxy, Get-VhcViHvProxy, Get-VhcCdpProxy, Get-VhcRepoGateway.
Also ensures consistent multi-role TaskCountKey guard on existing entries.

NOTE: CDP proxy TotalTasks was not previously incremented; now fixed.
[include 'Bug-fix: CDPProxy now contributes to TotalTasks' if that is confirmed]

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

## Task 4: Amend ADR 0007 + Extract `Add-VhcModuleError` Private Helper

There are 28 `$script:ModuleErrors.Add(...)` call sites across 16 public function files.
This task extracts them into a single private helper and amends ADR 0007 to document
the canonical error registration pattern.

**Files:**
- Modify: `docs/adr/0007-collection-manifest-for-error-surfacing.md`
- Create: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Private/Add-VhcModuleError.ps1`
- Modify: every Public .ps1 that contains `$script:ModuleErrors.Add(`:
  - `Get-VhcArchiveTier.ps1`, `Get-VhcCapacityTier.ps1`, `Get-VhcEntraId.ps1`
  - `Get-VhcJob.ps1` (11 call sites), `Get-VhcLicense.ps1`, `Get-VhcMajorVersion.ps1` (2)
  - `Get-VhcMalwareDetection.ps1`, `Get-VhcProtectedWorkloads.ps1` (3)
  - `Get-VhcRegistrySettings.ps1`, `Get-VhcRepository.ps1`, `Get-VhcSecurityCompliance.ps1`
  - `Get-VhcServer.ps1`, `Get-VhcTrafficRules.ps1`, `Get-VhcUserRoles.ps1`
  - `Get-VhcVbrInfo.ps1`, `Get-VhcWanAccelerator.ps1`

**Step 1: Amend ADR 0007**

Append this section to `docs/adr/0007-collection-manifest-for-error-surfacing.md`
before the `## Validation` heading:

```markdown
## Amendment (2026-03-05): Add-VhcModuleError canonical helper

During the DRY/SOLID refactor (docs/plans/2026-03-05-vbr-config-dry-solid-refactor.md),
the repeated `$script:ModuleErrors.Add([PSCustomObject]@{...})` call was extracted into
a private helper `Add-VhcModuleError`. The canonical registration pattern is now:

```powershell
Add-VhcModuleError -CollectorName 'MyCollector' -ErrorMessage $_.Exception.Message
```

The helper executes in module scope (dot-sourced via psm1), so `$script:ModuleErrors`
resolves to the module's registry, exactly as described in the original ADR. The
`$script:` scope behaviour is unchanged; only the call site is standardised.
```

**Step 2: Create the helper**

`vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Private/Add-VhcModuleError.ps1`:

```powershell
#Requires -Version 5.1

function Add-VhcModuleError {
    <#
    .Synopsis
        Registers a failure in the module-level error registry ($script:ModuleErrors).
        Must be called from within a catch block in a public collector function.
        Initialize-VhcModule must have been called before this function is used.
        See ADR 0007 and its 2026-03-05 amendment.
    .Parameter CollectorName
        The collector name string as used in the Invoke-VhcCollector -Name argument
        in Get-VBRConfig.ps1. Case must match exactly for manifest merging.
    .Parameter ErrorMessage
        The error message string to record (typically $_.Exception.Message).
    #>
    [CmdletBinding()]
    param (
        [Parameter(Mandatory)] [string] $CollectorName,
        [Parameter(Mandatory)] [string] $ErrorMessage
    )

    $script:ModuleErrors.Add([PSCustomObject]@{
        CollectorName = $CollectorName
        Error         = $ErrorMessage
        Timestamp     = Get-Date -Format 'yyyy-MM-ddTHH:mm:ss'
    })
}
```

**Step 3: Write and run a test script**

```powershell
# Run from Private/ directory with pwsh
. ./Add-VhcModuleError.ps1

# Stub the module-level list
$script:ModuleErrors = [System.Collections.Generic.List[PSCustomObject]]::new()

Add-VhcModuleError -CollectorName 'TestCollector' -ErrorMessage 'test error'

if ($script:ModuleErrors.Count -ne 1) { throw "FAIL: expected 1 entry" }
if ($script:ModuleErrors[0].CollectorName -ne 'TestCollector') { throw "FAIL: CollectorName" }
if ($script:ModuleErrors[0].Error -ne 'test error') { throw "FAIL: Error" }
if (-not $script:ModuleErrors[0].Timestamp) { throw "FAIL: Timestamp missing" }
Write-Host "PASS: Add-VhcModuleError records entry correctly"
```

**Step 4: Replace all 28 call sites**

For each file listed above, replace every instance of:
```powershell
$script:ModuleErrors.Add([PSCustomObject]@{
    CollectorName = 'XYZ'
    Error         = $_.Exception.Message
    Timestamp     = Get-Date -Format 'yyyy-MM-ddTHH:mm:ss'
})
```
with:
```powershell
Add-VhcModuleError -CollectorName 'XYZ' -ErrorMessage $_.Exception.Message
```

The inline single-line variants in `Get-VhcJob.ps1` (lines 70-102) become:
```powershell
Add-VhcModuleError -CollectorName 'Jobs' -ErrorMessage $_.Exception.Message
```

**Step 5: Run structural tests**

```bash
# Windows only; assumes Validation prerequisites were completed earlier in the plan
dotnet test vHC/VhcXTests/VhcXTests.csproj \
  --filter "AllVbrScripts_ShouldContainOnlyAsciiCharacters|AllPs51Scripts_ShouldNotContain" \
  --no-build 2>&1 | tail -20
```

**Step 6: Commit**

```bash
git add docs/adr/0007-collection-manifest-for-error-surfacing.md \
        vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Private/Add-VhcModuleError.ps1 \
        vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcArchiveTier.ps1 \
        vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcCapacityTier.ps1 \
        vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcEntraId.ps1 \
        vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.ps1 \
        vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcLicense.ps1 \
        vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcMajorVersion.ps1 \
        vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcMalwareDetection.ps1 \
        vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcProtectedWorkloads.ps1 \
        vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcRegistrySettings.ps1 \
        vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcRepository.ps1 \
        vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcSecurityCompliance.ps1 \
        vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcServer.ps1 \
        vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcTrafficRules.ps1 \
        vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcUserRoles.ps1 \
        vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcVbrInfo.ps1 \
        vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcWanAccelerator.ps1
git commit -m "refactor(errors): extract Add-VhcModuleError, amend ADR 0007

Replaces 28 identical \$script:ModuleErrors.Add([PSCustomObject]@{...})
call sites across 16 public functions with Add-VhcModuleError calls.
Amends ADR 0007 to document the canonical registration pattern.

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

## Task 5: DRY `Get-VhcSecurityCompliance` — Extract Version Dispatch

The `if ($VBRVersion -ge 13) { Get-VBRSecurityComplianceAnalyzerResults } else { [CDBManager]::... }`
block appears twice in this function (once in the poll loop, once in the timeout-fallback block).

**Files:**
- Create: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Private/Get-VhcComplianceResults.ps1`
- Modify: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcSecurityCompliance.ps1`

**Step 1: Create the private helper**

`Private/Get-VhcComplianceResults.ps1`:

```powershell
#Requires -Version 5.1

function Get-VhcComplianceResults {
    <#
    .Synopsis
        Retrieves raw Security & Compliance Analyzer results for the given VBR version.
        v13+: calls Get-VBRSecurityComplianceAnalyzerResults cmdlet.
        v12:  calls [Veeam.Backup.DBManager.CDBManager]::Instance.BestPractices.GetAll().
        Callers are responsible for catching exceptions from this function.
    .Parameter VBRVersion
        Major VBR version integer.
    #>
    param (
        [Parameter(Mandatory)] [int] $VBRVersion
    )

    if ($VBRVersion -ge 13) {
        return Get-VBRSecurityComplianceAnalyzerResults
    } else {
        return [Veeam.Backup.DBManager.CDBManager]::Instance.BestPractices.GetAll()
    }
}
```

**Step 2: Update Get-VhcSecurityCompliance**

Inside the poll loop, replace:
```powershell
if ($VBRVersion -ge 13) {
    $SecurityCompliances = Get-VBRSecurityComplianceAnalyzerResults
}
else {
    $SecurityCompliances = [Veeam.Backup.DBManager.CDBManager]::Instance.BestPractices.GetAll()
}
```
With:
```powershell
$SecurityCompliances = Get-VhcComplianceResults -VBRVersion $VBRVersion
```

In the final fallback block (after polling timed out), replace the identical branch:
```powershell
if ($VBRVersion -ge 13) {
    Write-LogFile "Using Get-VBRSecurityComplianceAnalyzerResults for VBR v13+"
    $SecurityCompliances = Get-VBRSecurityComplianceAnalyzerResults
}
else {
    Write-LogFile "Using database method for VBR v12"
    $SecurityCompliances = [Veeam.Backup.DBManager.CDBManager]::Instance.BestPractices.GetAll()
}
```
With:
```powershell
$SecurityCompliances = Get-VhcComplianceResults -VBRVersion $VBRVersion
```

Remove the entire `if`/`else` block at both call sites, including the version-specific log
messages ("Using Get-VBRSecurityComplianceAnalyzerResults for VBR v13+" and "Using database
method for VBR v12"). The helper is a pure dispatch function with no logging of its own --
emitting those messages inside the helper would cause them to fire on every poll attempt
(up to 15 times at 3-second intervals), whereas in the original they appeared only once in
the final fallback path. The existing "Polling timed out..." and "Final retrieval attempt..."
log lines already provide sufficient context at the fallback call site.

**Step 3: Run structural tests and commit**

```bash
dotnet test vHC/VhcXTests/VhcXTests.csproj \
  --filter "AllVbrScripts_ShouldContainOnlyAsciiCharacters" --no-build 2>&1 | tail -10

git add vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Private/Get-VhcComplianceResults.ps1 \
        vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcSecurityCompliance.ps1
git commit -m "refactor(security): extract Get-VhcComplianceResults to remove v12/v13 dispatch duplication

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

## Task 6: DRY `Get-VhcRepository` — Options Columns and Combined Detail Loop

Two fixes in one file:

1. The 11-column `Options(...)` block is duplicated between `$repoInfo` and `$AllSOBRExtentsOutput`.
2. Two sequential identical `foreach ($Repo in ...) { $RepositoryDetails.Add([pscustomobject]...) }` loops can be merged.

**Files:**
- Modify: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcRepository.ps1`

**Step 1: Extract the shared Options column array**

At the top of the `try` block (before the `foreach` loops), add:

```powershell
$repoOptionsColumns = @(
    @{name = 'Options(maxtasks)';                    expression = { $_.Options.MaxTaskCount } },
    @{name = 'Options(Unlimited Tasks)';             expression = { $_.Options.IsTaskCountUnlim } },
    @{name = 'Options(MaxArchiveTaskCount)';         expression = { $_.Options.MaxArchiveTaskCount } },
    @{name = 'Options(CombinedDataRateLimit)';       expression = { $_.Options.CombinedDataRateLimit } },
    @{name = 'Options(Uncompress)';                  expression = { $_.Options.Uncompress } },
    @{name = 'Options(OptimizeBlockAlign)';          expression = { $_.Options.OptimizeBlockAlign } },
    @{name = 'Options(RemoteAccessLimitation)';      expression = { $_.Options.RemoteAccessLimitation } },
    @{name = 'Options(EpEncryptionEnabled)';         expression = { $_.Options.EpEncryptionEnabled } },
    @{name = 'Options(OneBackupFilePerVm)';          expression = { $_.Options.OneBackupFilePerVm } },
    @{name = 'Options(IsAutoDetectAffinityProxies)'; expression = { $_.Options.IsAutoDetectAffinityProxies } },
    @{name = 'Options(NfsRepositoryEncoding)';       expression = { $_.Options.NfsRepositoryEncoding } }
)
```

**Step 2: Replace both inline Options blocks with `+ $repoOptionsColumns`**

For `$AllSOBRExtentsOutput`, replace the 11 inline `@{name='Options(...)'; ...}` entries
in the `Select-Object` call with `+ $repoOptionsColumns`. The full call becomes:

```powershell
$AllSOBRExtentsOutput = $AllSOBRExtents | Select-Object -Property (
    @(
        @{name = 'Host'; expression = { $_.host.name } },
        "Id", "Name", "HostId", "MountHostId", "Description", "CreationTime", "Path",
        "FullPath", "FriendlyPath", "ShareCredsId", "Type", "Status", "IsUnavailable",
        "Group", "UseNfsOnMountHost", "VersionOfCreation", "Tag", "IsTemporary",
        "TypeDisplay", "IsRotatedDriveRepository", "EndPointCryptoKeyId",
        "HasBackupChainLengthLimitation", "IsSanSnapshotOnly", "IsDedupStorage",
        "SplitStoragesPerVm", "IsImmutabilitySupported", "SOBR_Name"
    ) + $repoOptionsColumns + @(
        "CachedFreeSpace", "CachedTotalSpace", "gatewayHosts", "ObjectLockEnabled"
    )
)
```

For `$repoInfo`, replace its 11 inline `@{name='Options(...)'; ...}` entries similarly:

```powershell
$repoInfo = $Repositories | Select-Object -Property (
    @(
        "Id", "Name", "HostId", "Description", "CreationTime", "Path",
        "FullPath", "FriendlyPath", "ShareCredsId", "Type", "Status", "IsUnavailable",
        "Group", "UseNfsOnMountHost", "VersionOfCreation", "Tag", "IsTemporary",
        "TypeDisplay", "IsRotatedDriveRepository", "EndPointCryptoKeyId",
        "Options", "HasBackupChainLengthLimitation", "IsSanSnapshotOnly", "IsDedupStorage",
        "SplitStoragesPerVm",
        @{n = "IsImmutabilitySupported"; e = { $_.GetImmutabilitySettings().IsEnabled } }
    ) + $repoOptionsColumns + @(
        @{n = 'CachedTotalSpace'; e = { $_.GetContainer().CachedTotalSpace.InGigabytes } },
        @{n = 'CachedFreeSpace';  e = { $_.GetContainer().CachedFreeSpace.InGigabytes } },
        @{name = 'gatewayHosts';  expression = { $_.GetActualGateways().Name } }
    )
)
```

**Step 3: Merge the two RepositoryDetails loops**

Replace the two separate `foreach ($Repo in $Repositories)` and `foreach ($Repo in $SOBRs)` blocks (which add identical ID/Name objects) with:

```powershell
[System.Collections.ArrayList]$RepositoryDetails = @()
foreach ($Repo in ($Repositories + $SOBRs)) {
    $null = $RepositoryDetails.Add([pscustomobject][ordered]@{
        'ID'   = $Repo.ID
        'Name' = $Repo.Name
    })
}
```

**Step 4: Run structural tests and commit**

```bash
dotnet test vHC/VhcXTests/VhcXTests.csproj \
  --filter "AllVbrScripts_ShouldContainOnlyAsciiCharacters" --no-build 2>&1 | tail -10

git add vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcRepository.ps1
git commit -m "refactor(repository): DRY Options columns and merge RepositoryDetails loops

Extracts shared Options column array reused by both repoInfo and
AllSOBRExtentsOutput Select-Object calls. Merges two identical
foreach loops into one combined iteration over Repositories + SOBRs.

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

## Task 7: Extract `Get-VhcServerOsOverhead` + Simplify `Invoke-VhcConcurrencyAnalysis`

Collapses 8 OS-overhead variables (4 CPU + 4 RAM, computed per server) into a single
private helper call, eliminating the variable explosion in the analysis loop.

**Files:**
- Create: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Private/Get-VhcServerOsOverhead.ps1`
- Modify: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Invoke-VhcConcurrencyAnalysis.ps1`

**Step 1: Create the helper**

`Private/Get-VhcServerOsOverhead.ps1`:

```powershell
#Requires -Version 5.1

function Get-VhcServerOsOverhead {
    <#
    .Synopsis
        Computes the total OS-level CPU and RAM overhead for a server entry by summing
        the fixed per-role overhead for each role that has active tasks.
        Returns @{ CPU = [int]; RAM = [int] }.
    .Parameter Entry
        The value portion of a HostRoles hashtable entry (not the key).
    .Parameter Thresholds
        The Thresholds object from VbrConfig.json (Config.Thresholds).
    #>
    param (
        [Parameter(Mandatory)] $Entry,
        [Parameter(Mandatory)] $Thresholds
    )

    $cpu = 0
    $ram = 0

    if ((SafeValue $Entry.TotalRepoTasks) -gt 0 -or (SafeValue $Entry.TotalGWTasks) -gt 0) {
        $cpu += $Thresholds.RepoOSCPU
        $ram += $Thresholds.RepoOSRAM
    }
    if ((SafeValue $Entry.TotalVpProxyTasks) -gt 0) {
        $cpu += $Thresholds.VpProxyOSCPU
        $ram += $Thresholds.VpProxyOSRAM
    }
    if ((SafeValue $Entry.TotalGPProxyTasks) -gt 0) {
        $cpu += $Thresholds.GpProxyOSCPU
        $ram += $Thresholds.GpProxyOSRAM
    }
    if ((SafeValue $Entry.TotalCDPProxyTasks) -gt 0) {
        $cpu += $Thresholds.CdpProxyOSCPU
        $ram += $Thresholds.CdpProxyOSRAM
    }

    return @{ CPU = $cpu; RAM = $ram }
}
```

**Step 2: Write and run a test**

```powershell
# Run from Private/ directory with pwsh
. ./SafeValue.ps1
. ./Get-VhcServerOsOverhead.ps1

$t = [pscustomobject]@{
    RepoOSCPU = 2; RepoOSRAM = 4; VpProxyOSCPU = 1; VpProxyOSRAM = 2
    GpProxyOSCPU = 1; GpProxyOSRAM = 2; CdpProxyOSCPU = 0; CdpProxyOSRAM = 0
}

# Entry with Repo + VpProxy tasks
$entry = @{ TotalRepoTasks = 2; TotalVpProxyTasks = 4; TotalGWTasks = 0;
            TotalGPProxyTasks = 0; TotalCDPProxyTasks = 0 }
$oh = Get-VhcServerOsOverhead -Entry $entry -Thresholds $t
if ($oh.CPU -ne 3 -or $oh.RAM -ne 6) { throw "FAIL: expected CPU=3 RAM=6, got CPU=$($oh.CPU) RAM=$($oh.RAM)" }
Write-Host "PASS: Repo+VpProxy overhead correct"

# Entry with no tasks
$empty = @{ TotalRepoTasks = 0; TotalVpProxyTasks = 0; TotalGWTasks = 0;
            TotalGPProxyTasks = 0; TotalCDPProxyTasks = 0 }
$oh2 = Get-VhcServerOsOverhead -Entry $empty -Thresholds $t
if ($oh2.CPU -ne 0 -or $oh2.RAM -ne 0) { throw "FAIL: empty entry must return 0/0" }
Write-Host "PASS: empty entry returns zero overhead"
```

**Step 3: Update Invoke-VhcConcurrencyAnalysis**

Inside the `foreach ($server in $HostRoles.GetEnumerator())` loop, replace the entire 8-variable block (lines ~76–86) and its uses in `$RequiredCores`, `$RequiredRAM`, `$SuggestedTasksByCores`, `$SuggestedTasksByRAM`:

Remove:
```powershell
$RepoGWOSCPUOverhead   = if (...) { ... } else { 0 }
$VpProxyOSCPUOverhead  = if (...) { ... } else { 0 }
$GPProxyOSCPUOverhead  = if (...) { ... } else { 0 }
$CDPProxyOSCPUOverhead = if (...) { ... } else { 0 }
$RepoGWOSRAMOverhead   = if (...) { ... } else { 0 }
$VpProxyOSRAMOverhead  = if (...) { ... } else { 0 }
$GPProxyOSRAMOverhead  = if (...) { ... } else { 0 }
$CDPProxyOSRAMOverhead = if (...) { ... } else { 0 }
```

Add before `$RequiredCores`:
```powershell
$overhead = Get-VhcServerOsOverhead -Entry $server.Value -Thresholds $t
```

Replace `$RequiredCores` calculation — remove all four `$*OSCPUOverhead` terms and replace with `$overhead.CPU`:
```powershell
$RequiredCores = [Math]::Ceiling(
    (SafeValue $server.Value.TotalRepoTasks)     * $RepoGWCPUReq   +
    (SafeValue $server.Value.TotalGWTasks)       * $RepoGWCPUReq   +
    (SafeValue $server.Value.TotalVpProxyTasks)  * $VPProxyCPUReq  +
    (SafeValue $server.Value.TotalGPProxyTasks)  * $GPProxyCPUReq  +
    (SafeValue $server.Value.TotalCDPProxyTasks) * $CDPProxyCPUReq +
    $overhead.CPU
)
```

Replace `$RequiredRAM`:
```powershell
$RequiredRAM = [Math]::Ceiling(
    (SafeValue $server.Value.TotalRepoTasks)     * $RepoGWRAMReq   +
    (SafeValue $server.Value.TotalGWTasks)       * $RepoGWRAMReq   +
    (SafeValue $server.Value.TotalVpProxyTasks)  * $VPProxyRAMReq  +
    (SafeValue $server.Value.TotalGPProxyTasks)  * $GPProxyRAMReq  +
    (SafeValue $server.Value.TotalCDPProxyTasks) * $CDPProxyRAMReq +
    $overhead.RAM
)
```

Replace `$SuggestedTasksByCores`:
```powershell
$SuggestedTasksByCores = [Math]::Floor((SafeValue $coresAvailable) - $overhead.CPU)
```

Replace `$SuggestedTasksByRAM`:
```powershell
$SuggestedTasksByRAM = [Math]::Floor((SafeValue $ramAvailable) - $overhead.RAM)
```

**Step 4: Run structural tests and commit**

```bash
dotnet test vHC/VhcXTests/VhcXTests.csproj \
  --filter "AllVbrScripts_ShouldContainOnlyAsciiCharacters" --no-build 2>&1 | tail -10

git add vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Private/Get-VhcServerOsOverhead.ps1 \
        vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Invoke-VhcConcurrencyAnalysis.ps1
git commit -m "refactor(concurrency): extract Get-VhcServerOsOverhead to simplify analysis loop

Collapses 8 per-server OS overhead variables into a single helper call,
eliminating the variable explosion in Invoke-VhcConcurrencyAnalysis.
No behavioral change to CSV output.

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

## Task 8: Split `Get-VhcJob` — Extract `Invoke-VhcJobSubCollectors`

`Get-VhcJob` has two distinct responsibilities: (1) orchestrate and fault-isolate
9 private sub-collectors, and (2) run the main job loop with restore-point size
calculation. Task 4 (`Add-VhcModuleError`) must be complete before this task.

**Files:**
- Create: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Private/Invoke-VhcJobSubCollectors.ps1`
- Modify: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.ps1`

**Step 1: Create Invoke-VhcJobSubCollectors**

`Private/Invoke-VhcJobSubCollectors.ps1`:

```powershell
#Requires -Version 5.1

function Invoke-VhcJobSubCollectors {
    <#
    .Synopsis
        Runs the nine private job-type sub-collectors with individual fault isolation.
        A single sub-collector failure does not abort the remaining ones.
        Called exclusively by Get-VhcJob. See ADR 0007.
    .Parameter Jobs
        Array of VBR job objects (from Get-VBRJob). Passed to Get-VhcReplication.
    .Parameter ReportInterval
        Number of days for NAS session lookback. Passed to Get-VhcNasJob.
    #>
    [CmdletBinding()]
    param (
        [Parameter(Mandatory = $false)] [object[]] $Jobs          = @(),
        [Parameter(Mandatory = $false)] [int]      $ReportInterval = 14
    )

    try { Get-VhcCatalystJob } catch {
        Write-LogFile "Get-VhcCatalystJob failed: $($_.Exception.Message)" -LogLevel "ERROR"
        Add-VhcModuleError -CollectorName 'Jobs' -ErrorMessage $_.Exception.Message
    }
    try { Get-VhcAgentJob } catch {
        Write-LogFile "Get-VhcAgentJob failed: $($_.Exception.Message)" -LogLevel "ERROR"
        Add-VhcModuleError -CollectorName 'Jobs' -ErrorMessage $_.Exception.Message
    }
    try { Get-VhcSureBackup } catch {
        Write-LogFile "Get-VhcSureBackup failed: $($_.Exception.Message)" -LogLevel "ERROR"
        Add-VhcModuleError -CollectorName 'Jobs' -ErrorMessage $_.Exception.Message
    }
    try { Get-VhcTapeInfrastructure } catch {
        Write-LogFile "Get-VhcTapeInfrastructure failed: $($_.Exception.Message)" -LogLevel "ERROR"
        Add-VhcModuleError -CollectorName 'Jobs' -ErrorMessage $_.Exception.Message
    }
    try { Get-VhcNasJob -ReportInterval $ReportInterval } catch {
        Write-LogFile "Get-VhcNasJob failed: $($_.Exception.Message)" -LogLevel "ERROR"
        Add-VhcModuleError -CollectorName 'Jobs' -ErrorMessage $_.Exception.Message
    }
    try { Get-VhcPluginAndCdpJob } catch {
        Write-LogFile "Get-VhcPluginAndCdpJob failed: $($_.Exception.Message)" -LogLevel "ERROR"
        Add-VhcModuleError -CollectorName 'Jobs' -ErrorMessage $_.Exception.Message
    }
    try { Get-VhcReplication -Jobs @($Jobs) } catch {
        Write-LogFile "Get-VhcReplication failed: $($_.Exception.Message)" -LogLevel "ERROR"
        Add-VhcModuleError -CollectorName 'Jobs' -ErrorMessage $_.Exception.Message
    }
    try { Get-VhcCloudConnect } catch {
        Write-LogFile "Get-VhcCloudConnect failed: $($_.Exception.Message)" -LogLevel "ERROR"
        Add-VhcModuleError -CollectorName 'Jobs' -ErrorMessage $_.Exception.Message
    }
    try { Get-VhcCredentialsAndNotifications } catch {
        Write-LogFile "Get-VhcCredentialsAndNotifications failed: $($_.Exception.Message)" -LogLevel "ERROR"
        Add-VhcModuleError -CollectorName 'Jobs' -ErrorMessage $_.Exception.Message
    }
}
```

**Step 2: Update Get-VhcJob**

Remove the 9-sub-collector block from `Get-VhcJob.ps1` (lines 68-103) and the associated
individual error-log and ModuleErrors calls. Replace the entire block with:

```powershell
Invoke-VhcJobSubCollectors -Jobs @($Jobs) -ReportInterval $ReportInterval
```

This single call replaces:
```powershell
try { Get-VhcCatalystJob } catch { ... }
try { Get-VhcAgentJob    } catch { ... }
# ... 7 more
```

The `$Jobs` and `$configBackup` fetch at the top of `Get-VhcJob` stays. The main job
processing loop and the final `Export-VhcCsv` calls stay. Only the sub-collector
orchestration block moves.

**Step 3: Verify the log messages are still present**

After the extraction, `Get-VhcJob` should still log:
```powershell
$message = "Collecting jobs info..."
Write-LogFile $message
# ... $Jobs and $configBackup fetch ...
Invoke-VhcJobSubCollectors -Jobs @($Jobs) -ReportInterval $ReportInterval
# ... main loop ...
Write-LogFile ($message + "DONE")
```

`Invoke-VhcJobSubCollectors` logs only through `Add-VhcModuleError` → `Write-LogFile`
on failure. Add a single entry/exit log to the private function if desired:
```powershell
Write-LogFile "Running job sub-collectors..."
# ... sub-collector calls ...
Write-LogFile "Job sub-collectors complete."
```

**Step 4: Run structural tests and full test suite**

```bash
dotnet test vHC/VhcXTests/VhcXTests.csproj --no-build 2>&1 | tail -30
```

Expected: all tests pass (including `InvokeVhcCollector_ScriptblockThrows_ReturnsFailedResultWithoutThrowing`
and the Export-VhcCsv tests which test module infrastructure used by Get-VhcJob).

**Step 5: Commit**

```bash
git add vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Private/Invoke-VhcJobSubCollectors.ps1 \
        vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.ps1
git commit -m "refactor(jobs): split Get-VhcJob - extract Invoke-VhcJobSubCollectors (SRP)

Separates the two responsibilities in Get-VhcJob:
- Invoke-VhcJobSubCollectors: orchestrates + fault-isolates 9 sub-collectors
- Get-VhcJob: main job loop with restore-point size calculation

No behavioral change. Requires Add-VhcModuleError from Task 4.

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

## Final Validation

After all 8 tasks are complete:

**Step 1: Windows restore/build + full test suite**

```bash
cd /home/localadmin/git/github/comnam90/veeam-healthcheck

# Windows only
dotnet restore vHC/HC.sln
dotnet build vHC/HC.sln --configuration Debug
dotnet test vHC/VhcXTests/VhcXTests.csproj 2>&1 | tail -40
```

Expected: restore/build succeed and 0 tests fail.

**Step 2: PS5.1 compatibility sweep**

```bash
# Windows only; reuses build output from Step 1
dotnet test vHC/VhcXTests/VhcXTests.csproj \
  --filter "AllPs51Scripts_ShouldNotContain_AnyPs7OnlySyntax|AllVbrScripts_ShouldContainOnlyAsciiCharacters" \
  --no-build --logger "console;verbosity=normal" 2>&1 | tail -40
```

**Step 3: Verify intentional output changes only**

```bash
git --no-pager diff --no-index -- path/to/before/_Gateways.csv path/to/after/_Gateways.csv
git --no-pager diff --no-index -- path/to/before/_AllServersRequirementsComparison.csv path/to/after/_AllServersRequirementsComparison.csv
```

Confirm all of the following and nothing more:
- CDP totals fix: a CDP-only host can change from `Concurrent Tasks = 0` to `1`
  because `TotalTasks` is now incremented.
- Gateway rounding fix: a repository with 5 concurrent tasks and 2 gateway servers now
  reports `3` per gateway (not `2.5`) in `_Gateways.csv`, and the same integer is
  reflected in `_AllServersRequirementsComparison.csv`.
- Gateway names fix: a gateway row that previously repeated the gateway server name in
  `Names` now lists the repository names served by that gateway (for example,
  `Repo-A/Repo-B`).

If any other row/column changes, stop and investigate before committing.

**Step 4: Verify private helpers remain unexported in the manifest**

```bash
powershell -NoProfile -Command "
$manifest = Import-PowerShellDataFile 'vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/vHC-VbrConfig.psd1'
$privateHelpers = @(
  'Add-VhcHostRoleEntry',
  'Add-VhcModuleError',
  'Get-VhcHostHardware',
  'Get-VhcServerOsOverhead',
  'Get-VhcComplianceResults',
  'Invoke-VhcJobSubCollectors'
)
$bad = $privateHelpers | Where-Object { $_ -in $manifest.FunctionsToExport }
if ($bad) { throw ('Private helpers exported: ' + ($bad -join ', ')) }
if ($manifest.FunctionsToExport.Count -ne 24) {
  throw ('Unexpected FunctionsToExport count: ' + $manifest.FunctionsToExport.Count)
}
Write-Host 'PASS: manifest exports unchanged'
"
```

Expected: PASS message, no private helpers exported, 24 public functions exported.

**Step 5: Module import smoke check**

```bash
powershell -NoProfile -Command "
Import-Module './vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/vHC-VbrConfig.psd1' -Force
$exports = (Get-Command -Module 'vHC-VbrConfig').Name
$privateHelpers = @(
  'Add-VhcHostRoleEntry',
  'Add-VhcModuleError',
  'Get-VhcHostHardware',
  'Get-VhcServerOsOverhead',
  'Get-VhcComplianceResults',
  'Invoke-VhcJobSubCollectors'
)
$bad = $privateHelpers | Where-Object { $_ -in $exports }
if ($bad) { throw ('Private helpers visible after import: ' + ($bad -join ', ')) }
if ($exports.Count -ne 24) { throw ('Unexpected exported command count: ' + $exports.Count) }
Write-Host 'PASS: module import/export surface unchanged'
"
```

Expected: PASS message.

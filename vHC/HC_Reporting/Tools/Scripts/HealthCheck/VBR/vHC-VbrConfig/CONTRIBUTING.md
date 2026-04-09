# Contributing to vHC-VbrConfig

This guide explains how to add a new collector or extend an existing one.
For module architecture, prerequisites, and the full public/private function reference,
see [README.md](README.md) — this document focuses exclusively on the how-to.

---

## Contents

1. [Architecture Overview](#1-architecture-overview)
2. [Naming Conventions](#2-naming-conventions)
3. [Adding a Brand-New Collector](#3-adding-a-brand-new-collector)
4. [Extending an Existing Collector](#4-extending-an-existing-collector)
5. [Private Helpers and Sub-Collectors](#5-private-helpers-and-sub-collectors)
6. [Error Handling Rules](#6-error-handling-rules)
7. [Checklist](#7-checklist)
8. [Testing](#8-testing)

---

## 1. Architecture Overview

```
PS collector  ->  CSV file  ->  C# parser  ->  HTML table
```

**PowerShell side**

`Get-VBRConfig.ps1` is the orchestrator. It calls each public collector function
through `Invoke-VhcCollector`, which handles timing, fault isolation, and manifest
recording:

```powershell
# Fire-and-forget (CSV output only)
$collectorResults.Add((Invoke-VhcCollector -Name 'License' -Action { Get-VhcLicense }))

# Returns data for downstream use
$repoResult        = Invoke-VhcCollector -Name 'Repository' -Action { Get-VhcRepository -VBRVersion $VBRVersion }
$RepositoryDetails = $repoResult.Output   # passed explicitly to dependent collectors (ADR 0006)
```

`Invoke-VhcCollector` returns `[PSCustomObject]@{Name; Success; Duration; Error; Output}`.
The `.Output` field carries whatever the collector function returns.

**Module state**

`Initialize-VhcModule` sets `$script:ReportPath`, `$script:VBRServer`, and other
module-scoped variables before any collector runs. Never read `$script:` variables
from outside the module — use the exported accessor `Get-VhcModuleErrors` for the
error registry.

**Error registry**

`Add-VhciModuleError -CollectorName '<name>' -ErrorMessage $_.Exception.Message`
appends a record to `$script:ModuleErrors`. After all collectors complete,
`Get-VBRConfig.ps1` reads this list via `Get-VhcModuleErrors` and merges it into
`_CollectionManifest.csv`. That manifest is then parsed by C# to surface failures
in the HTML report, CLI output, and GUI status (ADR 0007).

**C# side**

1. `CCsvValidator` checks for expected CSV files at startup.
2. `CCsvParser` opens and parses each CSV — either into a typed model (using
   `[Index(n)]` attributes) or as `dynamic` records.
3. `CDataTypesParser.Init()` calls the parsers and stores the data in public fields.
4. `CHtmlBodyHelper.FormVbrFullReport()` calls `CHtmlTables` methods to render
   each section.

---

## 2. Naming Conventions

| Concept | Convention | Example |
|---|---|---|
| Public collector | `Get-Vhc<Feature>` | `Get-VhcProxmoxProxy` |
| Private helper | `Get-Vhci<Feature>` | `Get-VhciProxmoxNode` |
| Private orchestrator | `Invoke-Vhci<Feature>` | `Invoke-VhciProxmoxSubCollectors` |
| CSV suffix (PS side) | `_<FeatureName>.csv` | `_ProxmoxProxies.csv` |
| Collector name string | PascalCase, matches `-Name` in `Invoke-VhcCollector` | `'ProxmoxProxy'` |
| C# CSV model class | `C<Feature>CsvInfos` | `CProxmoxProxyCsvInfos` |
| C# parser method | `<Feature>CsvParser()` | `ProxmoxProxyCsvParser()` |
| C# data type class | `C<Feature>TypeInfo` | `CProxmoxProxyTypeInfo` |

---

## 3. Adding a Brand-New Collector

The Proxmox proxy collector is used throughout this section as a concrete example.
Adapt names and fields to your actual feature area.

### Step 1 — Create the Public Function

File: `Public/Get-VhcProxmoxProxy.ps1`

Use `Get-VhcLicense.ps1` as a template for simple collectors and
`Get-VhcRepository.ps1` as a template when the function returns data for downstream
use. Key rules:

- All files must contain **only ASCII characters** and must **not include a UTF-8 BOM**.
- Output objects must use `[pscustomobject][ordered]@{}` for deterministic CSV column order.
- Top-level try/catch only — do not nest try/catch inside the main block.
- Call `Export-VhciCsv` from the pipeline, not by assignment.

**Simple (fire-and-forget) collector:**

The canonical pattern (from `Get-VhcLicense.ps1`) keeps the VBR cmdlet call inside
`try/catch` for error isolation, then exports outside the try block with a null guard.
The `else` branch pipes `$null` through `Export-VhciCsv` as a signal that the
collector ran but found no data. With the current `Export-VhciCsv` implementation
that guard is a no-op (zero-record pipelines are silently skipped), but the pattern
is retained for clarity and consistency.

```powershell
#Requires -Version 5.1

function Get-VhcProxmoxProxy {
    <#
    .Synopsis
        Collects Proxmox Backup proxy details. Exports to _ProxmoxProxies.csv.
    .Parameter VBRVersion
        Major VBR version integer. Used to gate version-specific cmdlets/properties.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $false)]
        [int]$VBRVersion = 0
    )

    $message = "Collecting Proxmox proxy info..."
    $proxies  = $null
    Write-LogFile $message

    try {
        $proxies = Get-VBRProxmoxProxy   # replace with the real cmdlet
        Write-LogFile ($message + "DONE")
    } catch {
        Write-LogFile ($message + "FAILED!")
        Write-LogFile $_.Exception.Message -LogLevel "ERROR"
        Add-VhciModuleError -CollectorName 'ProxmoxProxy' -ErrorMessage $_.Exception.Message
    }

    if ($null -ne $proxies) {
        $proxies | ForEach-Object {
            [pscustomobject][ordered]@{
                Id          = $_.Id
                Name        = $_.Name
                Host        = $_.Host.Name
                MaxTasks    = $_.Options.MaxTaskCount
                Description = $_.Description
            }
        } | Export-VhciCsv -FileName '_ProxmoxProxies.csv'
    } else {
        $null | Export-VhciCsv -FileName '_ProxmoxProxies.csv'
    }
}
```

**Returns-data collector** (downstream collector needs the output):

Follow `Get-VhcRepository.ps1`. The key differences from the simple pattern are:
- Declare `[OutputType([System.Collections.ArrayList])]` (or the appropriate type).
- The VBR cmdlet calls, transformations, and `Export-VhciCsv` calls all go **inside**
  the `try` block — no post-try null guard needed.
- Return the data object at the end of the `try` block.
- Return `$null` explicitly from the `catch` block.

```powershell
    try {
        # ... collect and export CSV ...
        return $someData
    } catch {
        Write-LogFile ($message + "FAILED!")
        Write-LogFile $_.Exception.Message -LogLevel "ERROR"
        Add-VhciModuleError -CollectorName 'ProxmoxProxy' -ErrorMessage $_.Exception.Message
        return $null
    }
```

### Step 2 — Register in the Module Manifest

File: `vHC-VbrConfig.psd1`

Add the function name to `FunctionsToExport` in alphabetical order:

```powershell
FunctionsToExport = @(
    ...
    'Get-VhcProxmoxProxy',
    ...
)
```

The `.psm1` dot-sources all files in `Public\` automatically; the manifest entry is
what makes the function visible outside the module.

### Step 3 — Wire into the Orchestrator

File: `Get-VBRConfig.ps1`

Choose a placement that makes sense (e.g., after existing proxy collectors).

**Fire-and-forget:**

```powershell
$collectorResults.Add((Invoke-VhcCollector -Name 'ProxmoxProxy' -Action {
    Get-VhcProxmoxProxy -VBRVersion $VBRVersion
}))
```

**Returns data for a downstream collector (ADR 0006 — explicit data passing):**

```powershell
$proxmoxResult  = Invoke-VhcCollector -Name 'ProxmoxProxy' -Action {
    Get-VhcProxmoxProxy -VBRVersion $VBRVersion
}
$collectorResults.Add($proxmoxResult)
$ProxmoxProxies = $proxmoxResult.Output   # pass explicitly; never read $script: vars

$collectorResults.Add((Invoke-VhcCollector -Name 'ProxmoxBackup' -Action {
    Get-VhcProxmoxBackup -ProxmoxProxies $ProxmoxProxies
}))
```

The `-Name` string **must exactly match** the `CollectorName` passed to
`Add-VhciModuleError` in the public function. The manifest merge depends on this.

### Step 4 — C#: Add to CSV Validator

File: `Functions/Collection/CCsvValidator.cs`

Add an entry to the `ExpectedVbrFiles` dictionary. Choose the severity that best
describes the impact of a missing file:

```csharp
// Warning files - important but report can work without them
{ "ProxmoxProx", CsvValidationSeverity.Warning },
```

`ValidateSingleFile` passes this key to `Directory.GetFiles("*ProxmoxProx*.csv")`,
so it is a **substring** wildcard match — keep it unique enough to avoid false
matches against other CSV files in the directory.

> **Note:** This substring lookup in `CCsvValidator` is separate from the exact-stem
> lookup used by `CCsvParser.VbrFileReader` (Step 5b). They use different matching
> rules; see Step 5b for details.

### Step 5 — C#: Add CSV Parser

**5a. Create the typed model**

File: `Functions/Reporting/CsvHandlers/CProxmoxProxyCsvInfos.cs`

Map each CSV column to a property using `[Index(n)]` in the order your PowerShell
`[pscustomobject][ordered]@{}` object defines them (zero-based):

```csharp
using CsvHelper.Configuration.Attributes;

namespace VeeamHealthCheck.Functions.Reporting.CsvHandlers
{
    public class CProxmoxProxyCsvInfos
    {
        [Index(0)] public string Id          { get; set; }
        [Index(1)] public string Name        { get; set; }
        [Index(2)] public string Host        { get; set; }
        [Index(3)] public string MaxTasks    { get; set; }
        [Index(4)] public string Description { get; set; }
    }
}
```

**5b. Add the parser method**

File: `Functions/Reporting/CsvHandlers/CCsvParser.cs`

Declare the filename constant with the other report name fields:

```csharp
public readonly string proxmoxProxies = "ProxmoxProxies";
```

Add a parser method in the `#region oldCsvParsers` block, following the pattern of
`ProxyCsvParser()`:

```csharp
public IEnumerable<CProxmoxProxyCsvInfos> ProxmoxProxyCsvParser()
{
    var res = this.VbrFileReader(this.proxmoxProxies);
    if (res != null)
        return res.GetRecords<CProxmoxProxyCsvInfos>();
    return null;
}
```

`VbrFileReader` calls `CCsvReader.FileFinder`, which enumerates all `*.csv` files
in the CSV directory and matches using `EndsWith("_ProxmoxProxies.csv")`.
The token must be the **exact filename stem** — `"ProxmoxProxies"` matches
`myserver_ProxmoxProxies.csv` but will not match a truncated or partial name.
The search is case-insensitive.

### Step 6 — C#: Surface in HTML

**6a. Data type class**

Create `Functions/Reporting/DataTypes/CProxmoxProxyTypeInfo.cs` if you need a
presentation model distinct from the CSV model. For simple cases the CSV model is
often used directly.

**6b. `CDataTypesParser.Init()`**

File: `Functions/Reporting/DataTypes/CDataTypesParser.cs`

Follow the existing field-declaration style used throughout the class (public fields,
not auto-properties). Declare the field alongside the others at the top of the class,
then assign it in `Init()`:

```csharp
// Field declaration (top of class, with the others):
public List<CProxmoxProxyCsvInfos> ProxmoxProxies = new();

// Inside Init():
this.ProxmoxProxies = this.csvParser.ProxmoxProxyCsvParser()?.ToList() ?? new();
```

**6c. HTML table method**

File: `Functions/Reporting/Html/VBR/VbrTables/CHtmlTables.cs`

Add a method that renders the data as an HTML table. For fields that contain server
names or IPs, apply the scrubber:

```csharp
public string ProxmoxProxyTable(IEnumerable<CProxmoxProxyCsvInfos> proxies)
{
    var sb = new StringBuilder();
    sb.Append("<table><thead><tr>");
    sb.Append("<th>Name</th><th>Host</th><th>Max Tasks</th><th>Description</th>");
    sb.Append("</tr></thead><tbody>");

    foreach (var p in proxies)
    {
        sb.Append("<tr>");
        sb.Append($"<td>{CScrubHandler.ScrubString(p.Name)}</td>");
        sb.Append($"<td>{CScrubHandler.ScrubString(p.Host)}</td>");
        sb.Append($"<td>{p.MaxTasks}</td>");
        sb.Append($"<td>{p.Description}</td>");
        sb.Append("</tr>");
    }

    sb.Append("</tbody></table>");
    return sb.ToString();
}
```

**6d. Register the section in the report**

File: `Functions/Reporting/Html/VBR/CHtmlBodyHelper.cs`

Call your new table method from `FormVbrFullReport()` at the appropriate position:

```csharp
body.Append(_tables.ProxmoxProxyTable(_data.ProxmoxProxies));
```

---

## 4. Extending an Existing Collector

Adding a column to an existing CSV is simpler but touches both sides of the
pipeline. The Repository collector is used as an example.

### 4a. PowerShell side

File: `Public/Get-VhcRepository.ps1`

Add the new property to the `Select-Object` expression that produces `$repoInfo`.
Use `[pscustomobject][ordered]@{}` if you are building objects manually, or
add a calculated property to the existing pipeline:

```powershell
@{n = 'IsImmutabilityEnabled'; e = { $_.GetImmutabilitySettings().IsEnabled }}
```

The column position matters for the C# typed model — always append new columns at
the end to avoid shifting existing `[Index(n)]` values.

### 4b. C# CSV model

File: `Functions/Reporting/CsvHandlers/CRepoCsvInfos.cs` (or the relevant model)

Append a new property using the next available index:

```csharp
[Index(42)] public string IsImmutabilityEnabled { get; set; }
```

Never insert in the middle — that shifts all subsequent `[Index(n)]` values and
will cause every following column to be mis-read.

### 4c. HTML table

File: `Functions/Reporting/Html/VBR/VbrTables/CHtmlTables.cs`

Add a `<th>` in the header row and a `<td>` in the data row of the relevant table
method.

### Gotcha: header normalisation

`CCsvReader` normalises CSV headers to lowercase and strips the following characters
before matching column names to model properties: space, `.`, `?`, `-`, `(`, `)`,
`/`, `#`. Keep CSV column names simple (ASCII letters, digits, and underscores only)
to avoid unexpected header-match failures. If you encounter a mismatch, inspect
`CCsvReader.GetCsvConfig()` for the full stripping logic.

---

## 5. Private Helpers and Sub-Collectors

Split logic into private functions when it keeps the public function readable or
when the same logic is reused by multiple callers.

**`Get-Vhci*`** — a function called by exactly one public function. Place in
`Private/`. No try/catch required; exceptions propagate up to the public caller's
catch block (this is intentional — see README error handling conventions).

**`Invoke-Vhci*`** — an orchestrator that fans out to multiple private helpers
with per-call fault isolation. Pattern from `Invoke-VhciJobSubCollectors`:

```powershell
function Invoke-VhciProxmoxSubCollectors {
    param($VBRVersion)

    try { Get-VhciProxmoxNode -VBRVersion $VBRVersion }
    catch { Write-LogFile "ProxmoxNode sub-collector failed: $($_.Exception.Message)" -LogLevel "ERROR" }

    try { Get-VhciProxmoxCredential }
    catch { Write-LogFile "ProxmoxCredential sub-collector failed: $($_.Exception.Message)" -LogLevel "ERROR" }
}
```

**Per-item loop failures** (e.g., one proxy out of many fails): catch locally and
`Write-LogFile` the error, but do **not** call `Add-VhciModuleError`. That function
is reserved for top-level collector failures — calling it inside a loop would
falsely mark the entire collector as failed in the manifest (ADR 0007).

---

## 6. Error Handling Rules

| Situation | Action |
|---|---|
| Public function catches an exception | `Write-LogFile` the message at `ERROR` level, then call `Add-VhciModuleError` |
| Private function encounters an error | Let the exception propagate (no catch); the public caller handles it |
| Per-item failure inside a loop | `catch` locally and `Write-LogFile`; do NOT call `Add-VhciModuleError` |
| Collector produces no data | Export `$null` via `Export-VhciCsv` or skip — do NOT call `Add-VhciModuleError` for empty results |

The catch block pattern for a public function:

```powershell
} catch {
    Write-LogFile ($message + "FAILED!")
    Write-LogFile $_.Exception.Message -LogLevel "ERROR"
    Add-VhciModuleError -CollectorName 'ProxmoxProxy' -ErrorMessage $_.Exception.Message
    return $null   # only if the function has a return contract
}
```

The `CollectorName` string must **exactly match** (including case) the `-Name`
argument used in `Invoke-VhcCollector` in `Get-VBRConfig.ps1`. The manifest merge
in `CCsvValidator.LoadManifest()` depends on this.

---

## 7. Checklist

Use this when reviewing a new collector PR.

### New collector

| # | Touch-point | Done? |
|---|---|---|
| 1 | `Public/Get-Vhc<Feature>.ps1` created | |
| 2 | Top-level try/catch with `Write-LogFile` + `Add-VhciModuleError` | |
| 3 | `CollectorName` in `Add-VhciModuleError` matches `-Name` in orchestrator | |
| 4 | `[pscustomobject][ordered]@{}` used for output objects | |
| 5 | ASCII-only, no UTF-8 BOM | |
| 6 | Function name added to `FunctionsToExport` in `vHC-VbrConfig.psd1` | |
| 7 | `Invoke-VhcCollector` call added to `Get-VBRConfig.ps1` | |
| 8 | CSV key added to `ExpectedVbrFiles` in `CCsvValidator.cs` | |
| 9 | Typed model `C<Feature>CsvInfos.cs` created with `[Index(n)]` properties | |
| 10 | Parser method added to `CCsvParser.cs` | |
| 11 | Property + parser call added to `CDataTypesParser.Init()` | |
| 12 | Table method added to `CHtmlTables.cs` | |
| 13 | Table method called from `CHtmlBodyHelper.FormVbrFullReport()` | |

### Extending an existing collector

| # | Touch-point | Done? |
|---|---|---|
| 1 | New property appended at end of `Select-Object` / `[ordered]@{}` | |
| 2 | New `[Index(n)]` property appended at end of typed model | |
| 3 | `<th>`/`<td>` pair added to HTML table method | |
| 4 | Column name uses only ASCII letters, digits, or underscores (no spaces, dots, hyphens, parens, `/`, `#`, `?`) | |

---

## 8. Testing

**PowerShell**

1. Connect to a test VBR server and run `Get-VBRConfig.ps1`.
2. Inspect the CSV output directory (`C:\temp\vHC\Original\VBR\<server>\<timestamp>\`).
3. Verify the new `_<FeatureName>.csv` is present and contains the expected columns and rows.
   Note: if the collector legitimately collects zero records, `Export-VhciCsv` skips
   writing the file entirely. Absence of the CSV in that case is expected, not an error.
4. Open `_CollectionManifest.csv` and confirm the new collector row shows `Success=True`
   and a sensible `DurationSeconds` value.
5. If `Success=False`, check the `Error` column and the log file for the root cause.

**C#**

1. Run `dotnet build vHC/HC.sln` and fix any compilation errors.
2. Run `dotnet test vHC/VhcXTests/VhcXTests.csproj` (Windows only) and confirm the
   test suite is green.
3. Point the tool at the CSV directory from step 1 above and generate a report.
4. Open the HTML output and verify the new section renders correctly, including that
   server names and IPs are scrubbed when running in scrub mode.

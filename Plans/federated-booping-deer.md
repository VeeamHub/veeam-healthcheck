# Plan: Fix VB365 Report Generation Failure

## Context

Running `VeeamHealthCheck.exe /run` on a VB365-only machine fails during report generation with:

```
[VB365][HTML] Error: The type initializer for 'VeeamHealthCheck.Resources.Localization.VB365.Vb365ResourceHandler' threw an exception.
```

The VBR CSV validation errors in the log are **red herrings** — they appear because the validator always checks for VBR files even on VB365-only machines, but they don't block the report. The VB365 collection itself succeeds (CSVs land at `C:\temp\vHC\Original\VB365`).

## Root Cause

`Vb365ResourceHandler.cs` initializes a `ResourceManager` at class-load time:

```csharp
// Resources/Localization/VB365/Vb365ResourceHandler.cs (line 6)
private static readonly ResourceManager vb365res = new(
    "VeeamHealthCheck.Resources.Localization.VB365.vb365_vhcres",
    typeof(Vb365ResourceHandler).Assembly);
```

The ResourceManager looks for an embedded stream named `VeeamHealthCheck.Resources.Localization.VB365.vb365_vhcres.resources` in the assembly. That stream **does not exist** in the build output because `vb365_vhcres.resources` is never embedded.

**Why VBR works:** `vhcres.resx` exists at `Resources/Localization/` and SDK-style `.csproj` auto-processes `.resx` files as `EmbeddedResource`. VB365 has no `.resx` — only a pre-compiled `vb365_vhcres.resources` binary and a source `vb365_vhcres.txt` — so nothing gets auto-embedded.

## Fix

### Step 1 — Embed the VB365 resource file

**File:** `vHC/HC_Reporting/VeeamHealthCheck.csproj`

Add one `<EmbeddedResource>` entry to the existing group at line 148–154:

```xml
<EmbeddedResource Include="Resources\Localization\VB365\vb365_vhcres.resources" />
```

The default logical name MSBuild assigns (`VeeamHealthCheck.Resources.Localization.VB365.vb365_vhcres.resources`) matches exactly what `ResourceManager` looks for.

### Step 2 — Verify the `.resources` binary is current

The binary at `Resources/Localization/VB365/vb365_vhcres.resources` was produced by `Vb365ResFileBuilder.ps1` from `vb365_vhcres.txt`. Before building, confirm the keys in the binary match the ~170+ fields declared in `Vb365ResourceHandler.cs`. If stale, re-run the builder script on Windows first.

### Step 3 (optional, low priority) — Suppress noisy VBR validation on VB365-only runs

**File:** `vHC/HC_Reporting/Startup/CClientFunctions.cs` (line ~340, `StartAnalysis()`)

Wrap the VBR CSV validator call in a `CGlobals.IsVbr` guard so it only runs when VBR is actually present:

```csharp
if (CGlobals.IsVbr)
{
    var validator = new CCsvValidator(CVariables.vbrDir);
    CGlobals.CsvValidationResults = validator.ValidateVbrCsvFiles();
}
```

This is cosmetic (eliminates 35 false-alarm log errors on VB365-only machines) and does not affect report output.

## Critical Files

| File | Change |
|------|--------|
| `vHC/HC_Reporting/VeeamHealthCheck.csproj` (line 148–154) | Add `<EmbeddedResource>` for VB365 resources — **primary fix** |
| `vHC/HC_Reporting/Startup/CClientFunctions.cs` (~line 340) | Guard VBR CSV validator with `IsVbr` check — optional cleanup |
| `vHC/HC_Reporting/Resources/Localization/VB365/vb365_vhcres.resources` | Source binary to embed — must be current |

## Verification

1. Build the project on Windows: `dotnet build vHC/HC.sln --configuration Debug`
2. Run: `.\VeeamHealthCheck.exe /run`
3. Confirm log shows NO `Vb365ResourceHandler` type initializer error
4. Confirm VB365 report HTML is generated in `C:\temp\vHC\`
5. Open the HTML report and verify navigation renders correctly (nav tables were the trigger point for the crash)

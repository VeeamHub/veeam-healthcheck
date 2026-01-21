# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build Commands

```bash
# Build the solution (Windows required for full build)
dotnet build vHC/HC.sln --configuration Debug

# Build release version
dotnet build vHC/HC.sln --configuration Release

# Restore dependencies
dotnet restore vHC/HC.sln
```

## Test Commands

```bash
# Run all tests (Windows only - tests require WPF/.NET Windows)
dotnet test vHC/VhcXTests/VhcXTests.csproj

# Run specific test class
dotnet test vHC/VhcXTests/VhcXTests.csproj --filter "FullyQualifiedName~CredentialHelperTests"

# Run tests by category
dotnet test vHC/VhcXTests/VhcXTests.csproj --filter "Category=Integration"

# Run with code coverage
dotnet test vHC/VhcXTests/VhcXTests.csproj --collect:"XPlat Code Coverage"
```

## Architecture Overview

Veeam Health Check is a Windows utility that generates configuration reports for Veeam Backup & Replication (VBR) and Veeam Backup for Microsoft 365 (VB365) installations.

### Three-Phase Pipeline

```
Collection → Processing/Analysis → Report Generation
```

### Key Components

**Entry Point & Flow**
- `vHC/HC_Reporting/Startup/EntryPoint.cs` - Main entry, handles single-file deployment
- `vHC/HC_Reporting/Startup/CArgsParser.cs` - CLI argument parsing, routes to GUI or CLI mode
- `vHC/HC_Reporting/Startup/VhcGui.xaml.cs` - WPF GUI for interactive use

**Global State**
- `vHC/HC_Reporting/Common/CGlobals.cs` - Central static configuration class holding all execution flags, paths, and shared data

**Data Collection** (`Functions/Collection/`)
- Multi-source collection: PowerShell scripts, SQL queries, registry reads, log parsing, WMI
- PowerShell scripts in `Tools/Scripts/HealthCheck/VBR/` and `Tools/Scripts/HealthCheck/VB365/`
- Outputs CSV files to `C:\temp\vHC\Original\{VBR|VB365}\{servername}\{timestamp}\`

**Report Generation** (`Functions/Reporting/`)
- `Functions/Reporting/Html/VBR/CHtmlCompiler.cs` - VBR report compiler
- `Functions/Reporting/Html/VB365/CVb365HtmlCompiler.cs` - VB365 report compiler
- `Functions/Reporting/CReportModeSelector.cs` - Routes to correct compiler based on detected product

**Data Processing**
- `Functions/Reporting/CsvHandlers/CCsvReader.cs` - CSV reading with CsvHelper
- `Functions/Reporting/CsvHandlers/CCsvParser.cs` - Static methods returning dynamic objects for flexible CSV parsing
- `Functions/Reporting/DataFormers/CDataFormer.cs` - Transforms raw data into typed report objects

### VBR vs VB365 Separation

Product detection happens in `CClientFunctions.ModeCheck()` by scanning running processes:
- `Veeam.Backup.Service` → VBR mode
- `Veeam.Archiver.Service` → VB365 mode

Each product has separate:
- Collection scripts in `Tools/Scripts/HealthCheck/`
- HTML compilers in `Functions/Reporting/Html/`
- Table renderers in `Functions/Reporting/Html/VBR/VbrTables/` and `Functions/Reporting/Html/VB365/`

### Export Formats

- HTML (primary) - with embedded CSS/JavaScript
- PDF - via DinkToPdf
- PowerPoint - via HtmlToOpenXml
- Scrubbed mode - anonymizes IPs, server names, credentials via `CScrubHandler`

## Tech Stack

- **.NET 8.0** targeting Windows 7.0+ (`net8.0-windows7.0`)
- **WPF** for GUI
- **PowerShell 7 SDK** for embedded script execution
- **CsvHelper** for CSV processing
- **xUnit + Moq** for testing
- **DocumentFormat.OpenXml + HtmlToOpenXml** for Office exports

## Test Naming Convention

`[MethodUnderTest]_[Scenario]_[ExpectedBehavior]`

Example: `EscapePasswordForPowerShell_SpecialCharacters_ProperlyEscapes`

## Important Notes

- Tests require Windows (WPF dependency) - non-Windows builds skip test compilation
- Internal types exposed to `VhcXTests` via `InternalsVisibleTo` in csproj
- Version auto-increments on build via `increment_version.ps1`
- Suppressed code analysis warnings: CA1305, CA1307, CA1820, CA2242, CA1031, CA1806, CA1822

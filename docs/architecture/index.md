# Architecture

## Three-Phase Pipeline

```
Collection → Processing/Analysis → Report Generation
```

| Phase | What Happens |
|---|---|
| **Collection** | PowerShell scripts, SQL queries, registry reads, log parsing, WMI — outputs CSV files |
| **Processing** | CSV parsing, data transformation, typing via `CDataFormer` |
| **Report Generation** | HTML compiler renders typed objects into the single-page report |

## Key Components

### Entry Point & Flow

| Component | File | Role |
|---|---|---|
| `EntryPoint` | `Startup/EntryPoint.cs` | Main entry, handles single-file deployment |
| `CArgsParser` | `Startup/CArgsParser.cs` | CLI argument parsing, routes to GUI or CLI mode |
| `VhcGui` | `VhcGui.axaml.cs` | Avalonia GUI for interactive use |
| `CGlobals` | `Common/CGlobals.cs` | Central static configuration — all execution flags and shared state |

### Data Collection

- Multi-source: PowerShell scripts, SQL, registry, log parsing, WMI
- PowerShell scripts live in `Tools/Scripts/HealthCheck/VBR/` and `Tools/Scripts/HealthCheck/VB365/`
- Output: CSV files to `C:\temp\vHC\Original\{VBR|VB365}\{servername}\{timestamp}\`

### Report Generation

| Component | File | Role |
|---|---|---|
| `CHtmlCompiler` | `Reporting/Html/VBR/CHtmlCompiler.cs` | VBR report compiler |
| `CVb365HtmlCompiler` | `Reporting/Html/VB365/CVb365HtmlCompiler.cs` | VB365 report compiler |
| `CReportModeSelector` | `Reporting/CReportModeSelector.cs` | Routes to correct compiler based on detected product |

### Data Processing

| Component | Role |
|---|---|
| `CCsvReader` | CSV reading via CsvHelper |
| `CCsvParser` | Static methods returning dynamic objects for flexible CSV parsing |
| `CDataFormer` | Transforms raw data into typed report objects |

## VBR vs VB365 Detection

Product detection in `CClientFunctions.ModeCheck()` by scanning running processes:

- `Veeam.Backup.Service` → VBR mode
- `Veeam.Archiver.Service` → VB365 mode

Each product has separate collection scripts, HTML compilers, and table renderers.

## Tech Stack

| Technology | Usage |
|---|---|
| **.NET 10.0** | Windows 7.0+ target |
| **Avalonia** | GUI |
| **PowerShell 7 SDK** | Embedded script execution |
| **CsvHelper** | CSV processing |
| **DinkToPdf** | PDF export |
| **DocumentFormat.OpenXml + HtmlToOpenXml** | PowerPoint export |
| **xUnit + Moq** | Testing |

## Architecture Decision Records

ADRs track non-obvious design decisions. See the [`docs/adr/`](../adr/) folder for the full record.

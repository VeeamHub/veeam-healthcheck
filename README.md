# Veeam Health Check

**[Download the latest release on the Releases page](https://github.com/VeeamHub/veeam-healthcheck/releases/) and select the VeeamHealthCheck zip file.**

This Windows utility is a lightweight executable that will generate an advanced configuration report covering details about the current Veeam Backup & Replication  or Veeam Backup for Microsoft 365 installation. Simply download, extract, and execute to generate the report. 

1. This tool is community supported and not an officially supported Veeam product.
2. The tool does not automatically phone home, or reach out to any network infrastructure beyond the Veeam Backup and Replication components or the Veeam Backup for 365 components if appropriate.

**[Sample Report](https://htmlpreview.github.io/?https://github.com/VeeamHub/veeam-healthcheck/blob/master/SAMPLE/Veeam%20Health%20Check%20Report_VBR_anon_2024.11.01.101304.html)**

## 📗 Documentation

**Author:** Adam Congdon (adam.congdon@veeam.com)

**System Requirements:**
- Must be run as elevated user
	- User must have Backup Administrator role in B&R
- Supported Platforms:
    - **Veeam Backup & Replication (vHC Version 3.x - Current):**
      - v12.3
      - v13 (Windows & Linux)
      - **Note:** For VBR v11 or v12 (earlier than 12.3), please use [Veeam Health Check v2](https://github.com/VeeamHub/veeam-healthcheck/releases/tag/v2.0.0.681)
    - **Veeam Backup for Microsoft 365:**
      - v6
      - v7
      - v8
- Must be executed on system where Veeam Backup & Replication Console or Veeam Backup for Microsoft 365 is installed
- C:\ must have at least 500MB free space: Output is sent to C:\temp\vHC by default.
- Veeam Cloud Service Provider Servers are not supported.

**Architecture:** Three-phase pipeline (Collection → Processing → Report Generation)
- Collects data via PowerShell, SQL, Registry, WMI
- Processes with custom analytics and calculations
- Generates comprehensive HTML report with embedded visualizations

**Operation:**
1. [Download the tool.](https://github.com/VeeamHub/veeam-healthcheck/releases/)
2. Extract the archive on the Backup & Replication Server
3. Run VeeamHealthCheck.exe from an elevated CMD/PS prompt or right-click 'Run as Administrator'
4. Configure desired options on the single-page GUI (or use CLI options)
5. Accept Terms
6. RUN
7. Review the report

**CLI Options:**
```
VeeamHealthCheck.exe [options]
```

| Option | Description |
|---|---|
| `/run` | Execute health check via CLI |
| `/gui` | Launch graphical user interface |
| `/help` | Show full help menu |
| `/days:<N>` | Reporting interval: 7, 12, 30, or 90 days (default: 7) |
| `/outdir=<path>` | Output directory (default: `C:\temp\vHC`) |
| `/pdf` | Also export report as PDF |
| `/pptx` | Also export report as PowerPoint |
| `/scrub:true` | Anonymize sensitive data in output |
| `/lite` | Skip per-job HTML exports (faster) |
| `/show:report` | Open report in browser after generation |
| `/show:files` | Open output folder in Explorer |
| `/remote` | Enable remote execution |
| `/host=<hostname>` | Target remote Veeam server |
| `/security` | Run security-focused assessment only |
| `/import[:<path>]` | Generate report from existing CSV data |
| `/clearcreds` | Clear stored credentials |
| `/debug` | Enable debug logging |

**Examples:**
```powershell
# Run a standard health check
VeeamHealthCheck.exe /run

# Run with 30-day reporting window and export as PDF
VeeamHealthCheck.exe /run /days:30 /pdf

# Run and write output to a custom directory, opening the report when done
VeeamHealthCheck.exe /run /outdir=D:\Reports /show:report

# Run against a remote VBR server
VeeamHealthCheck.exe /run /host=vbrserver.veeam.local

# Run a security-focused assessment against a remote VBR server
VeeamHealthCheck.exe /security /host=vbrserver.veeam.local

# Generate a report from previously collected CSV data
VeeamHealthCheck.exe /import:D:\Exports\VBR-data
```

**Features**
- Single-page report with B&R/VB365 Configuration information
- Custom calculations and tables:
	- Highlighting areas of potential improvement
	- Job sessions analysis:
		- Min/max/average calculations for: job duration, backup & data size, waiting for resources
		- success rate & change rate
		- Job & Task concurrency heat map
- Multiple export formats:
	- HTML (primary format with embedded CSS/JavaScript)
	- PDF export via DinkToPdf
	- PowerPoint export via HtmlToOpenXml
	- Scrubbed mode for sharing (anonymizes sensitive data)
- Curated summary & Notes:
	- SA curated description of each table. Including guidance, best practice, and recommendations with relevant documentation links.

## 🔨 Building from Source

**Requirements:**
- .NET 8.0 SDK
- Windows (WPF dependency)
- PowerShell 7+

**Build:**
```bash
dotnet restore vHC/HC.sln
dotnet build vHC/HC.sln --configuration Release
```

**Tests** (Windows only):
```bash
dotnet test vHC/VhcXTests/VhcXTests.csproj
```

## 🔧 Troubleshooting

**Common Issues:**
- **"Access Denied"** - Ensure running as Administrator with Backup Administrator role
- **"No Veeam installation detected"** - Tool must run on system with VBR Console or VB365 installed
- **Low disk space** - Ensure C:\ has at least 500MB free for output files
- **PowerShell errors** - Verify PowerShell 7+ is installed

## ✍ Contributions

We welcome contributions from the community! We encourage you to create [issues](https://github.com/VeeamHub/veeam-healthcheck/issues/) for Bugs & Feature Requests and submit Pull Requests. For more detailed information, refer to our [Contributing Guide](CONTRIBUTING.md).

## 🤝🏾 License

* [MIT License](LICENSE)

## 🤔 Questions

If you have any questions or something is unclear, please don't hesitate to [create an issue](https://github.com/VeeamHub/veeam-healthcheck/issues/new/choose) and let us know!

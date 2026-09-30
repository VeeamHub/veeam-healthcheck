# Getting Started

## Requirements

- Windows system with **VBR Console** or **VB365** installed
- Run as an **elevated user** with **Backup Administrator** role
- **PowerShell 7** on the machine running the tool, even for `/remote` runs. Required for VBR v13, where the minimum version comes from the installed VBR PowerShell module (7.6 or later for current v13 builds). VBR v12.3 can run under Windows PowerShell 5.1.
- **500 MB** free disk space on `C:\` (default output: `C:\temp\vHC`)
- Veeam Cloud Service Provider servers are **not** supported

## Supported Versions

| Product | Supported Versions | Notes |
|---|---|---|
| **Veeam Backup & Replication** | v12.3, v13 (Windows & Linux) | For v11/v12 pre-12.3, use [Health Check v2](https://github.com/VeeamHub/veeam-healthcheck/releases/tag/v2.0.0.681) |
| **Veeam Backup for Microsoft 365** | v6, v7, v8 | |

## Installation

1. **[Download](https://github.com/VeeamHub/veeam-healthcheck/releases/latest)** the latest `VeeamHealthCheck.zip`
2. **Extract** the archive on your Veeam server
3. **Run** `VeeamHealthCheck.exe` as Administrator

No installer. Single executable. The only prerequisite is PowerShell 7 where your VBR version requires it (see [Requirements](#requirements)).

## Running a Health Check

=== "GUI"
    1. Launch `VeeamHealthCheck.exe` as Administrator (running it with no arguments, or with `/gui`, opens the GUI)
    2. On the **Ad-hoc Health Check** tab, pick the server, the **Product Type** (*Auto-detect*, *VBR*, *VB365*, or *Both*), and the **Collection Period** (**7**, **30**, or **90 Days**), then set the export and anonymization checkboxes
    3. Click **Accept Terms**, then **Run**
    4. Review the generated report

    To add or remove servers, use the gear button next to the server list to open **Manage Servers**. Removing a server also deletes its saved credentials, and the local machine can't be removed. The theme button in the header cycles through Dark, Light, and **System** (which follows the OS setting) and remembers your choice, and **About / Disclaimer** shows the terms.

    !!! note
        The GUI offers 7, 30, and 90 days. The 12-day window is available only from the CLI (`/days:12`).

=== "CLI"
    ```powershell
    # Standard health check (7-day window)
    VeeamHealthCheck.exe /run

    # 30-day window, also export PDF
    VeeamHealthCheck.exe /run /days:30 /pdf

    # Custom output directory, open report when done
    VeeamHealthCheck.exe /run /outdir=D:\Reports /show:report
    ```

## CLI Reference

```
VeeamHealthCheck.exe [options]
```

| Option | Description |
|---|---|
| `/run` | Execute health check via CLI |
| `/gui` | Launch graphical interface (also the default when run with no arguments) |
| `/help` | Show full help menu |
| `/days:<N>` | Reporting window: 7, 12, 30, or 90 days (default: 7) |
| `/outdir=<path>` | Output directory (default: `C:\temp\vHC`) |
| `/pdf` | Also export as PDF |
| `/pptx` | Also export as PowerPoint |
| `/scrub:true` | Anonymize sensitive data |
| `/scrub:false` | Keep full detail (disable anonymization) |
| `/lite` | Skip per-job HTML exports (faster) |
| `/show:report` | Open report in browser when done |
| `/show:files` | Open output folder in Explorer |
| `/vbr`, `/vb365` | Target VBR or VB365 instead of auto-detecting; use both (`/vbr /vb365`) for a server running both products |
| `/remote` | Enable remote execution |
| `/host=<hostname>` | Target remote Veeam server |
| `/security` | Run security-focused assessment only |
| `/import` | Generate report from existing data, no new collection (default path: `C:\temp\vHC`) |
| `/import:<path>` | Generate report from CSV files at `<path>` (flat or nested `Original\VBR\<server>\<timestamp>` layout) |
| `/hotfix` | Run hotfix detection |
| `/path=<dir>` | Path for hotfix detection (used with `/hotfix`) |
| `/silent` | Never prompt; fail fast with an [exit code](getting-started.md#exit-codes). Mutually exclusive with `/savecreds` |
| `/savecreds` | One-shot interactive seed: prompts for a username and password and stores them (DPAPI, current user) for `/host=` (default: localhost), then exits |
| `/credfile=<path>` | Load host credentials from a JSON credfile into memory only (nothing is persisted). Composes with `/silent` |
| `/clearcreds` | Clear stored credentials |
| `/monitor:setup` | Install vhc-monitor and register a 5-minute scheduled task |
| `/monitor:run` | Trigger an immediate monitor check |
| `/monitor:status` | Show monitor installation and last-run status |
| `/monitor:disable` | Remove the scheduled task (keeps config and files) |
| `/debug` | Enable debug logging |

## Remote Execution

Run against a remote Veeam server without being locally logged into it:

```powershell
VeeamHealthCheck.exe /run /host=vbrserver.veeam.local
```

Credentials are prompted and stored securely in Windows Credential Manager.

### Unattended / fleet execution (scripting many servers)

For non-interactive runs (CI, scripts, running across many VBR servers) supply credentials
with a **credfile** instead of an interactive prompt. A credfile is a JSON map of
host → `{ username, passwordBase64 }`, where `passwordBase64` is the base64 of the
UTF‑8 plaintext password. One file can hold credentials for an entire fleet:

```json
{
  "vbr01.corp.local": { "username": "svc-vhc", "passwordBase64": "<base64-pw>" },
  "vbr02.corp.local": { "username": "svc-vhc", "passwordBase64": "<base64-pw>" },
  "vbr15.corp.local": { "username": "svc-vhc", "passwordBase64": "<base64-pw>" }
}
```

```powershell
# generate a passwordBase64 value
[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($plaintextPassword))

# run unattended against one host using the credfile
VeeamHealthCheck.exe /run /remote /host=vbr01.corp.local /credfile=C:\creds\fleet.json /silent

# loop a fleet
foreach ($h in 'vbr01.corp.local','vbr02.corp.local') {
  VeeamHealthCheck.exe /run /remote /host=$h /credfile=C:\creds\fleet.json /silent /outdir=D:\Reports\$h
}
```

`/silent` makes the run fail fast (exit code) instead of prompting. Prefer `/credfile=`
over passing a password inline — inline passwords leak into process arguments, shell
history, and CI logs. Treat the credfile as a secret and delete it after use.

### Exit codes

In silent mode the process exit code reports the outcome:

| Code | Meaning |
|---|---|
| 0 | Success |
| 1 | Generic failure |
| 2 | Credentials missing, or conflicting flags (`/silent` with `/savecreds`) |
| 3 | Authentication failed |
| 4 | Account is MFA-enabled (unsupported for unattended VBR) |
| 5 | Host unreachable |
| 6 | `/credfile=` invalid (malformed JSON, missing fields, bad Base64) |
| 7 | No Veeam product detected and no `/host=` provided |
| 8 | PowerShell 7 missing, or older than the VBR PowerShell module requires |

## Troubleshooting

| Problem | Solution |
|---|---|
| **"Access Denied"** | Run as Administrator with Backup Administrator role |
| **"No Veeam installation detected"** | Tool must run on a system with VBR Console or VB365 installed |
| **Low disk space errors** | Ensure `C:\` has at least 500 MB free |
| **PowerShell errors** | Verify PowerShell 7 is installed and meets the VBR module's minimum version (7.6+ for VBR v13). In silent mode this is exit code 8 |
| **Credentials not working** | Run `/clearcreds` then re-authenticate |

## Sample Report

[View a sample anonymized report](https://htmlpreview.github.io/?https://github.com/VeeamHub/veeam-healthcheck/blob/master/SAMPLE/Veeam%20Health%20Check%20Report_VBR_anon_2024.11.01.101304.html) to see what output looks like before running.

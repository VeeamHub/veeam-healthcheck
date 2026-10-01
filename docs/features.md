# Features

## Report Sections

### VBR (Veeam Backup & Replication)

| Section | Description |
|---|---|
| **Job Session Analytics** | Min/max/average for duration, backup size, data size, and wait times per job |
| **Success & Change Rates** | Environment-wide visibility — success rate, change rate by job |
| **Concurrency Heat Maps** | Job and task overlap visualized over time |
| **Managed Server Table** | All registered servers with platform identification (Proxmox, Nutanix AHV, etc.) |
| **Job Info Table** | Per-job configuration with platform, policy, and sizing data |
| **License & Auto-Update** | License expiry, edition, and auto-update status |
| **Repository Summary** | Capacity, free space, immutability, and dedup/compression settings |
| **Proxy Configuration** | Proxy count, type, sizing, and task capacity |
| **Configuration Review** | Best-practice flags highlighting areas needing attention |
| **Curated Guidance** | Recommendations with links to Veeam documentation |

### VB365 (Veeam Backup for Microsoft 365)

| Section | Description |
|---|---|
| **Organization Summary** | Protected users, groups, sites, and teams |
| **Job Configuration** | Schedule, retention, and policy overview |
| **Repository Health** | Capacity and utilization per repository |

## Export Formats

| Format | Notes |
|---|---|
| **HTML** | Default — single file, embedded CSS/JS, works offline |
| **PDF** | Via DinkToPdf, print-ready |
| **PowerPoint** | Via HtmlToOpenXml, editable slide deck |
| **Scrubbed** | Any format with IPs, server names, and credentials anonymized |

## Execution Modes

| Mode | Flag | Use Case |
|---|---|---|
| **GUI** | `/gui` | Interactive, guided configuration |
| **CLI** | `/run` | Scripted, automated, unattended |
| **Silent** | `/run /silent` | Fleet automation, no prompts |
| **Remote** | `/run /host=<name>` | Run from a jump host against a remote VBR server |
| **Security** | `/security` | Security-focused assessment only |
| **Import** | `/import` | Generate report from pre-collected CSV data |

## Platform Coverage

Veeam Health Check detects and reports on managed server platform types:

- Proxmox VE
- Nutanix AHV
- HPE Morpheus VME
- Scale Computing HyperCore
- XCP-ng
- Sangfor HCI
- Red Hat Virtualization
- Kasten
- Standard VMware/Hyper-V

## Silent / Unattended Mode

Added in May 2026. Enables fleet-wide automation with no interactive prompts.

```powershell
VeeamHealthCheck.exe /run /silent /days:30 /outdir=\\nas\reports\$env:COMPUTERNAME /pdf
```

Exit codes are listed in [Getting Started](getting-started.md#exit-codes) and in the help menu (`/help`).

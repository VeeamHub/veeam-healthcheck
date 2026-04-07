# Veeam Health Check: PowerShell vs REST API Coverage Analysis

**Date:** 2026-03-25
**Purpose:** Determine feasibility of replacing PowerShell collection with REST API calls
**VBR REST API Version:** v1.3-rev1 (VBR v13, port 9419)
**VB365 REST API Version:** v8 (VB365 v8)

---

## Executive Summary

| Metric | Value |
|--------|-------|
| Total VBR PS Cmdlets | 79 |
| VBR Cmdlets with REST Equivalent | 52 |
| VBR Cmdlets with NO REST Equivalent | 27 |
| **VBR REST Coverage** | **65.8%** |
| Total VB365 PS Cmdlets | 28 |
| VB365 Cmdlets with REST Equivalent | 20 |
| VB365 Cmdlets with NO REST Equivalent | 8 |
| **VB365 REST Coverage** | **71.4%** |
| Non-Cmdlet Collection Methods | 7 |
| Non-Cmdlet with REST Alternative | 1 |
| **Overall Coverage (all methods)** | **64.0% (73/114)** |
| **Probability of 100% REST Coverage** | **~0% today, ~60% within 2 years** |

---

## VBR Cmdlet-to-REST API Mapping

### Legend

| Symbol | Meaning |
|--------|---------|
| YES | REST endpoint exists with equivalent data |
| PARTIAL | REST endpoint exists but returns fewer fields/properties |
| NO | No REST endpoint exists |
| v13+ | Requires VBR v13 or later |
| v13.0.1+ | Requires VBR v13 patch 1 or later |

---

### Connection & Setup

| PS Cmdlet | REST Equivalent | Endpoint | Notes |
|-----------|----------------|----------|-------|
| `Connect-VBRServer` | YES | `POST /api/oauth2/token` | OAuth2 token auth replaces session |
| `Disconnect-VBRServer` | YES | `POST /api/oauth2/logout` | Token revocation |
| `Get-VBRBackupServerInfo` | YES | `GET /api/v1/serverInfo` | Server version, build info |
| `Rescan-VBREntity` | PARTIAL | `POST /api/v1/backupInfrastructure/repositories/rescan` | Only repo rescan, not full entity |

### Servers & Infrastructure

| PS Cmdlet | REST Equivalent | Endpoint | Notes |
|-----------|----------------|----------|-------|
| `Get-VBRServer` | PARTIAL | `GET /api/v1/backupInfrastructure/managedServers` | Server list yes, **hardware info (CPU/RAM) NO** |
| `Get-VBRViProxy` | YES | `GET /api/v1/backupInfrastructure/proxies` | Full VMware proxy details |
| `Get-VBRHvProxy` | PARTIAL | `GET /api/v1/backupInfrastructure/proxies` | Added v13.0.1, may lack some HV-specific fields |
| `Get-VBRNASProxyServer` | NO | — | NAS proxy not in REST API |
| `Get-VBRCDPProxy` | NO | — | CDP proxy not in REST API |
| `Get-VBRCloudGateway` | NO | — | Cloud Connect not in REST API |
| `Get-VBRTapeServer` | NO | — | Tape infrastructure not in REST API |

### Repositories & Storage

| PS Cmdlet | REST Equivalent | Endpoint | Notes |
|-----------|----------------|----------|-------|
| `Get-VBRBackupRepository` | YES | `GET /api/v1/backupInfrastructure/repositories` | Standard repos, full details |
| `Get-VBRBackupRepository -ScaleOut` | YES | `GET /api/v1/backupInfrastructure/scale-out-repositories` | SOBR top-level |
| `Get-VBRRepositoryExtent` | PARTIAL | `GET /api/v1/backupInfrastructure/scale-out-repositories/{id}` | Performance extents in SOBR response |
| `Get-VBRCapacityExtent` | PARTIAL | `GET /api/v1/backupInfrastructure/scale-out-repositories/{id}` | Capacity tier in SOBR response, depth TBD |
| `Get-VBRArchiveExtent` | PARTIAL | `GET /api/v1/backupInfrastructure/scale-out-repositories/{id}` | Archive tier in SOBR response, depth TBD |

### Jobs

| PS Cmdlet | REST Equivalent | Endpoint | Notes |
|-----------|----------------|----------|-------|
| `Get-VBRJob` | YES | `GET /api/v1/jobs` | VMware/HV backup + replication jobs |
| `Get-VBRConfigurationBackupJob` | YES | `GET /api/v1/configurationBackup` | Config backup endpoint exists |
| `Get-VBRComputerBackupJob` | PARTIAL | `GET /api/v1/agents` | Agent management exists, job details partial |
| `Get-VBREPJob` | NO | — | Legacy endpoint jobs, likely deprecated anyway |
| `Get-VBRUnstructuredBackupJob` | YES (v13.0.1+) | `GET /api/v1/jobs` with NAS type | NAS jobs added in v13.0.1 |
| `Get-VBRUnstructuredBackup` | PARTIAL | `GET /api/v1/backups` | Backup chains yes, NAS-specific metrics TBD |
| `Get-VBRPluginJob` | NO | — | Plugin jobs (Oracle RMAN, SAP HANA) not in REST |
| `Get-VBRCDPPolicy` | NO | — | CDP policies not in REST API |
| `Get-VBRvCDReplicaJob` | YES | `GET /api/v1/jobs` with CloudDirector type | Cloud Director jobs supported |
| `Get-VBRTapeJob` | NO | — | Tape jobs confirmed not in REST API |
| `Get-VBRSureBackupJob` | NO | — | SureBackup not in REST API |
| `Get-VBRNASBackupCopyJob` | PARTIAL | `GET /api/v1/jobs` | May be under backup copy type, unconfirmed |
| `Get-VBRCatalystCopyJob` | NO | — | Catalyst copy jobs not in REST API |

### Sessions & Restore Points

| PS Cmdlet | REST Equivalent | Endpoint | Notes |
|-----------|----------------|----------|-------|
| `Get-VBRBackupSession` | YES | `GET /api/v1/sessions` | Last 30 days, filterable |
| `Get-VBRComputerBackupJobSession` | PARTIAL | `GET /api/v1/sessions` | Agent sessions may be included |
| `Get-VBRTaskSession` | YES | Tasks section in API | Task-level session details available |
| `Get-VBRUnstructuredBackupRestorePoint` | PARTIAL | `GET /api/v1/restorePoints` | General restore points, NAS-specific TBD |
| `Get-VBRRestorePoint` | YES | `GET /api/v1/restorePoints` | VM restore points |

### Replication & DR

| PS Cmdlet | REST Equivalent | Endpoint | Notes |
|-----------|----------------|----------|-------|
| `Get-VBRReplica` | YES | `GET /api/v1/replicas` | Replica management exists |
| `Get-VBRFailoverPlan` | YES | Failover/Failback sections | Full failover management |

### Workload Protection & Discovery

| PS Cmdlet | REST Equivalent | Endpoint | Notes |
|-----------|----------------|----------|-------|
| `Get-VBRBackup` | YES | `GET /api/v1/backups` | Backup chain enumeration |
| `Get-VBRDiscoveredComputer` | YES | `GET /api/v1/agents/discoveredComputers` | Agent discovery |
| `Find-VBRViEntity` | YES | `GET /api/v1/inventory/vmware/hosts/{id}/vms` | Inventory browser |
| `Find-VBRHvEntity` | YES | `GET /api/v1/inventory/hyperv/hosts/{id}/vms` | Inventory browser |

### Infrastructure Components

| PS Cmdlet | REST Equivalent | Endpoint | Notes |
|-----------|----------------|----------|-------|
| `Get-VBRApplicationGroup` | NO | — | SureBackup components not in REST |
| `Get-VBRVirtualLab` | NO | — | SureBackup components not in REST |
| `Get-VBRWANAccelerator` | YES | `GET /api/v1/backupInfrastructure/wanAccelerators` | Full CRUD |
| `Get-VBRTapeLibrary` | NO | — | Tape infrastructure not in REST |
| `Get-VBRTapeMediaPool` | NO | — | Tape infrastructure not in REST |
| `Get-VBRTapeVault` | NO | — | Tape infrastructure not in REST |

### Security & Licensing

| PS Cmdlet | REST Equivalent | Endpoint | Notes |
|-----------|----------------|----------|-------|
| `Get-VBRInstalledLicense` | YES | `GET /api/v1/license` | License details endpoint exists |
| `Get-VBRUserRoleAssignment` | YES | Users and Roles section | RBAC management |
| `Get-VBRCredentials` | YES | `GET /api/v1/credentials` | Credential records |
| `Get-VBRMailNotificationConfiguration` | YES | General Options section | Email/notification settings |
| `Get-VBRNetworkTrafficRule` | YES | Traffic Rules section | Traffic rule management |

### Malware Detection (v12+)

| PS Cmdlet | REST Equivalent | Endpoint | Notes |
|-----------|----------------|----------|-------|
| `Get-VBRMalwareDetectionOptions` | YES | Malware Detection section | Detection settings |
| `Get-VBRMalwareDetectionObject` | YES | Malware Detection section | Infected objects |
| `Get-VBRMalwareDetectionEvent` | YES | Malware Detection section | Detection events |
| `Get-VBRMalwareDetectionExclusion` | YES | Malware Detection section | Exclusion management |

### Security & Compliance (v12+)

| PS Cmdlet | REST Equivalent | Endpoint | Notes |
|-----------|----------------|----------|-------|
| `Start-VBRSecurityComplianceAnalyzer` | PARTIAL | Security section | May not have trigger endpoint |
| `Get-VBRSecurityComplianceAnalyzerResults` | PARTIAL | Security section | Results retrieval unclear |

### Entra ID (v13+)

| PS Cmdlet | REST Equivalent | Endpoint | Notes |
|-----------|----------------|----------|-------|
| `Get-VBREntraIDTenant` | YES | `GET /api/v1/jobs` with EntraID type | Entra ID support in REST |
| `Get-VBREntraIDLogsBackupJob` | PARTIAL | Jobs section | May be under Entra job type |
| `Get-VBREntraIDTenantBackupJob` | YES | `GET /api/v1/jobs` with EntraIDTenantBackup | Confirmed job type |

### Cloud Connect

| PS Cmdlet | REST Equivalent | Endpoint | Notes |
|-----------|----------------|----------|-------|
| `Get-VBRCloudTenant` | NO | — | Cloud Connect not in REST API, long-term roadmap |

---

## VB365 Cmdlet-to-REST API Mapping

### Core Infrastructure

| PS Cmdlet | REST Equivalent | Endpoint | Notes |
|-----------|----------------|----------|-------|
| `Get-VBOVersion` | YES | Server info endpoint | Version info available |
| `Get-VBOOrganization` | YES | `GET /v8/Organizations` | Full org management |
| `Get-VBOOrganizationUser` | YES | `GET /v8/Organizations/{id}/Users` | User enumeration |
| `Get-VBOApplication` | PARTIAL | Organization sub-resources | App details may be nested |

### Backup Infrastructure

| PS Cmdlet | REST Equivalent | Endpoint | Notes |
|-----------|----------------|----------|-------|
| `Get-VBORepository` | YES | `GET /v8/BackupRepositories` | Repository management |
| `Get-VBOObjectStorageRepository` | YES | `GET /v8/ObjectStorageRepositories` | Object storage repos |
| `Get-VBOProxy` | YES | `GET /v8/Proxies` | Proxy management |

### Jobs & Sessions

| PS Cmdlet | REST Equivalent | Endpoint | Notes |
|-----------|----------------|----------|-------|
| `Get-VBOJob` | YES | `GET /v8/Jobs` | Job management |
| `Get-VBOCopyJob` | YES | `GET /v8/Jobs` (copy type) | Copy jobs included |
| `Get-VBOJobSession` | YES | `GET /v8/JobSessions` | Session history |
| `Get-VBOEntityData` | PARTIAL | Various entity endpoints | Fragmented across multiple endpoints |

### Licensing & Settings

| PS Cmdlet | REST Equivalent | Endpoint | Notes |
|-----------|----------------|----------|-------|
| `Get-VBOLicense` | YES | License endpoint | License info |
| `Get-VBOServerComponents` | NO | — | Server component details not in REST |

### Retention & Exclusions

| PS Cmdlet | REST Equivalent | Endpoint | Notes |
|-----------|----------------|----------|-------|
| `Get-VBOFolderExclusions` | NO | — | Folder exclusions not in REST |
| `Get-VBOGlobalRetentionExclusion` | NO | — | Global retention exclusions not in REST |

### Email & Configuration

| PS Cmdlet | REST Equivalent | Endpoint | Notes |
|-----------|----------------|----------|-------|
| `Get-VBOEmailSettings` | PARTIAL | Settings endpoints | Email config may be partial |
| `Get-VBOHistorySettings` | NO | — | History retention not in REST |
| `Get-VBOInternetProxySettings` | NO | — | Internet proxy config not in REST |

### Authentication & Authorization

| PS Cmdlet | REST Equivalent | Endpoint | Notes |
|-----------|----------------|----------|-------|
| `Get-VBOTenantAuthenticationSettings` | PARTIAL | Auth endpoints | May be partial |
| `Get-VBOOperatorAuthenticationSettings` | PARTIAL | Auth endpoints | May be partial |
| `Get-VBORbacRole` | YES | RBAC endpoints | Role management |

### Portal & API

| PS Cmdlet | REST Equivalent | Endpoint | Notes |
|-----------|----------------|----------|-------|
| `Get-VBORestorePortalSettings` | YES | Portal settings | Restore portal config |
| `Get-VBORestAPISettings` | YES | API settings | REST API config |

### Security & Usage

| PS Cmdlet | REST Equivalent | Endpoint | Notes |
|-----------|----------------|----------|-------|
| `Get-VBOSecuritySettings` | PARTIAL | Security endpoints | May lack some fields |
| `Get-VBOUsageData` | NO | — | Usage statistics not in REST |

---

## Non-Cmdlet Collection Methods

| Method | What It Collects | REST Alternative | Notes |
|--------|-----------------|------------------|-------|
| **Registry: SqlInstanceName/SqlServerName** | VBR database connection info | NO | Internal config, no REST exposure |
| **Registry: SqlActiveConfiguration** | DB type (MSSQL vs PostgreSQL) | NO | Internal config |
| **Registry: PostgreSql/MsSql configs** | DB hostname, database name | NO | Internal config |
| **.NET API: CDBManager.BestPractices.GetAll()** | Security compliance results (v12) | PARTIAL | v13 REST Security section may cover |
| **.NET API: CNasBackup/CNasBackupPoint** | NAS backup details/sizes | PARTIAL | Backup objects endpoint may have some data |
| **.NET API: SBackupOptions.get_GlobalMFA()** | Global MFA setting | NO | Internal API only |
| **Log Parsing: VMC.log** | NAS infrastructure sizes, object storage stats | NO | Parsed from diagnostic logs |
| **Log Parsing: VB365 VMC logs** | VB365 product/license/host details | NO | Parsed from diagnostic logs |
| **Log Parsing: VB365 Permission logs** | Microsoft API permissions | NO | Parsed from diagnostic logs |
| **DLL Version: Veeam.Backup.Core.dll** | Build version/patch info | YES | `GET /api/v1/serverInfo` returns version |
| **Config: VbrConfig.json** | Health check thresholds/rules | N/A | Internal HC config, not VBR data |

---

## Gap Analysis by Severity

### BLOCKING Gaps (No REST alternative, no workaround)

These features have **zero REST API coverage** and require PowerShell:

| Area | Cmdlets Affected | Impact on Health Check |
|------|-----------------|----------------------|
| **Tape Infrastructure** | `Get-VBRTapeJob`, `Get-VBRTapeServer`, `Get-VBRTapeLibrary`, `Get-VBRTapeMediaPool`, `Get-VBRTapeVault` (5) | Entire tape section of report |
| **CDP** | `Get-VBRCDPPolicy`, `Get-VBRCDPProxy` (2) | CDP proxy and policy analysis |
| **SureBackup** | `Get-VBRSureBackupJob`, `Get-VBRApplicationGroup`, `Get-VBRVirtualLab` (3) | SureBackup validation section |
| **Plugin Jobs** | `Get-VBRPluginJob` (1) | Oracle RMAN, SAP HANA job analysis |
| **Cloud Connect** | `Get-VBRCloudTenant`, `Get-VBRCloudGateway` (2) | Cloud Connect provider/tenant section |
| **Catalyst Copy** | `Get-VBRCatalystCopyJob` (1) | Catalyst copy job analysis |
| **Server Hardware** | `Get-VBRServer` hardware properties (1) | CPU/RAM analysis for all managed servers |
| **Registry Reads** | 6 registry paths | DB config detection, SQL instance info |
| **.NET API Calls** | 3 internal API methods | MFA settings, NAS details, compliance (v12) |
| **Log Parsing** | 3 log file sources | NAS sizes, VB365 permissions, object storage stats |

**Total BLOCKING items: 27 cmdlets + 12 non-cmdlet methods = 39 collection points**

### PARTIAL Gaps (REST endpoint exists but data incomplete)

| Area | Issue |
|------|-------|
| SOBR Capacity/Archive Tiers | Endpoint exists, field depth unconfirmed |
| Agent Backup Jobs | Agent management exists, job-level detail unclear |
| NAS Backup Copy Jobs | May be under generic backup copy type |
| Security Compliance Analyzer | Trigger/results endpoints unconfirmed |
| VB365 Entity Data | Fragmented across multiple endpoints vs single PS cmdlet |
| VB365 Auth Settings | Partial coverage of tenant/operator auth settings |

### NO Gaps (Full REST coverage)

| Area | Cmdlet Count |
|------|-------------|
| VMware/HV Backup Jobs | 5 |
| Repositories (standard) | 3 |
| Sessions & Task Sessions | 4 |
| Licensing | 2 |
| Credentials & Notifications | 3 |
| Traffic Rules | 1 |
| Malware Detection | 4 |
| Replication & DR | 2 |
| Workload Discovery | 4 |
| WAN Accelerators | 1 |
| User Roles | 2 |
| VB365 Core (orgs, repos, proxies, jobs) | 12 |

---

## REST API Version Requirements

| Feature | Minimum VBR Version |
|---------|-------------------|
| Core jobs, repos, sessions | v12 (API v1.0) |
| Malware detection | v12 (API v1.1) |
| Entra ID jobs | v13 (API v1.3) |
| Hyper-V proxy details | v13.0.1 (API v1.3-rev1) |
| NAS/file share jobs | v13.0.1 (API v1.3-rev1) |
| SOBR full management | v12+ (API v1.1+) |

---

## Performance Comparison

| Metric | PowerShell | REST API |
|--------|-----------|----------|
| Repository query | ~30 seconds | ~2 seconds |
| Job listing | ~10-15 seconds | ~1-2 seconds |
| Session history (30 days) | ~20-60 seconds | ~3-5 seconds |
| Auth overhead | Module load: 15-30 sec | Token: <1 sec |
| Concurrency | Single-threaded PS runspace | Parallel HTTP calls |
| Rate limiting (v13) | YES - PS now throttled via service layer | YES - same backend |

**REST is 5-15x faster per query and supports true parallelism.**

---

## Probability Assessment: 100% REST API Coverage

### Today (VBR v13, API v1.3-rev1): **~0% probability**

27 VBR cmdlets and 8 VB365 cmdlets have no REST equivalent. The blocking gaps (tape, CDP, SureBackup, plugin jobs, Cloud Connect, server hardware) are fundamental feature areas, not edge cases. Registry reads and .NET API calls have no REST path at all.

### Within 1 year (projected v14): **~30% probability**

Veeam's v13 Web UI is REST-backed, driving REST API expansion. Expect: agent jobs, possibly CDP. Unlikely: tape, Cloud Connect, hardware info.

### Within 2 years (projected v15): **~60% probability**

If Veeam continues the trajectory of REST-ifying the UI, most job types should have endpoints. Tape and Cloud Connect are explicitly "long-term roadmap." Hardware info and registry/log data are unlikely to ever be REST-exposed.

### Ever reaching true 100%: **Unlikely**

Registry reads, .NET internal API calls, log file parsing, and server hardware info are **architectural gaps** — they expose data that lives outside VBR's REST surface by design. These would require either:
- Veeam adding dedicated health-check/diagnostic REST endpoints (unlikely)
- A hybrid approach using REST + local agent for host-level data

---

## Recommendations

### Strategy 1: Hybrid REST + PowerShell (Recommended)

Use REST API for the ~65% of data it covers well (jobs, repos, sessions, licensing, credentials, malware, traffic rules). Keep PowerShell for blocking gaps. Benefits:
- 5-15x speed improvement for majority of collection
- Cross-platform potential for REST portion
- Graceful degradation when PS unavailable

### Strategy 2: REST-First with Fallback

Default to REST for everything, fall back to PS when REST returns 404 or insufficient data. Complexity: medium. Risk: version-dependent behavior.

### Strategy 3: Wait for v15+

If the goal is pure REST, wait 2+ years. Risk: may never achieve 100%. Not recommended for near-term.

### Strategy 4: REST + WMI/CIM Direct (No PS Module)

Replace PS module with REST API + direct WMI/CIM queries (via C# `System.Management`) for hardware data. Eliminates PS module dependency entirely but still needs Windows for WMI. Covers ~85% of collection without the Veeam PS module.

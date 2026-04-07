# Account & Cloud Infrastructure Mapping

**Date:** 2026-04-03
**Branch:** `feature/account-cloud-infra-mapping`

## Deliverables

### 1. PowerShell Collector — Object Storage Repos
- **File:** `Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Private/Get-VhciObjectStorageRepos.ps1`
- Calls `Get-VBRObjectStorageRepository` and exports `_ObjectStorageRepos.csv`
- Handles provider-specific object shapes (Amazon S3, Azure Blob) via try/catch
- Collects: Name, Type, Bucket/Container, Folder, Region, Account (display name only), UseGateway, Gateway
- Never exposes passwords or credential secrets

### 2. Collector Wiring — Get-VBRConfig.ps1
- Added `Invoke-VhcCollector -Name 'ObjectStorageRepos'` in Task 6 block after ArchiveTier

### 3. CSV Parser Fields & Methods — CCsvParser.cs
- Added fields: `objectStorageRepos`, `userRoles`
- Added methods: `GetDynamicObjectStorageRepos()`, `GetDynamicUserRoles()`

### 4. HTML Table — User Roles
- **File:** `Functions/Reporting/Html/VBR/VbrTables/GeneralSettings/CUserRolesTable.cs`
- Columns: Name (scrub-aware), Role, Description
- Modeled after `CCredentialsTable.cs`

### 5. HTML Table — Object Storage Repositories
- **File:** `Functions/Reporting/Html/VBR/VbrTables/Repositories/CObjectStorageReposTable.cs`
- Columns: Name (scrub-aware), Type, Bucket/Container, Folder, Region, Account (scrub-aware), Gateway
- Modeled after `CCredentialsTable.cs`

### 6. Report Wiring — CHtmlTables.cs & CHtmlBodyHelper.cs
- Added `AddCredentialsTable`, `AddUserRolesTable`, `AddObjectStorageReposTable` methods to `CHtmlTables`
- Added `GeneralSettingsSection` to `CHtmlBodyHelper` (renders Credentials → User Roles → Email Notification)
- Added `ObjectStorageReposTable` to `RepositoryInfoSection` (after ArchiveTier, before Repo)

## Build Verification
- `dotnet build vHC/HC.sln --configuration Debug` — **0 errors, 6 pre-existing warnings**

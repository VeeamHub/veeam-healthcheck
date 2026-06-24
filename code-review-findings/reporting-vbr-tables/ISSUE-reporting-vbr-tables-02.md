---
title: "Replace direct dynamic CSV member access with TryGetValue pattern in 22 table renderers"
severity: High
labels: [reliability, bug]
domain: reporting-vbr-tables
files:
  - vHC/HC_Reporting/Functions/Reporting/Html/VBR/VbrTables/CloudConnect/CCloudTenantsTable.cs:1
  - vHC/HC_Reporting/Functions/Reporting/Html/VBR/VbrTables/CloudConnect/CCloudTenantPerformanceTable.cs:47
  - vHC/HC_Reporting/Functions/Reporting/Html/VBR/VbrTables/CloudConnect/CCloudTenantBackupResourcesTable.cs:53
  - vHC/HC_Reporting/Functions/Reporting/Html/VBR/VbrTables/Replication/CReplicasTable.cs:1
  - vHC/HC_Reporting/Functions/Reporting/Html/VBR/VbrTables/TapeInfra/CTapeMediaPoolsTable.cs:1
  - vHC/HC_Reporting/Functions/Reporting/Html/VBR/VbrTables/ProtectedWorkloads/CEntraTenants.cs:25
confidence: High
---

## Summary

The bug class recently fixed in `CObjectStorageReposTable` (cast to `IDictionary<string,object>` + `TryGetValue` so a row missing a column doesn't throw `RuntimeBinderException` — see the comment at `CObjectStorageReposTable.cs:49-52` citing the "CsvHelper FastDynamicObject quirk on header-only / partial CSVs") still exists in 22 sibling renderers that access dynamic CSV records via direct member access like `(string)(item.tenantname ?? "")`.

## Impact

When a collected CSV is header-only, truncated, or produced by an older/newer collection script with different columns, the first row access throws `RuntimeBinderException`. The per-table `catch` swallows it, so the table renders empty or truncated mid-row with only a log line — exactly the symptom that prompted the objstorage fix. Whole report sections silently go blank for the user.

## Evidence

`CloudConnect/CCloudTenantPerformanceTable.cs:47` — `string name = (string)(item.name ?? "");` — throws if the `name` column is absent; loop aborts via the catch at line 66 and remaining tenants are dropped.

`ProtectedWorkloads/CEntraTenants.cs:25-26` — `entra.TenantName = rec.TenantName;` — same class of failure, and this one re-`throw`s (line 36), relying on callers to suppress.

Occurrence counts of `(string)(item.` per file (grep):

- CloudConnect/CCloudTenantsTable.cs (29), CCloudTenantReplicationResourcesTable.cs (9), CCloudHardwarePlansTable.cs (9), CCloudFailoverPlansTable.cs (9), CCloudTenantBackupResourcesTable.cs (8), CCloudGatewaysTable.cs (8, mixed — uses TryGetValue only for a server lookup at line 80), CCloudReplicasTable.cs (7), CCloudFailoverPlanObjectsTable.cs (5), CCloudHardwarePlanDatastoresTable.cs (4), CCloudGatewayPoolsTable.cs (3)
- Replication/CReplicasTable.cs (6), CReplicaJobsTable.cs (6), CFailoverPlansTable.cs (6)
- TapeInfra/CTapeMediaPoolsTable.cs (7), CTapeLibrariesTable.cs (5), CTapeVaultsTable.cs (3), CTapeServersTable.cs (3)
- SureBackup/CSureBackupVirtualLabsTable.cs (5), CSureBackupAppGroupsTable.cs (3)
- GeneralSettings/CEmailNotificationTable.cs (7), CCredentialsTable.cs (4)
- ProtectedWorkloads/CEntraTenants.cs, NasSourceInfo.cs (`rec.BackupMode`, `rec.FileShareType`, …)

Safe pattern already in tree: `Repositories/CObjectStorageReposTable.cs:53-55` and `GeneralSettings/CUserRolesTable.cs`.

## Suggested fix

Extract the proven helper into one shared utility, e.g. `static string DynGet(object row, string key)` (cast to `IDictionary<string,object>`, `TryGetValue`, null-coalesce to `""`) in `CHtmlTablesHelper` or a new `CsvDynamicExtensions`, and mechanically convert the 22 files. This also removes the per-file drift where some renderers crash the row loop and others rethrow.

# Changelog

All notable changes to Veeam Health Check are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Versions are four-part
(`Major.Minor.Patch.Revision`) and computed from commit history, not hand-edited; see
[ADR 0031](https://github.com/VeeamHub/veeam-healthcheck/blob/dev/docs/adr/0031-commit-driven-four-part-versioning-with-csproj-floor.md).
Release dates are the GitHub release publish dates (UTC).

Every release is also published on the
[GitHub Releases page](https://github.com/VeeamHub/veeam-healthcheck/releases), which carries the download,
checksums, and scan results.

## [Unreleased]

### Added

- Redesigned GUI, migrated from WPF to Avalonia: new theme, a fixed-size tabbed layout with two-column
  cards, a localization pass over the GUI strings, and the console window is hidden on double-click.
  ([#247](https://github.com/VeeamHub/veeam-healthcheck/pull/247))
- New VBR report section, **Orphaned & Superseded Backups**: restore points that do not count toward any
  job's active size, grouped per repository with a job-level rollup and an expandable per-object breakdown
  (HTML and JSON). Fixes [#192](https://github.com/VeeamHub/veeam-healthcheck/issues/192).
- JSON export now includes the `credentials`, `userRoles`, `emailNotification`, and `complianceTable`
  sections that were previously HTML-only, and stamps the `VhcVersion` that produced the file.
  Fixes [#169](https://github.com/VeeamHub/veeam-healthcheck/issues/169).

### Changed

- Moved the runtime from .NET 8 (end of support November 2026) to .NET 10 (LTS). The release is still a
  self-contained single-file executable, so nothing needs installing, and the embedded PowerShell SDK
  moves to 7.6.
- Experimental features are now disabled unless explicitly enabled with an environment flag.
- The PowerShell 7 preflight check now stops the run with a clear message when PowerShell 7 is missing
  entirely, and when the installed version is lower than the one the VBR PowerShell module requires (VBR 13.1
  raised it to 7.6). Fixes [#135](https://github.com/VeeamHub/veeam-healthcheck/issues/135) and
  [#186](https://github.com/VeeamHub/veeam-healthcheck/issues/186).
- Release versions are now computed from commit types
  ([ADR 0031](https://github.com/VeeamHub/veeam-healthcheck/blob/dev/docs/adr/0031-commit-driven-four-part-versioning-with-csproj-floor.md),
  [ADR 0032](https://github.com/VeeamHub/veeam-healthcheck/blob/dev/docs/adr/0032-base-tag-is-highest-ga-tag.md)).
- The VBR report's left navigation is now fully localized (group titles and every link, including Cloud
  Connect and Orphaned & Superseded Backups). The French, Japanese, and Chinese labels for these entries are
  machine-translated; please open an issue to request corrections.

### Fixed

- Scrubbed reports no longer lose their theme when a registered value such as a user named `root` matches a
  CSS or HTML word: the final scrub pass now only replaces values in text and attribute values, and leaves
  the embedded stylesheet and script, tag names and attribute names alone.
  Fixes [#263](https://github.com/VeeamHub/veeam-healthcheck/issues/263).
- PDF and PowerPoint export are no longer skipped silently when scrubbing is on: the GUI disables the PDF
  option with a tooltip explaining why, the CLI warns when `/pdf` or `/pptx` is combined with `/scrub:true`,
  and the log records the skip and each PDF export step.
  Fixes [#261](https://github.com/VeeamHub/veeam-healthcheck/issues/261).
- The GUI now writes the main log file to the chosen output folder instead of the default folder, and a log
  folder that can't be written to no longer causes errors. Fixes [#259](https://github.com/VeeamHub/veeam-healthcheck/issues/259).
- PDF export applies print styles, so the sidebar no longer overlaps content, long tables render in full, and
  columns no longer break mid-word. Fixes [#123](https://github.com/VeeamHub/veeam-healthcheck/issues/123).
- Printing the HTML report from a browser (Ctrl+P) no longer clips wide tables or prints scrollbars; tables
  shrink and wrap to fit the page, and the report always prints in landscape.
  Fixes [#257](https://github.com/VeeamHub/veeam-healthcheck/issues/257).
- Job Session Summary showed every job with 0 sessions and 0 backup size on machines with a day-first date
  locale (for example en-AU or en-GB); session dates are now parsed correctly.
  Fixes [#217](https://github.com/VeeamHub/veeam-healthcheck/issues/217).
- Session statistics are now anchored to the collection start time rather than "now", so they no longer drift
  on long collections. Fixes [#218](https://github.com/VeeamHub/veeam-healthcheck/issues/218).
- Backup Copy per-machine child sessions are rolled up under their parent job instead of appearing under
  "Other Jobs". Fixes [#219](https://github.com/VeeamHub/veeam-healthcheck/issues/219).
- Jobs that `Get-VBRJob` omits (for example Nutanix AHV on VBR 12.3.x) are now discovered, and per-machine
  child jobs resolve to their parent. Fixes [#222](https://github.com/VeeamHub/veeam-healthcheck/issues/222).
- Job sizing no longer reports 0 MB / 0 GB for HPE Morpheus VME, Nutanix AHV, oVirt KVM, Proxmox, public
  cloud jobs, and some Backup Copy jobs; restore points are now matched to jobs in tiers.
- VMware Cloud Director vApp backups no longer show a double-counted Source Size, and rebuilt machines no
  longer leave stale backup chains counted as active. Fixes
  [#197](https://github.com/VeeamHub/veeam-healthcheck/issues/197).
- VBR v13 collection no longer hangs at start when the server certificate has not been accepted
  ([#149](https://github.com/VeeamHub/veeam-healthcheck/issues/149)); the certificate flag is only passed to
  VBR v13 and later, which fixes a regression on v12. MFA checks now time out instead of waiting forever.
- A completed VBR collection is no longer discarded when PowerShell exits with a non-zero code, and an
  orphaned standalone backup (its job was deleted) no longer aborts the Jobs collection.
- VB365 collection reports its real outcome instead of always reporting success, and a missing `VMC.log` no
  longer stops the whole run. NAS collection failures are isolated per stage and logged.
- OS Version is no longer blank for the backup server's own host record on VBR 12.3 and 13.1.
  Fixes [#190](https://github.com/VeeamHub/veeam-healthcheck/issues/190).
- Email Notification table column names now match the Veeam cmdlet output
  ([#176](https://github.com/VeeamHub/veeam-healthcheck/pull/176)).
- HTML markup no longer leaks into JSON export values.
- The GUI now shows an error and re-enables the run button when report generation throws, instead of
  appearing to hang and exiting with code 0.
- The pre-v12 version guard now blocks 3.x and later builds as intended and no longer throws on short or
  non-numeric versions. Fixes [#245](https://github.com/VeeamHub/veeam-healthcheck/issues/245).
- Collection logging is more detailed, and SQL connections use an explicit timeout.
- CLI runs with `/remote`, `/host=`, `/vbr` or `/vb365` but no `/run` now collect and report instead of silently
  exiting with code 0, and `/lite` now renders its report; when no action is selected the tool prints guidance.
  `/help` and `/clearcreds` never imply a run (`/clearcreds` still needs an explicit `/run`).
  Fixes [#226](https://github.com/VeeamHub/veeam-healthcheck/issues/226).
- The VBR report's left navigation now links every top-level section: Server Sizing, SOBR Extent Info,
  Capacity and Archive Tier Configuration, Object Storage Repositories, and a new General Settings group
  (Credentials, User Roles, Email Notification). The Compliance Summary and Compliance Details links are
  hidden when the compliance scan produced no data instead of pointing at a missing section.
  Fixes [#184](https://github.com/VeeamHub/veeam-healthcheck/issues/184).

### Security

- Scrub mode: the key map file is locked down to the current user, Administrators, and SYSTEM; a final pass
  catches leaked hostnames and private-range IPv4 addresses in scrubbed HTML.
- CSV formula injection is neutralized on the PowerShell export path (including the session report), and
  usernames and hosts are escaped in every PowerShell command builder.

## [3.0.1.193] - 2026-06-04

### Added

- Cloud Connect: tenant performance sub-table (max concurrent tasks, bandwidth throttling, max bandwidth).

### Changed

- Report body now follows the sidebar order (Overview, Infrastructure, Cloud Connect, Jobs, Misc), with
  General Settings and Registry Keys moved to the end.
  Fixes [#146](https://github.com/VeeamHub/veeam-healthcheck/issues/146).
- Cloud Connect tenant table: "Throttle Value" and "Throttle Unit" are now "Max Bandwidth" and
  "Bandwidth Unit", and a concurrent-task limit of 0 displays as "Unlimited".

### Fixed

- Protected Workloads counts for VMware and physical workloads.
  Fixes [#125](https://github.com/VeeamHub/veeam-healthcheck/issues/125).
- VBR report is named after the VBR server, not the configuration database host, when the two roles are on
  different machines. Fixes [#158](https://github.com/VeeamHub/veeam-healthcheck/issues/158).
- VB365 Job Statistics, Job Processing, and Sessions tables are expanded.
  Fixes [#49](https://github.com/VeeamHub/veeam-healthcheck/issues/49).
- Job Info subsections can be collapsed independently.
  Fixes [#145](https://github.com/VeeamHub/veeam-healthcheck/issues/145).
- Cloud Connect gateway RAM is shown in GB instead of raw bytes.

## [3.0.1.178] - 2026-06-03

### Added

- Cloud Connect reporting: new tables for failover plans, gateway pools, hardware plans and their
  datastores, replicas, and tenant backup and replication resources, backed by new collection scripts.

### Fixed

- The Cloud Connect navigation entry is hidden when there is no Cloud Connect data.
- Cloud Connect collection scripts no longer contain non-ASCII characters that broke PowerShell 5.1.

## [3.0.1.169] - 2026-05-30

### Added

- Security Compliance table flags rules the report does not recognise (a drift banner and a `NEW` badge), and
  collection emits a catalog of all compliance rule types with their mapping status.
- Description scrubbing covers more tables in scrub mode.

### Changed

- The `/outdir=` argument now takes effect immediately for the output path.

### Fixed

- `Get-NasInfo.ps1` works with the VBR v13 `VMC.log` format. Fixes
  [#112](https://github.com/VeeamHub/veeam-healthcheck/issues/112).
- Compliance labels that Veeam renamed in v13 (from "is X" to "should be X") are mapped again.
- User Roles table handles CSV files that have no Description column.
- Scrub mode no longer fails on lazily-read CSV data.

### Security

- Suppressed a sensitive-information-exposure finding for email notification settings.

## [3.0.1.163] - 2026-05-28

### Added

- Standalone (unmanaged) agents are now covered by session and job reporting.
- `JobId`, `PolicyName`, and `PolicyTag` are emitted in the session CSV and exposed on `CJobSessionInfo` for
  downstream consumers.
- Session collection fast path using `CBackupSession`, with a cmdlet fallback
  ([ADR 0018](https://github.com/VeeamHub/veeam-healthcheck/blob/dev/docs/adr/0018-cbackupsession-fast-path-for-session-collection.md)).

### Changed

- Session report uses GUID-based grouping (`JobId`) to roll up policy child sessions into their parent row.
- Renamed jobs are canonicalised by `JobId`, so session history follows the job rather than the name.
- Per-parent session report files are written separately, and stale files from earlier runs are cleaned up
  automatically.
- `CJobInfoTable` job type resolution matches `CJobSessSummary` (ADR 0020), so the Job Info and Session
  Summary tables show the same job types.
- Agent job `FriendlyType` is resolved for managed and standalone agent rows (`EndpointBackup`,
  `EpAgentBackup`, and `EpAgentPolicy` map to readable names).

### Fixed

- Duplicate and missing rows for policy jobs in the session report; agent job duplicates are dropped, and
  only `ManagedByBackupServer` jobs are deduplicated.
- Agent jobs are counted through the `AgentJobs` collection, which removes double-counting in the Job Session
  Summary totals row.
- Job Summary `missingJobs`: Agent Backup and Agent Standalone are included or excluded according to whether
  the environment has agent jobs.
- `Get-VBR*` cmdlets in `Get-VhcSessionReport` are wrapped in try/catch, so snap-in problems no longer crash
  the report.

### Security

- Scrub mode now redacts the keys of the agent-jobs dictionary (server names) as well as its values.

## [3.0.1.157] - 2026-05-13

### Fixed

- Object Storage Repos collection was silently failing for every run since 3.0.1.131 because the collector
  was missing from the module export list; it is exported now.
- Session collection iterates per job to avoid the Veeam SDK's roughly 600-second SQL command timeout on
  large environments with a remote SQL server.
- A stale-field bug in `CDbAccessor.TestConnection` that logged a misleading "ConnectionString property has
  not been initialized" warning.
- The Object Storage Repos renderer tolerates missing properties.
- `Get-NasInfo.ps1` handles a missing `VMC.log`.
- Veeam Console path detection (used by the MFA check) adds a WMI product probe.

## [3.0.1.148] - 2026-05-11

### Security

- Scrub mode now scrubs email From and To addresses (CWE-359).

## [3.0.1.142] - 2026-05-11

### Added

- Unattended execution path for fleet automation: `/silent` never prompts and fails fast, `/savecreds` seeds
  the DPAPI credential store, and `/credfile=<path>` loads credentials from a JSON file without storing them.
  Granular exit codes: 0 success, 2 credentials missing, 3 authentication failed, 4 MFA detected, 5 host
  unreachable, 6 credential file invalid, 7 product not detected.
- Platform column in the Managed Server and Job Info HTML tables (also in JSON). New PowerShell helper
  `Get-VhciPlatformMap` collects host-to-platform mappings from VBR `PlatformBackupJob` sessions (VBR 12.1+
  only). Canonical platform strings: Proxmox VE, Nutanix AHV, HPE Morpheus VME, SC HyperCore, XCP-ng, Sangfor
  HCI, RHV, Kasten. `CServerCsvInfos` and `CJobCsvInfos` gain an optional `Platform` field, so CSV files
  from earlier versions without the column still load.
- License Summary table shows the "Auto Update" flag.
- Job Session Summary table gains Avg Dedup Ratio and Avg Compress Ratio columns.
- Compliance scan metadata is emitted with the report data.
- Standalone Corruption Guard and Health Check session scripts.

### Changed

- The compliance scan wait ceiling was raised from 45 seconds to 600 seconds for VBR v13, where the Security
  Compliance Analyzer runs asynchronously and can take 4-6 minutes. Previously the compliance CSV was never
  written on real environments.

### Fixed

- Success rate no longer counts retries in totals (re-fix of
  [#108](https://github.com/VeeamHub/veeam-healthcheck/issues/108)).
- Average Change Rate always reported 0 (VBR's `TaskAlgorithm` is "Increment", not "Incremental"), and
  Dedup and Compress ratios were off by a factor of 100.
- `Get-VhciPlatformMap` is exported from the module manifest.
- PowerShell 5.1 compatibility: em dashes and non-ASCII characters were removed from collection scripts.
- Veeam Console path detection (MFA check) probes the Mount Service registry entry.

## [3.0.1.131] - 2026-05-05

### Added

- Executive Dashboard layout for the VBR HTML report: KPI bar, status badges, active sidebar navigation,
  two-column layout, progress bars for storage utilization, infrastructure chips, concurrency heat maps, a
  job schedule heatmap, and a security compliance grid, in the Veeam colour palette.
- Collection of backup accounts and users, user roles, email notification settings, and object storage
  repositories (provider-aware), with new General Settings and Object Storage Repos tables.
- Product type selection.

### Changed

- The HTML report is built with a typed HTML builder (`CSectionTable<T>`); the sections were migrated to a
  section-card pattern.

### Fixed

- MFA check: Veeam Console path is resolved dynamically and derived from `CorePath` for non-C: installs;
  ANSI escape codes are stripped from PowerShell stderr.
- Null reference in the job session summary, and missing compliance tables.
- On-disk totals for non-NAS job types.
- Malware detection handling and logging for VBR compatibility.
- User Roles table handles a missing Description column.
- Malformed HTML, nested tables, and collapsible sections in several report sections.

## [3.0.1.118] - 2026-04-01

### Fixed

- Success rate formula no longer double-counts retries
  ([#108](https://github.com/VeeamHub/veeam-healthcheck/issues/108)).
- Dead debug variable with a sign error in the `CDataFormer` session filter
  ([#113](https://github.com/VeeamHub/veeam-healthcheck/issues/113)).
- Backup server info falls back to `Get-VBRBackupServerInfo` when the registry read fails for non-admin users
  ([#114](https://github.com/VeeamHub/veeam-healthcheck/issues/114)).
- PDF export: balanced margins, print styles for table overflow, and conversion on a dedicated STA thread
  with a 5-minute timeout so the UI no longer hangs
  ([#32](https://github.com/VeeamHub/veeam-healthcheck/issues/32),
  [#123](https://github.com/VeeamHub/veeam-healthcheck/issues/123)).
- Hotfix detector: `serverlist.txt` working directory and a file-exists check before reading.

## [3.0.1.110] - 2026-03-25

### Changed

- Collection process execution now times out after 7 days instead of waiting indefinitely.

### Fixed

- Division by zero when calculating average change rate.

## [3.0.1.103] - 2026-03-21

### Fixed

- Startup hang: PowerShell modules are unblocked and run with `ExecutionPolicy Bypass`.
- Stdout/stderr deadlock in `PSInvoker`, and earlier startup logging was added.

## [3.0.1.101] - 2026-03-18

### Added

- `/outdir=` parameter redirects all output (CSVs, reports, and logs) to a custom path.
- Collection manifest ([ADR 0007](https://github.com/VeeamHub/veeam-healthcheck/blob/dev/docs/adr/0007-collection-manifest-for-error-surfacing.md)): the
  GUI shows a warning dialog and the CLI prints console warnings when a collector fails.
- Security assessment report.

### Changed

- The VBR collector was decomposed into the modular `vHC-VbrConfig` PowerShell module.
- Missing data in CSV parsers now yields empty sets instead of `null`, and CSV files are read with CsvHelper
  rather than splitting on commas.

### Fixed

- VB365 localization resource.
- Report links to the output directory are HTML-encoded and use `file:///` URIs.

## Older releases

Releases before 3.0.1.101 (2026-03) are not itemised above; see the
[GitHub Releases page](https://github.com/VeeamHub/veeam-healthcheck/releases) for those, including 2.0.x,
3.0.0.x, and 3.0.1.46 to 3.0.1.95. The entries below are the original change notes for 1.0.0.920 through
1.0.3.718, kept as written (only the heading and list markup were normalised for Markdown).

### 1.0.3.718

- Fixed issue where program would crash and not create report
- Job Session Summary Table:
    - Added Fails & Retries to Job Session Summary Table
    - Fixed TOTALS line of Jobs
    - Fixed layout
    - Fixed values out of alignment
- Job Info Table:
    - Added TOTAL line & summed up job sizes
    - Fixed layout

### 1.0.3.683

- Fixed issue with PS Collection script.

### 1.0.3.681

- Fixed issue where some columns were swapped
- Fixed issue where unused jobs were incorrectly reported
- Updated VB365 collection script
- Added Various bug fixes, refactors, and unit testing

### 1.0.3.520

- Fixed major crash issues with v11 & v12 both.

### 1.0.3.512

- Fixed issue where Registry Keys were not visible if they were type multi-string.
- Fixed issue where program would produce mostly empty report on v12
    - Added extra logging to PS Scripts for future cases

### 1.0.3.462

- Updated VB365 Script
    - Fixed issue with data collection

### 1.0.3.459

- added error handling to fix a bug/crash

### 1.0.3.434

- Fixed typo in nav table
- Fixed issue where autogate would show TRUE and gateway will still be shown
- Added logic to support gateway pools
- Added logic to prevent crash on log parser
- Added logic to fix success rate going above 100%
- Updated proxy sizing calculations to match BP
- various refactors

### 1.0.3.406

- Updated VB365 collection script
- Fixed issue with Job Concurrency table
- Updated VBR collection script for v12 compatibility
    - Method change broke collection of protected workloads
- Fixed discrepency between Protected Workloads table + Managed Server table

### 1.0.3.392

- Fixed issue with HotFix Detector

### 1.0.3.390

- various bug fixes, refactors and logging changes

### 1.0.3.340

v12 updates

- Proxy & Repository Sizing adjust to new v12 recommendations
- Postgres compatibility
- /lite CLI function: Skips output of individual job reports which can slow down overall report. Default is to collect the reports
- Assorted bug fixes

### 1.2.2.934

- Disabled ability to change output. Will enable when breaking issues are resolved.

### 1.0.2.867

- Updaded collection script for VB365

### 1.0.2.866

- fixed issue where anonymized report contained inconsidtent item replacement

### 1.0.2.863

- Updated dependency to resolve vulnerability: Microsoft Security Advisory CVE 2022-41064

### 1.0.2.862

- Added Error handling to prevent crashes in the event some CSV files are not generated

### 1.0.2.859

- Modified GUI text
- Modified ReadME

### 1.0.2.847

- Security:
    - Removed deprecated/unused dependencies
- Updated Hotfix Detector (/Tools/Hotfixdetector.zip)

### 1.0.2.833

- Fixed broken Job Wait calculation. This could cause the program to hang or crash
- Fixed issue where custom path would cause program to hang
- Added error handling + logging

### 1.0.2.811

- Fixed performance issue with data collection
- Added Hyper-V to protected workloads counter

### 1.0.2.793

- Performance tweaks
- multiple reports of "hangs" should be addressed.
- Fixed "Waits" columns to populate correctly again.

### 1.0.2.748

- CLI enhancement
- variable date ranges (7/30/90)
- improved anonymizations
- various issue/bug fixes

### 1.0.2.676

- Fixed minor bug with column alignment

### 1.0.2.675

- VB365 HealthCheck
    - Scripts added to create Health Check Report for VB365.
- Software detection
    - Software detects B&R or VB365 and creates report for detected software.
- Localization
    - Translating 'vhcres.txt' or 'vb365_vhcres.txt' and adding locale (i.e. vhcres.FR-FR.txt), program can be rebuilt and detect system locale to display GUI and Report in desired language.
- CLI
    - .\VeeamHealthCheck.exe help to see menu

### 1.0.1.1273

- Fixed Issues:
    - [#3](https://github.com/VeeamHub/veeam-healthcheck/issues/3)
    - [#4](https://github.com/VeeamHub/veeam-healthcheck/issues/4)
    - [#5](https://github.com/VeeamHub/veeam-healthcheck/issues/5)
- Fixed math where Job Sessions' success rate could be greater than 100%.

### 1.0.1.1272

- Fixed a bug where capacity tier NAME would show under TYPE. Issue also caused inconsistency with SOBR details.

### 1.0.1.1251

- redesigned UI
    - updated colors
    - collapsible sections
    - expand/collapse all button
- redirected output for HTML reports:
    - default = C:\temp\vHC\Original\
    - scrubbed = C:\temp\vHC\Anonymous
    - optional custom output for report
- added detected VM count (VMware)
    - compares VM count to VMs in backups to look for missing protection
- added detection of physical computers in Protection Groups
    - compares to backups to find if any computers do not have a backup
- + config backup last status

### 1.0.0.938

- fixed out-of-order columns in jobsession reports
- added GUI link to sensitive data kb
- added back-port to v10; partially tested
- fixed issue with registry key detection
- fixed issue with HTML report links

### 1.0.0.929

- fixed issue with missing data when scrub option is enabled

### 1.0.0.925

- fixed issue with config backup reporting

### 1.0.0.924

- PS windows are hidden to make the program look cleaner
- Removed some "TBD" data from report
- Removed SqlSecurePassword from detected registry keys
- adjusted GUI formatting and text

### 1.0.0.920

- Suppressed errors caused by certain job types that were not configured in an environment.

[Unreleased]: https://github.com/VeeamHub/veeam-healthcheck/compare/v3.0.1.193...dev
[3.0.1.193]: https://github.com/VeeamHub/veeam-healthcheck/compare/v3.0.1.178...v3.0.1.193
[3.0.1.178]: https://github.com/VeeamHub/veeam-healthcheck/compare/v3.0.1.169...v3.0.1.178
[3.0.1.169]: https://github.com/VeeamHub/veeam-healthcheck/compare/v3.0.1.163...v3.0.1.169
[3.0.1.163]: https://github.com/VeeamHub/veeam-healthcheck/compare/v3.0.1.157...v3.0.1.163
[3.0.1.157]: https://github.com/VeeamHub/veeam-healthcheck/compare/v3.0.1.148...v3.0.1.157
[3.0.1.148]: https://github.com/VeeamHub/veeam-healthcheck/compare/v3.0.1.142...v3.0.1.148
[3.0.1.142]: https://github.com/VeeamHub/veeam-healthcheck/compare/v3.0.1.131...v3.0.1.142
[3.0.1.131]: https://github.com/VeeamHub/veeam-healthcheck/compare/v3.0.1.118...v3.0.1.131
[3.0.1.118]: https://github.com/VeeamHub/veeam-healthcheck/compare/v3.0.1.110...v3.0.1.118
[3.0.1.110]: https://github.com/VeeamHub/veeam-healthcheck/compare/v3.0.1.103...v3.0.1.110
[3.0.1.103]: https://github.com/VeeamHub/veeam-healthcheck/compare/v3.0.1.101...v3.0.1.103
[3.0.1.101]: https://github.com/VeeamHub/veeam-healthcheck/compare/v3.0.1.95...v3.0.1.101

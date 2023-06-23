1.0.3.681
- Fixed issue where some columns were swapped
- Fixed issue where unused jobs were incorrectly reported
- Updated VB365 collection script
- Added Various bug fixes, refactors, and unit testing

1.0.3.520
- Fixed major crash issues with v11 & v12 both.

1.0.3.512
- Fixed issue where Registry Keys were not visible if they were type multi-string.
- Fixed issue where program would produce mostly empty report on v12
		- Added extra logging to PS Scripts for future cases

1.0.3.462
- Updated VB365 Script
	- Fixed issue with data collection
1.0.3.459
- added error handling to fix a bug/crash

1.0.3.434
- Fixed typo in nav table
- Fixed issue where autogate would show TRUE and gateway will still be shown
- Added logic to support gateway pools
- Added logic to prevent crash on log parser
- Added logic to fix success rate going above 100%
- Updated proxy sizing calculations to match BP
- various refactors

1.0.3.406:
- Updated VB365 collection script
- Fixed issue with Job Concurrency table
- Updated VBR collection script for v12 compatibility
	- Method change broke collection of protected workloads
- Fixed discrepency between Protected Workloads table + Managed Server table

1.0.3.392
- Fixed issue with HotFix Detector

1.0.3.390
- various bug fixes, refactors and logging changes


1.0.3.340
 v12 updates
- Proxy & Repository Sizing adjust to new v12 recommendations
- Postgres compatibility
- /lite CLI function: Skips output of individual job reports which can slow down overall report. Default is to collect the reports
- Assorted bug fixes

 1.2.2.934
- Disabled ability to change output. Will enable when breaking issues are resolved.

1.0.2.867
- Updaded collection script for VB365

1.0.2.866
- fixed issue where anonymized report contained inconsidtent item replacement

1.0.2.863
- Updated dependency to resolve vulnerability: Microsoft Security Advisory CVE 2022-41064

1.0.2.862
- Added Error handling to prevent crashes in the event some CSV files are not generated

1.0.2.859
- Modified GUI text
- Modified ReadME

1.0.2.847
- Security:
	- Removed deprecated/unused dependencies
- Updated Hotfix Detector (/Tools/Hotfixdetector.zip)

1.0.2.833
- Fixed broken Job Wait calculation. This could cause the program to hang or crash
- Fixed issue where custom path would cause program to hang
- Added error handling + logging

1.0.2.811
- Fixed performance issue with data collection
- Added Hyper-V to protected workloads counter

1.0.2.793
- Performance tweaks
- multiple reports of "hangs" should be addressed.
- Fixed "Waits" columns to populate correctly again.

1.0.2.748
- CLI enhancement
- variable date ranges (7/30/90)
- improved anonymizations
- various issue/bug fixes

1.0.2.676
- Fixed minor bug with column alignment

 1.0.2.675
- VB365 HealthCheck
	- Scripts added to create Health Check Report for VB365.
- Software detection
	- Software detects B&R or VB365 and creates report for detected software.
- Localization
	- Translating 'vhcres.txt' or 'vb365_vhcres.txt' and adding locale (i.e. vhcres.FR-FR.txt), program can be rebuilt and detect system locale to display GUI and Report in desired language.
- CLI
	- .\VeeamHealthCheck.exe help to see menu


1.0.1.1273
- Fixed Issues:
	- #3
	- #4
	- #5
- Fixed math where Job Sessions' success rate could be greater than 100%.

1.0.1.1272
- Fixed a bug where capacity tier NAME would show under TYPE. Issue also caused inconsistency with SOBR details.

1.0.1.1251
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


1.0.0.938
- fixed out-of-order columns in jobsession reports
- added GUI link to sensitive data kb
- added back-port to v10; partially tested
- fixed issue with registry key detection
- fixed issue with HTML report links

1.0.0.929
- fixed issue with missing data when scrub option is enabled

1.0.0.925
- fixed issue with config backup reporting

1.0.0.924
- PS windows are hidden to make the program look cleaner
- Removed some "TBD" data from report
- Removed SqlSecurePassword from detected registry keys
- adjusted GUI formatting and text

1.0.0.920
- Suppressed errors caused by certain job types that were not configured in an environment.

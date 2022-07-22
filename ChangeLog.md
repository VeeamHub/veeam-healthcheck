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
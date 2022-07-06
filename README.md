# Veeam Health Check

**[Download the latest release on the Releases page](https://github.com/VeeamHub/veeam-healthcheck/releases/) and select the VeeamHealthCheck zip file.**

This Windows utility is a lightweight executable that will generate an advanced configuration report covering details about the current Veeam Backup & Replication installation. Simply download, extract, and execute to generate the report. 

**[Sample Report](https://htmlpreview.github.io/?https://github.com/VeeamHub/veeam-healthcheck/blob/master/SAMPLE/Veeam_HealthCheck_Report_live-backup_2022.01.10_102711.html)**

## 📗 Documentation

**Author:** Adam Congdon (adam.congdon@veeam.com)

**System Requirements:**
- Must be run as elevated user
	- User must have Backup Administrator role in B&R
- Veeam Backup & Replication v11
- Must be executed on system where Veeam Backup & Replication is installed (no remote execution)
- C:\ must have at least 500MB free space: Output is sent to C:\temp\vHC
- Veeam Cloud Service Provider Servers are not supported.

**Operation:** 
1. [Download the tool.](https://github.com/VeeamHub/veeam-healthcheck/releases/)
2. Extract the archive on the Backup & Replication Server
3. Run VeeamHealthCheck.exe from an elevated CMD/PS prompt or right-click 'Run as Administrator'
4. Configure desired options on the single-page GUI
5. Accept Terms
6. RUN
7. Review the report

**Features**
- Single-page report with B&R Configuration information
- Custom calculations and tables:
	- Highlighting areas of potential improvement
	- Job sessions analysis:
		- Min/max/average calculations for: job duration, backup & data size, waiting for resources
		- success rate & change rate
		- Job & Task concurrency heat map
- Curated summary & Notes:
	- SA curated description of each table. Including guidance, best practice, and recommendations with relevant documentation links.

## ✍ Contributions

We welcome contributions from the community! We encourage you to create [issues](https://github.com/VeeamHub/veeam-healthcheck/issues/) for Bugs & Feature Requests and submit Pull Requests. For more detailed information, refer to our [Contributing Guide](CONTRIBUTING.md).

## 🤝🏾 License

* [MIT License](LICENSE)

## 🤔 Questions

If you have any questions or something is unclear, please don't hesitate to [create an issue](https://github.com/VeeamHub/veeam-healthcheck/issues/new/choose) and let us know!

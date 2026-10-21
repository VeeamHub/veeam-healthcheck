# Security Policy

Veeam Health Check (vHC) is a community-supported VeeamHub project. It is not an officially supported Veeam product, and this policy covers the code in this repository only.

## Reporting a vulnerability

Please report vulnerabilities privately, not in a public issue or discussion.

Use GitHub's **private vulnerability reporting**: go to the [Security tab](https://github.com/VeeamHub/veeam-healthcheck/security) and choose **Report a vulnerability**. Include the version (`VeeamHealthCheck.exe` file properties or the release tag), what you found, steps to reproduce, and the impact you see.

Maintainers are volunteers, so there is no response-time guarantee. We will acknowledge a report, work on a fix in `dev`, and credit you in the release notes if you want that. Please give us a reasonable window to ship a fix before you disclose publicly.

Problems with Veeam Backup & Replication or Veeam Backup for Microsoft 365 themselves belong with [Veeam Support](https://www.veeam.com/support.html), not here.

## Supported versions

Only the latest GA release on the [releases page](https://github.com/VeeamHub/veeam-healthcheck/releases/latest) receives fixes. Older versions, including Health Check v2, are not patched.

## For security reviewers: the short version

- **You can read everything it runs.** The source is public, and the PowerShell collection scripts ship as plain files under `Tools\Scripts` in the package.
- **You can tie the download to the source.** Each release zip has a SHA256 in the release notes and a signed build-provenance attestation from this repository's release workflow (see [Verifying a download](#verifying-a-download)). An SBOM is attached to each GA release.
- **It stays inside your network.** No telemetry, no update check, and no connection to any internet service. You can run it on a jump box with outbound internet blocked.
- **It reads; it does not change Veeam.** It installs nothing and makes no changes to jobs or configuration. Output stays on local disk, and `/scrub:true` anonymizes a report before you share it.
- **The gaps are written down.** See [Known gaps](#known-gaps). The main one is that the executable is not code-signed yet.

## What the tool reads and changes

vHC is a read-oriented assessment tool. It runs on the machine where you start it and collects:

- **Veeam configuration and job history**, through the Veeam PowerShell module (`Veeam.Backup.PowerShell` for VBR, the VB365 module for VB365) and, where needed, the VBR configuration database, remote registry reads, log parsing, and WMI.
- **Output files** written as CSV, HTML, and optionally PDF or PowerPoint under the output directory (default `C:\temp\vHC`).

The collection scripts ship in the package under `Tools\Scripts` and can be read before you run anything. None of them is hidden inside the executable as an embedded resource. vHC does not install software, does not write to the registry, does not create scheduled tasks, and does not change backup jobs or Veeam configuration. The hotfix-detection option (`/hotfix`) inspects files and logs; it is not a patch installer.

Outside the output directory, vHC writes only:

- `%TEMP%\VeeamHealthCheck-Extract`, where the single-file executable unpacks its runtime files.
- `%APPDATA%\VeeamHealthCheck\`, for app settings and for credentials you choose to save with `/savecreds`.

The report contains infrastructure details such as server names, IP addresses, job names, and repository paths. Treat it as sensitive. **Scrubbed mode** (`/scrub:true`) anonymizes names and addresses before you share a report; review the output before sending it outside your organization. The HTML report is self-contained and makes no network requests when opened.

## Network and credentials

- vHC sends no telemetry and contacts nothing except your own Veeam infrastructure: the Veeam PowerShell module connection, the VBR configuration database, and remote registry and WMI reads against your Veeam servers. It makes no connection to GitHub, Veeam, or any other internet service during a run.
- One exception stays on the local machine: the VB365 collector asks the Azure Instance Metadata Service (`169.254.169.254`, a link-local address that never leaves the host, 5-second timeout) whether the server is an Azure VM. If it is, the VM name, region, SKU, and size appear in the report.
- `/remote` and `/host=` change which server the Veeam PowerShell module connects to. The collection scripts still run on the local machine that launched vHC; they do not execute on the remote server.
- By default vHC uses the Windows identity of the user running it. When explicit credentials are needed:
  - `/savecreds` stores them in `%APPDATA%\VeeamHealthCheck\creds.json`, with the password encrypted by Windows DPAPI for the current user on the current machine. `/clearcreds` removes them.
  - `/credfile=<path>` reads credentials from a JSON file you provide. Passwords in that file are only Base64-encoded, **not encrypted**, so protect it like a plaintext password file: restrict its ACL, keep it out of shared locations and source control, and delete it after the run.
  - `/silent` keeps unattended runs from ever prompting.

## How releases are built

Release builds come from the public GitHub Actions workflows in `.github/workflows/`:

- The .NET solution is built, the unit tests run, and the app is published and zipped on GitHub-hosted runners.
- That same zip is then exercised by integration tests on the maintainers' self-hosted VBR lab runners. Those runners test the zip; they do not build or modify it.
- The zip is submitted to VirusTotal. A release is created only when the scan is clean.
- GA releases (from `master`) and manual releases wait for maintainer approval through a protected GitHub environment before they publish. Nightly beta builds from `dev` are marked as pre-releases.
- Each published zip gets a build-provenance attestation, signed through GitHub's Sigstore integration, that records the repository, workflow, and commit it came from.
- CodeQL, Semgrep, dependency scanning, and SBOM generation run in CI. Versions are computed from commit history (see `CONTRIBUTING.md`).

## Verifying a download

1. Download the zip (named like `VeeamHealthCheck-3.1.0.279.zip`) only from the [Releases page](https://github.com/VeeamHub/veeam-healthcheck/releases) of this repository.
2. Verify the build provenance with the [GitHub CLI](https://cli.github.com/). This is the strongest check, because it does not rely on the release page itself:

   ```powershell
   gh attestation verify .\VeeamHealthCheck-3.1.0.279.zip --repo VeeamHub/veeam-healthcheck
   ```

3. Compare the SHA256 with the value in that release's notes:

   ```powershell
   Get-FileHash .\VeeamHealthCheck-3.1.0.279.zip -Algorithm SHA256
   ```

4. Optionally upload the zip to [VirusTotal](https://www.virustotal.com/) and compare the result with the scan linked from the release.
5. For full independence from our pipeline, build from source at the release tag (see the README) and run the unit tests.

## Known gaps

These are limitations we know about and have not closed yet:

- **The executable is not code-signed.** Windows SmartScreen may warn, and a few antivirus engines occasionally flag the unsigned single-file .NET binary (see [Security Scanning & False Positives](docs/SECURITY_SCANNING.md)). Until signing lands, the provenance attestation and the SHA256 are the integrity checks.
- **Attestations start with the first release after this policy.** Releases published before then (v3.1.0.279 and earlier) have only the SHA256 checksum, and that checksum sits on the same release page as the download.
- **An unused REST helper skips TLS certificate checks.** `Tools\Scripts\HealthCheck\VBR\vHC-VbrConfig\Private\Invoke-VhciRestApi.ps1` targets the VBR REST API (`https://<VBR server>:9419`, which uses a self-signed certificate by default) and turns off certificate validation. It ships in the package, but no normal run calls it today. Before it is wired in, it should validate the server certificate or let you pin one.

If you find something that is not on this list, please report it as described above.

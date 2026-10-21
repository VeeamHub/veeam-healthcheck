# Security Policy

Veeam Health Check (vHC) is a community-supported VeeamHub project. It is not an officially supported Veeam product, and this policy covers the code in this repository only.

## Reporting a vulnerability

Please report vulnerabilities privately, not in a public issue or discussion.

Use GitHub's **private vulnerability reporting**: go to the [Security tab](https://github.com/VeeamHub/veeam-healthcheck/security) and choose **Report a vulnerability**. Include the version (`VeeamHealthCheck.exe` file properties or the release tag), what you found, steps to reproduce, and the impact you see.

Maintainers are volunteers, so there is no response-time guarantee. We will acknowledge a report, work on a fix in `dev`, and credit you in the release notes if you want that. Please give us a reasonable window to ship a fix before you disclose publicly.

Problems with Veeam Backup & Replication or Veeam Backup for Microsoft 365 themselves belong with [Veeam Support](https://www.veeam.com/support.html), not here.

## Supported versions

Only the latest GA release on the [releases page](https://github.com/VeeamHub/veeam-healthcheck/releases/latest) receives fixes. Older versions, including Health Check v2, are not patched.

## What the tool reads and changes

vHC is a read-oriented assessment tool. It runs on the machine where you start it and collects:

- **Veeam configuration and job history**, through the Veeam PowerShell module (`Veeam.Backup.PowerShell` for VBR, the VB365 module for VB365) and, where needed, the VBR configuration database, registry reads, log parsing, and WMI.
- **Output files** written as CSV, HTML, and optionally PDF or PowerPoint under the output directory (default `C:\temp\vHC`).

The bundled collection scripts are shipped in the package under `Tools\Scripts` and can be read before you run anything. vHC does not install software, and it does not change backup jobs or Veeam configuration as part of a normal run. The hotfix-detection option (`/hotfix`) inspects files and logs; it is not a patch installer.

The report contains infrastructure details such as server names, IP addresses, job names, and repository paths. Treat it as sensitive. **Scrubbed mode** (`/scrub:true`) anonymizes names and addresses before you share a report; review the output before sending it outside your organization.

## Network and credentials

- vHC does not send telemetry and does not contact any service other than your own Veeam infrastructure components. It makes no connection to GitHub, Veeam, or any other internet service during a run.
- `/remote` and `/host=` change which server the Veeam PowerShell module connects to. The collection scripts still run on the local machine that launched vHC; they do not execute on the remote server.
- By default vHC uses the Windows identity of the user running it. When explicit credentials are needed, `/savecreds` stores them with Windows DPAPI scoped to the current user, `/credfile=<path>` loads them into memory only, and `/clearcreds` removes stored ones. Use `/silent` for unattended runs so the tool never prompts.
- Do not put credential files in shared locations or commit them to source control.

## How releases are built

Release builds come from the public GitHub Actions workflows in `.github/workflows/`:

- The .NET solution is built and the unit tests run on GitHub-hosted runners, then the app is published as a single-file executable and zipped.
- Versions are computed from commit history (see `CONTRIBUTING.md`).
- The release zip is submitted to VirusTotal, and the production release job is gated on that result and on environment approval.
- CodeQL, Semgrep, dependency scanning, and SBOM generation run in CI.
- The GitHub release notes list the SHA256 checksum of the zip.

## Verifying a download

1. Download `VeeamHealthCheck.zip` only from the [Releases page](https://github.com/VeeamHub/veeam-healthcheck/releases) of this repository.
2. Compute the hash and compare it with the SHA256 in that release's notes:

   ```powershell
   Get-FileHash .\VeeamHealthCheck.zip -Algorithm SHA256
   ```

3. Optionally upload the zip to [VirusTotal](https://www.virustotal.com/) and compare the result with the scan linked from the release.
4. For the strongest assurance, build from source (see the README) at the release tag and run the unit tests.

## Known gaps

These are limitations we know about and have not closed yet:

- **The executable is not code-signed.** Windows SmartScreen may warn, and a few antivirus engines occasionally flag the unsigned single-file .NET binary (see [Security Scanning & False Positives](docs/SECURITY_SCANNING.md)). Hash verification is the main integrity check today.
- **No build provenance yet.** Releases do not carry a signed build attestation (for example SLSA provenance or GitHub artifact attestations), so you cannot cryptographically tie the zip to the workflow run that built it.
- **The checksum lives on the same page as the download.** Anyone who could replace the zip on the release page could also edit the notes, so the checksum protects against corrupted or mismatched downloads more than against a compromised release.
- **A developer test harness ships in the package.** `Tools\Scripts\HealthCheck\VBR\vHC-VbrConfig\Validation\Invoke-VhcvHarness.ps1` is a lab validation script designed to create and delete VBR objects (today it is a scaffold with no object factories). It refuses to run unless `VHC_ALLOW_LIVE_MUTATION=YES_I_HAVE_A_LAB` is set and is never called by vHC. It should not be in the package, and you can delete it.

If you find something that is not on this list, please report it as described above.

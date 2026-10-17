# Systemic HTML injection / stored XSS: data values written into table cells without HTML-encoding

**Category:** reporting-vbr-tables
**Severity:** High
**Type:** Security
**File(s):** `Functions/Reporting/Html/Shared/CHtmlFormatting.cs:154,166,172,198,204,217,251` (the shared sink) plus every renderer that passes collected/attacker-influenceable strings through it, including:
- `Functions/Reporting/Html/VBR/VbrTables/Registry/CRegKeysTable.cs:62-63` (raw registry key path + value)
- `Functions/Reporting/Html/VBR/VbrTables/Repositories/CRepoTable.cs:73,87,88,137` (Name, Host, Path, Type)
- `Functions/Reporting/Html/VBR/VbrTables/Managed Server Table/CManagedServerTable.cs:61,64,65,66,98` (Name, Type, OsInfo, ApiVersion, Platform)
- `Functions/Reporting/Html/VBR/VbrTables/Proxies/CProxyTable.cs:74,77-95,102` (server name, host, transport mode, etc.)
- `Functions/Reporting/Html/VBR/VbrTables/SOBR/CSobrExtentTable.cs:75,76,83,84,94` (Name, SobrName, Host, Path, Type)
- `Functions/Reporting/Html/VBR/VbrTables/Security/CMalwareTable.cs:60,101,109,172,304-320` (Sensitivity, DetectionEngine, NotificationOptions, exclusion Name/Platform, infected ObjectName/Severity/Types/Platform/HostName)
- `Functions/Reporting/Html/VBR/VbrTables/Security/CComplianceTable.cs:165,172,208` (BestPractice label, Status)
- The full Cloud Connect, Tape, Replication, SureBackup, Jobs Info, GeneralSettings renderer sets (35+ files use `ScrubItem`-sourced object names and pass them to `TableData`/`TableDataLeftAligned`/`TableHeader` unencoded).

## Summary
The central cell/header builders in `CHtmlFormatting` interpolate caller-supplied strings directly into HTML with no encoding:

```csharp
public string TableData(string data, string toolTip)
{
    string titleAttr = string.IsNullOrEmpty(toolTip) ? "" : $" title=\"{toolTip}\"";
    return $"<td{titleAttr}>{data}</td>";            // data unescaped
}
public string TableHeader(string header, string tooltip) { ... return $"<th...>{header}</th>"; }
```

Every VBR table renderer feeds collected data (job names, repository names, server names, registry keys/values, file paths, object names, compliance rule labels) straight into these helpers. None of those values are HTML-encoded. The only encoded sites in the entire tree are a handful of explicit `WebUtility.HtmlEncode` calls in `CHtmlTables.cs` (lines 281-282, 429, 458-460) and `CSecuritySummarySection.cs:105` — proving encoding exists but is applied inconsistently / by exception only.

## Evidence
`CHtmlFormatting.cs:201-205` (sink, no encode). `CRegKeysTable.cs:62`:
```csharp
s += form.TableData(d.Key, string.Empty);   // d.Key is a raw registry path string
s += form.TableData(d.Value.ToString(), string.Empty);
```
`CManagedServerTable.cs:61`: `s += form.TableData(d.Name, string.Empty);` where `d.Name` is a collected server name. The `title="{toolTip}"` attribute path (e.g. `TableData(data, toolTip, shading)` at `CHtmlFormatting.cs:217`) is even worse: an unencoded value placed inside a double-quoted attribute allows `"` breakout.

## Impact
A backup object whose name/description/path contains HTML (e.g. a VM, job, repository, or registry value named `<img src=x onerror=...>` or `"><script>...`) is rendered verbatim into the report. When the HTML report is opened in a browser, arbitrary script executes in the context of whoever views the report (often an admin/SE reviewing a customer environment). Because object names are partly attacker-controllable (anyone able to create a backup object in the monitored VBR), this is a stored-XSS vector that crosses a trust boundary into the report consumer. Even without an active attacker, names containing `<`, `>`, `&`, or `"` corrupt table layout.

## Suggested Fix
HTML-encode at the single choke point. In `CHtmlFormatting`, encode `data`, `header`, and `tooltip` before interpolation:
```csharp
public string TableData(string data, string toolTip)
{
    string enc = System.Net.WebUtility.HtmlEncode(data ?? string.Empty);
    string titleAttr = string.IsNullOrEmpty(toolTip) ? "" : $" title=\"{System.Net.WebUtility.HtmlEncode(toolTip)}\"";
    return $"<td{titleAttr}>{enc}</td>";
}
```
Apply to `TableData` (all overloads), `TableDataLeftAligned`, `TableDataHeat`, `TableHeader*`. For the few callers that legitimately pass HTML fragments (e.g. `ComplianceBadge` returns a `<span>`, `labelCell` includes a `<span class="badge">`), introduce an explicit "raw HTML" overload/marker so those opt in, rather than leaving the default unsafe. Audit the badge/`AddA` helpers to ensure the human-readable text portion is encoded while only the known-safe markup is raw.

## Labels
security, xss, html-injection, systemic, reporting

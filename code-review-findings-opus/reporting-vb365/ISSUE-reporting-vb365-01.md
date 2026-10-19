# Unescaped CSV values written into HTML across all VB365 tables (stored XSS / report corruption)

**Category:** reporting-vb365
**Severity:** High
**Type:** Security
**File(s):** `vHC/HC_Reporting/Functions/Reporting/Html/VB365/CM365Tables.cs:319-330, 437-450, 868-883, 999-1009, 1302-1311, 1670-1682` and every other `TableData(...)` call in the file

## Summary
Every VB365 table renderer passes raw, attacker-influenceable CSV field values (organization names, job names/descriptions, proxy/server names, repository names, file paths, VM names, certificate subject names, bucket/container names) straight into `CHtmlFormatting.TableData(...)`, which performs **no HTML encoding**. Any value containing `<`, `>`, `&`, or `"` is emitted verbatim into the report. The newer VBR-side renderers were hardened to encode (`CHtmlBuilder.HtmlEncode`, `CHtmlTables` and `CSecuritySummarySection` use `System.Net.WebUtility.HtmlEncode`), but the VB365 path was never updated — a classic copy-paste/divergence gap.

## Evidence
`TableData` does not encode (`Html/Shared/CHtmlFormatting.cs:201-205`):
```csharp
public string TableData(string data, string toolTip)
{
    string titleAttr = string.IsNullOrEmpty(toolTip) ? "" : $" title=\"{toolTip}\"";
    return $"<td{titleAttr}>{data}</td>";   // data emitted raw
}
```
VB365 callers pass raw CSV values, e.g. Organizations (`CM365Tables.cs:1355-1372`):
```csharp
foreach (var g in gl)
{
    string output = g.Value;
    if (CGlobals.Scrub) { ... output = this.scrubber.ScrubItem(output, ...); }
    s += this.form.TableData(output, string.Empty);   // org/real-name emitted unencoded
}
```
Repositories path/name/description (`CM365Tables.cs:437-450`), Proxies name/description/OS (`319-330`), Jobs org/name/desc (`1670-1682`), Object storage name/path/bucket (`1302-1311`), Security cert subject names (`668-703`) — all unencoded. The scrubber (`Common/Scrubber/CXmlHandler.cs:63-115`) returns plain replacement strings and never HTML-encodes either, so even scrubbed reports are not protected against markup that survives in *non*-scrubbed fields.

## Impact
A Microsoft 365 tenant admin (or anyone who can name an org, job, repository, mailbox, SharePoint site, or set a description/cert CN) can inject HTML/JavaScript that executes when the generated report is opened in a browser, or when it is rendered to PDF/PPTX downstream. Beyond active XSS, even benign `<`/`&` in names silently corrupts table layout. This is the single highest-value VB365 reporting defect.

## Suggested Fix
Encode all dynamic data at the emit boundary. Either (a) make `TableData`/`TableHeader`/`TableDataLeftAligned` call `System.Net.WebUtility.HtmlEncode(data)` internally (preferred — fixes both products at once; verify VBR callers don't pre-encode to avoid double-encoding), or (b) wrap every dynamic VB365 value in `WebUtility.HtmlEncode(...)` before passing to `TableData`. Apply the same to the scrubber output.

## Labels
security, xss, html-injection, vb365, reporting, output-encoding

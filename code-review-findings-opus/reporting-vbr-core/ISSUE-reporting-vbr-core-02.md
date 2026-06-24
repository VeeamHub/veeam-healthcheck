# License holder name interpolated into report header without encoding

**Category:** reporting-vbr-core
**Severity:** Medium
**Type:** Security
**File(s):** `vHC/HC_Reporting/Functions/Reporting/Html/Shared/CHtmlFormatting.cs:540-551`, `:474-490`; `vHC/HC_Reporting/Functions/Reporting/Html/VBR/CHtmlCompiler.cs:290-304,321-330`

## Summary
`SetLicHolder()` reads the `licensedto` field from the license CSV and returns it verbatim. That string
is then interpolated raw into the page header (`PageHeader`) and the VB365/security banner
(`SetHeaderAndLogo`). No HTML-encoding is applied, so a license-holder string containing HTML
metacharacters breaks the header or injects markup into the `<h1>`.

## Evidence
```csharp
// CHtmlCompiler.cs:290-304 — value comes straight from CSV, returned unescaped
private string SetLicHolder()
{
    CCsvParser csv = new();
    var lic = csv.GetDynamicLicenseCsv();
    foreach (var l in lic)
        return l.licensedto;     // raw
    return string.Empty;
}

// CHtmlFormatting.cs:543-550 — interpolated raw into <h1>
return string.Format(@"<div class=""page-header"">
    <h1>{0} - Infrastructure Health Report</h1>
    ...", licenseHolder, reportDays, reportDate);
```
In the full-report path the scrubbed branch passes a blank (`FormBodyStart`:
`string licHolder = scrub ? " " : this.SetLicHolder();`), but the **non-scrub** report and the
**security report** (`SetVbrSecurityHeader` -> `SetHeaderAndLogo(this.SetLicHolder())`, which is never
scrubbed) emit the raw value.

## Impact
A crafted or merely awkward `licensedto` value (containing `<`, `>`, `&`, `"`) corrupts the report
header or injects HTML/script into a document shared with customers. Lower severity than the table-cell
issue only because there is a single license-holder value rather than per-row attacker data.

## Suggested Fix
HTML-encode `licenseHolder` at the point of interpolation in `PageHeader` and `SetHeaderAndLogo`
(reuse `CHtmlBuilder.HtmlEncode` / `WebUtility.HtmlEncode`).

## Labels
security, xss, html-encoding, license

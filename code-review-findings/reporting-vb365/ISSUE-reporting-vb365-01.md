---
title: "HTML-encode collected VB365 data before interpolating into report HTML"
severity: High
labels: [security]
domain: reporting-vb365
files:
  - vHC/HC_Reporting/Functions/Reporting/Html/VB365/CM365Tables.cs:319
  - vHC/HC_Reporting/Functions/Reporting/Html/Shared/CHtmlFormatting.cs:201
confidence: High
---

## Summary

Every value collected from the customer's VB365 environment (job names, job/repo/proxy descriptions, org names, paths, bucket names, session log text, license holder) is concatenated raw into the report HTML. `CHtmlFormatting.TableData`/`TableHeader` perform no HTML encoding, so any `<script>`, `<img onerror=...>`, or markup in a customer-controlled field is rendered/executed when the report is opened in a browser.

## Impact

Stored XSS in the generated report. A malicious or compromised tenant can name a backup job or write a description like `<img src=x onerror=...>`; the SE/analyst who opens the resulting `.html` locally executes attacker-controlled script (file:// context). It also corrupts rendering for benign values containing `<`, `>`, or `&` (e.g., descriptions like "Repo <primary>") — the cell content silently disappears.

## Evidence

`vHC/HC_Reporting/Functions/Reporting/Html/Shared/CHtmlFormatting.cs:201-219` — no encoding:

```csharp
public string TableData(string data, string toolTip)
{
    string titleAttr = string.IsNullOrEmpty(toolTip) ? "" : $" title=\"{toolTip}\"";
    return $"<td{titleAttr}>{data}</td>";
}
```

`vHC/HC_Reporting/Functions/Reporting/Html/VB365/CM365Tables.cs:319-320` — raw collected values:

```csharp
s += this.form.TableData(proxyname, string.Empty);
s += this.form.TableData(description, string.Empty);
```

Same pattern for jobs (`CM365Tables.cs:1670-1682`), orgs (`1372`), repos (`437-450`), job sessions (`1062`), and the license holder injected into the page header (`CVb365HtmlCompiler.cs:114` via `SetLicHolder()`).

Note: the collection script deliberately embeds `<br />` in two fields (JobSessions `Log`, LocalRepositories `Daily Change Rate` — `Collect-VB365Data.ps1:1357`), so a fix must encode by default and whitelist/convert only those known fields.

## Suggested fix

Encode in the single choke point: `System.Net.WebUtility.HtmlEncode(data)` (and `toolTip`) inside `TableData`/`TableHeader` overloads, with an explicit `TableDataRaw` used only for the two fields that intentionally carry `<br />`. This fixes both the VBR and VB365 reports at once.

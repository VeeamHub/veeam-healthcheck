---
title: "HTML-encode all data flowing through CHtmlFormatting table/cell helpers"
severity: High
labels: [security, bug]
domain: reporting-vbr-core
files:
  - vHC/HC_Reporting/Functions/Reporting/Html/Shared/CHtmlFormatting.cs:200
  - vHC/HC_Reporting/Functions/Reporting/Html/Shared/CHtmlFormatting.cs:149
  - vHC/HC_Reporting/Functions/Reporting/Html/Shared/CHtmlFormatting.cs:50
confidence: High
---

## Summary

The legacy formatting helpers that emit the vast majority of the VBR report (`TableData`, `TableHeader`, `TableHeaderLeftAligned`, `TableDataLeftAligned`, `AddA`, `PageHeader`, `SetHeaderAndLogo`, etc.) interpolate caller data directly into HTML element bodies and `title="..."` attributes with **no HTML encoding**. The data is customer-controlled (job names, VM names, repository paths, license holder, registry values read from the environment). The codebase already has two encoding-aware builders (`CHtmlBuilder.HtmlEncode`, `CSectionTable.Encode`), but the legacy path used by most tables bypasses them.

## Impact

- A job/VM/repo name containing `"` or `<` breaks the markup (attribute escape from `title=\"{toolTip}\"`, broken table structure).
- A maliciously named object (e.g., a VM named `<script>...</script>` or `"><img src=x onerror=...>`) becomes stored XSS in a report that is explicitly designed to be shared with third parties (Veeam SEs, support). The PPTX/PDF exporters also re-parse this HTML with regexes, so malformed names can silently corrupt those exports too.
- Registry values are rendered with `string.Join("<br>", ...)` (CDataFormer.RegOptions), so any environment string is trusted as raw HTML.

## Evidence

`vHC/HC_Reporting/Functions/Reporting/Html/Shared/CHtmlFormatting.cs:200` —

```csharp
public string TableData(string data, string toolTip)
{
    string titleAttr = string.IsNullOrEmpty(toolTip) ? "" : $" title=\"{toolTip}\"";
    return $"<td{titleAttr}>{data}</td>";
}
```

`data` and `toolTip` are emitted raw. Same pattern at `TableHeader` (:160-176), `TableDataLeftAligned` (:194), `AddA` (:50): `return string.Format("<div class=\"{0}\">{1}</div>", classInfo, displaytext);`, and `PageHeader` (:541) which interpolates the license-holder name into an `<h1>`.

Contrast with `vHC/HC_Reporting/Functions/Reporting/Html/Shared/CSectionTable.cs:261-271`, which correctly encodes — proving the team already considers encoding necessary, but the legacy path (used by `CHtmlTables`, `CVbrSummaries`, compiler nav/sidebar) does not.

## Suggested fix

Add a single `HtmlEncode` (reuse `CHtmlBuilder.HtmlEncode`, make it `internal static`) and apply it to `data`, `toolTip`, `header`, and `displaytext` parameters inside `CHtmlFormatting` cell/header/AddA helpers. For the few callers that intentionally pass markup (e.g., `<br>`-joined values, emoji entities), add explicit `*Raw` overloads so raw emission is opt-in, mirroring `CHtmlBuilder.Raw`.

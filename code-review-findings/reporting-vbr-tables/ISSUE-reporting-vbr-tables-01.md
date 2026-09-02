---
title: "HTML-encode all collected data before interpolating into VBR report tables"
severity: High
labels: [security]
domain: reporting-vbr-tables
files:
  - vHC/HC_Reporting/Functions/Reporting/Html/Shared/CHtmlFormatting.cs:201
  - vHC/HC_Reporting/Functions/Reporting/Html/VBR/VbrTables/Repositories/CRepoTable.cs:73
  - vHC/HC_Reporting/Functions/Reporting/Html/VBR/VbrTables/Repositories/CObjectStorageReposTable.cs:67
  - vHC/HC_Reporting/Functions/Reporting/Html/VBR/VbrTables/Managed Server Table/CManagedServerTable.cs:61
  - vHC/HC_Reporting/Functions/Reporting/Html/VBR/VbrTables/Jobs Info/CJobInfoTable.cs:149
  - vHC/HC_Reporting/Functions/Reporting/Html/VBR/VbrTables/Job Session Summary/IndividualJobSessionsHelper.cs:294
confidence: High
---

## Summary

Virtually every VBR table renderer interpolates data collected from the customer environment (job names, server names, repository paths, descriptions, tenant names, malware object names, error strings) directly into HTML without encoding. The shared sink `CHtmlFormatting.TableData`/`TableDataLeftAligned`/`TableHeader` performs no escaping, so all ~70 table classes inherit the problem. Encoding exists in a handful of newer spots (`CHtmlTables.Badge`, the collector-error rows, `CSecuritySummarySection.SecurityGridItem`, `CHtmlBuilder`), which makes the rest a drift/inconsistency, not a design decision.

## Impact

A VBR job, VM, tenant, file-share, or repository whose name contains `<`, `>`, `&`, or `"` breaks the report layout, and a hostile name (e.g. a VM named `<img src=x onerror=...>` — names are attacker-influenceable in shared/tenant environments) executes script when the report is opened in a browser. The report ships embedded JavaScript (`sortTable`, collapsible sections), so script execution is live. Reports are routinely shared with/by Veeam SEs, making stored-XSS-in-artifact a realistic vector. The same raw values are also injected into `title="{toolTip}"` attributes where an embedded quote breaks out of the attribute.

## Evidence

The sink — `vHC/HC_Reporting/Functions/Reporting/Html/Shared/CHtmlFormatting.cs:201`:

```csharp
public string TableData(string data, string toolTip)
{
    string titleAttr = string.IsNullOrEmpty(toolTip) ? "" : $" title=\"{toolTip}\"";
    return $"<td{titleAttr}>{data}</td>";
}
```

Representative raw-data call sites (collected CSV values, no encoding):

- `Repositories/CRepoTable.cs:73,87,88` — `form.TableData(d.Name, ...)`, `d.Host`, `d.Path`
- `Repositories/CObjectStorageReposTable.cs:67-75` — `Get("Name")`, `Get("Bucket")`, `Get("Endpoint")` etc.
- `Managed Server Table/CManagedServerTable.cs:61-66` — `d.Name`, `d.OsInfo`
- `Jobs Info/CJobInfoTable.cs:149-150` — `jobName`, `repoName`
- `Job Session Summary/IndividualJobSessionsHelper.cs:294-297` — private `TableData` re-implementation, also unencoded, plus `"<h2>" + jobname + "</h2>"` at line 212
- `Security/CMalwareTable.cs:315-320` — infected object name/host
- All CloudConnect, Replication, TapeInfra, SureBackup, GeneralSettings tables follow the same pattern.

Contrast (already encoded, proving intent): `CHtmlTables.cs:281-282` (`WebUtility.HtmlEncode(entry.Name)`), `CHtmlTables.cs:429` (`Badge`), `CSecuritySummarySection.cs:105`.

## Suggested fix

Encode at the sink: in `CHtmlFormatting.TableData`/`TableDataLeftAligned`/`TableHeader(LeftAligned)` apply `WebUtility.HtmlEncode` to `data`/`tooltip` by default, and add explicit `TableDataRaw(...)` overloads for the few callers that intentionally pass markup (`form.True`/`form.False` glyphs, `Badge(...)` output, GFS `<br>` joins, progress bars). Sweep callers passing pre-built HTML to the raw overload. Also fix the private duplicate in `IndividualJobSessionsHelper`.

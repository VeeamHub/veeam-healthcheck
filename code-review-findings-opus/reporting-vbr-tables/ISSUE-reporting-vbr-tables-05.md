# NullReferenceException when data-former returns null (foreach over unchecked list drops table)

**Category:** reporting-vbr-tables
**Severity:** Medium
**Type:** Bug
**File(s):** `Functions/Reporting/Html/VBR/VbrTables/SOBR/CSobrExtentTable.cs:49-57`; `Functions/Reporting/Html/VBR/VbrTables/Repositories/CRepoTable.cs:56-58`

## Summary
These renderers fetch a list, log a warning when it is null/empty, but then iterate it without guarding against null. If the data-former returns `null` (as the JSON pass at the bottom of the same files defensively assumes — `... ?? new List<CRepository>()`), the `foreach` throws `NullReferenceException`, which the surrounding `catch` swallows, silently dropping the entire table.

## Evidence
`CSobrExtentTable.cs`:
```csharp
List<CRepository> list = this.df.ExtentXmlFromCsv(scrub);

if (list == null || list.Count == 0)
{
    this.log.Warning("No SOBR Extent data found...");   // logs but does NOT return/continue
    this.log.Info("This could indicate...");
}

foreach (var d in list)   // <-- NRE if list == null
{ ... }
```
The JSON pass 60 lines later acknowledges the null case: `var list = this.df.ExtentXmlFromCsv(scrub) ?? new List<CRepository>();` (line 109) — so the HTML pass is inconsistent with the author's own null assumption.

`CRepoTable.cs:56` has the same shape: `List<CRepository> list = df.RepoInfoToXml(scrub);` followed immediately by `foreach (var d in list)` with no null check, while line 153 guards with `?? new List<CRepository>()`.

## Impact
When the underlying CSV is absent and the former returns null (rather than an empty list), the whole SOBR Extents / Repositories table disappears with only a buried log line — no placeholder row, no error surface to the user. The empty-list warning at the top is also useless because control falls straight into the loop instead of emitting a "no data" row.

## Suggested Fix
Guard and emit a placeholder before iterating:
```csharp
var list = this.df.ExtentXmlFromCsv(scrub);
if (list == null || list.Count == 0)
{
    this.log.Warning("No SOBR Extent data found...");
    s += "<tr><td colspan=\"16\" style=\"text-align:center;color:#666;\"><em>No SOBR extent data available.</em></td></tr>";
}
else
{
    foreach (var d in list) { ... }
}
```

## Labels
bug, null-deref, robustness, silent-failure

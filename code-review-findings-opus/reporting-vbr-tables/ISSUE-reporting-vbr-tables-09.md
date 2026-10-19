# Proxy JSON export drops a column and risks header/value misalignment vs the HTML table

**Category:** reporting-vbr-tables
**Severity:** Medium
**Type:** Bug
**File(s):** `Functions/Reporting/Html/VBR/VbrTables/Proxies/CProxyTable.cs:35-114` (HTML, 12 columns) vs `:133-139` (JSON, 11 columns)

## Summary
The HTML proxy table renders 12 columns from array indices `d[0]`..`d[11]`. The JSON section export uses only 11 header names and selects `d[0..4], d[6..11]` — it skips index `d[5]` entirely. The JSON headers and the selected values therefore describe a different, narrower table than the HTML, and at least one source column is silently dropped from the structured export.

## Evidence
HTML emits all 12 indices (lines 70-112): `d[0]`(name), `d[1]`,`d[2]`,`d[3]`,`d[4]`,`d[5]`, then `d[6]` as a bool, then `d[7]`,`d[8]`,`d[9]`,`d[10]`(host),`d[11]` as a bool — 12 cells, matching the 12 `TableHeader` calls (Prx0..Prx11, lines 35-46).

JSON (lines 133-139):
```csharp
List<string> headers = new() { "Name", "Type", "Tasks", "Cores", "Ram", "IsOnHost", "TransportMode", "NetBufferSize", "MaxConcurrentJobs", "Host", "IsHvOffload" }; // 11 names
List<List<string>> rows = list.Select(d => new List<string>
{
    d[0], d[1], d[2], d[3], d[4], d[6], d[7], d[8], d[9], d[10], d[11],   // 11 values, d[5] omitted
}).ToList();
```
`d[5]` (a column the HTML table shows) has no JSON header and no JSON value. The remaining JSON header-to-value mapping is also suspect: HTML treats `d[6]` as a boolean "IsOnHost"-style flag, but the JSON labels position 6 (`d[6]`) as "IsOnHost" while position 5 in the value list is `d[6]` — i.e. the human-readable header names may not line up with the indices actually emitted once `d[5]` is dropped.

## Impact
Any downstream consumer of the JSON report (the VHC Intelligence Portal / dashboards) sees one fewer proxy attribute than the HTML, and column headers may not correspond to the values beneath them. HTML and JSON disagree about the proxy inventory.

## Suggested Fix
Make the JSON projection mirror the HTML exactly — 12 headers and 12 values including `d[5]` — or, better, drive both HTML and JSON from a single typed projection so they cannot drift. Add a unit test asserting `headers.Count == rows[0].Count` for every `SetSection` call.

## Labels
bug, json-export, column-mismatch, html-json-divergence

# CSV re-parsed twice per render (HTML pass + JSON pass) across all extracted table renderers

**Category:** reporting-vbr-tables
**Severity:** Low
**Type:** Performance
**File(s):** `Functions/Reporting/Html/VBR/VbrTables/Repositories/CRepoTable.cs:56,153`; `Proxies/CProxyTable.cs:53,132`; `Managed Server Table/CManagedServerTable.cs:55,113`; `Registry/CRegKeysTable.cs:36,79`; `SOBR/CSobrExtentTable.cs:49,109` — and the same dual-call shape in the other `SetSection`-style renderers.

## Summary
Each renderer calls its data-former twice: once to build the HTML rows, then again immediately afterward to build the JSON section. The data-former re-reads and re-parses the underlying CSV on the second call, doubling I/O and parse cost for every table.

## Evidence
`CRepoTable.cs`:
```csharp
List<CRepository> list = df.RepoInfoToXml(scrub);   // line 56 — HTML pass
...
var list = df.RepoInfoToXml(scrub) ?? new List<CRepository>();   // line 153 — JSON pass, parses CSV again
```
Same in `CManagedServerTable.cs:55` then `:113` (`df.ServerXmlFromCsv(scrub)` twice), `CProxyTable.cs:53` then `:132` (`df.ProxyXmlFromCsv(scrub)` twice), `CRegKeysTable.cs:36` then `:79` (`df.RegOptions()` twice), `CSobrExtentTable.cs:49` then `:109` (`df.ExtentXmlFromCsv(scrub)` twice).

## Impact
On large environments (thousands of jobs/objects), every VBR section pays the CSV read+parse cost twice. Not correctness-affecting, but a straightforward 2x reduction in collection-to-render time is available. It also opens a consistency risk: if the underlying CSV changed between calls, HTML and JSON could diverge.

## Suggested Fix
Parse once, reuse for both passes:
```csharp
var list = df.RepoInfoToXml(scrub) ?? new List<CRepository>();
// build HTML rows from `list`
// build JSON section from the same `list`
```

## Labels
performance, csv-parsing, refactor, redundant-work

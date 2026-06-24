# Static proxy CSV parsers ignore custom import directory — read from default vbrDir, mixing datasets

**Category:** csv-datatypes
**Severity:** Medium
**Type:** Bug
**File(s):** `vHC/HC_Reporting/Functions/Reporting/CsvHandlers/CCsvParser.cs:549-567, 1140-1166`, consumed at `Functions/Reporting/Html/CDataFormer.cs:71-74`

## Summary
The proxy parsers `GetDynViProxy/GetDynHvProxy/GetDynNasProxy/GetDynCdpProxy` are `static` and route through `VbrGetDynamicCsvRecsStatic` → `VbrFileReaderStatic` → `vbrReaderStatic.VbrCsvReader(file)`, which always reads from `CVariables.vbrDir` (the global default collection dir). Every *instance* parser instead reads from `this.outPath`, which the `CCsvParser(string csvRepo)` constructor can point at a custom/imported CSV directory. So when a report is produced from a non-default CSV directory, all proxy data is silently pulled from the default `vbrDir` rather than the selected dataset.

## Evidence
Instance path honors `outPath`:
```csharp
// CCsvParser.cs:1140-1152
private CsvReader VbrFileReader(string file)
{
    var fileResult = this.vbrReader.FileFinder(file, this.outPath);  // custom dir
    ...
}
```
Static path is hard-wired to vbrDir:
```csharp
// CCsvParser.cs:1154-1166
private static CsvReader VbrFileReaderStatic(string file)
{
    var fileResult = vbrReaderStatic.VbrCsvReader(file);  // -> FileFinder(file, CVariables.vbrDir)
    ...
}
// CCsvReader.cs:29-32
public CsvReader VbrCsvReader(string file) => this.FileFinder(file, CVariables.vbrDir);
```
Consumed statically (no instance/outPath context):
```csharp
// Html/CDataFormer.cs:71-74
viProxy = (CCsvParser.GetDynViProxy() ?? Enumerable.Empty<dynamic>()).ToList();
hvProxy = (CCsvParser.GetDynHvProxy() ?? ...).ToList();
```

## Impact
If `CVariables.vbrDir` and the active report's CSV directory ever differ (custom output dir, multi-dataset import, re-run against a different collection), the proxy section of the report reflects the wrong server's data while every other section reflects the selected dataset — a silent data-correctness/consistency bug that's hard to diagnose. It also makes the parsers non-reentrant for multi-dataset processing.

## Suggested Fix
Make the proxy parsers instance methods that use `this.outPath` (consistent with all other parsers), or pass the resolved directory explicitly. Remove the `static` shortcut and the parallel `vbrReaderStatic` so there is a single code path keyed off `outPath`.

## Labels
static-state, wrong-directory, data-consistency, reentrancy

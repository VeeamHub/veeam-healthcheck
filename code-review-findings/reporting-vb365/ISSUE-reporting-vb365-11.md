---
title: "Dispose CsvReader/StreamReader — VB365 report leaks ~15 open file handles per compile"
severity: Medium
labels: [reliability, performance]
domain: reporting-vb365
files:
  - vHC/HC_Reporting/Functions/Reporting/CsvHandlers/CCsvReader.cs:65
  - vHC/HC_Reporting/Functions/Reporting/Html/VB365/CM365Tables.cs:46
confidence: High
---

## Summary

`CCsvReader.CReader()` opens a `StreamReader` and wraps it in a `CsvReader`; neither is ever disposed. Every VB365 table builder (`GetDynamicVboGlobal`, `GetDynamicVboProxies`, `GetDynVboController`, etc. — ~15 calls per report, plus extra calls from `CVb365HtmlCompiler.GetServerName`/`SetLicHolder`) opens a fresh handle on a CSV under `C:\temp\vHC\...` that stays open until GC finalization.

## Impact

On Windows the open handles can block subsequent operations on those files within the same run — the tool later zips/moves the output directory and scrub tooling rewrites CSVs; an undisposed `StreamReader` holding `Original\VB365\*.csv` open can cause intermittent `IOException`/sharing violations. Also leaks memory/handles in long GUI sessions where reports are generated repeatedly.

## Evidence

`vHC/HC_Reporting/Functions/Reporting/CsvHandlers/CCsvReader.cs:65-70`:

```csharp
private CsvReader CReader(string csvToRead)
{
    TextReader reader = new StreamReader(csvToRead);
    var csvReader = new CsvReader(reader, this.csvConfig);
    return csvReader;
}
```

Consumers never dispose, e.g. `vHC/HC_Reporting/Functions/Reporting/CsvHandlers/CCsvParser.cs:199-210` returns lazy `res.GetRecords<CGlobalCsv>()` with no ownership of `res`, and `CM365Tables.cs:46` / `:163` / `:744` etc. just enumerate.

## Suggested fix

Make the parser methods materialize and dispose: `using var reader = ...; return reader.GetRecords<T>().ToList();` (the call sites already call `.ToList()` in most places), or have `CCsvParser` implement `IDisposable` and track open readers. Note `CVb365HtmlCompiler.Dispose()` (line 30) is already declared but empty — wire it up.

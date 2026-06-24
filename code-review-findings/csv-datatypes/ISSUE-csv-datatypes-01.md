---
title: "Use InvariantCulture consistently for numeric CSV parsing (ParseToInt/ParseToDouble use machine locale)"
severity: High
labels: [bug, reliability]
domain: csv-datatypes
files:
  - vHC/HC_Reporting/Functions/Reporting/DataTypes/CDataTypesParser.cs:1124
  - vHC/HC_Reporting/Functions/Reporting/DataTypes/CDataTypesParser.cs:1134
confidence: High
---

## Summary

Numeric parsing of CSV values is culture-unstable. The CsvHelper configuration uses `CultureInfo.InvariantCulture` (`CCsvReader.cs:74`), but all manual numeric conversion in `CDataTypesParser` goes through `ParseToInt`/`ParseToDouble`, which call `int.TryParse`/`double.TryParse` with **no culture argument** — i.e., the report machine's current locale. The collection side (PowerShell `Export-Csv` via `Export-VhciCsv.ps1`) writes numbers using the *collection* machine's locale via `ToString()`. The result is that the same CSV parses to different values depending on which machine renders the report.

## Impact

On any locale where `.` is a group separator and `,` is the decimal separator (de-DE, fr-FR, ru-RU, most of EU/LATAM), `double.TryParse("1.5")` yields **15**, not 1.5 — silently. Conversely, a comma-decimal value written by a German collection server fails `TryParse` on a US report machine and `ParseToDouble` returns **0** (its catch/TryParse-default path). Affected values flow directly into the report:

- `jInfo.BackupSize = this.ParseToDouble(s.BackupSize)` (CDataTypesParser.cs:641)
- `jInfo.DataSize = this.ParseToDouble(s.DataSize)` (CDataTypesParser.cs:661)
- `double i = this.ParseToDouble(s.Ram)` (CDataTypesParser.cs:923)
- `eInfo.FreeSPace / eInfo.TotalSpace = this.ParseToInt(...)` (CDataTypesParser.cs:325-326, 438-439)
- `RestorePoints`, `MaxTasks`, `Cores`, `CPU`, `dataRateLimit`, `maxArchiveTasks`, `ConcurrentTaskNumber`, `MaxTasksCount`, `OperationalRestorePeriod`, `OverrideSpaceThreshold` (CDataTypesParser.cs:205, 208, 300, 410-413, 526, 903-904, 1004, 1071, 1098)

This is the classic "wrong sizes in the health check report on European systems" failure mode (the repo already worked around the DateTime variant of this for issue #41 in `TryParseDateTime`, CDataTypesParser.cs:688-736 — the numeric paths never got the same treatment).

## Evidence

`vHC/HC_Reporting/Functions/Reporting/DataTypes/CDataTypesParser.cs:1120-1138`:

```csharp
private int ParseToInt(string input)
{
    try
    {
        int.TryParse(input, out int i);   // current culture
        return i;
    }
    catch (Exception) { return 0; };
}

private double ParseToDouble(string input)
{
    try
    {
        double.TryParse(input, out double i);   // current culture
        return i;
    }
    catch (Exception) { return 0; };
}
```

Meanwhile `CCsvReader.cs:74` configures CsvHelper with `new CsvConfiguration(CultureInfo.InvariantCulture)`, so typed properties (e.g. `CJobCsvInfos.OriginalSize`, a `double`) are converted invariantly while string-then-manual conversions are converted locale-sensitively — two different numeric interpretations of the same file in the same run.

Also note: the `try/catch` around `TryParse` is dead code — `TryParse` does not throw; on failure it silently yields `0`, so unparseable values are indistinguishable from genuine zeros.

## Suggested fix

- Change both helpers to `int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out ...)` and `double.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out ...)`, with a current-culture fallback attempt (mirroring `TryParseDateTime`'s strategy) for CSVs collected on non-invariant locales.
- Remove the dead try/catch; optionally log when parsing fails instead of returning a silent 0.
- Longer-term: make the collection scripts emit invariant-formatted numbers (e.g., cast to `[string]` with `ToString(CultureInfo.InvariantCulture)` or use `Export-Csv -UseCulture:$false` semantics) so the round-trip is locale-independent end to end.

---
title: "MissingFieldFound/HeaderValidated disabled globally — missing columns become silent nulls for typed records and runtime binder crashes for dynamic records"
severity: Medium
labels: [reliability]
domain: csv-datatypes
files:
  - vHC/HC_Reporting/Functions/Reporting/CsvHandlers/CCsvReader.cs:85
  - vHC/HC_Reporting/Functions/Reporting/CsvHandlers/CCsvParser.cs:165
confidence: High
---

## Summary

The single shared `CsvConfiguration` sets `MissingFieldFound = null` and `HeaderValidated = null` (`CCsvReader.cs:85-86`). This suppresses *all* schema validation for *every* CSV the tool reads:

- **Typed records** (`GetRecords<CServerCsvInfos>()` etc.): a missing/renamed column yields `null` (strings) or `default` (value types) on the property with no warning. Downstream code then renders empty cells or computes from zeros — e.g. `ParseToInt(null)` → 0 cores/RAM → "NA" provisioning — with nothing in the log.
- **Dynamic records** (`GetRecords<dynamic>()`, returned by ~50 `GetDynamic*` methods in `CCsvParser.cs:159-567`): the ExpandoObject only has members for headers that exist in the file. Any consumer accessing `record.SomeColumn` for a column absent in an older collected CSV throws `RuntimeBinderException` at render time — far from the parse site, and version skew between collector and report binary is a supported scenario (import mode).

## Impact

Schema drift between the PowerShell collectors and the report code is invisible until either a wrong/empty report value (typed path) or a runtime crash in a table renderer (dynamic path). There is no single log line saying "column X expected but not found in file Y", which makes field-reported issues (the repo has several locale/CSV-format GitHub issues) hard to diagnose.

## Evidence

`vHC/HC_Reporting/Functions/Reporting/CsvHandlers/CCsvReader.cs:72-88`:

```csharp
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    PrepareHeaderForMatch = args => args.Header.ToLower() ...,
    MissingFieldFound = null,     // silent nulls
    HeaderValidated = null,       // no header check
};
```

`vHC/HC_Reporting/Functions/Reporting/CsvHandlers/CCsvParser.cs:159-170` — the dynamic producer whose records are consumed member-wise across the HTML compilers:

```csharp
private IEnumerable<dynamic> VbrGetDynamicCsvRecs(string file, string vbrOrVboPath)
{
    var res = this.VbrFileReader(file);
    if (res != null)
    {
        return res.GetRecords<dynamic>();
    }
    ...
```

## Suggested fix

Keep parsing tolerant but not silent:

```csharp
MissingFieldFound = args => CGlobals.Logger.Warning(
    $"CSV missing field index {args.Index} ({string.Join("/", args.HeaderNames ?? Array.Empty<string>())}) in {args.Context.Reader?.Path}"),
```

(throttle/dedupe per file to avoid log spam). For the dynamic path, prefer migrating consumers to typed records with `[Optional]` members, or provide a helper that does `IDictionary<string, object>` lookup with a default instead of raw dynamic member access.

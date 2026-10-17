# CSV writer in CLogParser produces malformed/unquoted CSV (injection of commas, culture-dependent dates)

**Category:** collection-data
**Severity:** Medium
**Type:** Bug
**File(s):** `vHC/HC_Reporting/Functions/Collection/LogParser/CLogParser.cs:83-99`, `vHC/HC_Reporting/Functions/Collection/LogParser/CLogParser.cs:67-81`

## Summary
`DumpWaitsToFile` writes a CSV row by naive string concatenation with `","` separators and no field quoting/escaping. `JobName` is derived from a directory name (`Path.GetFileName(d)` at `CLogParser.cs:139`) and Veeam job names routinely contain commas. Additionally, `DateTime` and `TimeSpan` are written via default `ToString()`, which is culture-dependent and itself can emit a comma (e.g. some locales) — corrupting column alignment for every downstream consumer of `waits.csv`.

## Evidence
```csharp
// CLogParser.cs:88-91
using (StreamWriter sw = new StreamWriter(this.pathToCsv, append: true))
{
    sw.WriteLine(JobName + "," + start + "," + end + "," + diff);
}
```
Header is written with the same assumption (`CLogParser.cs:74`):
```csharp
sw.WriteLine("JobName,StartTime,EndTime,Duration");
```
`JobName` source (`CLogParser.cs:139`): `string jobname = Path.GetFileName(d);` — a backup job folder name, attacker/user-influenced and free-form.

## Impact
Any job name containing a comma shifts all subsequent columns, so `waits.csv` is parsed incorrectly (wrong durations attributed to wrong jobs) or rejected by the strict CsvHelper reader used elsewhere (`CCsvValidator.LoadManifest`). `start`/`end`/`diff` rendered with the current thread culture means the same file is non-deterministic across machines and can introduce extra commas. This is a real data-integrity defect at the export boundary, not a style nit.

## Suggested Fix
Quote/escape fields (or use CsvHelper, already a dependency) and format dates with `CultureInfo.InvariantCulture`:
```csharp
static string Q(string s) => "\"" + s.Replace("\"", "\"\"") + "\"";
sw.WriteLine(string.Join(",",
    Q(JobName),
    Q(start.ToString("o", CultureInfo.InvariantCulture)),
    Q(end.ToString("o", CultureInfo.InvariantCulture)),
    Q(diff.ToString("c", CultureInfo.InvariantCulture))));
```

## Labels
bug, csv-quoting, culture, data-integrity, collection

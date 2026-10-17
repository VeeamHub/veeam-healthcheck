# CCsvValidator.ValidateSingleFile substring glob over-matches unrelated CSV files

**Category:** collection-data
**Severity:** Low
**Type:** Bug
**File(s):** `vHC/HC_Reporting/Functions/Collection/CCsvValidator.cs:133-161`

## Summary
File presence/record-count validation uses an unanchored substring glob `*{fileName}*.csv` recursively. Because the expected names are short tokens (e.g. `"SOBRs"`, `"Proxies"`, `"capTier"`, `"_Jobs"`), this matches any CSV whose name merely contains the token, including server-prefixed, suffixed, or unrelated files, and then picks "the shortest filename" as the canonical match. Record counts can be attributed to the wrong file.

## Evidence
```csharp
// CCsvValidator.cs:138
var matches = Directory.GetFiles(_csvDirectory, $"*{fileName}*.csv", SearchOption.AllDirectories);
...
// CCsvValidator.cs:154-156
string filePath = matches
    .OrderBy(p => Path.GetFileName(p).Length)
    .First();
int lineCount = File.ReadLines(filePath).Count();
```
For example validating `"Proxies"` also matches `NasProxy*.csv`, `HvProxy*.csv`, `CdpProxy*.csv`; validating `"NasProxy"` and `"Proxies"` can resolve to overlapping files. `"SOBRs"` is a substring of nothing else but `"capTier"`/`"archTier"` etc. are similarly loose.

## Impact
Validation can report a file "present" using a different file's contents, and the per-file record count (surfaced in the validation summary and report) can be wrong. Counting lines with `File.ReadLines(...).Count()` also miscounts records for any CSV containing embedded newlines inside quoted fields (it counts physical lines, not CSV records). Both reduce the trustworthiness of the "files loaded / records" summary.

## Suggested Fix
Anchor the match to the filename boundary: accept exactly `{fileName}.csv` or `*_{fileName}.csv` (mirroring the precise matching already used in `CImportPathResolver.HasCriticalFiles`). For record counts on files that may contain quoted newlines, count via the CsvHelper reader rather than physical lines.

## Labels
bug, glob-over-match, validation, csv, collection

# StreamReader opened without explicit UTF-8/BOM handling — encoding-dependent header/value corruption

**Category:** csv-datatypes
**Severity:** Medium
**Type:** Bug
**File(s):** `vHC/HC_Reporting/Functions/Reporting/CsvHandlers/CCsvReader.cs:67`

## Summary
CSVs are opened with `new StreamReader(csvToRead)` and no `Encoding` argument. `StreamReader`'s default detects a BOM and otherwise falls back to UTF-8, which is usually fine — but the collection scripts are PowerShell-generated and PowerShell 5.1's `Export-Csv`/`Out-File` historically emit UTF-16LE or ANSI depending on host/version, while PS7 emits UTF-8 (often *without* BOM). When a file is written in a codepage the reader doesn't auto-detect, non-ASCII server names, paths, and the Chinese AM/PM markers that `TryParseDateTime` (CDataTypesParser.cs:696) explicitly tries to clean up get mojibake'd. The existing `"??"`-stripping workaround in `TryParseDateTime` is direct evidence that encoding corruption is already happening upstream.

## Evidence
```csharp
// CCsvReader.cs:65-70
private CsvReader CReader(string csvToRead)
{
    TextReader reader = new StreamReader(csvToRead);   // no Encoding, no detectEncodingFromByteOrderMarks flag
    var csvReader = new CsvReader(reader, this.csvConfig);
    return csvReader;
}
```
Downstream symptom already coded around:
```csharp
// CDataTypesParser.cs:696-697
// Remove corrupted AM/PM indicators (Chinese systems may export "??" instead of ...)
string cleanedDateTime = dateTime.Replace("??", "").Replace("  ", " ").Trim();
```

## Impact
On localized/non-UTF-8 collection outputs, header matching (the `PrepareHeaderForMatch` lower/strip in CCsvReader.cs:76) and string fields (server names, paths, dates) can be corrupted, leading to unmatched columns (silently null via `MissingFieldFound = null`) or garbled report text.

## Suggested Fix
Open with explicit UTF-8 and BOM detection, and align with whatever encoding the collection scripts actually write:
```csharp
var reader = new StreamReader(csvToRead, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: true);
```
If collection can emit non-UTF-8, standardize the PowerShell exporters on `-Encoding UTF8` (PS7 BOM-less) and read with matching encoding.

## Labels
encoding, utf-8, bom, csv, localization

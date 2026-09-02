# Culture-sensitive numeric parsing of CSV values (decimal comma) produces wrong sizes/counts on non-US machines

**Category:** csv-datatypes
**Severity:** High
**Type:** Bug
**File(s):** `vHC/HC_Reporting/Functions/Reporting/DataTypes/CDataTypesParser.cs:1120-1138` (`ParseToInt`, `ParseToDouble`), used at `:641, :661, :923, :903-904, :325-326, :438-439`, etc.

## Summary
The CSV files are read by CsvHelper configured with `CultureInfo.InvariantCulture` (CCsvReader.cs:74) — so the *strings* extracted are invariant-formatted. But the hand-written `ParseToInt`/`ParseToDouble`/`ParseBool` helpers in `CDataTypesParser` call `int.TryParse`/`double.TryParse`/`bool.Parse` with **no culture argument**, so they use the *current thread culture*. On a machine with a decimal-comma locale (de-DE, fr-FR, ru-RU, pt-BR, etc.) a value like `1234.56` fails to parse as a double, and `double.TryParse` returns `false` → the helper silently returns `0`. Worse, in some locales `1.234` (thousands separator) parses to `1234`. This mismatches the invariant-formatted input and corrupts numeric report fields.

## Evidence
```csharp
// CDataTypesParser.cs:1130-1138
private double ParseToDouble(string input)
{
    try
    {
        double.TryParse(input, out double i);  // no IFormatProvider -> CurrentCulture
        return i;                              // returns 0 on locale mismatch
    }
    catch (Exception) { return 0; };
}
```
Consumed for size/space fields:
```csharp
jInfo.BackupSize = this.ParseToDouble(s.BackupSize);   // :641
jInfo.DataSize   = this.ParseToDouble(s.DataSize);     // :661
double i = this.ParseToDouble(s.Ram); // "410665353216" :923  -> then /1024^3
eInfo.FreeSPace  = this.ParseToInt(s.FreeSpace);       // :325/:438
eInfo.TotalSpace = this.ParseToInt(s.TotalSpace);      // :326/:439
```
`ParseBool` (`:244-263`) and the inline `bool.TryParse` calls are culture-insensitive for bool so they're fine, but the numeric ones are not.

## Impact
On any non-US-format Windows install (a large share of Veeam's customer base in EU/LATAM/Asia), repository free/total space, backup/data sizes, and RAM-derived task provisioning can silently read as `0` or as a value off by orders of magnitude. The report then shows wrong capacity numbers and wrong over/under-provisioning verdicts — a correctness bug that masquerades as real data. CA1305 is suppressed, but per the review scope this is a genuine correctness defect, not just the analyzer.

## Suggested Fix
Parse with `CultureInfo.InvariantCulture` to match how the data was written/read:
```csharp
double.TryParse(input, NumberStyles.Any, CultureInfo.InvariantCulture, out double i);
int.TryParse(input, NumberStyles.Any, CultureInfo.InvariantCulture, out int i);
```
Apply consistently to `ParseToInt`, `ParseToDouble`, and the `Convert.ToInt32(i)` at :925.

## Labels
culture, correctness, number-parsing, invariant-culture

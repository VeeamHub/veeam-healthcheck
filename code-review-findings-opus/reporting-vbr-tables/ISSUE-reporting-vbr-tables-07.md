# Culture-sensitive numeric ToString in cells produces wrong/ambiguous values in non-US locales

**Category:** reporting-vbr-tables
**Severity:** Low
**Type:** Bug
**File(s):** Pervasive across the renderer set. Representative: `Functions/Reporting/Html/VBR/VbrTables/SOBR/CSobrExtentTable.cs:87` (`d.FreeSpacePercent.ToString()`); `Repositories/CRepoTable.cs:89-90,167`; `Jobs Info/CJobSessionSummaryTable.cs:354-355` (ratio strings); `ProtectedWorkloads/NasSourceInfo.cs:42-43,90` (`Convert.ToDouble(rec.*)` with no culture).

## Summary
Numeric values are formatted for display with bare `.ToString()` / interpolation and parsed with culture-default `Convert.ToDouble` / `double.Parse`. On a host whose current culture uses comma as the decimal separator (e.g. de-DE, fr-FR), decimals render as `1,5` and—worse—`Convert.ToDouble` on a CSV value written with `.` (invariant) can either throw or misread the magnitude (e.g. `"1.234"` parsed as 1234). Since collection writes CSVs in one culture and the report may render on another machine, values can silently change by orders of magnitude.

## Evidence
Formatting: `CSobrExtentTable.cs:87` `this.form.TableData(d.FreeSpacePercent.ToString(), string.Empty, freeSpaceShade);` — decimal `FreeSpacePercent` rendered with locale separators.
Parsing without culture: `NasSourceInfo.cs:90` `double sizeD = Convert.ToDouble(size);` and `:42-43` `Convert.ToDouble(rec.TotalFilesCount)`. A `"1.5"` from an invariant-culture CSV becomes 15 under de-DE.
Note the codebase already knows the safe pattern elsewhere (CA1305 is suppressed project-wide, but several spots do use `InvariantCulture`), so this is inconsistency rather than universal absence.

## Impact
Reports generated on non-US-culture hosts can show wrong free-space percentages, sizes, and dedup/compression ratios, and NAS size parsing can be off by 10x/1000x. This is a data-correctness issue in a tool whose entire purpose is accurate configuration reporting. (CA1305 is suppressed, but the consequence here is a real wrong-value bug, not just an analyzer nag.)

## Suggested Fix
Use `CultureInfo.InvariantCulture` for both CSV parsing and report formatting:
```csharp
double sizeD = Convert.ToDouble(size, CultureInfo.InvariantCulture);
... d.FreeSpacePercent.ToString(CultureInfo.InvariantCulture) ...
```
Best done centrally: a shared numeric-format helper used by all renderers, and invariant-culture parsing in the data-formers/CSV layer so the HTML side receives already-typed values.

## Labels
bug, culture, formatting, parsing, correctness

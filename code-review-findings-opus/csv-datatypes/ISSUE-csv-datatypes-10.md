# TryParseDateTime second attempt uses CurrentCulture — DD/MM vs MM/DD ambiguity silently flips dates

**Category:** csv-datatypes
**Severity:** Low
**Type:** Bug
**File(s):** `vHC/HC_Reporting/Functions/Reporting/DataTypes/CDataTypesParser.cs:706-710`

## Summary
`TryParseDateTime` first tries `InvariantCulture` (good), but on failure falls back to a `DateTime.TryParse(cleanedDateTime, out result)` overload that uses the *current thread culture*. For an ambiguous date like `03/04/2026`, this means the parsed value depends on the machine locale: US (en-US) reads March 4, most of Europe reads April 3. The same physical CSV value parses to two different dates on two machines, and no error is raised because the parse "succeeds."

## Evidence
```csharp
// CDataTypesParser.cs:700-710
if (DateTime.TryParse(cleanedDateTime, CultureInfo.InvariantCulture,
        DateTimeStyles.None, out DateTime result))
    return result;

// Second attempt: Try current culture (respects system locale)
if (DateTime.TryParse(cleanedDateTime, out result))   // CurrentCulture -> locale-dependent
    return result;
```
The explicit `commonFormats` array that follows (:713-724) includes both `dd.MM.yyyy` and `MM/dd/yyyy`, but it is only reached if the current-culture parse already failed — so the ambiguous-but-parseable case is resolved by locale, not by a deterministic rule.

## Impact
Session `CreationTime` (the only consumer, :644) can be silently off by months for ambiguous day/month values, varying by the machine running the report. Lower severity because it only affects ambiguous formats and a single display field, but it is a genuine non-determinism.

## Suggested Fix
Drop the bare current-culture attempt, or move the explicit `TryParseExact(commonFormats, InvariantCulture, …)` ahead of it so a deterministic, documented format set wins before any locale guess. If a locale fallback is truly needed, log when it is used.

## Labels
culture, datetime, ambiguous-format, non-determinism

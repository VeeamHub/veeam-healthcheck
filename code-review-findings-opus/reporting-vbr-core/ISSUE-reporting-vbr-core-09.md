# PPTX section-title formatting uses CurrentCulture, producing locale-dependent report titles

**Category:** reporting-vbr-core
**Severity:** Low
**Type:** Bug
**File(s):** `vHC/HC_Reporting/Functions/Reporting/Html/Exportables/HtmlToPptxConverter.cs:1578-1598`

## Summary
`FormatSectionTitle` title-cases section ids with
`System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(result.ToLower())`. Both
`ToLower()` and `ToTitleCase` are culture-sensitive against the machine's current culture. On a Turkish
(`tr-TR`) locale the dotless-i mapping turns words containing `i`/`I` into unexpected forms (the classic
Turkish-I problem), so section titles in the exported deck differ by machine locale — e.g. the regex
fix-ups `\bVbr\b`/`\bVm\b` may not match after a locale-specific lowercasing.

## Evidence
```csharp
// HtmlToPptxConverter.cs:1588
result = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(result.ToLower());
```
`result.ToLower()` (no culture arg) and `CurrentCulture.TextInfo` both bind to the operator's locale.

## Impact
Locale-dependent, non-deterministic slide titles; on Turkish/Azeri locales, abbreviation re-casing
(VBR/VM/DB/SQL/SOBR) can fail and titles render incorrectly. Cosmetic and limited to the opt-in PPTX
path, hence Low.

## Suggested Fix
Use `CultureInfo.InvariantCulture` for the lowercasing and title-casing in report formatting
(`InvariantCulture.TextInfo.ToTitleCase(result.ToLowerInvariant())`). Note CA1305/CA1307 are suppressed
project-wide, so this is flagged as a real correctness bug (Turkish-I), not a bare analyzer hit.

## Labels
bug, culture, globalization, pptx

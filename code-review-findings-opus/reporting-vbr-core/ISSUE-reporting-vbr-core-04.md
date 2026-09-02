# CSectionTable "&#" heuristic passes raw HTML through, defeating encoding

**Category:** reporting-vbr-core
**Severity:** Medium
**Type:** Security
**File(s):** `vHC/HC_Reporting/Functions/Reporting/Html/Shared/CSectionTable.cs:185-193`

## Summary
`CSectionTable<T>.Render` is the hardened, encoding-aware table builder — except for one bypass: if a
cell value contains the substring `&#`, the value is written **raw** (unencoded) on the assumption it is
a checkbox/emoji HTML entity. Any real data value that happens to contain `&#` (or is crafted to) is
therefore emitted without encoding, reopening the exact XSS/markup-corruption hole the class was built
to close.

## Evidence
```csharp
// CSectionTable.cs:185-193
// Check if value contains HTML entities (like checkbox emoji) - pass through raw
if (value.Contains("&#"))
{
    sb.Append($"<td title=\"\"{alignStyle}>{value}</td>");   // RAW
}
else
{
    sb.Append($"<td title=\"\"{alignStyle}>{Encode(value)}</td>");
}
```
The intended emoji values come from a closed set of constants (`TrueEmoji = "&#9989;"`,
`FalseEmoji = "&#9744;"`). A data string such as `Backup&#Job` or
`AT&#x3C;script&#x3E;...` satisfies `Contains("&#")` and skips encoding.

## Impact
Content-based dispatch on `&#` is an unreliable trust signal. A repository/job/server name containing
`&#` is emitted unescaped, allowing markup/script injection in the otherwise-safe section-table path.

## Suggested Fix
Stop inferring "is this HTML?" from the string content. Represent glyph cells explicitly — e.g. a
dedicated boolean/glyph column type (the `Func<T,bool>` overload already exists) or a `RawHtml` wrapper
marker — and always `Encode` plain string values.

## Labels
security, xss, html-encoding, heuristic

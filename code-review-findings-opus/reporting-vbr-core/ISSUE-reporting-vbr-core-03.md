# Section title / icon / id interpolated raw into section-card markup

**Category:** reporting-vbr-core
**Severity:** Medium
**Type:** Security
**File(s):** `vHC/HC_Reporting/Functions/Reporting/Html/Shared/CHtmlFormatting.cs:101-111,591-601`

## Summary
`SectionCardStart(id, title)` and the public `SectionCardStart(id, title, iconLetter, iconBg, iconColor,
defaultOpen)` interpolate `id`, `title`, `iconLetter`, `iconBg`, and `iconColor` directly into the
emitted HTML without encoding. While most callers pass literal/localized constants, several VBR table
headers derive section titles from data-influenced values (job/section names), and the icon/color
values flow into a `style="..."` attribute, which is an injection sink if ever fed dynamic input.

## Evidence
```csharp
// CHtmlFormatting.cs:101-111
private string SectionCardStart(string id, string title)
{
    string firstLetter = string.IsNullOrEmpty(title) ? "?" : title[0].ToString().ToUpper();
    string s = $"<div class=\"section-card open\" id=\"{id}\">";          // id raw
    ...
    s += $"<h2><span class=\"icon\" style=\"background:#f0f9ff;color:#0369a1\">{firstLetter}</span> {title}</h2>"; // title raw
}

// CHtmlFormatting.cs:594-600
return string.Format(@"<div id=""{0}"" class=""section-card{5}"">
    ...
      <h2><span class=""icon"" style=""background:{2};color:{3}"">{1}</span> {4}</h2>",
        id, iconLetter, iconBg, iconColor, title, openClass);   // all raw, incl. style attr
```
Contrast with `CSectionTable` (`CSectionTable.cs:127,136-141`) which encodes id/title/icon — confirming
the project intends these to be encoded.

## Impact
Any section whose title is derived from collected data (or any future caller passing dynamic
icon/color/id) can break the card markup or inject markup/attributes. Consistency gap with the
already-hardened `CSectionTable` path.

## Suggested Fix
Encode `id`, `title`, and (where dynamic) the icon/style values in both `SectionCardStart` overloads,
matching `CSectionTable.Encode`.

## Labels
security, xss, html-encoding, consistency

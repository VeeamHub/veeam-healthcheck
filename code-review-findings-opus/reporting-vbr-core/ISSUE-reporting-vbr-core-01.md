# Collected data emitted into HTML table cells without HTML-encoding (XSS / broken report)

**Category:** reporting-vbr-core
**Severity:** High
**Type:** Security
**File(s):** `vHC/HC_Reporting/Functions/Reporting/Html/Shared/CHtmlFormatting.cs:195-219`

## Summary
The legacy table-cell emitters in `CHtmlFormatting` (`TableData`, `TableDataLeftAligned`, and the
shaded `TableData(data, toolTip, shading)` overload) interpolate the `data` argument straight into
`<td>...</td>` with no HTML-encoding. These are the cell emitters used by every VBR table renderer
under `VBR/VbrTables/`, and the `data` values are collected, attacker-influenceable strings: VM names,
job names, repository names, server hostnames, paths, credential descriptions, registry values, etc.
A value containing `<`, `>`, `"`, or a `<script>`/`<img onerror=...>` fragment will either corrupt the
report markup or execute when the HTML is opened in a browser.

## Evidence
```csharp
// CHtmlFormatting.cs:201-205
public string TableData(string data, string toolTip)
{
    string titleAttr = string.IsNullOrEmpty(toolTip) ? "" : $" title=\"{toolTip}\"";
    return $"<td{titleAttr}>{data}</td>";   // data emitted raw
}

// CHtmlFormatting.cs:195-199
public string TableDataLeftAligned(string data, string toolTip)
{
    string titleAttr = string.IsNullOrEmpty(toolTip) ? "" : $" title=\"{toolTip}\"";
    return $"<td{titleAttr} style=\"text-align:left\">{data}</td>";   // data + toolTip raw
}

// CHtmlFormatting.cs:207-219  (toolTip also unencoded into the title attribute)
return string.Format("<td title=\"{0}\">{1}</td>", toolTip, data);
```
Confirmed callers pass collected values directly, e.g.:
```csharp
// VBR/VbrTables/CMultiRoleTable.cs:59-64
s += this.form.TableData(d[0], string.Empty);   // server name (non-scrub branch)
s += this.form.TableData(d[1], string.Empty);
```
The team already built an encoding-aware path (`CHtmlBuilder.HtmlEncode`, `CSectionTable.Encode`), so
the safe primitive exists — these legacy emitters simply bypass it.

## Impact
- A backup object named e.g. `<img src=x onerror=alert(document.cookie)>` (or any name containing
  `<`/`"`) injects arbitrary HTML/JS into the generated report. These reports are routinely shared with
  and opened by Veeam SEs and customers, so this is stored XSS in a document that crosses trust
  boundaries.
- Even without malicious intent, any legitimate value containing `<`, `>`, or `&` silently breaks table
  layout or truncates the cell, producing a wrong report.
- The `title="{toolTip}"` overload additionally allows attribute-breakout via a `"` in the value.

## Suggested Fix
Route all cell/attribute values through the existing encoder. Either change these methods to encode
`data` and `toolTip` (e.g. `WebUtility.HtmlEncode`, with an explicit raw overload for the few callers
that intentionally pass markup like checkbox glyphs), or migrate the VBR table renderers onto
`CSectionTable<T>` / `CHtmlBuilder`, which already encode. Provide a clearly-named `TableDataRaw(...)`
for the intentional-HTML cases so the default is safe-by-construction.

## Labels
security, xss, html-encoding, reporting

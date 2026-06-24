---
title: "Section summaries are built then silently discarded (LicSum returns null; SectionEnd ignores its parameter)"
severity: Medium
labels: [bug, maintainability]
domain: reporting-vbr-core
files:
  - vHC/HC_Reporting/Functions/Reporting/Html/VBR/CVbrSummaries.cs:41
  - vHC/HC_Reporting/Functions/Reporting/Html/Shared/CHtmlFormatting.cs:73
confidence: High
---

## Summary

Two cooperating defects mean none of the localized "Summary / Notes" blocks defined in `CVbrSummaries` reach the report:

1. `CVbrSummaries.LicSum()` builds a full summary string `s` (including two `</div>` closers) and then `return null;` — the computed value is thrown away. It is actively called (`CHtmlTables.cs:148`).
2. `CHtmlFormatting.SectionEnd(string summary)` has the consuming line commented out (`// s += summary;`), so every summary passed to `SectionEnd(summary)` (`CHtmlTables.cs:246, 1021, 1361` — license, job concurrency, task concurrency, plus `SecSum`, `Extents`, `ProtectedWorkloads` consumers) is computed and dropped.

The rest of `CVbrSummaries` (~440 lines of localized content, including the JobCon SQL sizing table at :355-373) is therefore dead weight: it executes, allocates, and contributes nothing. Worse, if anyone "fixes" `SectionEnd` by un-commenting the append, the summaries emit stray `</div></div>` closers (e.g., `MissingJobsSUmmary`, :157-167 closes two divs it never opened), corrupting document structure.

## Impact

Either this is a regression (explanatory summary/notes content the localization team maintains is missing from the report) or it's abandoned code that misleads maintainers and wastes cycles per section. The mismatched `</div>` closers are a structural-HTML trap for whoever re-enables it.

## Evidence

`vHC/HC_Reporting/Functions/Reporting/Html/VBR/CVbrSummaries.cs:41-62` —

```csharp
public string LicSum()
{
    string s = this.form.AddA("hdr", VbrLocalizationHelper.GeneralSummaryHeader) + ...
    s += "</div>";
    s += "</div>";

    return null;     // built string discarded
}
```

`vHC/HC_Reporting/Functions/Reporting/Html/Shared/CHtmlFormatting.cs:73-82` —

```csharp
public string SectionEnd(string summary)
{
    string s = "</tbody>";
    s += "</table>";

    // s += summary;     // summary parameter ignored
    s += this.endDiv;
    s += this.endDiv;
```

## Suggested fix

Decide intent: if summaries were removed in the dashboard redesign, delete `CVbrSummaries` usages and the `summary` parameter so the dead pipeline disappears; if they should render, fix `LicSum` to `return s;`, re-enable the append, and rebalance the `</div>` pairs in each summary method (open the wrappers inside the summary methods instead of relying on callers).

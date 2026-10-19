---
title: "CHtmlFormatting.cs bloated to 3 MB by two 1.5 MB commented-out base64 lines"
severity: Low
labels: [maintainability]
domain: reporting-vbr-core
files:
  - vHC/HC_Reporting/Functions/Reporting/Html/Shared/CHtmlFormatting.cs:433
  - vHC/HC_Reporting/Functions/Reporting/Html/Shared/CHtmlFormatting.cs:465
confidence: High
---

## Summary

`CHtmlFormatting.cs` is 647 lines but ~3 MB on disk: lines 433 and 465 are each a ~1,545,000-character commented-out `// string s = "<img src=\"data:image/png;base64,iVBOR...` literal (the old banner image, retained in both `SetHeaderAndLogo` and `SetHeaderAndLogoVB365`). The live code already loads the banner from the embedded resource `banner_string.txt`.

## Impact

Every IDE open, grep, diff, code review, and analyzer pass on this file chews through 3 MB of dead comment. Several tools (including this review's own file reader) refuse or truncate the file. Git history and PR diffs that touch this file are similarly degraded. Zero functional value.

## Evidence

`vHC/HC_Reporting/Functions/Reporting/Html/Shared/CHtmlFormatting.cs:433` (and identically :465) —

```csharp
// string s = "<img src=\"data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAACzQAAAHg... [1.5 MB more]
string s = string.Empty;
string bannerString = CHtmlCompiler.GetEmbeddedCssContent("banner_string.txt");
```

Line-length scan: lines 433 and 465 are each 1,545,081 characters; the next longest line in the file is 195.

## Suggested fix

Delete both commented-out base64 lines. The banner already lives in the `banner_string.txt` embedded resource.

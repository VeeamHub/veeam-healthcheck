---
title: "Fix duplicated document header in security report assembly"
severity: Medium
labels: [bug]
domain: reporting-vbr-core
files:
  - vHC/HC_Reporting/Functions/Reporting/Html/VBR/CHtmlCompiler.cs:343
  - vHC/HC_Reporting/Functions/Reporting/Html/VBR/CHtmlCompiler.cs:332
confidence: High
---

## Summary

`FormSecurityBody` passes the already-accumulated document (`this.htmldocOriginal`, which contains the full `<!DOCTYPE html><head>...<style>...` header from `FormHeader()`) into `FormSecurityBodyStart`, which appends to it and returns the *whole* string — and then the caller appends that return value back onto `htmldocOriginal` with `+=`. The entire header (doctype, `<head>`, full embedded CSS) is therefore emitted twice in every security report.

## Impact

The exported `VBR_Security` HTML contains a duplicated doctype/head/CSS block in the middle of the document. Browsers mostly recover, but the file is malformed (doubles the CSS payload, invalid structure), and any tooling that parses the report (or the PPTX regex parser) sees duplicate content. Wrong/malformed report on a main feature path (`RunSecurityReport`).

## Evidence

`vHC/HC_Reporting/Functions/Reporting/Html/VBR/CHtmlCompiler.cs:332-338` —

```csharp
private string FormSecurityBodyStart(string htmlString, bool scrub)
{
    htmlString += this.form.body;
    htmlString += this.SetVbrSecurityHeader();
    htmlString += this.form.SetSecurityBannerAndIntro(scrub);
    return htmlString;   // returns ORIGINAL content + new content
}
```

`vHC/HC_Reporting/Functions/Reporting/Html/VBR/CHtmlCompiler.cs:343` —

```csharp
this.htmldocOriginal += this.FormSecurityBodyStart(this.htmldocOriginal, false);
```

`htmldocOriginal` = `H`; the call returns `H + body + ...`; after `+=` the field holds `H + H + body + ...` — header duplicated. Note the full-report path avoids this only by accident: `FormBodyStart(string htmlString, bool scrub)` (:308) ignores its `htmlString` parameter entirely.

## Suggested fix

Make `FormSecurityBodyStart` build and return only the *new* fragment (start from `string.Empty`, not the passed-in document), and drop the unused `htmlString` parameter here and in `FormBodyStart` so the pattern can't recur.

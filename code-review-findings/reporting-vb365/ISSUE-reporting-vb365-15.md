---
title: "CVb365HtmlCompiler emits duplicate <body> tag and never closes body/html"
severity: Low
labels: [bug, maintainability]
domain: reporting-vb365
files:
  - vHC/HC_Reporting/Functions/Reporting/Html/VB365/CVb365HtmlCompiler.cs:47
confidence: High
---

## Summary

`FormVb365Body()` appends `this.form.body` (`"<body>"`, per `CHtmlFormatting.cs:20`) at line 47, then immediately appends the result of `FormBodyStartVb365(...)`, whose first statement is again `string h = this.form.body;` — so the document contains `<body><body>`. The `htmlString` parameter passed to `FormBodyStartVb365` is never used. Additionally, the compiler never appends `form.bodyend`/`</html>`, so the exported document ends after the `<script>` block with unclosed `<body>`/`<html>`.

## Impact

Browsers auto-repair, so the HTML report looks fine — but the same string is fed to DinkToPdf and HtmlToOpenXml exporters (`CHtmlExporter.ExportHtmlVb365`, lines 71-80), which are far less forgiving of duplicated/unclosed structural tags and can render or convert inconsistently.

## Evidence

`vHC/HC_Reporting/Functions/Reporting/Html/VB365/CVb365HtmlCompiler.cs:47-49`:

```csharp
this.htmldoc += this.form.body;

this.htmldoc += this.FormBodyStartVb365(this.htmldoc);
```

`CVb365HtmlCompiler.cs:102-104`:

```csharp
private string FormBodyStartVb365(string htmlString)
{
    string h = this.form.body;
```

`CHtmlFormatting.cs:20-21`:

```csharp
public string body = "<body>";
public string bodyend = "</body>";
```

## Suggested fix

Remove the first `this.htmldoc += this.form.body;`, drop the unused parameter, and append `this.form.bodyend + "</html>"` after the script block before exporting.

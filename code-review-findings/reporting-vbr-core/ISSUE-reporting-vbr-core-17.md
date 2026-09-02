---
title: "GetEmbeddedCssContent throws ArgumentNullException (not a clear error) when an embedded resource is missing; helper duplicated"
severity: Low
labels: [reliability, maintainability]
domain: reporting-vbr-core
files:
  - vHC/HC_Reporting/Functions/Reporting/Html/VBR/CHtmlCompiler.cs:190
  - vHC/HC_Reporting/Functions/Reporting/Html/Shared/CHtmlFormatting.cs:325
confidence: High
---

## Summary

`GetEmbeddedCssContent` passes the result of `GetManifestResourceStream` straight into `new StreamReader(stream)`. If the resource name doesn't resolve (resource renamed, assembly name changed, single-file packaging quirk), the stream is `null` and `StreamReader`'s constructor throws `ArgumentNullException: stream` — failing the whole report (`Header()` for css.css, `FormFooter` for ReportScript.js, `SetHeaderAndLogo` for banner_string.txt) with an error that doesn't name the missing resource. The identical helper exists twice: `CHtmlCompiler.GetEmbeddedCssContent` and `CHtmlFormatting.GetEmbeddedCssContent` — they can drift independently.

By contrast, `GetIconAsBase64` (CHtmlCompiler.cs:472-488) handles the null-stream case correctly.

## Impact

A packaging/rename mistake produces an opaque crash at report time instead of a diagnostic naming the missing embedded resource. Duplication invites divergent fixes.

## Evidence

`vHC/HC_Reporting/Functions/Reporting/Html/VBR/CHtmlCompiler.cs:190-200` —

```csharp
public static string GetEmbeddedCssContent(string embeddedFileName)
{
    var assembly = Assembly.GetExecutingAssembly();
    var resourceName = $"{assembly.GetName().Name}.{embeddedFileName}";

    using (var stream = assembly.GetManifestResourceStream(resourceName))
    using (var reader = new StreamReader(stream))   // ArgumentNullException if resource missing
```

Duplicated verbatim at `CHtmlFormatting.cs` (`GetEmbeddedCssContent`, region "helper functions").

## Suggested fix

Keep a single copy (the `CHtmlFormatting` one, or a new `EmbeddedResources` helper) and add a null check that throws `InvalidOperationException($"Embedded resource '{resourceName}' not found")`. Have `CHtmlCompiler` call it.

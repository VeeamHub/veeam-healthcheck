---
title: "Don't swallow exceptions in FormVb365Body — failure skips ExportHtml and no report is written"
severity: High
labels: [reliability]
domain: reporting-vb365
files:
  - vHC/HC_Reporting/Functions/Reporting/Html/VB365/CVb365HtmlCompiler.cs:96
confidence: High
---

## Summary

`FormVb365Body()` wraps the entire body build *and* the final `this.ExportHtml()` call in one try block whose catch only logs `e.Message`. Any exception anywhere in section generation (or in `FormBodyStartVb365`/`SetLicHolder`, which are unguarded inside the try) aborts before `ExportHtml()` runs — so no VB365 HTML file is produced at all, while the constructor still logs "HTML complier complete!".

## Impact

A single unexpected error silently produces zero output. The user sees a success-looking log ("[VB365] HTML complier complete!") and an exit with no report, with only a one-line `e.Message` (no stack trace, no exception type) buried in the log to diagnose it.

## Evidence

`vHC/HC_Reporting/Functions/Reporting/Html/VB365/CVb365HtmlCompiler.cs:94-99`:

```csharp
        this.ExportHtml();
    }
    catch (System.Exception e)
    {
        this.log.Error("[VB365][HTML] Error: " + e.Message);
    }
```

and `CVb365HtmlCompiler.cs:23-28`:

```csharp
public CVb365HtmlCompiler()
{
    this.log.Info("[VB365] HTML complier init...");
    this.RunCompiler();
    this.log.Info("[VB365] HTML complier complete!");
}
```

`ExportHtml()` is the last statement inside the try, so it is skipped on any failure; the "complete!" message is logged unconditionally.

## Suggested fix

Move `ExportHtml()` outside the try (export whatever was built), log `e.ToString()` instead of `e.Message`, and surface a non-zero/failed status to the caller so the run is not reported as successful.

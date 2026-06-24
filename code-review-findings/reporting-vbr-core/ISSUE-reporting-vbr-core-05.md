---
title: "HtmlToPdfConverter: timed-out conversion leaves a foreground thread that blocks process exit; Dispose is a no-op"
severity: Medium
labels: [reliability]
domain: reporting-vbr-core
files:
  - vHC/HC_Reporting/Functions/Reporting/Html/Exportables/HtmlToPdfConverter.cs:57
  - vHC/HC_Reporting/Functions/Reporting/Html/Exportables/HtmlToPdfConverter.cs:86
confidence: High
---

## Summary

`ConvertHtmlToPdf` runs the DinkToPdf conversion on a dedicated STA thread and waits with `thread.Join(TimeSpan.FromMinutes(5))`. On timeout it throws, but:

1. The worker thread is never marked `IsBackground = true`. A hung wkhtmltopdf conversion (the exact scenario the timeout guards against) leaves a **foreground** thread alive, so the process cannot exit after the run completes — the CLI hangs forever even though the timeout "fired".
2. `Dispose()` only nulls the converter reference; nothing about the native wkhtmltopdf library is released, and the class doesn't implement `IDisposable`, so callers can't `using` it.
3. If `converter.Convert(doc)` returns `null` without throwing, `File.WriteAllBytes(outputPath, pdf)` throws `ArgumentNullException` with a misleading message.
4. A new `SynchronizedConverter(new PdfTools())` is created per `HtmlToPdfConverter` instance. wkhtmltopdf must only be initialized once per process; DinkToPdf documents the converter as a process-wide singleton. With multiple conversions in one process (e.g., repeated GUI runs) this is a known crash/AccessViolation source.

## Impact

Hung PDF conversion = hung vHC process (scheduled/automated runs never terminate). Repeat conversions in one process risk native crashes that take down the whole report run.

## Evidence

`vHC/HC_Reporting/Functions/Reporting/Html/Exportables/HtmlToPdfConverter.cs:57-75` —

```csharp
var thread = new Thread(() => { ... pdf = this.converter.Convert(doc); ... });
thread.SetApartmentState(ApartmentState.STA);
thread.Start();

if (!thread.Join(TimeSpan.FromMinutes(5)))
{
    this.log.Error("[PdfConverter] PDF conversion timed out after 5 minutes.", false);
    throw new TimeoutException("PDF conversion timed out after 5 minutes.");
}
```

No `thread.IsBackground = true;` — the abandoned thread keeps the process alive.

`HtmlToPdfConverter.cs:86-89` —

```csharp
public void Dispose()
{
    this.converter = null;   // releases nothing
}
```

## Suggested fix

Set `thread.IsBackground = true;` before `Start()`. Hold the `SynchronizedConverter` in a `static readonly` (process singleton) per DinkToPdf guidance. Guard `pdf == null` after a successful join and throw a descriptive exception. Implement `IDisposable` properly or remove the misleading `Dispose`.

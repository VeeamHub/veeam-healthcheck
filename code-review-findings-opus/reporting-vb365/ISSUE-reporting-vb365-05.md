# Empty catch blocks silently drop entire VB365 report sections

**Category:** reporting-vb365
**Severity:** Medium
**Type:** Bug
**File(s):** `vHC/HC_Reporting/Functions/Reporting/Html/VB365/CM365Tables.cs:127-129, 335-337, 454-456, 503-505, 707-709, 887-889, 1014-1016, 1069-1071, 1123-1125, 1182-1184, 1315-1317, 1379-1381, 1429-1431, 1519-1521, 1687-1689`; `CVb365HtmlCompiler.cs:96-99`

## Summary
Every table method wraps its data-rendering loop in `try { ... } catch (Exception) { }` with a completely empty handler. Any exception — divide-by-zero, index-out-of-range, null field, parse failure on one row — aborts the loop and produces a partially-rendered or empty table with **no logging and no indication anything went wrong**. The whole-body `FormVb365Body()` catch only logs `e.Message` (no stack, no section context), so a failure in one section can also abort everything downstream of it.

## Evidence
Representative (`CM365Tables.cs:126-129`):
```csharp
catch (Exception)
{
}
```
This pattern repeats in all 15 table methods. The compiler-level catch (`CVb365HtmlCompiler.cs:96-99`) is only marginally better:
```csharp
catch (System.Exception e)
{
    this.log.Error("[VB365][HTML] Error: " + e.Message);   // no stack trace, no section id
}
```

## Impact
Silent data loss: sections render empty or truncated and the operator has no signal that collection/parsing failed for that section. Debugging field issues is near-impossible because nothing is logged. This finding compounds the divide-by-zero (#06) and index-out-of-range (#04) bugs — those are exactly the exceptions being swallowed here. (Note: CA1031 is project-suppressed, but the issue is the *empty* body + lost section, not merely catching `Exception`.)

## Suggested Fix
At minimum log the exception (with stack and section name) in each catch so failures are visible. Better: catch per-row inside the loop and `continue`, so one bad row doesn't drop the rest of the table:
```csharp
catch (Exception ex)
{
    this.log.Error($"[VB365][HTML] <SectionName> render failed: {ex}");
}
```

## Labels
bug, exception-swallowing, observability, vb365, reporting

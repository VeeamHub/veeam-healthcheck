# /vbr and /vb365 flags can silently auto-escalate to "Both", overriding explicit single-product intent

**Category:** startup-cli
**Severity:** Low
**Type:** Bug
**File(s):** `vHC/HC_Reporting/Startup/CArgsParser.cs:215-228`

## Summary
The `/vbr` and `/vb365` cases each promote `TargetProductType` to `Both` if the other was already set. This is order/duplication sensitive: passing `/vbr /vbr` is fine, but the logic means there is no way to express "switch from a previously-set value back to single product," and a stray duplicate or scripted flag concatenation (`/vbr` appended to an env that already injected `/vb365`) silently becomes a full Both-product run — doubling collection scope and runtime — with only an info log.

## Evidence
```csharp
case "/vbr":
    if (CGlobals.TargetProductType == TargetProduct.Vb365)
        CGlobals.TargetProductType = TargetProduct.Both;   // silent escalation
    else
        CGlobals.TargetProductType = TargetProduct.Vbr;
    ...
case "/vb365":
    if (CGlobals.TargetProductType == TargetProduct.Vbr)
        CGlobals.TargetProductType = TargetProduct.Both;   // silent escalation
    else
        CGlobals.TargetProductType = TargetProduct.Vb365;
    ...
```

## Impact
Low, but it is surprising: a fleet script that templates flags and accidentally emits both `/vbr` and `/vb365` (or a user who types both to "be safe") triggers a Both run that collects from products that may not be installed, lengthening runtime and potentially generating the no-op VB365 path (ISSUE-01). The escalation is implicit; there is no warning that single-product intent was widened.

## Suggested Fix
Log a Warning (not Info) when escalating to Both, and document that `/vbr /vb365` means Both. Consider requiring an explicit `/both` flag instead of inferring it from the presence of both single-product flags, so intent is unambiguous.

## Labels
bug, argument-parsing, product-selection, usability, low

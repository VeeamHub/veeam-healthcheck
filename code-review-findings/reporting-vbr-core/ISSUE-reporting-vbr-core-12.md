---
title: "SecSummary: bare empty catch silently defaults MFA/Four-Eyes security flags"
severity: Low
labels: [reliability]
domain: reporting-vbr-core
files:
  - vHC/HC_Reporting/Functions/Reporting/Html/CDataFormer.cs:134
confidence: High
---

## Summary

The first try block in `SecSummary` (MFA + Four-Eyes detection from `vbrinfo.csv`) ends with a completely empty catch — no log line, unlike the four sibling blocks below it which at least log before defaulting. If `GetDynamicVbrInfo()` fails (missing file is handled, but a malformed row / missing `mfa`/`foureyes` member on the dynamic record throws `RuntimeBinderException`), `MFAEnabled` silently stays `false` and the Security Summary reports "MFA disabled" with no trace of the detection failure.

## Impact

A security-posture indicator can be reported as a hard "false" when the truth is "unknown", with nothing in the log to distinguish the two. For a health-check tool whose security summary drives remediation conversations, false negatives should at least be diagnosable.

## Evidence

`vHC/HC_Reporting/Functions/Reporting/Html/CDataFormer.cs:134-136` —

```csharp
catch (Exception)
{
}
```

Compare with the immutability block right below (:168-172), which logs `"Unable to find immutability. Marking false"`.

## Suggested fix

Log the exception like the sibling blocks do (`this.log.Error(this.logStart + "Unable to detect MFA/FourEyes: " + ex.Message)`), and consider a tri-state (true/false/unknown) for these security flags so the table can render "Unknown" rather than a confident false.

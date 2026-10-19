---
title: "CMessages.PsVbrFunctionDone is built from the wrong constant (copy-paste error)"
severity: Low
labels: [bug, maintainability]
domain: startup-cli
files:
  - vHC/HC_Reporting/Common/CMessages.cs:125
confidence: High
---

## Summary
`PsVbrFunctionDone` concatenates `PsVbrConfigStart` instead of `PsVbrFunctionStart`, so the "done" message for the function setter reads "[PS] Enter Config Collection Invoker...DONE!" instead of "[PS] Enter Function Setter...DONE!".

## Impact
Misleading log lines when correlating start/done pairs during troubleshooting. (Currently the constant has no references outside its declaration, so it is also a dead-code candidate.)

## Evidence
`vHC/HC_Reporting/Common/CMessages.cs:124-125`:

```csharp
public static string PsVbrFunctionStart = "[PS] Enter Function Setter...";
public static string PsVbrFunctionDone = PsVbrConfigStart + ProcEnd;   // should be PsVbrFunctionStart
```

## Suggested fix
```csharp
public static string PsVbrFunctionDone = PsVbrFunctionStart + ProcEnd;
```
or delete the unused pair.

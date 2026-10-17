# run_Click calls VerifyPath() twice and proceeds even after the first call's failure message

**Category:** startup-cli
**Severity:** Medium
**Type:** Bug
**File(s):** `vHC/HC_Reporting/VhcGui.xaml.cs:236-246`

## Summary
`run_Click` calls `this.functions.VerifyPath()` (which has the side effect of creating the directory) and shows an error MessageBox if it returns false — but does **not** return. It then calls `VerifyPath()` a second time and runs if that returns true. The flow is contradictory: on the failure path the user sees the error dialog and the run still proceeds to the second check; on the success path the directory-creating method runs twice.

## Evidence
```csharp
if (!this.functions.VerifyPath())
{
    MessageBox.Show("Error: Failed to validate desired output path. ...");
    // <-- no return; execution continues
}

if (this.functions.VerifyPath())     // second call, re-runs Directory.CreateDirectory side effect
{
    this.DisableGuiAndStartProgressBar();
    this.Run(false);
}
```

## Impact
- Wasted/duplicated filesystem side effects (two `Directory.Exists`/`CreateDirectory` passes).
- Confusing UX: if the path is invalid, the error box appears, then the second `VerifyPath()` is evaluated; if it transiently succeeds (e.g. the first call created the dir) the run starts anyway despite the error having been shown. The intent was clearly "show error and stop," but the missing `return` defeats it.

## Suggested Fix
```csharp
if (!this.functions.VerifyPath())
{
    MessageBox.Show("Error: Failed to validate desired output path. Please try a different path.");
    return;
}
this.DisableGuiAndStartProgressBar();
this.Run(false);
```

## Labels
bug, wpf, validation, control-flow, medium

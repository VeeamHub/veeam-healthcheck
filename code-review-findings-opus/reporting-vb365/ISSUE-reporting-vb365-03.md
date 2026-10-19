# Jobs() uses `break` on empty Organization, silently dropping all remaining jobs

**Category:** reporting-vb365
**Severity:** High
**Type:** Bug
**File(s):** `vHC/HC_Reporting/Functions/Reporting/Html/VB365/CM365Tables.cs:1639-1642`

## Summary
Inside the per-job loop of `Jobs()`, when a job's `organization` field is empty the code executes `break`, which terminates the **entire** outer `foreach` over all jobs. A single job row with a missing/blank organization therefore truncates the Jobs table — every job after it is omitted from the report. The intent was almost certainly to skip that one row (`continue`).

## Evidence
`CM365Tables.cs:1557-1642`:
```csharp
foreach (var gl in global)
{
    ...
    foreach (var g in gl) { /* populate org, name, ... */ }

    if (string.IsNullOrEmpty(org))
    {
        break;          // BUG: aborts the whole jobs loop, not just this row
    }
    ...
    s += this.form.TableData(org, string.Empty);
    ...
}
```
A partially-opened `<tr>` is also already appended (`s += "<tr>";` at line 1573) before the `break`, leaving an unclosed/empty row in the HTML.

## Impact
Reports under-report protected workloads: any job ordered after a blank-org row vanishes from the Jobs section, giving engineers and customers a false picture of configured backup coverage. Combined with the stray open `<tr>`, it also emits malformed HTML.

## Suggested Fix
Replace `break;` with `continue;`, and move the empty-org check *before* `s += "<tr>";` so no partial row is emitted:
```csharp
if (string.IsNullOrEmpty(org)) { continue; }
```

## Labels
bug, control-flow, data-loss, vb365, jobs

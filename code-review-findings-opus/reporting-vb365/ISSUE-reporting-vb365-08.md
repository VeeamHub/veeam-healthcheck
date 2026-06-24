# Proxies scrubbing runs inside the column loop, partially scrubbing other fields and wasting work

**Category:** reporting-vb365
**Severity:** Low
**Type:** Bug
**File(s):** `vHC/HC_Reporting/Functions/Reporting/Html/VB365/CM365Tables.cs:181-229`

## Summary
In `Vb365Proxies()`, the scrub block is placed *inside* the inner `foreach (var g in gl)` that walks each column of the row. As a result `proxyname` and `description` are re-scrubbed on every column iteration. The scrub runs against whatever values those locals hold at that point in the column walk — which depends on dictionary ordering. If `name`/`description` columns are not the first encountered, the scrub executes against still-empty strings on early iterations and against the populated value only after the matching `case` has run, relying on the `ScrubItem` idempotency guard (`StartsWith(type + "_")`) to avoid double-replacement. It works by luck, not design, and burns N redundant scrub calls per row.

## Evidence
`CM365Tables.cs:181-229`:
```csharp
foreach (var g in gl)
{
    switch (g.Key) { case "name": proxyname = g.Value; break; /* ... */ }

    string output = g.Value;            // unused
    if (CGlobals.Scrub)
    {
        proxyname   = this.scrubber.ScrubItem(proxyname, Scrubber.ScrubItemType.Server);
        description = this.scrubber.ScrubItem(description, Scrubber.ScrubItemType.Item);
    }
}
```
Contrast with `Vb365Repos()` (lines 428-435) and `Vb365ObjectRepos()` (1267-1274), where scrubbing is correctly done once, after the column loop completes.

## Impact
Mostly redundant work and fragile reliance on scrubber idempotency. Edge risk: if a proxy name happens to already start with `Server_` (or description with `Item_`) in the source data, the guard short-circuits and the value is emitted unscrubbed — a scrub bypass for that row. The dead `string output = g.Value;` also signals the copy-paste origin.

## Suggested Fix
Move the scrub block out of the inner `foreach`, after the column walk, matching the Repos/ObjectRepos pattern; remove the unused `output` local.

## Labels
bug, scrubbing, maintainability, vb365, proxies

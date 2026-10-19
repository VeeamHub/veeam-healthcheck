# Positional-index scrubbing is brittle and risks emitting identifiers unscrubbed

**Category:** reporting-vb365
**Severity:** Medium
**Type:** Security
**File(s):** `vHC/HC_Reporting/Functions/Reporting/Html/VB365/CM365Tables.cs:1051-1063 (JobSessions), 1106-1116 (ProcStats), 1163-1175 (JobStats), 1355-1372 (Orgs), 1410-1422 (Perms)`

## Summary
Several VB365 tables scrub sensitive columns by hard-coded ordinal position (`counter == 0`, `counter == 5`, the `0/1/4/5/6/8/9` set in Orgs, etc.) while iterating a dictionary of CSV columns. This couples scrubbing correctness to the exact column order and column set produced by the collection scripts. If the CSV schema changes (a column added, removed, or reordered — which has happened historically across VB version bumps), the positional indices silently target the wrong columns, so real organization names, job names, or user/mailbox identifiers can be emitted **unscrubbed** in scrub mode, or harmless columns get scrubbed instead. Unlike the strongly-typed tables (Repos, ObjectRepos, Jobs) that scrub by named field, these rely on fragile positions with no validation.

## Evidence
JobSessions scrubs columns 0 and 5 by index (`CM365Tables.cs:1054-1060`):
```csharp
if (CGlobals.Scrub)
{
    if (counter == 0 || counter == 5)
        output = this.scrubber.ScrubItem(output, Scrubber.ScrubItemType.Job);
}
```
Orgs scrubs a positional set (`CM365Tables.cs:1360-1369`):
```csharp
if (counter == 0 || counter == 1 || counter == 4 ||
    counter == 5 || counter == 6 || counter == 8 || counter == 9)
    output = this.scrubber.ScrubItem(output, Scrubber.ScrubItemType.Item);
```
The Organizations table header lists "Real Name", "Friendly Name", EXO/SPO settings, "Licensed Users", etc. — sensitive tenant/user identifiers whose scrubbing depends entirely on these magic indices matching `GetDynVboOrg()`'s column order. There is no key check (`g.Key == "realname"`) guarding the scrub.

## Impact
Scrub mode is the feature customers rely on to share VHC reports externally without leaking tenant names, user identities, and infrastructure names. Index-based scrubbing means a future CSV schema change can silently defeat anonymization — a data-leak/privacy regression that would not surface in a normal build or test.

## Suggested Fix
Scrub by column key, not ordinal: `switch (g.Key) { case "organization": case "realname": output = scrubber.ScrubItem(...); }`. This matches the safer pattern already used in `Vb365Repos`, `Vb365ObjectRepos`, and `Jobs`, and is resilient to column reordering.

## Labels
security, scrubbing, privacy, brittle, vb365, reporting

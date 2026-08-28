# ADR 0028: Orphaned/Superseded JSON Export Uses a Dedicated `CFullReportJson` Property, Not `SetSection`/`HtmlSection`

* **Status:** Accepted
* **Date:** 2026-08-25
* **Decider:** Ben Thomas (@comnam90)
* **Consulted:** Claude Code (design)

## Context and Problem Statement

Every existing report section's JSON output goes through
`CHtmlTables.SetSection(key, headers, rows, summary)`, which stores one
`HtmlSection` (`SectionName`, `Headers: List<string>`, `Rows:
List<List<string>>`, `Summary`) per section key in
`CGlobals.FullReportJson.Sections`. Confirmed by inspecting every current
`SetSection` call site: `Rows` is a flat, rectangular grid of strings —
there is no precedent anywhere in this codebase for a nested array or
object inside a section's row.

Issue #192's report needs exactly that: a nested per-object array (fulls,
incrementals, sizes, oldest/newest restore point per machine) inside each
job-level row, not just per-job totals.

## Considered Options

### Option A — Extend `HtmlSection.Rows` to allow nested values

E.g. change `List<List<string>>` to `List<List<object>>`, or add a
secondary nested-rows field to `HtmlSection` itself.

**Rejected.** Every existing report section and every consumer of
`CFullReportJson.Sections` relies on the current flat-string contract.
Changing it risks every other section's JSON serialization for the benefit
of one new feature, and a "sometimes-nested" `HtmlSection` contract is
harder to reason about than a type that's uniformly flat.

### Option B — Flatten to match every other section's shape

Emit one JSON row per `(job, object)` pair, matching every other section's
flat rows, discarding the nesting.

**Rejected.** Defeats the report requirement directly — a JSON consumer
would have to re-group flat rows by job name/Id to reconstruct what the
HTML already shows nested, pushing the aggregation work downstream onto
every consumer instead of doing it once.

### Option C — A new, dedicated, strongly-typed property on `CFullReportJson`, bypassing `SetSection` (chosen)

## Decision

`CGlobals.FullReportJson.OrphanedSupersededBackups` is a
`List<OrphanedSupersededBackupRecord>` — the exact same type the HTML
renderer and the `OrphanedSupersededBackupAggregator` already produce and
consume — populated directly, not through `SetSection`.

This reuses an existing, if currently dormant, precedent rather than
inventing a new one: `CFullReportJson.cProtectedWorkloads` is already a
dedicated typed property sitting alongside the generic `Sections`
dictionary — it has simply never been populated in practice (confirmed:
grep finds it assigned nowhere in the current codebase).

## Rationale

- DRY: the same DTO serves both HTML rendering and JSON export, rather
  than inventing a third shape purely for JSON.
- Zero risk to the shared `HtmlSection` contract every other section
  depends on — nothing about `SetSection`'s existing callers changes.
- Following an existing (if unused) pattern in this codebase is preferable
  to introducing a second, different way to add a typed JSON property,
  even though the first way was never actually exercised before this.

## Consequences

### Positive
- Nested per-object detail survives into JSON output, not just HTML.
- No changes required to `HtmlSection`, `SetSection`, or any existing
  section's JSON.

### Negative
- This section's JSON shape is genuinely different from every other
  section's. A future maintainer scanning
  `CGlobals.FullReportJson.Sections` for "every report section that has
  JSON output" will miss this one unless they also know to check
  `CFullReportJson`'s dedicated properties.
- If a *second* future feature also needs nested JSON, this ad hoc,
  one-off property is not itself a reusable pattern — it would need its
  own similarly-dedicated property, or this decision would be worth
  revisiting in favor of a proper nested-JSON mechanism on `HtmlSection`
  (Option A, deliberately deferred here rather than ruled out forever).

## Validation

None needed beyond compilation — this is a data-shape decision, not an
empirical one. See
[`docs/superpowers/specs/2026-08-24-orphaned-superseded-backups-design.md`](../superpowers/specs/2026-08-24-orphaned-superseded-backups-design.md)
and the implementation plan's Task 7.

## Addendum (2026-08-28)

`cProtectedWorkloads`, the precedent property cited above, was removed
from `CFullReportJson` by issue #172 — confirmed dead (assigned nowhere in
the codebase; the real protected-workloads data has always lived in
`Sections["protectedWorkloads"]`). This ADR's decision — a dedicated typed
property for `OrphanedSupersededBackups`, bypassing `SetSection` — is
unaffected; only the precedent example cited above no longer exists in
the code. See
[ADR 0029](0029-html-free-values-in-json-export-producers.md).

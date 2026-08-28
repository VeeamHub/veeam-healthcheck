# ADR 0029: JSON Export Producers Emit HTML-Free Values; HTML Rendering Injects `<br>` at the Call Site

* **Status:** Accepted
* **Date:** 2026-08-28
* **Decider:** Ben Thomas (@comnam90)
* **Consulted:** Claude Code (design)

## Context and Problem Statement

Issue #171 reported that the JSON report (`*.json`) leaks raw HTML markup
into field values: `regKeys`'s `AzureArchiveFreezingUnSupportedRegions`
value and `serversRequirements`'s `Type` column both contain literal
`<br>` tags. Root cause: `CDataFormer.RegOptions()` and
`CDataFormer.SummarizeRoleTypes()` join multi-value fields with `<br>` so
the value can be dropped straight into an HTML table cell, and that same
joined string is then reused, unchanged, as the JSON value for the same
section (`SetSection`/`Sections[key]`). A third, unreported instance of
the identical pattern was found during triage: `CDataFormer.SetGateHosts()`
joins SOBR gateway hostnames with `<br>`, feeding both HTML and the
`Sections[...]` JSON entry written by `CRepoTable.cs`.

There is no single choke point for JSON section values: `SetSection`/
`Sections[key] = new HtmlSection {...}` is duplicated across six files
(`CHtmlTables.cs`, `CRegKeysTable.cs`, `CManagedServerTable.cs`,
`CRepoTable.cs`, `CProxyTable.cs`, `CJobSummaryInfoTable.cs`), each with
its own local helper.

## Considered Options

### Option A — Sanitize at each JSON-capture call site

Strip/replace `<br>` with a JSON-safe delimiter immediately before each of
the (at least) three affected `SetSection` calls.

**Rejected.** Fixes only the symptom, at a choke point that doesn't
actually exist as a single shared function. Six-plus separately-duplicated
`SetSection` helpers mean the same patch would need reapplying wherever a
new HTML-formatted value is captured for JSON in the future — the bug
class would recur.

### Option B — Consolidate all `SetSection` call sites into one shared helper first, then sanitize once

**Rejected for this change.** A real refactor, unrelated to what either
issue asked for, and it expands the diff far beyond a JSON-contract bug
fix.

### Option C — Fix the producers: they emit a delimiter-neutral value; HTML rendering converts to `<br>` only when building markup (chosen)

## Decision

`RegOptions()`, `SummarizeRoleTypes()`, and `SetGateHosts()` join their
multi-value output with `|`, not `<br>`. Each HTML call site that
previously consumed the `<br>`-joined string directly now replaces `|`
with `<br>` when building the table cell markup; the JSON path captures
the `|`-delimited value unchanged.

## Rationale

- Matches the issue's own diagnosis: values were "formatted for HTML
  rendering first... then captured into JSON as-is." Fixing the producer
  fixes the actual defect, not just its two currently-reported symptoms.
- `|` keeps the JSON value a plain string (no schema/type change from
  `string` to `string[]`), so existing consumers parsing these fields as
  strings are unaffected beyond the delimiter character itself.
- Covers the third, unreported `SetGateHosts` instance for free, since it
  shares the same producer-level fix.

## Consequences

### Positive

- The bug class (HTML-formatted value reused verbatim for JSON) is fixed
  at its source for all three known instances, not patched per-callsite.
- No change to the `HtmlSection`/`SetSection` contract itself.

### Negative

- HTML call sites for these three values now carry an explicit `|` →
  `<br>` conversion step that wasn't there before — a future reader of
  `RegOptions()`/`SummarizeRoleTypes()`/`SetGateHosts()` in isolation will
  see a `|`-delimited value and need to know it's meant to render as an
  HTML line-break list.
- Does not address the six-plus duplicated `SetSection` helpers themselves
  (see Option B) — a fourth instance of this bug class, in a producer not
  yet identified, remains possible until that duplication is addressed.

## Validation

None needed beyond compilation and the new xUnit facts covering each of
the three producers' JSON output (no `<br>` present) and each HTML call
site (line-break rendering unchanged).

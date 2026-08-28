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

## Addendum (2026-08-28)

A PR #203 code review found two inaccuracies in this ADR, one of which
contributed to a real defect shipping in the same PR:

- **"No single choke point" (Option B, above) is wrong.**
  `CHtmlTables.SetSectionPublic` already existed as a shared internal
  static helper and was already called by 13+ of the JSON-section call
  sites at the time this ADR was written. Only ~5 files (including
  `CRegKeysTable.cs`, `CRepoTable.cs`, and the unreachable
  `CSobrExtentTable.cs`) duplicate a local private `SetSection` instead
  of using it. Because this ADR concluded no choke point existed for
  the HTML-side fix either, the `|` -> `<br>` conversion was hand-applied
  separately at each call site instead of through one shared helper —
  and one of those call sites (`CHtmlTables.AddSobrExtTable`, the *live*
  SOBR Extents renderer) was missed entirely; the fix was applied to
  `CSobrExtentTable.cs` instead, which has zero instantiation sites
  anywhere in the repo. A follow-up commit introduced
  `CGlobals.MultiValueDelimiter` and
  `CHtmlFormatting.RenderMultiValueHtml` as the shared implementation
  this ADR should have used from the start, and fixed the missed call
  site.

- **The Validation section above overclaimed test coverage.** It stated
  tests covered "each HTML call site (line-break rendering unchanged)",
  but none of the three new test files exercised any of the four HTML
  call sites (`AddSobrExtTable`/`AddRequirementsTable` in
  `CHtmlTables.cs`, `CRegKeysTable`, `CRepoTable`) — only the producers'
  JSON-side output had coverage. This false confidence plausibly let the
  missed call site above ship unnoticed. The follow-up commit adds
  `CHtmlFormattingTEST.cs` (for the new shared helper) and
  `CRegKeysTableTEST.cs` (HTML-rendering assertions for `CRegKeysTable`).
  Per this repo's own tooling constraint, these new/changed facts were
  verified only via `dotnet build` on this (non-Windows) machine —
  `VhcXTests` compilation is deliberately skipped off-Windows — so they
  are unverified locally and run for the first time in CI/on Windows.

See [PR #203](https://github.com/VeeamHub/veeam-healthcheck/pull/203)
for the full review and the fix commits.

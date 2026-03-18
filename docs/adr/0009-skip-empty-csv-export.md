# ADR 0009: Skip CSV Export When Collector Produces No Records

* **Status:** Accepted
* **Date:** 2026-03-09
* **Decider:** Ben Thomas (@comnam90)
* **Consulted:** Claude Code (architecture review)

## Context and Problem Statement

`Export-VhciCsv` is a generic pipeline utility that buffers all input objects and writes
them to a CSV file via `Export-Csv`. When a collector produces no data — which is normal
for optional features (Cloud Connect, SureBackup, Tape, etc.) not configured on a given
VBR installation — the pipeline delivers zero objects to `Export-VhciCsv`. In this case
`Export-Csv` still creates the output file, but writes **0 bytes** (no header, no rows).

The report compilation pipeline in C# (`CCsvReader.FileFinder`) finds any file matching
the expected name pattern. A 0-byte file is found and a non-null `CsvReader` is returned.
`CCsvParser` then calls `GetRecords<T>()` on that empty file. CsvHelper expects at least
a header row on the first read; with 0 bytes it throws `CsvHelperException: No header
record was found`, crashing the report compiler for that section.

This is distinct from the "collector failed" case tracked by ADR 0007: here the collector
succeeded — it correctly determined that no data exists — but the 0-byte artefact it
leaves behind is misread by the compiler as a corrupt file.

## Decision Drivers

- **Correctness:** A missing file and a 0-byte file must have the same meaning downstream:
  no data to show. The C# pipeline already handles a missing file (returns null, renders
  an empty section). It must not be forced to also handle a 0-byte file.
- **Minimal change:** The fix should be local to `Export-VhciCsv` with no caller changes.
- **Manifest compatibility:** The "ran but produced no data" state is already represented
  in `_CollectionManifest.csv` (`Success=True`, `DurationSeconds` recorded). The file
  system does not need to duplicate this signal.

## Considered Options

### Option A — Skip writing when record count is zero (chosen)

Guard at the top of the `end` block: if `$allObjects.Count -eq 0`, log a warning and
return without calling `Export-Csv`. No file is written. `CCsvReader.FileFinder` finds
nothing, returns null, and `CCsvParser` renders an empty section — the same code path
already exercised by collectors whose CSV was never produced.

**Accepted.**

### Option B — Write a header-only CSV

Write the CSV header row (column names) even when there are no data rows. CsvHelper can
read a header-only file without throwing, returning an empty enumerable for typed records.

**Rejected.** `Export-VhciCsv` is schema-agnostic: it derives the header from the
property names of the first piped object. With zero objects there is no schema available.
Supporting this would require adding an explicit `[string[]]$Headers` parameter to
`Export-VhciCsv` and updating every caller to pass the schema — duplicating information
already encoded in the PowerShell object structure and the C# data-type mapping.

## Decision

Option A. `Export-VhciCsv` skips file creation when no objects were received. A `WARN`
log entry is written so the absence is visible in the collection log without being treated
as an error. No changes to callers are required.

## Consequences

* **Good:** CsvHelper never sees a 0-byte file; no `No header record was found` exception.
* **Good:** Fix is entirely local to `Export-VhciCsv`; zero caller changes.
* **Good:** `_CollectionManifest.csv` continues to be the authoritative record of
  collector run status; the file system is not a parallel truth source.
* **Good:** The WARN log makes "skipped because empty" visible and distinguishable from
  "file was never attempted".
* **Neutral:** The existing convention — missing file = no data, handled by null-check
  in `CCsvParser` — now applies to the zero-record case as well as the file-not-found case.
* **Bad:** A caller that expects a file to always be written (e.g., for downstream file
  existence checks outside C#) will silently get nothing. No such caller exists today;
  this risk is documented here to prevent future assumptions.

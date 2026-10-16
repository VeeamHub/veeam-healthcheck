# Scrubbed report

A user who must share a report outside the customer environment runs with scrubbing on. VHC writes an anonymized copy in which job, repository, proxy and server names are replaced, while version and structure remain.

## Sub-features

- `scrub-html` writes `vHC-AnonymousReport/Veeam Health Check Report_VBR_anon_<timestamp>.html`.
- `scrub-json` writes the matching anonymized `.json`.
- `scrub-names-removed` removes identifying names that exist in the input CSVs.
- `scrub-pptx-skipped` ignores `/pptx` when scrubbing (a warning is logged).

## How to get to it (user POV)

- CLI: `VeeamHealthCheck.exe /run /scrub:true` (live) or `/import=<path> /run /scrub:true` (from CSVs).
- GUI scrub toggle: **not drivable here**, see [gui-and-collection](./gui-and-collection.md).

## Driving it with vhc-verify.sh

Preconditions:

- Same as [csv-import](./csv-import.md).

- **Run the scrubbed import.** Run `.claude/skills/verify-vhc/scripts/vhc-verify.sh import-scrub <ID>`. Output ends with `IMPORT-REPORT: verified` and `assertions: 9/9 passed`.
- **Names absent.** The helper asserts these strings from the input CSVs are NOT in the anonymized HTML: `Production-VM-Backup`, `SQL-Servers-Backup`, `Linux Hardened Repo`, `PROXY01`, `VEEAM-VBR01`.
- **Structure retained.** The HTML still has `<title>Veeam HealthCheck Report</title>` and the VBR version `12.1.2.172`; sections are generic labels such as `Job 1`, `Repository 1`.
- **Proof.** Keep `.verify-vhc/evidence/<ID>/output/vHC-AnonymousReport/` and `MANIFEST.sha256`. A side effect to note: the run writes `Original\vHC_KeyFile.xml|json` (the scrub mapping) under `/outdir`.

## Gotchas

- The scrub key files hold the real-to-anonymous mapping. With fixture data that is harmless; on a real customer run treat them as sensitive and keep them out of any shared evidence.
- Off Windows, stdout shows many `[Scrub] Failed to restrict key-file ACLs` lines because the ACL hardening is Windows-only. The key files are still written; this is expected here and a thing to check on Windows.
- `/pptx` and per-job HTML exports are skipped under scrub by design. Do not report them as failures.
- Absence of a few known names proves those names are scrubbed, not that every identifier is. Add a fixture string to the assertion list when a change touches a new field.
- Also logs `Failed to load VBR CSV data or no data found.` once before the scrubbed report is built; the report is still produced.

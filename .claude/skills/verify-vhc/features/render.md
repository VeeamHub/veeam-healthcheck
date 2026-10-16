# Report render (browser smoke)

A reader opens the generated HTML report in a browser. `import-report` proves the HTML *contains* the right strings; `render` proves the page actually works when loaded: scripts run, the sidebar links land on their sections, each section header collapses and expands, and the core sections are not empty.

## Sub-features

- `render-console` fails on any `console.error`, uncaught page error or failed sub-request.
- `render-nav` clicks every `a[href^="#"]`; the target element must exist and be visible afterwards.
- `render-toggle` clicks every `.section-header[onclick*="toggleSection"]`; the card's `open` state **and** the body's visibility must flip, and a second click must restore them.
- `render-core` requires the six core VBR sections to exist with at least one body row (a table data row, or a status tile for Security Summary): `license`, `vbrserver`, `secsummary`, `proxies`, `repos`, `jobsummary`.
- `render-screenshot` saves a full-page PNG per report.

## How to get to it (user POV)

Open `Veeam Health Check Report_VBR_*.html` in any browser.

## Driving it with vhc-verify.sh

Preconditions:

- `bun` on PATH. A browser: on macOS with Google Chrome installed the helper uses it (`REPORT_SMOKE_CHANNEL=chrome`); elsewhere run `cd tools/report-smoke && bun install --frozen-lockfile && bunx playwright install chromium` once.

- **Render the import output.** `.claude/skills/verify-vhc/scripts/vhc-verify.sh render <ID>` runs `import-report <ID>` and `import-scrub <ID>-scrub` if their output is missing, then opens every html in both `vHC-Report/` and `vHC-AnonymousReport/` (the VBR and the VB365 sibling, plain and scrubbed = 4 files). Output ends with `report-smoke: 4/4 reports clean` and `RENDER: verified`.
- **Screenshots.** `.verify-vhc/evidence/<ID>/render/*.png` (full page, 1440 px wide).
- **Any report, directly.** `bun tools/report-smoke/smoke.ts [--out <dir>] [--profile auto|vbr|none] <report.html|dir> ...` exits 1 if any report has a finding. This is how CI checks the real reports from the live integration jobs (`report-render-smoke` job in `ci-cd.yaml`).
- **Prove the tool can fail.** `cd tools/report-smoke && bun selftest.ts <clean-report.html>` writes five copies (clean, injected `<script>throw new Error('x')</script>`, Proxy Info section removed, nav target id renamed, `toggleSection` disabled) and requires exit 0 for the clean one and exit 1 for each broken one.

## Gotchas

- The product profile is detected from the page header (`Backup & Replication` means the VBR core list applies). VB365 and unknown reports are still console/nav/toggle checked, but have no core-section list yet (`core=0` in the output).
- The VB365 sibling report that the CLI writes for VBR-only golden input has 13 nav links and no collapsible sections; that is expected.
- `@media print` forces every `.section-body` visible; the smoke runs with screen media, so a collapsed section is genuinely hidden.
- The 3 real CI reports used while developing this are internal lab data and are never committed.

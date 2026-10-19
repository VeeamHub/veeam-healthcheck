#!/usr/bin/env bash
# vhc-verify.sh - drive Veeam Health Check (VHC Core) off Windows.
#
# Subcommands:
#   build                 dotnet build the solution (Debug); reverts the csproj version auto-bump
#   doctor                read-only readiness check; exit 0 = worth driving
#   import-report [ID]    run the CLI in /import mode against the checked-in golden CSVs,
#                         then assert on the generated HTML/JSON; evidence -> .verify-vhc/evidence/ID/
#   import-scrub [ID]     same, with /scrub:true; asserts identifying names are ABSENT from the output
#   tests [ID]            run the xUnit projects (VhcXTests, CrossPlatform, headless GUI); evidence -> .verify-vhc/evidence/ID/
#   cli-checks [ID]       /help, no-action (/bogus) and bad /import path, asserted on log lines, exit code and absent output
#   cli-matrix [ID]       /import combined with /lite /security /days:N /debug /show:* /pptx /scrub:false; per-flag output/log assertions
#   import-layouts [ID]   /import over every documented CSV folder layout; asserts byte-identical reports
#   render [ID]           open the import-report output (plain + scrubbed) in a browser via tools/report-smoke
#   ci [ID]               build, import-report, import-scrub, cli-checks, cli-matrix, import-layouts, render (what crossplatform-tests.yml runs)
#   pr <N|branch> [opts]  verify a pull request: did its green checks exercise what it changed? Writes
#                         .verify-vhc/evidence/pr-N/report.md + report.json; exit 0 only on VERIFIED
#                         (1 FAILED, 2 NEEDS-RUN, 3 NEEDS-HUMAN). Options: --run (run the missing local checks),
#                         --dispatch (ci-cd.yaml on the PR branch if live-lab checks are missing), --comment (one
#                         sticky PR comment), --repo OWNER/REPO, --wait MINUTES. Needs pwsh 7 and gh.
#   cleanup [ID]          stop only the process this skill started, remove scratch input; KEEPS evidence
#
# Everything is repo-relative. State lives in .verify-vhc/ (git-ignored).

set -uo pipefail

ROOT="$(git rev-parse --show-toplevel)"
cd "$ROOT" || exit 2

CSPROJ="vHC/HC_Reporting/VeeamHealthCheck.csproj"
DLL="vHC/HC_Reporting/bin/Debug/net10.0-windows7.0/VeeamHealthCheck.dll"
FIXTURES="vHC/HC_Reporting/Tools/GoldenBaselines"
STATE=".verify-vhc"
PIDFILE="$STATE/run.pid"
TIMEOUT_SECS="${VHC_VERIFY_TIMEOUT:-180}"

mkdir -p "$STATE/evidence"

say() { printf '%s\n' "$*"; }
fail() { printf 'FAIL: %s\n' "$*" >&2; exit 1; }

note_csproj_state() {
  if git diff --quiet -- "$CSPROJ"; then CSPROJ_WAS_CLEAN=1; else CSPROJ_WAS_CLEAN=0; fi
}

revert_csproj_if_we_dirtied() {
  # The build auto-increments the csproj version segment (CLAUDE.md says to revert it).
  # Only revert when it was clean before we started, so we never discard a real edit.
  if [ "${CSPROJ_WAS_CLEAN:-0}" = "1" ] && ! git diff --quiet -- "$CSPROJ"; then
    git checkout -- "$CSPROJ"
    say "reverted auto-bumped $CSPROJ"
  fi
}

cmd_build() {
  note_csproj_state
  dotnet build vHC/HC.sln --configuration Debug 2>&1 | tail -n 4
  rc=${PIPESTATUS[0]}
  revert_csproj_if_we_dirtied
  [ "$rc" -eq 0 ] || fail "dotnet build exited $rc"
  [ -f "$DLL" ] || fail "build succeeded but $DLL is missing"
  say "build ok: $DLL"
}

cmd_doctor() {
  ok=1
  sdk="$(dotnet --version 2>/dev/null || true)"
  case "$sdk" in
    10.*|1[1-9].*) say "ok   dotnet SDK $sdk" ;;
    *) say "BAD  need .NET SDK 10+, found '${sdk:-none}'"; ok=0 ;;
  esac

  if [ -f "$DLL" ]; then
    say "ok   built binary present: $DLL"
    stale="$(find vHC/HC_Reporting -name '*.cs' -not -path '*/obj/*' -not -path '*/bin/*' -newer "$DLL" | head -n 1)"
    if [ -n "$stale" ]; then
      say "BAD  binary older than source ($stale); run: $0 build"; ok=0
    else
      say "ok   binary is newer than all sources"
    fi
  else
    say "BAD  no built binary; run: $0 build"; ok=0
  fi

  missing=0
  for f in _Jobs.csv _Proxies.csv _Repositories.csv _Servers.csv _vbrinfo.csv; do
    if [ ! -f "$FIXTURES/VBRConfig/$f" ]; then say "BAD  fixture missing: $FIXTURES/VBRConfig/$f"; missing=1; fi
  done
  if [ "$missing" -eq 0 ]; then say "ok   golden CSV fixtures present"; else ok=0; fi

  if [ -f "$PIDFILE" ]; then
    pid="$(cat "$PIDFILE")"
    if kill -0 "$pid" 2>/dev/null; then
      say "BAD  a previous run (pid $pid) is still alive; run: $0 cleanup"; ok=0
    else
      say "warn stale pidfile for dead pid $pid (cleanup removes it)"
    fi
  else
    say "ok   no verification process running"
  fi

  if git diff --quiet -- "$CSPROJ"; then
    say "ok   csproj clean (no un-reverted version bump)"
  else
    say "warn csproj is modified (build auto-bump?): git diff -- $CSPROJ"
  fi
  say "info os=$(uname -s) arch=$(uname -m); a real GUI window cannot open here, but the headless GUI suite runs (tests); see features/gui-and-collection.md"
  if [ "$ok" -eq 1 ]; then say "DOCTOR: ready"; else say "DOCTOR: not ready"; exit 1; fi
}

# ---------------------------------------------------------------------------
# Shared helpers
# ---------------------------------------------------------------------------

PASS=0; TOTAL=0
check_reset() { PASS=0; TOTAL=0; }
ok_if() {   # ok_if <description> <exit-status-of-test>
  TOTAL=$((TOTAL + 1))
  if [ "$2" -eq 0 ]; then PASS=$((PASS + 1)); say "  PASS  $1"; else say "  FAIL  $1"; fi
}
has()    { grep -q -F -- "$2" "$1"; ok_if "contains '$2'" $?; }
hasnt()  { if grep -q -F -- "$2" "$1"; then ok_if "absent   '$2'" 1; else ok_if "absent   '$2'" 0; fi; }
finish() {  # finish <label>
  say "assertions: $PASS/$TOTAL passed"
  [ "$PASS" -eq "$TOTAL" ] || fail "$1: assertions failed"
}

# stage_golden <dir>: copy the tracked golden CSVs (never the originals) flat into <dir>
stage_golden() {
  mkdir -p "$1"
  cp "$FIXTURES"/VBRConfig/*.csv "$FIXTURES"/NasInfo/*.csv "$FIXTURES"/SessionReports/*.csv "$1/"
}

# run_cli <logfile> <args...>: run the real CLI in the background with the recorded pid and timeout.
# Sets RC. VHC_EXPERIMENTS is unset so the default (experiments off) report is what gets checked.
run_cli() {
  logf="$1"; shift
  [ -f "$DLL" ] || fail "no binary; run: $0 build"
  env -u VHC_EXPERIMENTS dotnet "$ROOT/$DLL" "$@" > "$logf" 2>&1 &
  pid=$!
  echo "$pid" > "$PIDFILE"
  waited=0
  while kill -0 "$pid" 2>/dev/null; do
    if [ "$waited" -ge "$TIMEOUT_SECS" ]; then
      kill "$pid" 2>/dev/null; sleep 1; kill -9 "$pid" 2>/dev/null
      fail "run exceeded ${TIMEOUT_SECS}s; killed pid $pid"
    fi
    sleep 1; waited=$((waited + 1))
  done
  wait "$pid"; RC=$?
  rm -f "$PIDFILE"
}

# Full VBR report (not the /security one) and its json, in report dir $1 ($2 = file glob suffix)
vbr_file() { find "$1" -maxdepth 1 -name "Veeam Health Check Report_VBR_*.$2" 2>/dev/null | grep -v '_VBR_Security_' | head -n 1; }
vb365_file() { find "$1" -maxdepth 1 -name "Veeam Health Check Report_VB365_*.$2" 2>/dev/null | head -n 1; }

# Every in-page nav anchor (<a href="#id">) must have an element with that id.
nav_sections_present() {  # nav_sections_present <html>
  python3 -I - "$1" <<'PY'
import re, sys
h = open(sys.argv[1], encoding="utf-8").read()
hrefs = {m for m in re.findall(r'<a [^>]*href="#([^"]+)"', h) if "'" not in m and "+" not in m}
ids = set(re.findall(r'\bid="([^"]+)"', h))
missing = sorted(hrefs - ids)
print(f"nav anchors: {len(hrefs)}, missing section ids: {missing or 'none'}")
sys.exit(1 if missing or not hrefs else 0)
PY
}

cmd_import_report() {
  id="${1:-$(date +%Y%m%d-%H%M%S)}"
  scrub="${2:-false}"   # "true" exercises the anonymizing (scrub) path
  if [ "$scrub" = "true" ]; then rdir="vHC-AnonymousReport"; else rdir="vHC-Report"; fi
  ev="$STATE/evidence/$id"
  in="$STATE/scratch/$id/input"
  out="$ev/output"
  mkdir -p "$in" "$out"

  # Stage a COPY of the fixtures so the tracked files can never be touched by the run.
  stage_golden "$in"
  say "staged $(ls "$in" | wc -l | tr -d ' ') fixture CSVs into $in"

  # The CLI wants absolute paths for /import and /outdir.
  abs_in="$ROOT/$in"
  abs_out="$ROOT/$out"
  say "run: dotnet $DLL /import=<repo>/$in /outdir=<repo>/$out /run /scrub:$scrub"
  say "dotnet $DLL /import=$in /outdir=$out /run /scrub:$scrub (paths relative to repo root)" > "$ev/command.txt"
  run_cli "$ev/stdout.log" "/import=$abs_in" "/outdir=$abs_out" /run "/scrub:$scrub"
  echo "$RC" > "$ev/exit_code.txt"
  say "exit code: $RC (after ${waited}s)"
  [ "$RC" -eq 0 ] || fail "CLI exited $RC; see $ev/stdout.log"

  html="$(vbr_file "$out/$rdir" html)"
  json="$(vbr_file "$out/$rdir" json)"
  [ -n "$html" ] || fail "no VBR html report under $out/$rdir"
  [ -n "$json" ] || fail "no VBR json report under $out/$rdir"
  say "html: $(wc -c < "$html" | tr -d ' ') bytes"

  # Assertions: the report must reflect the INPUT csv rows, not just exist.
  check_reset
  has "$html" "<title>Veeam HealthCheck Report</title>"
  has "$html" "12.1.2.172"             # _vbrinfo.csv Version (not anonymized)
  # Orphaned & Superseded Backups ships dark behind VHC_EXPERIMENTS: no section card, no nav link.
  hasnt "$html" 'id="orphanedsupersededbackups"'
  hasnt "$html" 'href="#orphanedsupersededbackups"'
  if [ "$scrub" = "true" ]; then
    # Scrub must remove identifying names that exist in the input CSVs.
    hasnt "$html" "Production-VM-Backup"  # _Jobs.csv row 1
    hasnt "$html" "SQL-Servers-Backup"    # _Jobs.csv row 4
    hasnt "$html" "Linux Hardened Repo"   # _Repositories.csv row 3
    hasnt "$html" "PROXY01"               # _Proxies.csv row 1
    hasnt "$html" "VEEAM-VBR01"           # _vbrinfo.csv SqlServer / host name
  else
    has "$html" "Production-VM-Backup"
    has "$html" "SQL-Servers-Backup"
    has "$html" "Linux Hardened Repo"
    has "$html" "PROXY01"
  fi
  nav_out="$(nav_sections_present "$html")"; nav_rc=$?
  ok_if "every nav href has a section with that id ($nav_out)" "$nav_rc"
  python3 -I -c "import json,sys; d=json.load(open(sys.argv[1])); assert d['Sections'] and 'VhcVersion' in d" "$json"
  ok_if "json parses and has Sections + VhcVersion" $?

  # Tracked fixtures must be byte-identical afterwards.
  git diff --quiet -- "$FIXTURES"; ok_if "tracked fixtures untouched" $?

  ( cd "$out" && find . -type f -print0 | xargs -0 shasum -a 256 ) > "$ev/MANIFEST.sha256"
  say "manifest: $ev/MANIFEST.sha256"
  say "evidence: $ev"
  finish "import-report"
  say "IMPORT-REPORT: verified"
}

# ---------------------------------------------------------------------------
# cli-checks: parse-level behaviour, asserted on log lines + exit code + absent output
# ---------------------------------------------------------------------------
cmd_cli_checks() {
  id="${1:-$(date +%Y%m%d-%H%M%S)}"
  ev="$STATE/evidence/$id/cli-checks"
  mkdir -p "$ev"
  check_reset

  say "[/help]"
  run_cli "$ev/help.log" /help
  ok_if "/help exits 0 (got $RC)" "$([ "$RC" -eq 0 ]; echo $?)"
  has "$ev/help.log" "Veeam Health Check - Command Line Help"
  for f in "  /run" "  /import" "  /scrub:true" "  /outdir=<path>" "  /lite" "  /security"; do has "$ev/help.log" "$f"; done

  say "[/bogus: unknown flag, no action]"
  run_cli "$ev/bogus.log" /bogus "/outdir=$ROOT/$ev/bogus-out"
  ok_if "/bogus exits 0 (got $RC)" "$([ "$RC" -eq 0 ]; echo $?)"
  has "$ev/bogus.log" "No action specified - nothing was collected or reported."
  has "$ev/bogus.log" "Add /run to execute a health check, or /gui to open the interface."
  [ ! -d "$ev/bogus-out/vHC-Report" ]; ok_if "no vHC-Report written for /bogus" $?

  say "[bad /import path]"
  run_cli "$ev/badimport.log" "/import=/nonexistent-dir-vhc-verify" "/outdir=$ROOT/$ev/bad-out" /run
  ok_if "bad /import exits 0 (EntryPoint swallows it; got $RC)" "$([ "$RC" -eq 0 ]; echo $?)"
  has "$ev/badimport.log" "Import path does not exist: /nonexistent-dir-vhc-verify"
  has "$ev/badimport.log" "Failed to resolve import path. Exiting."
  [ ! -d "$ev/bad-out/vHC-Report" ]; ok_if "no vHC-Report written for bad /import" $?

  say "[empty dir /import: no CSVs]"
  mkdir -p "$ev/empty-in"
  run_cli "$ev/emptyimport.log" "/import=$ROOT/$ev/empty-in" "/outdir=$ROOT/$ev/empty-out" /run
  has "$ev/emptyimport.log" "Failed to resolve import path. Exiting."
  [ ! -d "$ev/empty-out/vHC-Report" ]; ok_if "no vHC-Report written for CSV-less /import dir" $?

  say "[/import:<path> colon form]"
  stage_golden "$ev/colon-in"
  run_cli "$ev/colon.log" "/import:$ROOT/$ev/colon-in" "/outdir=$ROOT/$ev/colon-out" /run
  [ -n "$(vbr_file "$ev/colon-out/vHC-Report" html)" ]; ok_if "colon form produced a VBR html report (exit $RC)" $?

  rm -r "$ev/colon-in" "$ev/empty-in" 2>/dev/null
  say "evidence: $ev"
  finish "cli-checks"
  say "CLI-CHECKS: verified"
}

# ---------------------------------------------------------------------------
# cli-matrix: flags combined with /import. Semantics from CArgsParser.ParseAllArgs.
# ---------------------------------------------------------------------------
matrix_case() {  # matrix_case <name> <flags...>   -> runs; sets MOUT (outdir), MLOG
  name="$1"; shift
  MOUT="$ROOT/$mev/$name"; MLOG="$mev/$name.log"
  run_cli "$MLOG" "/import=$ROOT/$mev/in" "/outdir=$MOUT" /run "$@"
  say "[$name] flags: $* -> exit $RC"
  ok_if "$name: exit 0 (got $RC)" "$([ "$RC" -eq 0 ]; echo $?)"
}

cmd_cli_matrix() {
  id="${1:-$(date +%Y%m%d-%H%M%S)}"
  mev="$STATE/evidence/$id/cli-matrix"
  mkdir -p "$mev"
  stage_golden "$mev/in"
  check_reset

  # baseline for comparison
  matrix_case baseline
  base_html="$(vbr_file "$MOUT/vHC-Report" html)"
  [ -n "$base_html" ] && [ -n "$(vbr_file "$MOUT/vHC-Report" json)" ]; ok_if "baseline: VBR html + json written" $?
  has "$base_html" "7 day report"
  has "$base_html" 'id="jobsummary"'
  base_dbg="$(grep -c 'DEBUG' "$MLOG")"

  # /lite: still renders the main report (CArgsParser sets RunFullReport); only per-job exports are skipped
  matrix_case lite /lite
  l_html="$(vbr_file "$MOUT/vHC-Report" html)"
  [ -n "$l_html" ] && [ -n "$(vbr_file "$MOUT/vHC-Report" json)" ]; ok_if "lite: VBR html + json written" $?
  has "$l_html" 'id="jobsummary"'

  # /security: security report only, no full VBR report, no json
  matrix_case security /security
  sec_html="$(find "$MOUT/vHC-Report" -maxdepth 1 -name 'Veeam Health Check Report_VBR_Security_*.html' | head -n 1)"
  [ -n "$sec_html" ]; ok_if "security: Security-only html written" $?
  has "$MLOG" "Starting Security Report generation"
  [ -z "$(vbr_file "$MOUT/vHC-Report" html)" ]; ok_if "security: no full VBR report" $?
  [ -z "$(vbr_file "$MOUT/vHC-Report" json)" ]; ok_if "security: no VBR json" $?

  # /days:N: log line + report header
  for d in 7 12 30 90; do
    matrix_case "days$d" "/days:$d"
    has "$MLOG" "Days set to $d"
    has "$(vbr_file "$MOUT/vHC-Report" html)" "$d day report"
  done

  # /debug: DEBUG-level lines appear in the log
  matrix_case debug /debug
  dbg="$(grep -c 'DEBUG' "$MLOG")"
  [ "$dbg" -gt "$base_dbg" ]; ok_if "debug: DEBUG log lines $dbg > baseline $base_dbg" $?
  [ -n "$(vbr_file "$MOUT/vHC-Report" html)" ]; ok_if "debug: VBR html written" $?

  # /show:*: CLI mode forces openexplorer/openhtml off, so these must not hang or launch anything and still render
  for s in files report all; do
    matrix_case "show-$s" "/show:$s"
    [ -n "$(vbr_file "$MOUT/vHC-Report" html)" ]; ok_if "show-$s: VBR html written" $?
  done

  # /scrub:false is the explicit default
  matrix_case scrub-false /scrub:false
  has "$MLOG" "Setting SCRUB = false"
  has "$(vbr_file "$MOUT/vHC-Report" html)" "Production-VM-Backup"

  # /pptx: a real, openable pptx next to the html
  matrix_case pptx /pptx
  pptx="$(find "$MOUT/vHC-Report" -maxdepth 1 -name 'Veeam Health Check Report_VBR_*.pptx' | head -n 1)"
  [ -n "$pptx" ]; ok_if "pptx: VBR pptx written" $?
  python3 -I -c "import sys,zipfile; z=zipfile.ZipFile(sys.argv[1]); assert z.testzip() is None and 'ppt/presentation.xml' in z.namelist()" "$pptx"
  ok_if "pptx: valid OOXML zip with ppt/presentation.xml" $?

  # /pdf is deliberately absent: DinkToPdf's native library crashes the process on non-Windows (exit 134)
  say "note: /pdf is not driven here (native DinkToPdf crash off Windows); see features/cli-arguments.md"

  git diff --quiet -- "$FIXTURES"; ok_if "tracked fixtures untouched" $?
  rm -r "$mev/in" 2>/dev/null
  say "evidence: $mev"
  finish "cli-matrix"
  say "CLI-MATRIX: verified"
}

# ---------------------------------------------------------------------------
# import-layouts: every layout CImportPathResolver documents must yield the same report
# ---------------------------------------------------------------------------
normalize_html() { sed -E 's/Generated [A-Z][a-z]{2} [0-9]{1,2}, [0-9]{4}/Generated X/g' "$1"; }

cmd_import_layouts() {
  id="${1:-$(date +%Y%m%d-%H%M%S)}"
  lev="$STATE/evidence/$id/import-layouts"
  mkdir -p "$lev"
  check_reset
  ts="2026.10.16.120000"

  # name:relative-dir-holding-the-CSVs (Strategy numbers are CImportPathResolver.FindCsvDirectory's)
  layouts="flat:.
vbr-direct:VBR
vbr-server-ts:VBR/VBR01/$ts
original-vbr:Original/VBR
original-vbr-server-ts:Original/VBR/VBR01/$ts
recursive:archive/2026/customer-a/collected"

  ref_html=""; ref_json=""
  while IFS=: read -r name rel; do
    root="$lev/$name-in"
    stage_golden "$root/$rel"
    run_cli "$lev/$name.log" "/import=$ROOT/$root" "/outdir=$ROOT/$lev/$name-out" /run
    h="$(vbr_file "$lev/$name-out/vHC-Report" html)"; j="$(vbr_file "$lev/$name-out/vHC-Report" json)"
    say "[$name] CSVs at <root>/$rel -> exit $RC"
    [ -n "$h" ] && [ -n "$j" ]; ok_if "$name: VBR html + json written" $?
    [ -n "$h" ] || continue
    if [ -z "$ref_html" ]; then
      ref_html="$h"; ref_json="$j"; ref_name="$name"
    else
      diff -q <(normalize_html "$ref_html") <(normalize_html "$h") >/dev/null
      ok_if "$name: html identical to $ref_name" $?
      diff -q <(python3 -I -c "import json,sys; print(json.dumps(json.load(open(sys.argv[1])),sort_keys=True))" "$ref_json") \
              <(python3 -I -c "import json,sys; print(json.dumps(json.load(open(sys.argv[1])),sort_keys=True))" "$j") >/dev/null
      ok_if "$name: json identical to $ref_name" $?
    fi
    rm -r "$root"
  done <<EOF
$layouts
EOF

  git diff --quiet -- "$FIXTURES"; ok_if "tracked fixtures untouched" $?
  say "evidence: $lev"
  finish "import-layouts"
  say "IMPORT-LAYOUTS: verified"
}

# ---------------------------------------------------------------------------
# render: open the generated report in a real browser (tools/report-smoke)
# ---------------------------------------------------------------------------
cmd_render() {
  rid="${1:-$(date +%Y%m%d-%H%M%S)}"
  command -v bun >/dev/null || fail "bun is required for render (https://bun.sh)"
  # Reuse a prior import-report/import-scrub for this ID if present, else generate both.
  [ -d "$STATE/evidence/$rid/output/vHC-Report" ] || cmd_import_report "$rid" false >/dev/null || fail "import-report failed"
  scrub_id="$rid-scrub"
  [ -d "$STATE/evidence/$scrub_id/output/vHC-AnonymousReport" ] || cmd_import_report "$scrub_id" true >/dev/null || fail "import-scrub failed"

  if [ -z "${REPORT_SMOKE_CHANNEL:-}" ] && [ "$(uname -s)" = "Darwin" ] && [ -d "/Applications/Google Chrome.app" ]; then
    export REPORT_SMOKE_CHANNEL=chrome
    say "using system Chrome (REPORT_SMOKE_CHANNEL=chrome)"
  fi
  ( cd tools/report-smoke && bun install --frozen-lockfile >/dev/null ) || fail "bun install failed in tools/report-smoke"
  shots="$ROOT/$STATE/evidence/$rid/render"
  set -o pipefail
  bun tools/report-smoke/smoke.ts --out "$shots" \
    "$STATE/evidence/$rid/output/vHC-Report" "$STATE/evidence/$scrub_id/output/vHC-AnonymousReport" | tee "$STATE/evidence/$rid/render.log"
  rc=$?
  set +o pipefail
  [ "$rc" -eq 0 ] || fail "report-smoke found problems (see above)"
  say "screenshots: $shots"
  say "RENDER: verified"
}

# ---------------------------------------------------------------------------
# ci: the subcommands crossplatform-tests.yml runs
# ---------------------------------------------------------------------------
cmd_ci() {
  cid="${1:-ci-$(date +%Y%m%d-%H%M%S)}"
  cmd_build || exit 1
  ( cmd_import_report "$cid" false ) || exit 1
  ( cmd_import_report "$cid-scrub" true ) || exit 1
  ( cmd_cli_checks "$cid" ) || exit 1
  ( cmd_cli_matrix "$cid" ) || exit 1
  ( cmd_import_layouts "$cid" ) || exit 1
  ( cmd_render "$cid" ) || exit 1
  say "CI: all verified"
}

cmd_tests() {
  id="${1:-$(date +%Y%m%d-%H%M%S)}"
  ev="$STATE/evidence/$id"
  mkdir -p "$ev"
  note_csproj_state
  dotnet test vHC/VhcXTests.CrossPlatform/VhcXTests.CrossPlatform.csproj > "$ev/tests-crossplatform.log" 2>&1; rc1=$?
  dotnet test vHC/VhcXTests/VhcXTests.csproj > "$ev/tests-vhcxtests.log" 2>&1; rc2=$?
  dotnet test vHC/VhcGuiTests/VhcGuiTests.csproj > "$ev/tests-gui.log" 2>&1; rc3=$?
  revert_csproj_if_we_dirtied
  grep -h -E 'Passed!|Failed!' "$ev/tests-crossplatform.log" "$ev/tests-vhcxtests.log" "$ev/tests-gui.log"
  say "exit codes: crossplatform=$rc1 vhcxtests=$rc2 gui=$rc3; logs in $ev"
  if [ "$rc1" -ne 0 ] || [ "$rc2" -ne 0 ] || [ "$rc3" -ne 0 ]; then exit 1; fi
}

cmd_cleanup() {
  if [ -f "$PIDFILE" ]; then
    pid="$(cat "$PIDFILE")"
    # Only stop a pid we recorded, and only if it still looks like our dotnet run.
    if kill -0 "$pid" 2>/dev/null && ps -p "$pid" -o command= | grep -q 'VeeamHealthCheck.dll'; then
      kill "$pid" 2>/dev/null; sleep 1
      if kill -0 "$pid" 2>/dev/null; then kill -9 "$pid" 2>/dev/null; fi
      say "stopped pid $pid"
    fi
    rm -f "$PIDFILE"
  fi
  # Scratch input copies only; evidence stays.
  if [ -n "${1:-}" ]; then rm -r "$STATE/scratch/$1" 2>/dev/null || true; else rm -r "$STATE/scratch" 2>/dev/null || true; fi
  say "cleanup done. evidence kept under $STATE/evidence/"
}

cmd_pr() {
  command -v pwsh >/dev/null || fail "pwsh (PowerShell 7) is required for 'pr'"
  [ $# -ge 1 ] || fail "usage: $0 pr <number|branch> [--run] [--dispatch] [--comment] [--repo OWNER/REPO] [--wait MINUTES]"
  target="$1"; shift
  pargs=()
  while [ $# -gt 0 ]; do
    case "$1" in
      --run) pargs+=(-Run) ;;
      --dispatch) pargs+=(-Dispatch) ;;
      --comment) pargs+=(-Comment) ;;
      --repo) shift; pargs+=(-Repo "${1:?--repo needs OWNER/REPO}") ;;
      --wait) shift; pargs+=(-WaitMinutes "${1:?--wait needs minutes}") ;;
      --self-workflow) shift; pargs+=(-SelfWorkflow "${1:?--self-workflow needs a file name}") ;;
      --out-dir) shift; pargs+=(-OutDir "${1:?--out-dir needs a path}") ;;
      *) fail "unknown option for pr: $1" ;;
    esac
    shift
  done
  exec pwsh -NoProfile -File "$ROOT/.claude/skills/verify-vhc/scripts/Invoke-PrVerify.ps1" "$target" ${pargs[@]+"${pargs[@]}"}
}

case "${1:-}" in
  build) cmd_build ;;
  doctor) cmd_doctor ;;
  import-report) shift; cmd_import_report "${1:-}" false ;;
  import-scrub) shift; cmd_import_report "${1:-}" true ;;
  cli-checks) shift; cmd_cli_checks "$@" ;;
  cli-matrix) shift; cmd_cli_matrix "$@" ;;
  import-layouts) shift; cmd_import_layouts "$@" ;;
  render) shift; cmd_render "$@" ;;
  ci) shift; cmd_ci "$@" ;;
  tests) shift; cmd_tests "$@" ;;
  pr) shift; cmd_pr "$@" ;;
  cleanup) shift; cmd_cleanup "$@" ;;
  *) sed -n '2,25p' "$0"; exit 2 ;;
esac

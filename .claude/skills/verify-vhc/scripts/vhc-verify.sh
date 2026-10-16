#!/usr/bin/env bash
# vhc-verify.sh - drive Veeam Health Check (VHC Core) off Windows.
#
# Subcommands:
#   build                 dotnet build the solution (Debug); reverts the csproj version auto-bump
#   doctor                read-only readiness check; exit 0 = worth driving
#   import-report [ID]    run the CLI in /import mode against the checked-in golden CSVs,
#                         then assert on the generated HTML/JSON; evidence -> .verify-vhc/evidence/ID/
#   import-scrub [ID]     same, with /scrub:true; asserts identifying names are ABSENT from the output
#   tests [ID]            run the cross-platform test projects; evidence -> .verify-vhc/evidence/ID/
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
  say "info os=$(uname -s) arch=$(uname -m); the GUI cannot start here, see features/gui-and-collection.md"
  if [ "$ok" -eq 1 ]; then say "DOCTOR: ready"; else say "DOCTOR: not ready"; exit 1; fi
}

cmd_import_report() {
  id="${1:-$(date +%Y%m%d-%H%M%S)}"
  scrub="${2:-false}"   # "true" exercises the anonymizing (scrub) path
  if [ "$scrub" = "true" ]; then rdir="vHC-AnonymousReport"; rglob="Veeam Health Check Report_VBR_anon_*"; else rdir="vHC-Report"; rglob="Veeam Health Check Report_VBR_*"; fi
  ev="$STATE/evidence/$id"
  in="$STATE/scratch/$id/input"
  out="$ev/output"
  [ -f "$DLL" ] || fail "no binary; run: $0 build"
  mkdir -p "$in" "$out"

  # Stage a COPY of the fixtures so the tracked files can never be touched by the run.
  cp "$FIXTURES"/VBRConfig/*.csv "$FIXTURES"/NasInfo/*.csv "$FIXTURES"/SessionReports/*.csv "$in/"
  say "staged $(ls "$in" | wc -l | tr -d ' ') fixture CSVs into $in"

  # The CLI wants absolute paths for /import and /outdir.
  abs_in="$ROOT/$in"
  abs_out="$ROOT/$out"
  say "run: dotnet $DLL /import=<repo>/$in /outdir=<repo>/$out /run /scrub:$scrub"
  say "dotnet $DLL /import=$in /outdir=$out /run /scrub:$scrub (paths relative to repo root)" > "$ev/command.txt"

  dotnet "$DLL" "/import=$abs_in" "/outdir=$abs_out" /run "/scrub:$scrub" > "$ev/stdout.log" 2>&1 &
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
  wait "$pid"; rc=$?
  rm -f "$PIDFILE"
  echo "$rc" > "$ev/exit_code.txt"
  say "exit code: $rc (after ${waited}s)"
  [ "$rc" -eq 0 ] || fail "CLI exited $rc; see $ev/stdout.log"

  html="$(find "$out/$rdir" -maxdepth 1 -name "$rglob.html" | head -n 1)"
  json="$(find "$out/$rdir" -maxdepth 1 -name "$rglob.json" | head -n 1)"
  [ -n "$html" ] || fail "no VBR html report under $out/$rdir"
  [ -n "$json" ] || fail "no VBR json report under $out/$rdir"
  say "html: $(wc -c < "$html" | tr -d ' ') bytes"

  # Assertions: the report must reflect the INPUT csv rows, not just exist.
  pass=0; total=0
  check() {
    total=$((total + 1))
    if grep -q -F -- "$2" "$1"; then pass=$((pass + 1)); say "  PASS  contains '$2'"; else say "  FAIL  missing  '$2'"; fi
  }
  absent() {
    total=$((total + 1))
    if grep -q -F -- "$2" "$1"; then say "  FAIL  leaked   '$2'"; else pass=$((pass + 1)); say "  PASS  absent   '$2'"; fi
  }
  check "$html" "<title>Veeam HealthCheck Report</title>"
  check "$html" "12.1.2.172"             # _vbrinfo.csv Version (not anonymized)
  if [ "$scrub" = "true" ]; then
    # Scrub must remove identifying names that exist in the input CSVs.
    absent "$html" "Production-VM-Backup"  # _Jobs.csv row 1
    absent "$html" "SQL-Servers-Backup"    # _Jobs.csv row 4
    absent "$html" "Linux Hardened Repo"   # _Repositories.csv row 3
    absent "$html" "PROXY01"               # _Proxies.csv row 1
    absent "$html" "VEEAM-VBR01"           # _vbrinfo.csv SqlServer / host name
  else
    check "$html" "Production-VM-Backup"
    check "$html" "SQL-Servers-Backup"
    check "$html" "Linux Hardened Repo"
    check "$html" "PROXY01"
  fi
  total=$((total + 1))
  if python3 -I -c "import json,sys; d=json.load(open(sys.argv[1])); assert d['Sections'] and 'VhcVersion' in d" "$json"; then
    pass=$((pass + 1)); say "  PASS  json parses and has Sections + VhcVersion"
  else
    say "  FAIL  json structure"
  fi

  # Tracked fixtures must be byte-identical afterwards.
  total=$((total + 1))
  if git diff --quiet -- "$FIXTURES"; then pass=$((pass + 1)); say "  PASS  tracked fixtures untouched"; else say "  FAIL  tracked fixtures modified"; fi

  ( cd "$out" && find . -type f -print0 | xargs -0 shasum -a 256 ) > "$ev/MANIFEST.sha256"
  say "assertions: $pass/$total passed; manifest: $ev/MANIFEST.sha256"
  say "evidence: $ev"
  [ "$pass" -eq "$total" ] || fail "assertions failed"
  say "IMPORT-REPORT: verified"
}

cmd_tests() {
  id="${1:-$(date +%Y%m%d-%H%M%S)}"
  ev="$STATE/evidence/$id"
  mkdir -p "$ev"
  note_csproj_state
  dotnet test vHC/VhcXTests.CrossPlatform/VhcXTests.CrossPlatform.csproj > "$ev/tests-crossplatform.log" 2>&1; rc1=$?
  dotnet test vHC/VhcXTests/VhcXTests.csproj > "$ev/tests-vhcxtests.log" 2>&1; rc2=$?
  revert_csproj_if_we_dirtied
  grep -h -E 'Passed!|Failed!' "$ev/tests-crossplatform.log" "$ev/tests-vhcxtests.log"
  say "exit codes: crossplatform=$rc1 vhcxtests=$rc2; logs in $ev"
  if [ "$rc1" -ne 0 ] || [ "$rc2" -ne 0 ]; then exit 1; fi
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

case "${1:-}" in
  build) cmd_build ;;
  doctor) cmd_doctor ;;
  import-report) shift; cmd_import_report "${1:-}" false ;;
  import-scrub) shift; cmd_import_report "${1:-}" true ;;
  tests) shift; cmd_tests "$@" ;;
  cleanup) shift; cmd_cleanup "$@" ;;
  *) sed -n '2,13p' "$0"; exit 2 ;;
esac

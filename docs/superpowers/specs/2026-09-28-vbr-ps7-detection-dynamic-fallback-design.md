# Design: fix VBR PowerShell 5/7 detection gap in the Dynamic Fallback path

**Date:** 2026-09-28
**Issue:** [#233](https://github.com/VeeamHub/veeam-healthcheck/issues/233) — GUI cold-start/remote runs pick PowerShell 5.1 instead of 7 when local VBR isn't running
**Branch:** `fix/ps7-detection-cold-start` (off `feature/gui-redesign-port`)

## Background

Stage E of the GUI redesign arc (cold-start recovery) was validated end-to-end on real
Windows/VBR lab hardware for the first time this arc. The GUI flow itself worked correctly, but
the actual collection failed: `Import-Module Veeam.Backup.PowerShell` requires PowerShell 7.6+ on
the target VBR 13.1.1.18 server, and the tool launched the collection script under PowerShell
5.1 instead.

## Root cause

`PSInvoker.VbrConfigStartInfo()`/`ConfigStartInfo()` pick PowerShell 7 only when
`CGlobals.VBRMAJORVERSION >= 13`. That field defaults to `0` and is only ever set by
`CClientFunctions.DetectVbrVersion()` succeeding.

`DetectVbrVersion()` has three existing call sites:
1. `ModeCheck()` — only `if (CGlobals.IsVbr)` (true only when `Veeam.Backup.Service` is running
   locally).
2. `RunVbrPreflightGateIfTargeted()` (added in #187, specifically to stop VB365-only runs from
   getting spurious VBR-detection errors) — only `if (CGlobals.EffectiveIsVbr)`, which for
   `TargetProductType == Auto` just returns `CGlobals.IsVbr`. This gate is the "single
   authoritative PS 7.6+ module preflight," called once from `StartCollections()` before any
   collection begins, reached by both the GUI Run button and every CLI run path. Its
   `EffectiveIsVbr`-only gate is deliberate and covered by an existing regression test
   (`RunVbrPreflightGateIfTargeted_TargetProductAutoNoLocalVbr_SkipsVbrDetection`, explicitly
   commented as "the realistic auto-detected VB365-only scenario from the bug report" — i.e.
   #187's own fix).
3. `CArgsParser.DetectVbrVersionIfTargeted()` — called unconditionally after the CLI's
   argument-parsing loop. Only reachable by CLI invocations with explicit arguments; the GUI's
   zero-argument launch never reaches it.

None of these three fire in the actual failing scenario: GUI, `TargetProductType == Auto`
(default, never changed by the user), `REMOTEEXEC == true` (a remote host was selected via
Manage Servers, per `VhcGui.axaml.cs:281`), and no local `Veeam.Backup.Service` process running
(`IsVbr == false`).

`CCollections.ExecPSScripts()` already has its own mechanism for exactly this ambiguity: when
`TargetProductType == Auto && CGlobals.REMOTEEXEC` and nothing was detected locally
(`!runVbr && !runVb365`), it calls `DynamicFallback()`, which runs `TryModuleLoad()` — a bare
**local** `Import-Module {moduleName}` with no server argument and no connection attempt
(`CCollections.cs:804-809`; the "Trying VBR connection to {REMOTEHOST}" log line at `:802` is
misleading — it's testing whether the module is importable on *this* machine, not reachability
of the remote server) — against both the VBR and VB365 modules, to determine which product is
actually installed locally. Once `DynamicFallback()` resolves `runVbr = true`, execution proceeds
straight to `MfaTestPassed(p)` and `ExecVbrScripts(p)` — **nothing re-attempts VBR version
detection**. `CGlobals.VBRMAJORVERSION` stays `0` for the rest of the run.

This local-only nature of `TryModuleLoad()` actually makes the proposed fix safer than it might
first appear: `runVbr = true` already implies the local `Veeam.Backup.PowerShell` module just
imported successfully under whatever local pwsh is present, which means that pwsh already met
the module's minimum version requirement. So `ValidatePowerShellVersionMeetsVbrRequirement()`'s
hard-exit path (see Design section) is practically unreachable from this call site in practice —
it would only fire in the narrow window where local console files exist but are a *different,
newer* VBR version than whatever pwsh version was just proven able to import them, which is an
edge case, not the common case this fix targets.

### Evidence

Two comparison logs from the same binary against the same lab VBR 13.1.1.18 server
(`lab-m01-lvbr01.lab.garagecloud.net`):

- **Failing GUI run** (`Job.HealthCheck_2026.09.28_124202_.log`): `[Dynamic Fallback] VBR module
  load: available` appears, but no `VBR Version:` log line ever appears anywhere in the run
  (detection was never attempted — not attempted-and-failed). Both `TestMfa.ps1` and
  `Get-VBRConfig.ps1` are launched with `-VBRVersion 0` / `-VBRVersion "0"`.
  - `TestMfa.ps1` itself did **not** actually run under the wrong PowerShell —
    `MfaTestPassed()`/`BuildRemoteMfaConnectArgs` hardcode the PS7 executable path regardless of
    version (`CCollections.cs:429`). Its real symptom from `-VBRVersion 0` was a missing
    `-ForceAcceptTlsCertificate` flag (`TestMfa.ps1:129-132` gates that on `$VBRVersion -ge 13`).
    It's `Get-VBRConfig.ps1` (and the NAS collection script) that actually picked PowerShell 5.1,
    via `PSInvoker.VbrConfigStartInfo()`/`ConfigStartInfo()`.
  - The fix's `DetectVbrVersion()` call also sets `CGlobals.PowerShellVersion` (currently `0`,
    will become `7`), which changes branches at `CCollections.cs:433` and `:610` too — this
    matches the CLI path's already-working behavior, it's just worth calling out as an explicit
    side effect rather than a silent one.
- **Working CLI run** (`Job.HealthCheck_2026.09.28_130202_.log`, `/run /vbr /remote
  /host=lab-m01-lvbr01.lab.garagecloud.net`): `VBR Version: 13.1.1.18` is logged twice; both
  scripts are launched with `-VBRVersion 13` / `-VBRVersion "13"`.
- Not an admin/elevation issue — the "Starting Admin Check...done!" step succeeds identically in
  both logs.
- **Real-hardware confirmation:** manually selecting "VBR" (instead of "Auto") in the GUI's
  product-type dropdown forces `CGlobals.TargetProductType = TargetProduct.Vbr`, which makes
  `EffectiveIsVbr` return `true` unconditionally, so `RunVbrPreflightGateIfTargeted()` fires and
  the run correctly uses PowerShell 7 — same behavior as the CLI. This isolates the bug to
  exactly the `Auto`-plus-`DynamicFallback` path and rules out any other explanation (e.g. a
  broken `DetectVbrVersion()` itself, since it works fine once reached).
  - Caveat: this workaround is not fully equivalent to the fixed `Auto` path. With
    `TargetProduct.Vbr`, `EffectiveIsVbr` is `true` for the *entire* run, so other
    `EffectiveIsVbr`-gated behavior (e.g. `GetCsvFileSizesToLog`/`PopulateWaits`,
    `CCollections.cs:161,172`) also runs, which it still won't in the `Auto`+`DynamicFallback`
    case even after this fix — `EffectiveIsVbr` itself is intentionally untouched (see Design).
    The dropdown test isolates and confirms the version-detection bug specifically; it doesn't
    prove the `Auto` path will behave identically to `Vbr` in every respect once this fix lands.

## Approaches considered

1. **(Chosen) Detect version at the point `DynamicFallback()` confirms `runVbr = true`,** inside
   `CCollections.ExecPSScripts()`, before `MfaTestPassed()` runs (that script's `-VBRVersion`/
   `-ForceAcceptTlsCertificate` args need the value too, not just `Get-VBRConfig.ps1`'s).
2. **Widen `RunVbrPreflightGateIfTargeted()`'s own gate** to include `Auto && REMOTEEXEC`.
   Rejected: that gate runs in `StartCollections()`, *before* `DynamicFallback()` has determined
   the real product type. Widening it risks resurrecting the #187 VB365-spam bug and could
   wrongly hard-exit (via `ValidatePowerShellVersionMeetsVbrRequirement()`) a remote VB365-only
   run that never touches VBR at all.
3. **Hoist detection into `VhcGui.SetUiSync()`** (the original idea from the prior session's
   handoff). Rejected: fires before Manage Servers/`REMOTEEXEC` is even resolved in the
   cold-start flow, and doesn't cover the actual gap — the CLI already has its own working
   early-detection path (`DetectVbrVersionIfTargeted()`); this would have added GUI-side work in
   the wrong place without fixing the `DynamicFallback` scenario that actually failed.

## Design

### `CClientFunctions.RunVbrPreflightGateForDynamicFallback()` (new)

```csharp
/// <summary>
/// Unconditional variant of RunVbrPreflightGateIfTargeted(), for callers that have already
/// confirmed VBR is the target through some means other than EffectiveIsVbr — currently only
/// CCollections.ExecPSScripts()'s DynamicFallback-confirmed branch, where a successful local
/// Import-Module probe (TryModuleLoad(), not a remote connection) established that the VBR
/// module is installed locally and therefore VBR is the target worth detecting a version for.
/// </summary>
internal void RunVbrPreflightGateForDynamicFallback()
{
    this.GetVbrVersion();
}
```

`GetVbrVersion()` stays `private`; it already fails soft (catches `DetectVbrVersion()`'s
exception — though note `DetectVbrVersion()` itself logs several ERROR-level lines before
rethrowing, `CClientFunctions.cs:479-482,504-506`, so "fails soft" means the run continues, not
that it fails silently — logs its own message at Debug, and returns) when local VBR detection
genuinely can't succeed — e.g. a true zero-local-Veeam-software box. That's an existing,
documented limitation, not something this fix needs to solve. This lab environment has local
console files present (just no running service), so `DetectVbrVersion()` succeeds by reading
local registry keys.

**Doc comments that become inaccurate and need updating as part of this fix** (all currently
describe `RunVbrPreflightGateIfTargeted()`/`StartCollections()` as the *sole* choke point for
`GetVbrVersion()` — they need to describe two choke points instead):
- `CClientFunctionsGateTests.cs:12-18, 59-60, 75-76` ("the only possible caller at compile time")
- `CClientFunctions.cs:411` ("run once from StartCollections()") and `:439-445`
  (`RunVbrPreflightGateIfTargeted()` is described as `GetVbrVersion()`'s only caller)
- `CArgsParser.cs:351-356` ("enforced exactly once, from StartCollections()")

This is a reasonable pattern (two internal wrappers around one private method, each gated
differently) — it just needs the documentation to say so explicitly rather than silently going
stale.

**Behavior change on the CLI's own Auto+remote path** (e.g. `/run /remote /host=X` without an
explicit `/vbr` flag): `CArgsParser.DetectVbrVersionIfTargeted()` already runs early and sets
`VBRMAJORVERSION` correctly, but since `EffectiveIsVbr` is still false (`IsVbr` is false, no
local service), today's `RunVbrPreflightGateIfTargeted()` in `StartCollections()` skips —
meaning `ValidatePowerShellVersionMeetsVbrRequirement()`'s hard-exit-if-PS7-too-old check has
never actually applied to this specific CLI path. After this fix, `DynamicFallback()` will still
run (since `EffectiveIsVbr` is still false) and the new call site will re-run
`DetectVbrVersion()` (redundant but harmless — same value, one extra "VBR Version:" log line)
and then run `ValidatePowerShellVersionMeetsVbrRequirement()` for the first time on this path.
This is a **beneficial** side effect, not a regression: it closes the same "confusing
Import-Module cascade instead of a clear message" gap that issue #201 already fixed for the
`EffectiveIsVbr == true` case, just extends it to this previously-uncovered Auto+remote CLI
scenario too. Worth noting in the PR description; not worth guarding against.

### Call site: `CCollections.ExecPSScripts()`

```csharp
if (CGlobals.TargetProductType == TargetProduct.Auto && CGlobals.REMOTEEXEC && !runVbr && !runVb365)
{
    (runVbr, runVb365) = this.DynamicFallback();
    if (runVbr)
    {
        using var functions = new CClientFunctions();
        functions.RunVbrPreflightGateForDynamicFallback();
    }
}
```

Matches the existing `using var functions = new CClientFunctions();` pattern already used in
`CClientFunctionsGateTests.cs`. Only called in the branch where `DynamicFallback()` just
confirmed VBR — not for the `runVb365`-only outcome, and not duplicating the earlier
`StartCollections()`-level gate for the already-working `EffectiveIsVbr == true` case (the new
call site requires `!runVbr` to be reached at all, i.e. `!EffectiveIsVbr`, so it can never fire
in the same run as the `StartCollections()` gate — no double-preflight case exists).
`CCollections.cs` doesn't currently have a `using VeeamHealthCheck.Startup;` — needs adding.
Note (corrected after code-quality review): `CImpersonation.cs:11` has the same `using`, but
grepping that file shows its only use of the namespace is a **commented-out** line
(`// CClientFunctions cf = new(); // cf.GetVbrVersion();`) — there is no live, compiled precedent
for `Functions.Collection` reaching into `Startup` anywhere in the repo before this change. This
call site is genuinely the first live instance of that coupling direction, and it creates a
two-way namespace dependency (`CClientFunctions.cs` already depends on `Functions.Collection` via
its own `using`, so this adds the reverse edge). Not a blocking defect — C# doesn't enforce
acyclic namespace graphs, and this was the least-bad option among the alternatives already
rejected above (widening `EffectiveIsVbr` risks the #187 regression; hoisting into
`VhcGui.SetUiSync()` fires too early) — but worth flagging explicitly as a real, accepted
architectural trade-off rather than something that was "just following precedent."

### Out of scope, filed separately: VB365's mirror of this gap

While tracing this, found a related-but-distinct, pre-existing bug: `ExecVb365Scripts()` is
gated on `CGlobals.EffectiveIsVb365` (`CCollections.cs:789`), which stays `false` in the
`Auto`+`DynamicFallback` case exactly like `EffectiveIsVbr` did — meaning when `DynamicFallback()`
resolves a VB365-only or dual VBR+VB365 target, **VB365 collection silently never runs** (a
VB365-only outcome leaves `SCRIPTSUCCESS` false and exits via `WeighSuccessContinuation()`,
`CCollections.cs:369-387`). This is the VB365-side mirror of the exact guard `ExecVbrScripts()`
deliberately dropped for VBR (`CCollections.cs:773-774`), just never fixed for VB365. Not fixed
by this design — different symptom (collection not running at all, vs. running under the wrong
PowerShell version), different code path. Filed separately as
[issue #235](https://github.com/VeeamHub/veeam-healthcheck/issues/235).

## Testing

CI runs on macOS/Linux with no Windows registry or VBR install — the real runtime behavior
(`DetectVbrVersion()` actually succeeding, PS7 actually getting selected) can't be exercised
there, matching every other fix in this arc. Automated coverage is limited to the API-contract
level:

- **Not viable:** a sentinel-based test asserting `RunVbrPreflightGateForDynamicFallback()` "always calls
  `GetVbrVersion()` regardless of `EffectiveIsVbr`" can't actually prove that here, and is
  actively unsafe to run. On a CI runner without VBR, the sentinel value can't distinguish "ran
  and failed" from "skipped" (`CRegReader.cs:233-236` only writes on success — same limitation
  already documented at `CClientFunctionsGateTests.cs:151-158`). Worse: on a Windows dev box that
  *does* have a local VBR 13+ console but an under-versioned pwsh, this call chain reaches
  `ValidatePowerShellVersionMeetsVbrRequirement()` → `Environment.Exit()`
  (`CClientFunctions.cs:618`), which would kill the test host process outright.
- **Instead:** a reflection-only shape test, deliberately never invoking `RunVbrPreflightGateForDynamicFallback()`
  at all — unlike every other test in this file, this method has no skip path (it's a one-line
  `{ this.GetVbrVersion(); }`), and there's no global/`CGlobals` state that can force it onto a
  safe branch, since `CRegReader` reads the real OS registry directly with no test seam. Actually
  invoking it on a machine that genuinely has both a local VBR 13+ console and an under-versioned
  local pwsh (this repo's own lab/dev hardware included) would hit `Environment.Exit()` for real.
  So the test only confirms the method exists as an internal, parameterless instance method on
  `CClientFunctions` via `BindingFlags.NonPublic | BindingFlags.Instance` +
  `MethodInfo.IsAssembly` (internal reports as `Assembly` accessibility) — proving it's reachable
  from `CCollections` (same-assembly `internal`) without ever executing its body. The one-line
  body itself is verified by code review, not a runtime assertion. The call site inside
  `ExecPSScripts()`/`DynamicFallback()` stays untested by automation, same as the rest of that
  method's real-process-launch logic — real verification is the Windows/VBR hardware check below.
- No changes needed to `EffectiveIsVbr`, `RunVbrPreflightGateIfTargeted()`, or their existing
  test *assertions* — this fix adds a new, narrowly-scoped call site rather than modifying the
  existing gate. (Their doc comments do need updating — see Design section above.)

Real hardware verification (already done for this specific fix, ahead of writing the code): the
user manually selected "VBR" in the dropdown on the lab machine and confirmed PowerShell 7 gets
used correctly — isolating the bug to the `Auto`+`DynamicFallback` path. Full verification of the
actual code fix (leaving the dropdown on "Auto" and confirming PS7 is now used) still needs a
real Windows/VBR run once the fix lands.

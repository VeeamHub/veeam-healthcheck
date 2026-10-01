# VBR PS7 Detection Fix (Dynamic Fallback Path) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix issue #233 — when `CCollections.ExecPSScripts()`'s `DynamicFallback()` resolves a remote VBR target in the `Auto`+`REMOTEEXEC`+no-local-detection scenario, `CGlobals.VBRMAJORVERSION` never gets populated, so `PSInvoker` picks PowerShell 5.1 against VBR 13+ servers that require 7.6+.

**Architecture:** Add a new unconditional `CClientFunctions.RunVbrPreflightGateForDynamicFallback()` (thin wrapper around the existing private `GetVbrVersion()`), and call it from `CCollections.ExecPSScripts()` only in the branch where `DynamicFallback()` just confirmed `runVbr = true` — before `MfaTestPassed()` runs, since that script's arguments need the version too. Full rationale, evidence, and rejected alternatives: `docs/superpowers/specs/2026-09-28-vbr-ps7-detection-dynamic-fallback-design.md`.

**Tech Stack:** C# / .NET 8, xUnit, this repo's existing `CClientFunctions`/`CCollections` classes.

---

## Task 1: Add `RunVbrPreflightGateForDynamicFallback()` with a reflection-only contract test

**Files:**
- Modify: `vHC/HC_Reporting/Startup/CClientFunctions.cs:435-436` (insert new method)
- Test: `vHC/VhcXTests/CClientFunctionsGateTests.cs:200-201` (insert new test)

This method has no skip path (unlike every other gate on this class), so the test can never
actually invoke it — see the design spec's Testing section for why (a machine with a real local
VBR 13+ console and an under-versioned pwsh would hit `Environment.Exit()` for real). The test
only proves the method exists with the right shape.

- [ ] **Step 1: Write the failing test**

Open `vHC/VhcXTests/CClientFunctionsGateTests.cs`. Insert this new test method right before the
class's closing `}` (i.e. immediately after the closing `}` of
`EffectiveIsVbr_ForTargetProductAndIsVbr_ReturnsExpected`, which currently ends the class):

```csharp
        [Fact]
        public void RunVbrPreflightGateForDynamicFallback_MethodExists_IsInternalInstanceMethodOnCClientFunctions()
        {
            // Shape/contract test only - deliberately does NOT invoke
            // RunVbrPreflightGateForDynamicFallback(). Unlike every other gate test in this
            // file, this method has no skip path: it's a one-line { this.GetVbrVersion(); }
            // with no CGlobals state that can force it onto a safe branch, since CRegReader
            // reads the real OS registry directly with no test seam. On a machine that
            // genuinely has both a local VBR 13+ console and an under-versioned local pwsh
            // (this repo's own lab/dev hardware included), actually running it would reach
            // ValidatePowerShellVersionMeetsVbrRequirement() -> Environment.Exit() and kill the
            // test host process outright. So this only confirms the method exists, is internal
            // (same-assembly callable from CCollections), and takes no parameters - the
            // one-line body is verified by code review. See #233.
            // Includes Public in the query (unlike a plain NonPublic-only lookup) so that if
            // this method were ever mistakenly made public, GetMethod would still find it and
            // Assert.True(method.IsAssembly) below - not a misleading "method not found" from
            // Assert.NotNull - is the assertion that actually catches the mistake.
            var method = typeof(CClientFunctions).GetMethod(
                "RunVbrPreflightGateForDynamicFallback",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.NotNull(method);
            Assert.True(method.IsAssembly); // C# "internal" reports as Assembly via reflection
            Assert.Empty(method.GetParameters());
        }
```

The file already has `using System.Reflection;` at the top (used by the existing
`GetVbrVersion_MethodVisibility_IsPrivate` test) — no new using needed.

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test vHC/VhcXTests/VhcXTests.csproj --filter "FullyQualifiedName~RunVbrPreflightGateForDynamicFallback_MethodExists"`

Expected: FAIL — `Assert.NotNull(method)` fails because `RunVbrPreflightGateForDynamicFallback` doesn't exist yet
on `CClientFunctions` (`GetMethod` returns `null`).

- [ ] **Step 3: Implement the method**

Open `vHC/HC_Reporting/Startup/CClientFunctions.cs`. Insert this new method between the closing
`}` of `RunVbrPreflightGateIfTargeted()` (currently line 435) and the doc comment for
`GetVbrVersion()` (currently starting line 437):

```csharp

        /// <summary>
        /// Unconditional variant of RunVbrPreflightGateIfTargeted(), for callers that have
        /// already confirmed VBR is the target through some means other than EffectiveIsVbr -
        /// currently only CCollections.ExecPSScripts()'s DynamicFallback-confirmed branch, where
        /// a successful local Import-Module probe (TryModuleLoad(), not a remote connection)
        /// established that the VBR module is installed locally and therefore VBR is worth
        /// detecting a version for. Named for that one caller deliberately: unlike
        /// RunVbrPreflightGateIfTargeted(), this has NO target check at all, so calling it from
        /// anywhere that hasn't already confirmed VBR by some other means - e.g. ModeCheck(),
        /// whose past misuse of the ungated path is exactly what RunVbrPreflightGateIfTargeted()'s
        /// EffectiveIsVbr gate exists to prevent, see GetVbrVersion_MethodVisibility_IsPrivate's
        /// test comment - can reach ValidatePowerShellVersionMeetsVbrRequirement() ->
        /// Environment.Exit() for a run that never touches VBR at all. See issue #233.
        /// </summary>
        internal void RunVbrPreflightGateForDynamicFallback()
        {
            this.GetVbrVersion();
        }
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test vHC/VhcXTests/VhcXTests.csproj --filter "FullyQualifiedName~RunVbrPreflightGateForDynamicFallback_MethodExists"`

Expected: PASS

- [ ] **Step 5: Discard the auto-incremented csproj version bump and commit**

`dotnet test` auto-increments `vHC/HC_Reporting/VeeamHealthCheck.csproj` on every run — this
must never be committed.

```bash
git checkout -- vHC/HC_Reporting/VeeamHealthCheck.csproj
git add vHC/HC_Reporting/Startup/CClientFunctions.cs vHC/VhcXTests/CClientFunctionsGateTests.cs
git commit -m "$(cat <<'EOF'
feat(vbr): add unconditional preflight-gate variant for Dynamic Fallback

RunVbrPreflightGateIfTargeted() only fires when EffectiveIsVbr is true,
which stays false in the Auto+remote+no-local-detection scenario that
DynamicFallback() exists to handle. Add an unconditional variant for
that specific caller to use once it has confirmed VBR by other means.

Part of #233.
EOF
)"
```

---

## Task 2: Wire the new gate into `CCollections.ExecPSScripts()`'s Dynamic Fallback branch

**Files:**
- Modify: `vHC/HC_Reporting/Functions/Collection/CCollections.cs:19-20` (add using)
- Modify: `vHC/HC_Reporting/Functions/Collection/CCollections.cs:313-317` (call site)

This is the actual functional fix. No new automated test — the design spec explains why this
specific call site (inside real PowerShell-process-launch logic) can't be exercised in CI
without a real VBR install; the existing full test suite (Task 2, Step 4 below) proves nothing
else broke, and real verification is a Windows/VBR hardware run after this lands.

- [ ] **Step 1: Add the missing using directive**

Open `vHC/HC_Reporting/Functions/Collection/CCollections.cs`. Find:

```csharp
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Shared.Logging;
```

Replace with:

```csharp
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Shared.Logging;
using VeeamHealthCheck.Startup;
```

- [ ] **Step 2: Wire the call site**

In the same file, find this block inside `ExecPSScripts()` (currently lines 313-317):

```csharp
                    // Dynamic fallback when remote + Auto + no local detection
                    if (CGlobals.TargetProductType == TargetProduct.Auto && CGlobals.REMOTEEXEC && !runVbr && !runVb365)
                    {
                        (runVbr, runVb365) = this.DynamicFallback();
                    }
```

Replace with:

```csharp
                    // Dynamic fallback when remote + Auto + no local detection
                    if (CGlobals.TargetProductType == TargetProduct.Auto && CGlobals.REMOTEEXEC && !runVbr && !runVb365)
                    {
                        (runVbr, runVb365) = this.DynamicFallback();

                        if (runVbr)
                        {
                            // DynamicFallback() only proves the local Veeam.Backup.PowerShell
                            // module is importable - it never calls DetectVbrVersion(), so
                            // CGlobals.VBRMAJORVERSION stays 0 unless detected here. See #233.
                            using var functions = new CClientFunctions();
                            functions.RunVbrPreflightGateForDynamicFallback();
                        }
                    }
```

- [ ] **Step 3: Build**

Run: `dotnet build vHC/HC.sln --configuration Debug`

Expected: Build succeeds, no errors.

- [ ] **Step 4: Run the full test suite**

Run: `dotnet test vHC/VhcXTests/VhcXTests.csproj`

Expected: same pass/fail/skip counts as the pre-change baseline (925 passed, 0 failed, 12
skipped, 937 total, plus the 1 new test from Task 1 → 926 passed, 938 total), confirming this
change didn't break anything else.

- [ ] **Step 5: Discard the auto-incremented csproj version bump and commit**

```bash
git checkout -- vHC/HC_Reporting/VeeamHealthCheck.csproj
git add vHC/HC_Reporting/Functions/Collection/CCollections.cs
git commit -m "$(cat <<'EOF'
fix(vbr): detect PS version when Dynamic Fallback confirms VBR

CGlobals.VBRMAJORVERSION never got populated when a GUI cold-start or
remote run left product type on Auto and had no local VBR/VB365
process running - the exact scenario DynamicFallback() exists to
resolve. This left PSInvoker picking PowerShell 5.1 against VBR 13+
servers that require 7.6+, failing deep inside Import-Module.

Confirmed via two real-hardware log comparisons (GUI run: VBRVersion 0
throughout; CLI run: VBRVersion 13 throughout) and a real-hardware
workaround test (forcing product type to VBR instead of Auto works
correctly today, isolating the bug to this exact code path).

Fixes #233
EOF
)"
```

---

## Task 3: Update doc comments that describe the now-outdated "single caller" invariant

**Files:**
- Modify: `vHC/HC_Reporting/Startup/CClientFunctions.cs:411-426` (RunVbrPreflightGateIfTargeted's doc comment)
- Modify: `vHC/HC_Reporting/Startup/CClientFunctions.cs:437-452` (GetVbrVersion's doc comment)
- Modify: `vHC/VhcXTests/CClientFunctionsGateTests.cs:11-18` (class doc comment)
- Modify: `vHC/VhcXTests/CClientFunctionsGateTests.cs:58-60` (GetVbrVersion_MethodVisibility_IsPrivate's comment)
- Modify: `vHC/VhcXTests/CClientFunctionsGateTests.cs:72-77` (StartCollections_ImportModeEnabled_DoesNotThrow's comment)
- Modify: `vHC/HC_Reporting/Startup/CArgsParser.cs:351-356`

Several existing comments assert `RunVbrPreflightGateIfTargeted()`/`StartCollections()` is the
*sole* choke point for `GetVbrVersion()`. After Task 2, that's no longer true — there are two
choke points now. This task is documentation-only; no behavior changes, no new tests.

- [ ] **Step 1: Update `RunVbrPreflightGateIfTargeted()`'s doc comment**

In `vHC/HC_Reporting/Startup/CClientFunctions.cs`, find:

```csharp
        /// <summary>
        /// Single authoritative PS 7.6+ module preflight gate, run once from StartCollections()
        /// right before the two branches that both lead into real PowerShell-module-based
        /// collection. Gated on EffectiveIsVbr - not IMPORT, not REMOTEEXEC - because this
        /// preflight (and the PowerShell-version check it wraps) is meaningless for a run that
        /// doesn't target VBR: DetectVbrVersion() reads the LOCAL machine's VBR registry keys,
        /// which are legitimately absent on a VB365-only server, and previously spammed 5
        /// ERROR-level log lines for a totally expected condition on every single such run.
        /// Still runs whenever the target includes VBR (TargetProductType Vbr/Both, or Auto with
        /// local VBR actually detected) regardless of REMOTEEXEC: per repo convention, a preflight
        /// on the local PowerShell/module install must run regardless of REMOTEEXEC, since it is
        /// never the remote machine's problem. GetVbrVersion -> DetectVbrVersion throws by design
        /// when local VBR detection fails - not fatal, the preflight just can't run; GetVbrVersion
        /// catches only that expected failure, scoped to the DetectVbrVersion call, so an exception
        /// out of the hard-fail path (ValidatePowerShellVersionMeetsVbrRequirement) is never
        /// mistaken for it and swallowed here too.
        /// </summary>
        internal void RunVbrPreflightGateIfTargeted()
```

Replace with:

```csharp
        /// <summary>
        /// Single authoritative PS 7.6+ module preflight gate, run once from StartCollections()
        /// right before the two branches that both lead into real PowerShell-module-based
        /// collection. Gated on EffectiveIsVbr - not IMPORT, not REMOTEEXEC - because this
        /// preflight (and the PowerShell-version check it wraps) is meaningless for a run that
        /// doesn't target VBR: DetectVbrVersion() reads the LOCAL machine's VBR registry keys,
        /// which are legitimately absent on a VB365-only server, and previously spammed 5
        /// ERROR-level log lines for a totally expected condition on every single such run.
        /// Still runs whenever the target includes VBR (TargetProductType Vbr/Both, or Auto with
        /// local VBR actually detected) regardless of REMOTEEXEC: per repo convention, a preflight
        /// on the local PowerShell/module install must run regardless of REMOTEEXEC, since it is
        /// never the remote machine's problem. GetVbrVersion -> DetectVbrVersion throws by design
        /// when local VBR detection fails - not fatal, the preflight just can't run; GetVbrVersion
        /// catches only that expected failure, scoped to the DetectVbrVersion call, so an exception
        /// out of the hard-fail path (ValidatePowerShellVersionMeetsVbrRequirement) is never
        /// mistaken for it and swallowed here too.
        /// Not the only caller of GetVbrVersion(): CCollections.ExecPSScripts() also calls the
        /// unconditional RunVbrPreflightGateForDynamicFallback() once its own DynamicFallback() has confirmed VBR
        /// as the target by a different means (a local module-import probe, not EffectiveIsVbr) -
        /// see issue #233.
        /// </summary>
        internal void RunVbrPreflightGateIfTargeted()
```

- [ ] **Step 2: Update `GetVbrVersion()`'s doc comment**

In the same file, find:

```csharp
        /// <summary>
        /// Detects the VBR version and required PowerShell version and gates on the PS 7.6+
        /// module requirement. Private: RunVbrPreflightGateIfTargeted() is the only caller,
        /// since it's the single choke point (reached from StartCollections(), itself reached
        /// from both the GUI Run button and every CLI run path) immediately before real
        /// PowerShell-module-based collection begins, and only when EffectiveIsVbr is true.
        /// Every other caller (ModeCheck, RunHotfixDetector, early CLI arg-parsing detection)
        /// must call the ungated DetectVbrVersion instead, so a too-old-PowerShell machine
        /// doesn't hard-exit a feature that never touches the Veeam.Backup.PowerShell module.
        /// Known limitation, not fixed here: when DetectVbrVersion fails (e.g. non-admin
        /// execution, where CRegReader.GetVbrVersionFilePath() returns null), we can't know
        /// whether the local VBR is 13+ at all, so ValidatePowerShellVersionMeetsVbrRequirement
        /// is skipped rather than called - it would no-op anyway, since it gates on
        /// CGlobals.PowerShellVersion, which DetectVbrVersion only ever sets on success. Making
        /// this reachable needs VBR-version detection to work without admin rights first; that's
        /// separate, larger work in CRegReader, not a fix to this gate's catch scope.
        /// </summary>
        private void GetVbrVersion()
```

Replace with:

```csharp
        /// <summary>
        /// Detects the VBR version and required PowerShell version and gates on the PS 7.6+
        /// module requirement. Private: reachable only through its two internal gate wrappers -
        /// RunVbrPreflightGateIfTargeted() (gated on EffectiveIsVbr, the choke point reached from
        /// StartCollections() for both the GUI Run button and every CLI run path) and the
        /// unconditional RunVbrPreflightGateForDynamicFallback() (called only from CCollections.ExecPSScripts()'s
        /// DynamicFallback-confirmed-VBR branch, issue #233) - never called directly. Every other
        /// caller (ModeCheck, RunHotfixDetector, early CLI arg-parsing detection) must call the
        /// ungated DetectVbrVersion instead, so a too-old-PowerShell machine doesn't hard-exit a
        /// feature that never touches the Veeam.Backup.PowerShell module.
        /// Known limitation, not fixed here: when DetectVbrVersion fails (e.g. non-admin
        /// execution, where CRegReader.GetVbrVersionFilePath() returns null), we can't know
        /// whether the local VBR is 13+ at all, so ValidatePowerShellVersionMeetsVbrRequirement
        /// is skipped rather than called - it would no-op anyway, since it gates on
        /// CGlobals.PowerShellVersion, which DetectVbrVersion only ever sets on success. Making
        /// this reachable needs VBR-version detection to work without admin rights first; that's
        /// separate, larger work in CRegReader, not a fix to this gate's catch scope.
        /// </summary>
        private void GetVbrVersion()
```

- [ ] **Step 3: Update the test class's doc comment**

In `vHC/VhcXTests/CClientFunctionsGateTests.cs`, find:

```csharp
    /// <summary>
    /// Regression tests for the PS 7.6+ module preflight gate's call-site contract: GetVbrVersion
    /// (gated: detect + hard-exit-if-too-old) must only ever be called from
    /// RunVbrPreflightGateIfTargeted(), reached from StartCollections() - the single choke point
    /// immediately before real PowerShell-module-based collection, and only when
    /// CGlobals.EffectiveIsVbr is true. Every other caller (ModeCheck, RunHotfixDetector) must use
    /// the ungated DetectVbrVersion so a too-old-PowerShell machine doesn't hard-exit a feature
    /// that never touches Veeam.Backup.PowerShell.
    ///
    /// Naming convention: [Method]_[Scenario]_[Expected].
    /// </summary>
```

Replace with:

```csharp
    /// <summary>
    /// Regression tests for the PS 7.6+ module preflight gate's call-site contract: GetVbrVersion
    /// (gated: detect + hard-exit-if-too-old) must only ever be called through one of its two
    /// internal gate wrappers - RunVbrPreflightGateIfTargeted(), reached from StartCollections()
    /// when CGlobals.EffectiveIsVbr is true, or RunVbrPreflightGateForDynamicFallback(), the unconditional variant
    /// CCollections.ExecPSScripts() calls once its own DynamicFallback() has confirmed VBR as the
    /// target by a different means (issue #233) - never called directly. Every other caller
    /// (ModeCheck, RunHotfixDetector) must use the ungated DetectVbrVersion so a too-old-
    /// PowerShell machine doesn't hard-exit a feature that never touches Veeam.Backup.PowerShell.
    ///
    /// Naming convention: [Method]_[Scenario]_[Expected].
    /// </summary>
```

- [ ] **Step 4: Update `GetVbrVersion_MethodVisibility_IsPrivate`'s comment**

In the same file, find:

```csharp
            // Regression guard for the root cause of the ModeCheck() hard-exit-on-GUI-startup
            // bug: GetVbrVersion must stay private so RunVbrPreflightGateIfTargeted() remains
            // its only possible caller.
```

Replace with:

```csharp
            // Regression guard for the root cause of the ModeCheck() hard-exit-on-GUI-startup
            // bug: GetVbrVersion must stay private so it's only reachable through its two
            // internal gate wrappers (RunVbrPreflightGateIfTargeted() and RunVbrPreflightGateForDynamicFallback()),
            // never called directly.
```

- [ ] **Step 5: Update `StartCollections_ImportModeEnabled_DoesNotThrow`'s comment**

In the same file, find:

```csharp
            // Smoke test only: on a CI runner without VBR installed, DetectVbrVersion() would
            // leave VBRMAJORVERSION at 0 whether or not the !IMPORT guard actually skipped it, so
            // this can't distinguish "gate correctly skipped" from "gate ran and failed anyway".
            // The real regression guard for the gate's call-site contract is
            // GetVbrVersion_MethodVisibility_IsPrivate below, which makes
            // RunVbrPreflightGateIfTargeted() the only possible caller at compile time.
```

Replace with:

```csharp
            // Smoke test only: on a CI runner without VBR installed, DetectVbrVersion() would
            // leave VBRMAJORVERSION at 0 whether or not the !IMPORT guard actually skipped it, so
            // this can't distinguish "gate correctly skipped" from "gate ran and failed anyway".
            // The real regression guard for the gate's call-site contract is
            // GetVbrVersion_MethodVisibility_IsPrivate below, which confirms GetVbrVersion() is
            // only reachable through its two internal gate wrappers at compile time.
```

- [ ] **Step 6: Update `CArgsParser.cs`'s comment**

In `vHC/HC_Reporting/Startup/CArgsParser.cs`, find:

```csharp
                // The PS 7.6+ module gate is no longer called here. It's private on
                // CClientFunctions and enforced exactly once, from StartCollections(), the single
                // choke point every path below (import, remote, local) eventually reaches via
                // FullRun -> CliRun -> StartPrimaryFunctions. Calling it here too used to run it
                // unconditionally even for /import (which never reaches real collection) and
                // spawn pwsh.exe a second time on the plain local /run path.
```

Replace with:

```csharp
                // The PS 7.6+ module gate is no longer called here. It's private on
                // CClientFunctions and enforced from StartCollections() - the choke point every
                // path below (import, remote, local) eventually reaches via
                // FullRun -> CliRun -> StartPrimaryFunctions - and, since issue #233, also from
                // CCollections.ExecPSScripts()'s DynamicFallback-confirmed-VBR branch, for the
                // Auto+remote+no-local-detection case StartCollections()'s own EffectiveIsVbr gate
                // can't cover. Calling it here too used to run it unconditionally even for
                // /import (which never reaches real collection) and spawn pwsh.exe a second time
                // on the plain local /run path.
```

- [ ] **Step 7: Build and run the full test suite**

Run: `dotnet build vHC/HC.sln --configuration Debug`
Expected: Build succeeds, no errors.

Run: `dotnet test vHC/VhcXTests/VhcXTests.csproj`
Expected: same counts as Task 2 (926 passed, 0 failed, 12 skipped, 938 total) — this task is
documentation-only, so nothing should change.

- [ ] **Step 8: Discard the auto-incremented csproj version bump and commit**

```bash
git checkout -- vHC/HC_Reporting/VeeamHealthCheck.csproj
git add vHC/HC_Reporting/Startup/CClientFunctions.cs vHC/VhcXTests/CClientFunctionsGateTests.cs vHC/HC_Reporting/Startup/CArgsParser.cs
git commit -m "$(cat <<'EOF'
docs(vbr): update stale single-caller doc comments after #233

RunVbrPreflightGateIfTargeted() was documented as GetVbrVersion()'s
only caller in several places. It no longer is now that
RunVbrPreflightGateForDynamicFallback() exists as a second, unconditional wrapper -
update the comments to describe both choke points instead of letting
them go stale.
EOF
)"
```

---

## Task 4: Final whole-branch verification

**Files:** none (verification only)

- [ ] **Step 1: Full clean build**

```bash
dotnet restore vHC/HC.sln
dotnet build vHC/HC.sln --configuration Debug
```

Expected: restore and build both succeed with no errors.

- [ ] **Step 2: Full test suite**

```bash
dotnet test vHC/VhcXTests/VhcXTests.csproj
```

Expected: 926 passed, 0 failed, 12 skipped, 938 total (925 baseline + the 1 new test from
Task 1).

- [ ] **Step 3: Discard the auto-incremented csproj version bump**

```bash
git checkout -- vHC/HC_Reporting/VeeamHealthCheck.csproj
git status --short
```

Expected: clean working tree (no output from `git status --short`).

- [ ] **Step 4: Review the full diff against the branch base**

```bash
git diff origin/feature/gui-redesign-port...HEAD --stat
git log origin/feature/gui-redesign-port..HEAD --oneline
```

Confirm the diff only touches the 4 files this plan modified (`CClientFunctions.cs`,
`CCollections.cs`, `CArgsParser.cs`, `CClientFunctionsGateTests.cs`) plus the design spec doc,
and that there are exactly 3 commits from Tasks 1-3 (plus the earlier spec-doc commits from
brainstorming).

This plan does not cover opening a PR — that happens afterward via the
`superpowers:finishing-a-development-branch` skill, same as every prior stage in this arc, once
this task's verification and a final independent review both pass. Real-hardware verification
(does a GUI run with product type left on "Auto" against a remote VBR 13+ server now correctly
use PowerShell 7) still needs to happen on Windows/VBR lab hardware after the PR is up — this
can't be proven in this sandbox.

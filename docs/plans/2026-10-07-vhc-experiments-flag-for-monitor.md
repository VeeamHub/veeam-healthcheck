# Gate vhc-monitor Behind `VHC_EXPERIMENTS` Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Hide every user-visible trace of the vhc-monitor integration (CLI commands, `/help` entries, the GUI "Continuous Monitoring" tab, public docs) unless the `VHC_EXPERIMENTS` environment variable is `1` or `true`.

**Architecture:** One small static class, `CFeatureFlags.ExperimentsEnabled`, reads the env var on every call (strict allowlist, fail-closed). The four `/monitor:*` switch cases in `CArgsParser` collapse into one testable `TryHandleMonitorCommand` method that returns `false` when the flag is off, so the argument behaves like any other unrecognised one (the parser's `switch` has no `default`, so it is skipped). `CMessages.helpMenu` becomes a property that concatenates head + optional monitor section + tail. The GUI tab strip defaults to hidden in XAML and is revealed in code only when the flag is on.

**Tech Stack:** C# / .NET 8 (`net8.0-windows7.0`), Avalonia XAML, xUnit.

---

## Decisions already made (do not relitigate)

| # | Decision |
|---|---|
| 1 | `/monitor:disable` is gated too, like the other three commands. |
| 2 | Flag off => no visible trace. No warning, no "unknown option" message. |
| 3 | `vhc-monitor.exe` stays bundled by `.github/workflows/manual-release.yml`. Do not touch that file. |
| 4 | **No ADR.** The change is trivially reversible (delete the guards), so it fails the "hard to reverse" test. The non-obvious parts are recorded in code comments and `docs/contributing.md` instead. |
| 5 | Strict allowlist: only `1` or `true` (case-insensitive, whitespace-trimmed) enable the flag. Unset, empty, `0`, `false`, `yes`, `on`, typos => off. |
| 6 | vhc-monitor is a beta feature that was never officially released. So there is **no** release-note text and **no** user-facing cleanup/uninstall guidance anywhere (release notes, public docs, PR body). |

## Things you need to know about this codebase

- `vHC/HC_Reporting/Common/CMessages.cs` and `Common/CGlobals.cs` live in namespace `VeeamHealthCheck.Shared`. Put the new `CFeatureFlags` there too.
- Internal types are visible to the `VhcXTests` project (`InternalsVisibleTo`), so tests can call `internal` members directly.
- Tests that mutate process-global state (env vars, `CGlobals`) must carry `[Collection("GlobalState")]`. That collection has parallelisation disabled (`vHC/VhcXTests/GlobalStateCollection.cs`).
- Test names follow `[MethodUnderTest]_[Scenario]_[ExpectedBehavior]`.
- Source files start with the two-line header `// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>` / `// MIT License`. New files copy it.
- Source files in `HC_Reporting` use LF line endings; `CMessages.cs` and `CArgsParser.cs` have a UTF-8 BOM. The Edit tool preserves both.
- A local `dotnet build` auto-increments the version in `vHC/HC_Reporting/VeeamHealthCheck.csproj`. **Never commit that change.** Run `git checkout -- vHC/HC_Reporting/VeeamHealthCheck.csproj` before every commit.
- Commit messages in this repo end with these two trailer lines (a blank line before them):
  ```
  Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
  Claude-Session: https://claude.ai/code/session_01A9msExwDGusnevNhYJ5B13
  ```
- There is no issue to close, so **no** `Fixes #N` line. Commit type is `chore` (patch bump).

## Behaviour when the flag is off (traced, so you do not have to)

`VeeamHealthCheck.exe /monitor:status` with the flag off: the `switch (a)` in `CArgsParser.ParseAllArgs` has no matching case and no `default`, so the argument is skipped. `ParseAllArgs` then runs `ValidateSilentArgs()` (returns 0), `DetectVbrVersionIfTargeted()` (reads local VBR registry keys, failure is logged at Debug and swallowed), finds none of `runHfd` / `ui` / `run` set, and returns `0`. Result: exit code 0, no monitor-specific output and no scheduled-task side effects. The generic startup lines that `LogInitialInfo()` writes before parsing (version, `Args count`, `Input: ...`) and any VBR-detection logging still appear, exactly as they do today for any unknown flag such as `/foo`; "no trace" means no monitor behaviour, not zero console output. `ParseAllArgs` itself is private and calls `Environment.Exit` / the registry, so it is **not** unit-tested; the unit tests target `TryHandleMonitorCommand`, and Task 6 has a manual check for the end-to-end path.

## File structure

| File | Action | Responsibility |
|---|---|---|
| `vHC/HC_Reporting/Common/CFeatureFlags.cs` | Create | Reads `VHC_EXPERIMENTS`, exposes `ExperimentsEnabled`. |
| `vHC/VhcXTests/ExperimentsFlagScope.cs` | Create | Test helper: sets the env var, restores the original on dispose. |
| `vHC/VhcXTests/CFeatureFlagsTests.cs` | Create | Allowlist behaviour. |
| `vHC/VhcXTests/CMessagesHelpMenuTests.cs` | Create | Help menu shows/hides the monitor section. |
| `vHC/VhcXTests/CArgsParserMonitorGateTests.cs` | Create | `TryHandleMonitorCommand` gating. |
| `vHC/HC_Reporting/Common/CMessages.cs` | Modify (lines 12 and 60-66) | Split the help text; `helpMenu` becomes a property. |
| `vHC/HC_Reporting/Startup/CArgsParser.cs` | Modify (lines 287-298, new method near 732) | Single guarded monitor case + `TryHandleMonitorCommand`. |
| `vHC/HC_Reporting/VhcGui.axaml` | Modify (line 33) | Name the tab strip, default it hidden. |
| `vHC/HC_Reporting/VhcGui.axaml.cs` | Modify (lines 136 and ~1350) | Reveal tab + init status only when flag on; gate post-run offer. |
| `README.md`, `docs/getting-started.md` | Modify | Remove the four `/monitor:*` table rows. |
| `COMPANION_SPEC.md` | Modify | "Experimental" callout. |
| `docs/contributing.md` | Modify | New "Experimental Features" section. |

**Deliberately untouched:** the 24 `GuiMonitor*` localization keys in the five `.resx` files, `CGlobals.cs` monitor path properties, `Functions/Monitor/*`, `CVhcMonitorIntegrationTests.cs`, `ISA.md`, `.github/workflows/manual-release.yml`, and the historical files under `docs/plans/` and `docs/superpowers/`.

---

### Task 0: Branch and baseline

**Files:** none

- [ ] **Step 1: Work on a branch cut from `dev`. Never commit to `dev` directly**

The branch `chore/hide-monitor-behind-vhc-experiments` was already created from `dev` (at `600bd6d`) when this plan was written. Confirm you are on it:

```bash
git branch --show-current
```

Expected: `chore/hide-monitor-behind-vhc-experiments`. If you are on `dev` or anything else, stop and create it first with `git switch dev && git pull --ff-only && git switch -c chore/hide-monitor-behind-vhc-experiments`.

The plan file itself (`docs/plans/2026-10-07-vhc-experiments-flag-for-monitor.md`) is still untracked. Commit it as the branch's first commit:

```bash
git add docs/plans/2026-10-07-vhc-experiments-flag-for-monitor.md
git commit -m "docs(plans): add plan to gate vhc-monitor behind VHC_EXPERIMENTS" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01A9msExwDGusnevNhYJ5B13"
```

- [ ] **Step 2: Record the baseline test result**

```bash
dotnet test vHC/VhcXTests/VhcXTests.csproj 2>&1 | tail -n 8
git checkout -- vHC/HC_Reporting/VeeamHealthCheck.csproj
```

Expected: a summary line like `Passed! - Failed: 0, Passed: N, Skipped: M`. Write down `N` and `M` and any failing test names; Task 6 compares against them.

---

### Task 1: `CFeatureFlags` and the test helper

**Files:**
- Create: `vHC/HC_Reporting/Common/CFeatureFlags.cs`
- Create: `vHC/VhcXTests/ExperimentsFlagScope.cs`
- Test: `vHC/VhcXTests/CFeatureFlagsTests.cs`

- [ ] **Step 1: Write the test helper**

Create `vHC/VhcXTests/ExperimentsFlagScope.cs`:

```csharp
// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using VeeamHealthCheck.Shared;

namespace VhcXTests
{
    // Sets VHC_EXPERIMENTS for the lifetime of a `using` block and restores whatever
    // was there before, even if the test throws. Passing null removes the variable.
    // Only use from classes marked [Collection("GlobalState")]: environment variables
    // are process-wide, so a parallel test would see the change.
    internal sealed class ExperimentsFlagScope : IDisposable
    {
        private readonly string? original;

        public ExperimentsFlagScope(string? value)
        {
            this.original = Environment.GetEnvironmentVariable(CFeatureFlags.ExperimentsEnvVar);
            Environment.SetEnvironmentVariable(CFeatureFlags.ExperimentsEnvVar, value);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(CFeatureFlags.ExperimentsEnvVar, this.original);
        }
    }
}
```

- [ ] **Step 2: Write the failing tests**

Create `vHC/VhcXTests/CFeatureFlagsTests.cs`:

```csharp
// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using VeeamHealthCheck.Shared;
using Xunit;

namespace VhcXTests
{
    [Collection("GlobalState")]
    public class CFeatureFlagsTests
    {
        [Theory]
        [InlineData("1")]
        [InlineData("true")]
        [InlineData("TRUE")]
        [InlineData("True")]
        [InlineData(" 1 ")]
        [InlineData("  true  ")]
        public void ExperimentsEnabled_AllowlistedValue_ReturnsTrue(string value)
        {
            using var scope = new ExperimentsFlagScope(value);

            Assert.True(CFeatureFlags.ExperimentsEnabled);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("0")]
        [InlineData("false")]
        [InlineData("FALSE")]
        [InlineData("yes")]
        [InlineData("on")]
        [InlineData("enabled")]
        [InlineData("ture")]
        [InlineData("2")]
        [InlineData("11")]
        [InlineData("true1")]
        public void ExperimentsEnabled_AnyOtherValue_ReturnsFalse(string? value)
        {
            using var scope = new ExperimentsFlagScope(value);

            Assert.False(CFeatureFlags.ExperimentsEnabled);
        }

        [Fact]
        public void ExperimentsEnabled_EnvironmentChangesBetweenCalls_ReflectsLatestValue()
        {
            using var scope = new ExperimentsFlagScope("1");
            Assert.True(CFeatureFlags.ExperimentsEnabled);

            Environment.SetEnvironmentVariable(CFeatureFlags.ExperimentsEnvVar, "0");

            Assert.False(CFeatureFlags.ExperimentsEnabled);
        }

        [Fact]
        public void ExperimentsFlagScope_Dispose_RestoresOriginalValue()
        {
            using (new ExperimentsFlagScope("true"))
            {
                using (new ExperimentsFlagScope(null))
                {
                    Assert.False(CFeatureFlags.ExperimentsEnabled);
                }

                Assert.True(CFeatureFlags.ExperimentsEnabled);
            }
        }
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test vHC/VhcXTests/VhcXTests.csproj --filter "FullyQualifiedName~CFeatureFlagsTests"`
Expected: build FAILS with a compile error mentioning `CFeatureFlags` (typically `CS0103` or `CS0117`).

- [ ] **Step 4: Write the implementation**

Create `vHC/HC_Reporting/Common/CFeatureFlags.cs`:

```csharp
// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;

namespace VeeamHealthCheck.Shared
{
    /// <summary>
    /// Gate for work that is not ready for general use.
    /// </summary>
    /// <remarks>
    /// <see cref="ExperimentsEnabled"/> is true only when the <c>VHC_EXPERIMENTS</c>
    /// environment variable is exactly <c>1</c> or <c>true</c> (case-insensitive, surrounding
    /// whitespace ignored). Everything else - unset, empty, <c>0</c>, <c>false</c>,
    /// <c>yes</c>, <c>on</c>, typos - is OFF. This is deliberately an allowlist: the gate
    /// exists to keep unfinished work away from users, so an unrecognised value must fail
    /// closed rather than open. The variable is re-read on every call (it is cheap, and it
    /// lets tests flip it without reflection).
    /// </remarks>
    internal static class CFeatureFlags
    {
        internal const string ExperimentsEnvVar = "VHC_EXPERIMENTS";

        internal static bool ExperimentsEnabled
        {
            get
            {
                string value = (Environment.GetEnvironmentVariable(ExperimentsEnvVar) ?? string.Empty).Trim();
                return value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test vHC/VhcXTests/VhcXTests.csproj --filter "FullyQualifiedName~CFeatureFlagsTests"`
Expected: `Passed!` with 21 tests (6 + 13 + 1 + 1), 0 failed.

- [ ] **Step 6: Commit**

```bash
git checkout -- vHC/HC_Reporting/VeeamHealthCheck.csproj
git add vHC/HC_Reporting/Common/CFeatureFlags.cs vHC/VhcXTests/ExperimentsFlagScope.cs vHC/VhcXTests/CFeatureFlagsTests.cs
git commit -m "chore(monitor): add CFeatureFlags with strict VHC_EXPERIMENTS allowlist" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01A9msExwDGusnevNhYJ5B13"
```

---

### Task 2: Help menu hides the monitor section

**Files:**
- Modify: `vHC/HC_Reporting/Common/CMessages.cs:12` and `:60-66`
- Test: `vHC/VhcXTests/CMessagesHelpMenuTests.cs`

Existing tests (`CArgsParserTEST.cs`, `SilentModeTests.cs`) read `CMessages.helpMenu` as a `string`, so turning the field into a property keeps them source-compatible.

- [ ] **Step 1: Write the failing tests**

Create `vHC/VhcXTests/CMessagesHelpMenuTests.cs`:

```csharp
// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using VeeamHealthCheck.Shared;
using Xunit;

namespace VhcXTests
{
    [Collection("GlobalState")]
    public class CMessagesHelpMenuTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("0")]
        [InlineData("false")]
        [InlineData("yes")]
        public void HelpMenu_ExperimentsOff_OmitsMonitorSection(string? flag)
        {
            using var scope = new ExperimentsFlagScope(flag);

            string help = CMessages.helpMenu;

            Assert.DoesNotContain("CONTINUOUS MONITORING", help);
            Assert.DoesNotContain("/monitor:", help);
            Assert.DoesNotContain("vhc-monitor", help);
        }

        [Theory]
        [InlineData("1")]
        [InlineData("true")]
        public void HelpMenu_ExperimentsOn_IncludesEveryMonitorCommand(string flag)
        {
            using var scope = new ExperimentsFlagScope(flag);

            string help = CMessages.helpMenu;

            Assert.Contains("CONTINUOUS MONITORING:", help);
            Assert.Contains("/monitor:setup", help);
            Assert.Contains("/monitor:run", help);
            Assert.Contains("/monitor:status", help);
            Assert.Contains("/monitor:disable", help);
        }

        [Fact]
        public void HelpMenu_ExperimentsOff_KeepsNeighbouringSectionsSeparatedByOneBlankLine()
        {
            using var scope = new ExperimentsFlagScope(null);

            string help = CMessages.helpMenu.Replace("\r\n", "\n");

            Assert.Contains("troubleshooting\n\nUNATTENDED / SILENT MODE:", help);
        }

        [Fact]
        public void HelpMenu_ExperimentsOn_PlacesMonitorSectionBetweenUtilityAndSilentSections()
        {
            using var scope = new ExperimentsFlagScope("1");

            string help = CMessages.helpMenu;

            int utility = help.IndexOf("UTILITY OPTIONS:", System.StringComparison.Ordinal);
            int monitor = help.IndexOf("CONTINUOUS MONITORING:", System.StringComparison.Ordinal);
            int silent = help.IndexOf("UNATTENDED / SILENT MODE:", System.StringComparison.Ordinal);

            Assert.True(utility >= 0 && utility < monitor && monitor < silent);
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test vHC/VhcXTests/VhcXTests.csproj --filter "FullyQualifiedName~CMessagesHelpMenuTests"`
Expected: it builds, and `HelpMenu_ExperimentsOff_OmitsMonitorSection` (5 rows) and `HelpMenu_ExperimentsOff_KeepsNeighbouringSectionsSeparatedByOneBlankLine` FAIL (the section is currently always present). The "On" tests pass already.

- [ ] **Step 3: Turn `helpMenu` into a property over three constants**

In `vHC/HC_Reporting/Common/CMessages.cs`, change line 12. Old:

```csharp
        public static string helpMenu = @"
```

New:

```csharp
        public static string helpMenu =>
            HelpMenuHead + (CFeatureFlags.ExperimentsEnabled ? HelpMenuMonitorSection : string.Empty) + HelpMenuTail;

        private const string HelpMenuHead = @"
```

Then split the text at the monitor section. Old (lines 58-66):

```
  /debug            Enable debug logging for troubleshooting

CONTINUOUS MONITORING:
  /monitor:setup    Install vhc-monitor and register a 5-minute scheduled task
  /monitor:run      Trigger an immediate monitor check
  /monitor:status   Show current monitor installation and last-run status
  /monitor:disable  Remove the scheduled task (keeps config and files)

UNATTENDED / SILENT MODE:
```

New:

```
  /debug            Enable debug logging for troubleshooting

";

        // Experimental: only shown when VHC_EXPERIMENTS enables the vhc-monitor commands
        // (see CFeatureFlags, and the matching gate in CArgsParser.TryHandleMonitorCommand).
        private const string HelpMenuMonitorSection = @"CONTINUOUS MONITORING:
  /monitor:setup    Install vhc-monitor and register a 5-minute scheduled task
  /monitor:run      Trigger an immediate monitor check
  /monitor:status   Show current monitor installation and last-run status
  /monitor:disable  Remove the scheduled task (keeps config and files)

";

        private const string HelpMenuTail = @"UNATTENDED / SILENT MODE:
```

Leave the rest of the original literal (everything from `  /silent  Master ...` down to the closing `";`) exactly as it is; it is now the body of `HelpMenuTail`. The original closing line has trailing spaces after `";`, which are harmless.

- [ ] **Step 4: Run the new and the existing help tests**

Run:
```bash
dotnet test vHC/VhcXTests/VhcXTests.csproj --filter "FullyQualifiedName~CMessagesHelpMenuTests|FullyQualifiedName~HelpMenu_"
```
Expected: all `CMessagesHelpMenuTests` PASS, and the existing `HelpMenu_*` tests in `CArgsParserTEST.cs` and `SilentModeTests.cs` still PASS (the `/monitor:` switches are not asserted there).

- [ ] **Step 5: Commit**

```bash
git checkout -- vHC/HC_Reporting/VeeamHealthCheck.csproj
git add vHC/HC_Reporting/Common/CMessages.cs vHC/VhcXTests/CMessagesHelpMenuTests.cs
git commit -m "chore(monitor): show the monitor help section only when VHC_EXPERIMENTS is on" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01A9msExwDGusnevNhYJ5B13"
```

---

### Task 3: Gate the `/monitor:*` commands in `CArgsParser`

**Files:**
- Modify: `vHC/HC_Reporting/Startup/CArgsParser.cs:287-298` and the area just above `private void RunMonitorSetup()` (line ~732)
- Test: `vHC/VhcXTests/CArgsParserMonitorGateTests.cs`

`/monitor:disable` is gated like the rest (decision 1). Only flag-OFF behaviour and "does not swallow other arguments" are unit-tested: with the flag ON the real commands touch PowerShell, the scheduled-task API and `CGlobals.Logger`, which tests must not do.

- [ ] **Step 1: Write the failing tests**

Create `vHC/VhcXTests/CArgsParserMonitorGateTests.cs`:

```csharp
// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using VeeamHealthCheck.Startup;
using Xunit;

namespace VhcXTests
{
    [Collection("GlobalState")]
    public class CArgsParserMonitorGateTests
    {
        [Theory]
        [InlineData("/monitor:setup", null)]
        [InlineData("/monitor:run", null)]
        [InlineData("/monitor:status", null)]
        [InlineData("/monitor:disable", null)]
        [InlineData("/monitor:disable", "")]
        [InlineData("/monitor:disable", "0")]
        [InlineData("/monitor:disable", "false")]
        [InlineData("/monitor:status", "yes")]
        public void TryHandleMonitorCommand_ExperimentsOff_DoesNotHandleTheArgument(string arg, string? flag)
        {
            using var scope = new ExperimentsFlagScope(flag);
            var parser = new CArgsParser(new string[] { });

            bool handled = parser.TryHandleMonitorCommand(arg, out int exitCode);

            Assert.False(handled);
            Assert.Equal(0, exitCode);
        }

        [Theory]
        [InlineData("/run")]
        [InlineData("/gui")]
        [InlineData("/monitor")]
        [InlineData("/monitor:")]
        [InlineData("/monitor:bogus")]
        public void TryHandleMonitorCommand_ExperimentsOn_IgnoresArgumentsThatAreNotMonitorCommands(string arg)
        {
            using var scope = new ExperimentsFlagScope("1");
            var parser = new CArgsParser(new string[] { });

            bool handled = parser.TryHandleMonitorCommand(arg, out int exitCode);

            Assert.False(handled);
            Assert.Equal(0, exitCode);
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test vHC/VhcXTests/VhcXTests.csproj --filter "FullyQualifiedName~CArgsParserMonitorGateTests"`
Expected: build FAILS with a compile error mentioning `TryHandleMonitorCommand` (typically `CS1061`).

- [ ] **Step 3: Add `TryHandleMonitorCommand`**

In `vHC/HC_Reporting/Startup/CArgsParser.cs`, insert immediately above `        private void RunMonitorSetup()` (line ~732):

```csharp
        // Experimental (VHC_EXPERIMENTS, see CFeatureFlags): the /monitor:* commands are
        // inert unless the flag is on. That includes /monitor:disable on purpose, so the
        // flag hides the feature completely rather than leaving one stray command visible
        // (vhc-monitor is a beta feature that was never officially released). With the flag
        // off this returns false and the argument is treated like any other unrecognised
        // one: ParseAllArgs's switch has no default case, so it is ignored.
        internal bool TryHandleMonitorCommand(string arg, out int exitCode)
        {
            exitCode = 0;
            if (!CFeatureFlags.ExperimentsEnabled)
            {
                return false;
            }

            switch (arg)
            {
                case "/monitor:setup":
                    this.RunMonitorSetup();
                    return true;
                case "/monitor:run":
                    exitCode = this.RunMonitorNow();
                    return true;
                case "/monitor:status":
                    this.PrintMonitorStatus();
                    return true;
                case "/monitor:disable":
                    CVhcMonitorIntegration.Uninstall();
                    CGlobals.Logger.Info("VHC Monitor scheduled task removed.", false);
                    return true;
                default:
                    return false;
            }
        }

```

`CFeatureFlags` is in `VeeamHealthCheck.Shared`, which `CArgsParser.cs` already imports.

- [ ] **Step 4: Replace the four switch cases with one guarded case**

In `ParseAllArgs` (lines 287-298). Old:

```csharp
                    case "/monitor:setup":
                        this.RunMonitorSetup();
                        return 0;
                    case "/monitor:run":
                        return this.RunMonitorNow();
                    case "/monitor:status":
                        this.PrintMonitorStatus();
                        return 0;
                    case "/monitor:disable":
                        CVhcMonitorIntegration.Uninstall();
                        CGlobals.Logger.Info("VHC Monitor scheduled task removed.", false);
                        return 0;
```

New:

```csharp
                    case var _ when this.TryHandleMonitorCommand(a, out int monitorExitCode):
                        return monitorExitCode;
```

Behaviour with the flag on is identical to before: `setup`, `status`, and `disable` return 0; `run` returns `RunMonitorNow()`'s code.

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test vHC/VhcXTests/VhcXTests.csproj --filter "FullyQualifiedName~CArgsParser"`
Expected: `CArgsParserMonitorGateTests` (13 rows: 8 + 5) PASS, and all existing `CArgsParser*` tests still PASS.

- [ ] **Step 6: Commit**

```bash
git checkout -- vHC/HC_Reporting/VeeamHealthCheck.csproj
git add vHC/HC_Reporting/Startup/CArgsParser.cs vHC/VhcXTests/CArgsParserMonitorGateTests.cs
git commit -m "chore(monitor): gate /monitor:* commands behind VHC_EXPERIMENTS" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01A9msExwDGusnevNhYJ5B13"
```

---

### Task 4: Hide the GUI "Continuous Monitoring" tab

**Files:**
- Modify: `vHC/HC_Reporting/VhcGui.axaml:33`
- Modify: `vHC/HC_Reporting/VhcGui.axaml.cs:136` and `:1350`

No unit test: there is no Avalonia headless harness in `VhcXTests`, and the repo verifies GUI layout by hand on Windows (see the Stage C/D verification docs). The build proves the XAML name wiring; Task 6 has the manual check.

The design fails closed: the tab strip is `IsVisible="False"` in XAML and only code reveals it when the flag is on. The strip row is `Auto` height, so hiding the `StackPanel` also removes its vertical space. `MonitoringTabPanel` is already `IsVisible="False"` and `SelectTab(isAdHoc: true)` already makes Ad-hoc the default, so nothing else changes.

- [ ] **Step 1: Name the tab strip and default it hidden**

In `vHC/HC_Reporting/VhcGui.axaml`, old (line 33):

```xml
        <StackPanel Grid.Row="1" Orientation="Horizontal" Spacing="8" Margin="25,0,25,12">
```

New:

```xml
        <StackPanel x:Name="tabStrip" Grid.Row="1" Orientation="Horizontal" Spacing="8" Margin="25,0,25,12"
                    IsVisible="False">
```

Also update the comment on the line above. Old (`VhcGui.axaml:30`):

```xml
        <!-- Tab strip row -->
```

New:

```xml
        <!-- Tab strip row. Hidden unless VHC_EXPERIMENTS is on (VhcGui.axaml.cs reveals it):
             Continuous Monitoring is the only other tab, so a lone Ad-hoc tab is just noise. -->
```

- [ ] **Step 2: Reveal the strip and initialise monitor status only when the flag is on**

In `vHC/HC_Reporting/VhcGui.axaml.cs`, old (lines 135-136). **Include the `InitializeServerList()` line: `this.InitializeMonitorStatus();` alone also appears at line 1323, inside `monitorRunBtn_Click`, so it is not a unique match and the Edit would be refused. Leave line 1323 alone.**

```csharp
            this.InitializeServerList();
            this.InitializeMonitorStatus();
```

New:

```csharp
            this.InitializeServerList();

            // Experimental (VHC_EXPERIMENTS, see CFeatureFlags): everything monitor-related
            // stays hidden unless the flag is on. InitializeMonitorStatus also spawns
            // PowerShell probes on a worker thread, so skipping it saves that work too.
            if (CFeatureFlags.ExperimentsEnabled)
            {
                this.tabStrip.IsVisible = true;
                this.InitializeMonitorStatus();
            }
```

- [ ] **Step 3: Gate the post-run "set up monitoring" offer**

In the same file, in `OfferMonitorSetupIfNeeded` (~line 1350). Old:

```csharp
        private void OfferMonitorSetupIfNeeded()
        {
            if (!CVhcMonitorIntegration.IsExePresentInBundle()) return;
```

New:

```csharp
        private void OfferMonitorSetupIfNeeded()
        {
            if (!CFeatureFlags.ExperimentsEnabled) return;
            if (!CVhcMonitorIntegration.IsExePresentInBundle()) return;
```

`VhcGui.axaml.cs` already uses `CGlobals` from `VeeamHealthCheck.Shared`, so no new `using` is needed. If the build complains, add `using VeeamHealthCheck.Shared;`.

Leave lines 551-569 (the `VbrLocalizationHelper.GuiMonitor*` text assignments) alone: they only set text on controls that stay hidden.

- [ ] **Step 4: Build to verify the XAML name (`tabStrip`) resolves**

Run: `dotnet build vHC/HC.sln --configuration Debug 2>&1 | tail -n 6`
Expected: `Build succeeded.` with `0 Error(s)`. (A missing name would show `CS1061 ... 'VhcGui' does not contain a definition for 'tabStrip'`.)

- [ ] **Step 5: Commit**

```bash
git checkout -- vHC/HC_Reporting/VeeamHealthCheck.csproj
git add vHC/HC_Reporting/VhcGui.axaml vHC/HC_Reporting/VhcGui.axaml.cs
git commit -m "chore(monitor): hide the Continuous Monitoring tab unless VHC_EXPERIMENTS is on" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01A9msExwDGusnevNhYJ5B13"
```

---

### Task 5: Docs

**Files:**
- Modify: `README.md:104-107`
- Modify: `docs/getting-started.md:104-107`
- Modify: `COMPANION_SPEC.md` (after the Overview paragraph)
- Modify: `docs/contributing.md` (new section before `## License`)

- [ ] **Step 1: Remove the four rows from `README.md`**

Delete exactly these four lines (they sit between the `/clearcreds` and `/debug` rows):

```
| `/monitor:setup` | Install vhc-monitor and register a 5-minute scheduled task |
| `/monitor:run` | Trigger an immediate monitor check |
| `/monitor:status` | Show monitor installation and last-run status |
| `/monitor:disable` | Remove the scheduled task (keeps config and files) |
```

- [ ] **Step 2: Remove the same four lines from `docs/getting-started.md`**

Same four lines, same position (between `/clearcreds` and `/debug`).

- [ ] **Step 3: Verify no public doc still mentions the commands**

Run: `git grep -n "monitor:" -- README.md docs/getting-started.md docs/index.md docs/features.md`
Expected: no output.

- [ ] **Step 4: Add the "Experimental" callout to `COMPANION_SPEC.md`**

Insert directly after the `## Overview` paragraph (which ends "...managed through the VHC GUI and CLI."), with a blank line either side:

```markdown
> **Experimental.** The VHC integration described in this spec is hidden unless the `VHC_EXPERIMENTS` environment variable is `1` or `true` (see [Experimental Features](docs/contributing.md#experimental-features)). With it unset, the `/monitor:*` commands, their `/help` entries and the GUI tab are absent. `vhc-monitor.exe` is still bundled in release builds.
```

- [ ] **Step 5: Add the "Experimental Features" section to `docs/contributing.md`**

Insert before `## License`, after the "Commit Convention" section. The content (note the inner fence):

````markdown
## Experimental Features

Work that is not ready for general use ships hidden behind the `VHC_EXPERIMENTS` environment variable. The values `1` and `true` (case-insensitive) turn it on. Anything else, including `0`, `false`, `yes` or leaving it unset, keeps it off. The variable is read from the environment of the process that launches `VeeamHealthCheck.exe`, so set it in the same shell session. In code, check `CFeatureFlags.ExperimentsEnabled`.

Currently gated:

- **vhc-monitor (continuous monitoring, beta):** the `/monitor:setup`, `/monitor:run`, `/monitor:status` and `/monitor:disable` commands, their `/help` entries, and the Continuous Monitoring tab in the GUI. Release builds still bundle `vhc-monitor.exe` so testers do not need a separate download.

When a feature graduates, delete its `CFeatureFlags.ExperimentsEnabled` checks and its entry in this list.
````

- [ ] **Step 6: Commit**

```bash
git add README.md docs/getting-started.md COMPANION_SPEC.md docs/contributing.md
git commit -m "docs(monitor): drop /monitor:* from public docs and document VHC_EXPERIMENTS" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01A9msExwDGusnevNhYJ5B13"
```

---

### Task 6: Full verification and PR

**Files:** none

- [ ] **Step 1: Run the whole test suite and compare to the Task 0 baseline**

```bash
dotnet test vHC/VhcXTests/VhcXTests.csproj 2>&1 | tail -n 8
git checkout -- vHC/HC_Reporting/VeeamHealthCheck.csproj
```

Expected: `Failed: 0` (or only the failures you recorded in Task 0), and `Passed` equals the baseline `N` plus 43 new tests (21 flag + 9 help menu + 13 parser gate).

- [ ] **Step 2: Confirm nothing outside the plan changed**

Run: `git status --short && git diff --stat dev...HEAD`
Expected: only the files in the "File structure" table (plus this plan). In particular `VeeamHealthCheck.csproj`, the `.resx` files and `manual-release.yml` are NOT in the diff.

- [ ] **Step 3: Manual check on Windows, flag OFF** (this is the only coverage for the GUI and the end-to-end parser path)

With `VHC_EXPERIMENTS` unset, and again with `VHC_EXPERIMENTS=0`:

1. `VeeamHealthCheck.exe /help`: output has no `CONTINUOUS MONITORING` section and no `/monitor:` lines; `UTILITY OPTIONS` and `UNATTENDED / SILENT MODE` are separated by a single blank line.
2. `VeeamHealthCheck.exe /monitor:status`, then `echo %ERRORLEVEL%`: no monitor output (the generic startup lines such as `Args count` and `Input: ...` still print, as for any unknown flag), exit code `0`. Repeat for `/monitor:setup`, `/monitor:run`, `/monitor:disable`: no scheduled task is created or removed.
3. `VeeamHealthCheck.exe` (GUI): no tab strip is shown, the Ad-hoc content sits directly under the header, there is no vertical scrollbar at the default 900x620 window, and a completed run does not enable any monitor control.

- [ ] **Step 4: Manual check on Windows, flag ON**

With `set VHC_EXPERIMENTS=1` (and again with `true`):

1. `/help` shows the `CONTINUOUS MONITORING` section between `UTILITY OPTIONS` and `UNATTENDED / SILENT MODE`.
2. `/monitor:status` prints the `=== VHC Monitor Status ===` block.
3. GUI: the tab strip appears, switching between "Ad-hoc Health Check" and "Continuous Monitoring" works with no bottom-bar reflow, and the monitor status text/buttons behave as before this change.

- [ ] **Step 5: Push the branch and open the PR into `dev`**

**This PR will be squash-merged by the maintainer after they approve it. Do not merge it yourself, and do not enable auto-merge.** That shapes the title and body (see [Versioning in `CLAUDE.md`](../../CLAUDE.md#versioning)):

- On a squash merge, the whole PR becomes **one commit on `dev`**, and the release tooling reads the bump **from that commit's subject, which is the PR title**. The title must therefore be a valid Conventional Commit: `type(scope): description` (scope optional), lower-case type, no `!` (nothing here is breaking). `chore` gives a patch bump, which is what we want. **The title is deliberately neutral.** `ci-cd.yaml` copies `chore:` subjects verbatim into the "Tests & CI" section of the generated release notes, and the feature was never officially released, so the title must not name vhc-monitor. A non-conforming title is counted as an untyped patch and the `Commit Lint` check annotates it.
- `Release-As:` and `BREAKING CHANGE:` footers are read from the squash commit **body**. The PR body must not contain either, and must not contain `Fixes #N` / `Closes #N` / `Resolves #N` (there is no issue to close).
- The branch's own commits (Tasks 0-5) are discarded by the squash, so their messages do not need to be perfect, but they should stay conventional anyway.

```bash
git push -u origin chore/hide-monitor-behind-vhc-experiments

gh pr create \
  --base dev \
  --head chore/hide-monitor-behind-vhc-experiments \
  --title "chore: gate experimental features behind an opt-in environment flag" \
  --body "$(cat <<'EOF'
Hides the vhc-monitor integration unless the `VHC_EXPERIMENTS` environment variable is `1` or
`true` (case-insensitive). Any other value, including `0`, `false` or unset, leaves it off.

What is gated:
- the `/monitor:setup`, `/monitor:run`, `/monitor:status` and `/monitor:disable` commands
  (flag off => ignored, like any unrecognised argument)
- the `CONTINUOUS MONITORING` section of `/help`
- the Continuous Monitoring tab in the GUI (the whole tab strip is hidden, since it was the only
  other tab), plus its startup status probes and the post-run "set up monitoring" offer
- the four `/monitor:*` rows in `README.md` and `docs/getting-started.md`

Things to know when reviewing:
- `vhc-monitor.exe` is still bundled by `manual-release.yml` so testers need no separate download.
- `/monitor:disable` is gated too, so the feature is hidden completely. vhc-monitor is a beta
  feature that was never officially released, so no migration or cleanup guidance is provided.
- No ADR: the change is trivially reversible. The rationale lives in code comments and
  `docs/contributing.md`.
- The localization keys, `CGlobals` monitor paths and `Functions/Monitor/*` are untouched.
- The GUI change is verified manually on Windows (flag off and on); the parser, help text and
  flag parsing are covered by unit tests.

Plan: `docs/plans/2026-10-07-vhc-experiments-flag-for-monitor.md`

🤖 Generated with [Claude Code](https://claude.com/claude-code)

https://claude.ai/code/session_01A9msExwDGusnevNhYJ5B13
EOF
)"
```

Expected: `gh` prints the new PR URL. Report that URL to the maintainer.

- [ ] **Step 6: Check the PR's automated checks**

```bash
gh pr checks --watch
```

Expected: `Commit Lint` reports no annotation on the title, and the build/test checks pass. If `Commit Lint` flags the title, fix it with `gh pr edit --title "..."` rather than adding commits. Leave the PR open for the maintainer's approval and squash merge.

---

## Self-review

**Spec coverage**

| Requirement | Task |
|---|---|
| `VHC_EXPERIMENTS` strict allowlist, `0`/`false` explicitly off, env read per call | 1 |
| All four `/monitor:*` cases gated incl. `/monitor:disable`; flag off => silent, no trace | 3 |
| Fall-through for a lone `/monitor:setup` traced and pinned | "Behaviour when the flag is off" section + Task 3 tests + Task 6 step 3 |
| `/help` entries hidden | 2 |
| GUI tab hidden; `InitializeMonitorStatus` and `OfferMonitorSetupIfNeeded` gated | 4 |
| README + getting-started rows removed | 5 |
| COMPANION_SPEC note, ISA.md unchanged | 5 / "Deliberately untouched" |
| `manual-release.yml` untouched (exe stays bundled) | "Decisions" + "Deliberately untouched" |
| No ADR; comments + contributing.md instead | Tasks 1, 3, 5 |
| No release-note or cleanup guidance (beta, never officially released) | Decision 6; nothing to implement |
| Env-var tests restore state in try/finally | `ExperimentsFlagScope` (Task 1) |
| Localization keys, `CGlobals` paths, historical docs untouched | "Deliberately untouched" |

**Placeholder scan:** no TBD/TODO; every code step shows the code.

**Type consistency:** `CFeatureFlags.ExperimentsEnabled` / `CFeatureFlags.ExperimentsEnvVar` (Task 1) are the names used in Tasks 2-4 and in `ExperimentsFlagScope`. `TryHandleMonitorCommand(string arg, out int exitCode)` (Task 3) matches its test calls. `tabStrip` (Task 4 XAML) matches the `this.tabStrip` usage.

# GUI-port cleanup + console-hide-on-double-click Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove dead WPF-era code/config/wording left on `feature/gui-redesign-port`, and make a double-clicked exe hide its console window while a CLI-launched one keeps it.

**Architecture:** Branch `chore/gui-port-cleanup-console-hide` (base `origin/feature/gui-redesign-port`, merges back into it - NOT dev). Four commits, one per item. The console-hide decision is a pure function (`ShouldHideConsole(uint)`) over `GetConsoleProcessList`'s return value: exactly 1 process on the console = we own it (double-click) = hide via `ShowWindow(SW_HIDE)`.

**Tech Stack:** .NET 8, Avalonia 11, xUnit. Windows-only P/Invoke guarded by `OperatingSystem.IsWindows()`.

**Repo rules:**
- `dotnet build`/`dotnet test` auto-bumps `vHC/HC_Reporting/VeeamHealthCheck.csproj` version lines. After EVERY build/test run `git checkout -- vHC/HC_Reporting/VeeamHealthCheck.csproj` (commit csproj edits BEFORE building, so the checkout restores your committed edit, not the original).
- Run tests: `dotnet test vHC/VhcXTests/VhcXTests.csproj` (runs on macOS post-Avalonia).
- Commits end with `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>`. Nothing here closes an issue, so no `Fixes #N`.

**Out of scope (decided with user):** general analyzer dead code (unused privates, usings), the two excluded-from-compile source files (`CNasFileDataVmc.cs`, `JobSessionSummaryRow.cs`), tracked `*.csproj.lscache`, `Run(bool import)` param and `SetImportDebug()` (kept: import button may return), deleting `feature/gui-modernization`.

---

### Task 0: Baseline (no commit)

- [ ] record the baseline: `dotnet test vHC/VhcXTests/VhcXTests.csproj -v q -nologo 2>&1 | tail -5`, then `git checkout -- vHC/HC_Reporting/VeeamHealthCheck.csproj`. Note passed/failed/skipped totals. Any pre-existing failures are not this branch's to fix, but must be reported.

### Task 1: Remove dead GUI code (commit 1)

**Files:**
- Modify: `vHC/HC_Reporting/VhcGui.axaml.cs` (delete `HandleThirdState` + its comment, ~lines 906-914)
- Modify: `vHC/HC_Reporting/App.axaml` (delete `Caution*Brush` x6 + comments in both theme dictionaries; delete `Button.link` styles)

- [ ] **Step 1: Delete `HandleThirdState`.** Remove exactly this block from `VhcGui.axaml.cs`:

```csharp
        // Retained dead code from the real WPF file: not wired to any control
        // event in either the original XAML or the Task 10 AXAML (scrubBox has
        // no IsThreeState/Indeterminate wiring in either) - pre-existing, not
        // introduced by this port.
        private void HandleThirdState(object sender, RoutedEventArgs e)
        {
            this.functions.LogUIAction("Scrub 3rd state = false");
            CGlobals.Scrub = false;
        }

```

- [ ] **Step 2: Delete unused `Caution*` brushes in `App.axaml`.** In the Light dictionary remove the `CautionBackgroundBrush`, `CautionBorderBrush` lines, the WCAG comment block, and `CautionLinkBrush`. In the Dark dictionary remove `CautionBackgroundBrush`, `CautionBorderBrush`, its comment block, and `CautionLinkBrush`. First re-confirm zero users: `git grep -n "Caution" -- '*.axaml' '*.cs'` must show only App.axaml lines and the explanatory comment in `AboutDisclaimerDialog.axaml:38-39` (a comment, leave it).

- [ ] **Step 3: Delete the unused `Button.link` styles** (three `<Style>` elements: `Button.link`, `Button.link:pointerover /template/ ContentPresenter`, `Button.link:pressed /template/ ContentPresenter`). Re-confirm zero users: `git grep -nE 'Classes="[^"]*\blink\b' -- '*.axaml'` and `git grep -n '"link"' -- '*.cs'` return nothing.

- [ ] **Step 4: Verify build + tests**

Run: `dotnet build vHC/HC_Reporting/VeeamHealthCheck.csproj -c Debug -v q -nologo` -> Expected: `Build succeeded`, 0 errors (Avalonia compiles AXAML, so a dangling `{DynamicResource ...}` to a removed key would surface at runtime only - hence the grep in Step 2 must be clean).
Run: `dotnet test vHC/VhcXTests/VhcXTests.csproj -v q -nologo` -> Expected: all pass (record counts; compare to Task-0 baseline below).
Then: `git checkout -- vHC/HC_Reporting/VeeamHealthCheck.csproj`

- [ ] **Step 5: Commit**

```bash
git add vHC/HC_Reporting/VhcGui.axaml.cs vHC/HC_Reporting/App.axaml
git commit -m "chore(gui): remove dead HandleThirdState, unused Caution brushes and Button.link style

HandleThirdState was never wired (no IsThreeState on scrubBox in the old XAML
or the AXAML). The Caution* brushes lost their only consumer when the caution
Border was downgraded to plain info in AboutDisclaimerDialog. Button.link has
no users.

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Remove stale csproj items (commit 2)

**Files:** Modify `vHC/HC_Reporting/VeeamHealthCheck.csproj`

- [ ] **Step 1:** In the first `Remove` ItemGroup delete every line for the four paths that have no tracked files: `Functions\Reporting\Html\VBR\VbrTables\NewFolder\**`, `Softwares\**`, `Startup\GUI\**`, `VBO\**` - across all four item types (`Compile`, `EmbeddedResource`, `None`, `Page`). The whole ItemGroup becomes empty -> delete the `<ItemGroup>` element. Re-confirm first: `git ls-files vHC/HC_Reporting/Softwares vHC/HC_Reporting/VBO vHC/HC_Reporting/Startup/GUI 'vHC/HC_Reporting/Functions/Reporting/Html/VBR/VbrTables/NewFolder' | wc -l` -> `0`.
- [ ] **Step 2:** Delete the `<ItemGroup>` containing the five `<Folder Include=...>` entries (all empty dirs; SDK projects don't need them).
- [ ] **Step 3 (caveat):** these `Remove` rules may have been shielding untracked leftover folders on a developer machine (`git ls-files` cannot see those). Add to the commit body: "If a Windows build fails on files under VBO/Softwares/Startup/GUI, delete those untracked folders."
- [ ] **Step 3b:** KEEP the `Compile Remove` for `CNasFileDataVmc.cs` / `JobSessionSummaryRow.cs` (files exist; out of scope).
- [ ] **Step 4: Commit BEFORE building** (see repo rules), then build + test as in Task 1 Step 4 and `git checkout -- vHC/HC_Reporting/VeeamHealthCheck.csproj`. If build fails, fix, `git commit --amend`.

```bash
git add vHC/HC_Reporting/VeeamHealthCheck.csproj
git commit -m "chore(csproj): drop stale Remove rules and empty Folder includes

Startup\\GUI, Softwares, VBO and NewFolder have no tracked files; Page Remove
is a WPF item type that means nothing under Avalonia.

If a Windows build fails on files under VBO/Softwares/Startup/GUI, delete
those untracked local folders (the rules may have been shielding them).

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Fix stale WPF wording (commit 3, docs/comments only - no behaviour change)

**Files (Modify):**
- `CLAUDE.md:51` -> `` - `vHC/HC_Reporting/VhcGui.axaml.cs` - Avalonia GUI for interactive use ``
- `CLAUDE.md:93` -> `- **Avalonia** for GUI`
- `docs/architecture/index.md:23` -> `` | `VhcGui` | `VhcGui.axaml.cs` | Avalonia GUI for interactive use | ``; `:62` -> `| **Avalonia** | GUI |`
- `docs/contributing.md:30` -> `# Run tests`; delete the `!!! note` block at 33-35 ("Tests require Windows due to WPF...") - CLAUDE.md:133 documents that tests run on Windows/macOS/Linux.
- `ISA.md:36` "WPF for GUI." -> "Avalonia for GUI."; `ISA.md:128` "non-WPF logic" -> "logic that doesn't need the main project's Windows-targeted dependencies"
- `.github/agents/test-writer.agent.md:13,374` -> replace the WPF platform claims with "Windows, macOS, Linux (Windows-only tests use `[WindowsOnlyFact]`)"
- `vHC/VhcXTests.CrossPlatform/VhcXTests.CrossPlatform.csproj:21` -> `<!-- Include source files directly: this project targets net10.0 and cannot reference the net8.0-windows7.0 main project (which also drags in Avalonia and the PowerShell SDK) -->` (KEEP the Compile items - the workaround is still needed, only the stated reason was stale)
- Test comments: `vHC/VhcXTests/Functions/Collection/DB/CDbAccessorTests.cs:29-31` (drop the false "require Windows (WPF dependency)" sentence); `vHC/VhcXTests/Functions/Reporting/Html/CHtmlExporterTEST.cs:96` (WPF -> "an Avalonia/desktop Application.Current"; first check `CHtmlExporter.OpenHtmlIfEnabled` to state what it actually depends on - if unsure, say "launches a browser" only); `CClientFunctionsNotifierTests.cs:30,53` and `CredsHandlerPrompterTests.cs:38` (`WpfUiNotifier/AvaloniaUiNotifier` -> `AvaloniaUiNotifier`; `WpfCredentialPrompter/AvaloniaCredentialPrompter` -> `AvaloniaCredentialPrompter`)

Deliberately NOT touched: `docs/plans|specs`, `code-review-findings*`, `Plans/`, `CREDENTIAL_FIX_SUMMARY.md`, historical comments in `IUiNotifier.cs`, `HtmlToPdfConverter.cs:52`, `CPowerShellVersionChecker*.cs` (describe history/rationale; judgment call, leave).

- [ ] **Step 1:** Make each edit above (view surrounding lines first; keep edits minimal).
- [ ] **Step 2:** `git grep -nIiE 'wpf' -- CLAUDE.md docs/architecture docs/contributing.md ISA.md .github vHC/VhcXTests vHC/VhcXTests.CrossPlatform` -> only intentional history remains.
- [ ] **Step 3:** `dotnet test vHC/VhcXTests/VhcXTests.csproj -v q -nologo` (comment-only edits in .cs; must still compile) then `git checkout -- vHC/HC_Reporting/VeeamHealthCheck.csproj`.
- [ ] **Step 4: Commit** `docs: replace stale WPF references with Avalonia` (+ Co-Authored-By trailer).

---

### Task 4: Console hide on double-click (commit 4, TDD)

**Files:**
- Modify: `vHC/HC_Reporting/Startup/CArgsParser.cs`
- Test: `vHC/VhcXTests/CArgsParserTEST.cs` (add to existing `CArgsParserTests` class)

- [ ] **Step 1: Write the failing test** (add inside `CArgsParserTests`):

```csharp
        [Theory]
        [InlineData(0u, false)] // API failure / no console attached - never hide
        [InlineData(1u, true)]  // only our process on the console: launched by double-click
        [InlineData(2u, false)] // shell + us: launched from a terminal
        [InlineData(5u, false)]
        public void ShouldHideConsole_ConsoleProcessCount_HidesOnlyWhenSoleOwner(uint count, bool expected)
        {
            Assert.Equal(expected, CArgsParser.ShouldHideConsole(count));
        }
```

- [ ] **Step 2: Run it, expect compile FAIL** (`ShouldHideConsole` not defined):
`dotnet test vHC/VhcXTests/VhcXTests.csproj --filter "FullyQualifiedName~ShouldHideConsole" -v q -nologo` ; then `git checkout -- vHC/HC_Reporting/VeeamHealthCheck.csproj`.

- [ ] **Step 3: Implement in `CArgsParser.cs`.**
  a. Replace the P/Invoke block + constants (lines ~24-31) with:

```csharp
        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        // Returns the number of processes attached to the calling process's console,
        // or 0 on failure (e.g. no console). If the buffer is too small it returns the
        // required count without filling it, which is all we need.
        [DllImport("kernel32.dll")]
        static extern uint GetConsoleProcessList(uint[] processList, uint processCount);

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        const int SW_HIDE = 0;
```

  b. Replace the two `LaunchUi(this.Handle(), ...)` calls in `InitializeProgram` (the zero-args branch and the trailing `else`) with `this.LaunchUi()`; the `ui` branch (~line 348) likewise `this.LaunchUi();`.
  c. Replace `LaunchUi(IntPtr handle, bool hide)`, delete `Handle()`, and delete the commented-out `ParseZeroArgs` block (superseded by this approach):

```csharp
        private int LaunchUi()
        {
            CGlobals.Logger.Info("Executing GUI", false);
            CGlobals.RunFullReport = true;
            CGlobals.GUIEXEC = true;
            CGlobals.Notifier = new AvaloniaUiNotifier();
            CGlobals.CredentialPrompter = new AvaloniaCredentialPrompter();

            HideConsoleIfOwned();
            return AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .LogToTrace()
                .StartWithClassicDesktopLifetime(Array.Empty<string>());
        }

        // Double-clicking the exe makes Explorer create a fresh console with us as its
        // only process; launching from cmd/pwsh/Terminal puts the shell on it too. Hide
        // (not FreeConsole) so child processes inherit a hidden console instead of each
        // allocating a visible new one.
        private static void HideConsoleIfOwned()
        {
            // The console P/Invokes are Windows-only; calling them elsewhere throws
            // DllNotFoundException.
            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            IntPtr hwnd = GetConsoleWindow();
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            uint count = GetConsoleProcessList(new uint[2], 2);
            bool hide = ShouldHideConsole(count);
            CGlobals.Logger.Debug($"Console process count = {count}; hiding console = {hide}", false);
            if (hide)
            {
                ShowWindow(hwnd, SW_HIDE);
            }
        }

        internal static bool ShouldHideConsole(uint consoleProcessCount) => consoleProcessCount == 1;
```

- [ ] **Step 4: Run tests** -> new Theory passes (4 cases) and whole suite matches baseline: `dotnet test vHC/VhcXTests/VhcXTests.csproj -v q -nologo`; `git checkout -- vHC/HC_Reporting/VeeamHealthCheck.csproj`. Also `git grep -nE 'SW_SHOW|Handle\(\)|LaunchUi\(' -- vHC` shows no stale references.

- [ ] **Step 5: Commit**

```bash
git add vHC/HC_Reporting/Startup/CArgsParser.cs vHC/VhcXTests/CArgsParserTEST.cs
git commit -m "feat(gui): hide the console window when launched by double-click

Revives the dead LaunchUi(handle, hide) plumbing. GetConsoleProcessList == 1
means Explorer created the console for us, so hide it; a shell on the console
(cmd/pwsh/Terminal) leaves it visible. Replaces the unused hide/handle
parameters and the commented-out cursor-position heuristic.

Untested on Windows: needs manual verification, including Windows Terminal as
the default terminal host (hiding the pseudo-console window may not hide the
terminal tab).

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 5: Hand-off for Windows testing (no commit)

- [ ] At the end, ASK whether to push (the user cannot test on Windows until it is pushed). `git push -u origin chore/gui-port-cleanup-console-hide` ONLY after the user asks. Do not open a PR yet.
- [ ] Give the user this checklist for their Windows machine: (1) double-click, classic conhost -> console hidden, GUI opens, log shows `Console process count = 1; hiding console = True`; (2) double-click with Windows Terminal as default host -> observe whether a tab remains; (3) run `VeeamHealthCheck.exe` from cmd, pwsh and Windows Terminal -> console stays, log shows count >= 2; (4) `/gui` shortcut double-click; (5) an RDP/terminal-server session (the 2023 commit was titled "TS GUI not opening"); (6) a full report run from the GUI still spawns no visible console flashes; (7) F5 from Visual Studio/Rider: OBSERVE whether the console hides while debugging (may be the sole process on a fresh console; if so consider skipping the hide when `Debugger.IsAttached` - user's call); (8) if the Windows build fails on files under VBO/Softwares/Startup/GUI, delete those untracked folders (Task 2 dropped the Remove rules that shielded them); (9) app still themes/launches after Task 1 (dark + light, About dialog, tab buttons).
- [ ] PR later: base `feature/gui-redesign-port` (verify with `gh pr` / `git merge-base`; NOT dev), body ends with the Claude Code attribution line.

---

## Self-review
- Spec coverage: branch (setup, done) / tidy commit(s) (Tasks 1-3) / console hide (Task 4) / user tests on Windows then PR (Task 5). Each corrected finding honoured: `Run` param and `SetImportDebug` kept.
- Placeholders: none; Task 3's `CHtmlExporterTEST` edit has an explicit fallback wording.
- Consistency: `ShouldHideConsole(uint)`, `HideConsoleIfOwned()`, `LaunchUi()` used identically in tests, impl and call sites.

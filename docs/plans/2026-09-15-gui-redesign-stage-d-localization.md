# GUI Redesign Stage D: Localization Sweep — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix the notif-box `Content`-as-data correctness trap, resx-back every remaining hardcoded XAML/code-behind UI string in `VhcGui.axaml`/`VhcGui.axaml.cs`, fix 16 pre-existing orphaned/mistranslated keys across the four satellite locale files (a 17th, `HtmlIntroLine3`, was found post-fix to be unsafe to rename — see Task 4's "Correction applied after review" — and is deliberately left orphaned), bring the new keys to key-parity (English content, not translation) across those locales, and add a mechanical guard against silent localization regressions.

**Architecture:** No new components. This is a string-relocation exercise across five files (`VhcGui.axaml`, `VhcGui.axaml.cs`, `VbrLocalizationHelper.cs`, `vhcres.resx`, and the four satellite `vhcres.*.resx` files) plus two new xUnit tests. The one behavior change that is not pure relocation is Task 1: today `notifTypeBox`/`notifSeverityBox` read their own displayed `Content` back out as a backend value, and localizing those labels without fixing that first would corrupt monitor config. Task 1 fixes that with a `Tag` attribute, using the same pattern this file already uses once (`PeriodRadio_Checked` reading `Tag`, not `Content`).

**Tech Stack:** .NET 8 (`net8.0-windows7.0`), Avalonia (XAML + code-behind), `System.Resources.ResourceManager` (resx satellite assemblies, SDK-default compilation — no `ResGen.exe` involved for the VBR-side strings this stage touches), xUnit.

**Canonical spec:** `docs/superpowers/specs/2026-09-15-gui-redesign-stage-d-localization-design.md`. Read it. Where this plan and the spec disagree, the spec wins and the discrepancy is a bug in this plan — report it rather than guessing.

**Relationship to the spec's Design sections:** the spec's Design §2 ("XAML sweep") narratively includes `ThemeLabelFor()`, which is actually a `.cs` code-behind method, not XAML markup. This plan splits work by *which file changes*, not by the spec's narrative grouping: Task 2 below touches only `VhcGui.axaml` + the `SetUiText()` additions needed to wire it; Task 3 touches only `.cs` code-behind (including `ThemeLabelFor()` and the two hardcoded lines already inside `SetUiText()`). Both together implement spec Design §2+§3.

---

## Standing rules for every task

Read these once; they are not repeated per task.

1. **Revert the version bump after every build or test run**, before committing:
   ```bash
   git checkout -- vHC/HC_Reporting/VeeamHealthCheck.csproj
   ```
   Every build auto-increments it via `increment_version.ps1` and it will otherwise pollute your diff.

2. **Baseline to hold:** `908 passed, 0 failed, 12 skipped` (`dotnet test vHC/VhcXTests/VhcXTests.csproj`). Task 6 adds 2 tests, bringing passed to 910. Otherwise the passed count should not move; failed stays 0, skipped stays 12. Any other change is a stop-and-investigate.

3. **One commit per task.** Never `git commit --amend` — create a new commit instead.

4. **You cannot render the GUI here.** Avalonia crashes at native platform bootstrap in this sandbox before any application code runs. Tasks 1-3's UI-coupled correctness/visual claims are verified by a human on Windows — Task 7 collects that checklist. There is also no Avalonia headless-testing infrastructure anywhere in this repo today (`grep -rl "Avalonia.Headless" vHC/` returns nothing) — do not introduce it for this stage; that is separate, larger scope.

5. **Two localization files are UTF-16LE with CRLF** (`VbrLocalizationHelper.cs`, `vhcres.txt`). `cat`/`grep` on them reports "stream did not contain valid UTF-8" — that is expected. Never edit them with a tool that rewrites as UTF-8; every task that touches them gives you an encoding-safe Python script. `vhcres.resx` and the four satellite `vhcres.*.resx` files are plain UTF-8 and are also edited via the Python scripts below (not because they need it for encoding safety, but because inserting ~50 `<data>` blocks by hand across 5 files is error-prone — the scripts also handle XML-escaping `&`/`<`/`>` correctly, which a manual paste would not).

6. **Internal enums cannot be `[InlineData]` parameters.** Not relevant to this stage's tests (no internal enums are exercised), noted only because it's a standing repo-wide gotcha from Stage C.

7. **Any new test class that touches shared mutable static state must carry `[Collection("GlobalState")]`.** Not needed by this stage's tests — `VbrLocalizationHelperTests` only reads resources, never mutates `CGlobals`/`CredentialStore`/etc. — noted for completeness.

8. **`VbrLocalizationHelper` is `internal`** (no access modifier on the class declaration) but its fields are `public`, and `VeeamHealthCheck.csproj:32` has `<InternalsVisibleTo Include="VhcXTests" />`. Tests can reference the type by name directly; no reflection workaround for accessibility is needed (reflection is still used in Task 6, but only to *enumerate* the fields generically, not to bypass access control).

---

## File structure

**Created:**

| Path | Responsibility |
|---|---|
| `vHC/HC_Reporting/Resources/Localization/locale-known-missing.txt` | Checked-in allowlist of `Culture:KeyName` pairs the Task 6 guard test tolerates as pre-existing, not-yet-translated gaps (unrelated to Stage D, not fixed by it). |
| `vHC/HC_Reporting/Resources/Localization/untranslated-keys.txt` | Translator-facing to-do list of the ~52 Stage D keys added to all four locales with English placeholder content. Not read by any test. |
| `vHC/VhcXTests/VbrLocalizationHelperTests.cs` | The two Task 6 guard tests. |

**Modified:**

| Path | Change |
|---|---|
| `vHC/HC_Reporting/VhcGui.axaml` | Task 1: `Tag` on notif ComboBoxItems. Task 2: `xmlns:loc` added; `x:Name` added to 11 previously-unnamed elements; hardcoded `Text`/`Content`/`ToolTip.Tip` values removed; 7 `ComboBoxItem.Content` values (`productTypeSelector`'s 4, `notifSeverityBox`'s 3) set via `{x:Static}` instead of `x:Name`, because Avalonia's ComboBox snapshots an item's `Content` at attach time and never re-reads it — see Task 2 Step 5's note. |
| `vHC/HC_Reporting/VhcGui.axaml.cs` | Task 1: 3 `.Content` reads → `.Tag`. Task 2: `SetUiText()` additions + one `SetUiSync()` tooltip line. Task 3: `SetUiText()`'s 2 remaining hardcoded lines, `ThemeLabelFor()`, and 6 monitor/progress methods. |
| `vHC/HC_Reporting/Resources/Localization/vhcres.resx` | Task 2 adds 35 keys, Task 3 adds 17 keys (52 total). |
| `vHC/HC_Reporting/Resources/Localization/VbrLocalizationHelper.cs` | Matching 52 new accessors (UTF-16LE). |
| `vHC/HC_Reporting/Resources/Localization/vhcres.txt` | Matching ResGen source entries (UTF-16LE, `Key = Value` format) — inert for the actual VBR build (see spec Context). Kept in sync in spirit, not byte-for-byte: Task 3's entries use a plain hyphen in place of the em-dash the real resx carries, since nothing reads this file and a second UTF-16LE em-dash round-trip on it would verify nothing. |
| `vHC/HC_Reporting/Resources/Localization/vhcres.fR-FR.resx` | Task 4: 17 key renames. Task 5: 52 new keys (English content). |
| `vHC/HC_Reporting/Resources/Localization/vhcres.ja.resx` | Task 4: 1 key rename. Task 5: 52 new keys. |
| `vHC/HC_Reporting/Resources/Localization/vhcres.zh-cn.resx` | Task 4: 1 key rename. Task 5: 52 new keys. |
| `vHC/HC_Reporting/Resources/Localization/vhcres.zh-tw.resx` | Task 4: 1 key rename. Task 5: 52 new keys. |
| `vHC/VhcXTests/VhcXTests.csproj` | Task 6: one `<None Include>` linking `locale-known-missing.txt` into the test output. |

**Ordering rationale:** Task 1 must land before Task 2 touches either notif box's `Content`, per the spec. Tasks 2-3 are independently compilable and testable-by-build (each is its own commit, matching Stage C's Task 6 precedent of "add keys, verify build, commit" with no TDD ceremony for pure string relocation). Task 4 (rename bug fix) is independent of everything else and could technically run first, but is sequenced after 1-3 so that Task 5 adds *all* of Stage D's new keys (from Tasks 2 and 3) to the locale files in one pass rather than two. Task 6's tests depend on Tasks 4-5 having already fixed the orphans and added the new keys, or the guard tests would fail against the very state they're meant to police going forward.

---

## Task 1: Fix the notif-box `Content`-as-data correctness trap

**Files:**
- Modify: `vHC/HC_Reporting/VhcGui.axaml:199-202,214-216`
- Modify: `vHC/HC_Reporting/VhcGui.axaml.cs:1071,1073,1170`

- [ ] **Step 1: Add `Tag` to every `notifTypeBox`/`notifSeverityBox` item**

In `vHC/HC_Reporting/VhcGui.axaml`, find:

```xml
                                    <ComboBox x:Name="notifTypeBox" Grid.Column="0"
                                              Classes="modern" Height="32" FontSize="12"
                                              SelectedIndex="0"
                                              SelectionChanged="notifTypeBox_SelectionChanged">
                                        <ComboBoxItem Content="ntfy" IsSelected="True" />
                                        <ComboBoxItem Content="Teams" />
                                        <ComboBoxItem Content="Slack" />
                                        <ComboBoxItem Content="PagerDuty" />
                                    </ComboBox>
```

Replace with:

```xml
                                    <ComboBox x:Name="notifTypeBox" Grid.Column="0"
                                              Classes="modern" Height="32" FontSize="12"
                                              SelectedIndex="0"
                                              SelectionChanged="notifTypeBox_SelectionChanged">
                                        <ComboBoxItem Content="ntfy" Tag="ntfy" IsSelected="True" />
                                        <ComboBoxItem Content="Teams" Tag="Teams" />
                                        <ComboBoxItem Content="Slack" Tag="Slack" />
                                        <ComboBoxItem Content="PagerDuty" Tag="PagerDuty" />
                                    </ComboBox>
```

Find:

```xml
                                    <ComboBox x:Name="notifSeverityBox" Width="110"
                                              Classes="modern" Height="28" FontSize="12"
                                              SelectedIndex="0">
                                        <ComboBoxItem Content="warning" IsSelected="True" />
                                        <ComboBoxItem Content="critical" />
                                        <ComboBoxItem Content="ok" />
                                    </ComboBox>
```

Replace with:

```xml
                                    <ComboBox x:Name="notifSeverityBox" Width="110"
                                              Classes="modern" Height="28" FontSize="12"
                                              SelectedIndex="0">
                                        <ComboBoxItem Content="warning" Tag="warning" IsSelected="True" />
                                        <ComboBoxItem Content="critical" Tag="critical" />
                                        <ComboBoxItem Content="ok" Tag="ok" />
                                    </ComboBox>
```

`Tag` values match current `Content` values exactly — this step alone changes no observable behavior.

- [ ] **Step 2: Repoint the three read sites at `Tag`**

In `vHC/HC_Reporting/VhcGui.axaml.cs`, find `GetNotifSettings()`:

```csharp
        private (string notifType, string notifUrl, string minSeverity) GetNotifSettings()
        {
            string notifType = (notifTypeBox.SelectedItem as ComboBoxItem)?.Content?.ToString()?.ToLower() ?? "ntfy";
            string notifUrl = notifUrlBox.Text?.Trim() ?? string.Empty;
            string minSeverity = (notifSeverityBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "warning";
            return (notifType, notifUrl, minSeverity);
        }
```

Replace with:

```csharp
        private (string notifType, string notifUrl, string minSeverity) GetNotifSettings()
        {
            string notifType = (notifTypeBox.SelectedItem as ComboBoxItem)?.Tag?.ToString()?.ToLower() ?? "ntfy";
            string notifUrl = notifUrlBox.Text?.Trim() ?? string.Empty;
            string minSeverity = (notifSeverityBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "warning";
            return (notifType, notifUrl, minSeverity);
        }
```

Find `notifTypeBox_SelectionChanged`:

```csharp
        private void notifTypeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (notifUrlBox == null) return;
            string type = (notifTypeBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "ntfy";
            notifUrlBox.Tag = type switch
            {
                "Teams" => "https://org.webhook.office.com/...",
                "Slack" => "https://hooks.slack.com/services/...",
                "PagerDuty" => "https://events.pagerduty.com/...",
                _ => "https://ntfy.sh/your-topic"
            };
        }
```

Replace with (note: **no** `.ToLower()` here — this switch compares case-sensitively against the exact `Tag` casing `"Teams"`/`"Slack"`/`"PagerDuty"`; applying `.ToLower()` the way the other two sites do would make every arm fall through to the default case):

```csharp
        private void notifTypeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (notifUrlBox == null) return;
            string type = (notifTypeBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "ntfy";
            notifUrlBox.Tag = type switch
            {
                "Teams" => "https://org.webhook.office.com/...",
                "Slack" => "https://hooks.slack.com/services/...",
                "PagerDuty" => "https://events.pagerduty.com/...",
                _ => "https://ntfy.sh/your-topic"
            };
        }
```

(`notifUrlBox.Tag`'s write target is unrelated dead code — see Task 7's Recorded-not-fixed note. Left exactly as-is.)

- [ ] **Step 3: Build**

```bash
dotnet build vHC/HC.sln --configuration Debug
git checkout -- vHC/HC_Reporting/VeeamHealthCheck.csproj
```

Expected: 0 errors.

- [ ] **Step 4: Confirm no `.Content` reads remain on these two controls**

```bash
grep -n 'notifTypeBox.SelectedItem\|notifSeverityBox.SelectedItem' vHC/HC_Reporting/VhcGui.axaml.cs
```

Expected: three lines (the two in `GetNotifSettings()`, one in `notifTypeBox_SelectionChanged`), every one reading `?.Tag?.ToString()`, none reading `?.Content?.ToString()`.

No automated test is possible for this fix — it requires reading `ComboBoxItem.SelectedItem` from a live Avalonia control, and this repo has no Avalonia headless-testing setup (standing rule 4). Verification is manual: Task 7's checklist includes selecting each notif type/severity and confirming `GetNotifSettings()`'s effective values via the generated monitor config.

- [ ] **Step 5: Commit**

```bash
git add vHC/HC_Reporting/VhcGui.axaml vHC/HC_Reporting/VhcGui.axaml.cs
git commit -m "fix(gui): stop notif ComboBoxes reading localized Content as a backend value"
```

---

## Task 2: XAML sweep — resx-back `VhcGui.axaml`'s hardcoded strings

**Files:**
- Modify: `vHC/HC_Reporting/VhcGui.axaml`
- Modify: `vHC/HC_Reporting/VhcGui.axaml.cs` (`SetUiText()`, `SetUiSync()`)
- Modify: `vHC/HC_Reporting/Resources/Localization/vhcres.resx`
- Modify: `vHC/HC_Reporting/Resources/Localization/VbrLocalizationHelper.cs` (UTF-16LE)
- Modify: `vHC/HC_Reporting/Resources/Localization/vhcres.txt` (UTF-16LE)

This task adds 35 new keys and reuses one existing key (`GuiTitle`). It excludes, per the spec's non-goals: the gear glyph (`&#x2699;`, line 57), the ellipsis on `browseFolderBtn` (line 88), and the notif-type brand names `ntfy`/`Teams`/`Slack`/`PagerDuty` (lines 199-202, already given `Tag` in Task 1). It also removes one dead XAML literal (`monitorStatusText`'s initial `Text="Checking..."`) rather than resx-backing it — see Step 5.

- [ ] **Step 1: Add 35 keys to `vhcres.resx`**

```bash
python3 - <<'PY'
import io

def esc(s):
    return s.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")

pairs = [
    ("GuiImportTooltip", "Create a report from previously collected data"),
    ("GuiThemeToggleTooltip", "Toggle light/dark theme"),
    ("GuiAboutButton", "About / Disclaimer"),
    ("GuiAdHocTab", "Ad-hoc Health Check"),
    ("GuiMonitoringTab", "Continuous Monitoring"),
    ("GuiServerCardTitle", "Veeam Server"),
    ("GuiProductTypeLabel", "Product Type:"),
    ("GuiProductTypeTooltip", "For remote servers, specifying the product type avoids connection failures. If set to Auto-detect, the tool will try both connection types."),
    ("GuiProductTypeAuto", "Auto-detect"),
    ("GuiProductTypeVbr", "VBR (Backup & Replication)"),
    ("GuiProductTypeVb365", "VB365 (Backup for Microsoft 365)"),
    ("GuiProductTypeBoth", "Both"),
    ("GuiExportOptionsLabel", "Export Options"),
    ("GuiHtmlReportTooltip", "Display the HTML report in your default browser after generation"),
    ("GuiDataCollectionLabel", "Data Collection"),
    ("GuiCollectionPeriodLabel", "Collection Period:"),
    ("GuiRescanTooltip", "Rescan for Hardware change"),
    ("GuiSecurityPrivacyLabel", "Security & Privacy"),
    ("GuiScrubTooltip", "Anonymize sensitive data according to Veeam KB 2462"),
    ("GuiClearCredsTooltip", "Clear any previously saved credentials before running"),
    ("GuiPdfUnavailableTooltip", "PDF Export not available when both VB365 & VBR are detected on the same machine."),
    ("GuiMonitorStatusHeader", "Monitor Status"),
    ("GuiMonitorStatusLabel", "Status: "),
    ("GuiMonitorQuickSetup", "Quick Setup"),
    ("GuiMonitorQuickSetupTooltip", "Install monitor using selected VBR server and stored credentials"),
    ("GuiMonitorVhcSetup", "Setup from VHC"),
    ("GuiMonitorVhcSetupTooltip", "Configure monitor using data collected from the last health check run"),
    ("GuiMonitorRunNow", "Run Now"),
    ("GuiMonitorRunTooltip", "Trigger an immediate monitor check"),
    ("GuiAlertNotificationsHeader", "Alert Notifications"),
    ("GuiMinSeverityLabel", "Min severity: "),
    ("GuiNotifSeverityWarning", "warning"),
    ("GuiNotifSeverityCritical", "critical"),
    ("GuiNotifSeverityOk", "ok"),
    ("GuiProcessingText", "Processing health check..."),
]

p = "vHC/HC_Reporting/Resources/Localization/vhcres.resx"
s = io.open(p, encoding="utf-8").read()
block = "".join(
    '  <data name="%s" xml:space="preserve">\n    <value>%s</value>\n  </data>\n' % (k, esc(v))
    for k, v in pairs
)
assert "</root>" in s
s = s.replace("</root>", block + "</root>")
io.open(p, "w", encoding="utf-8").write(s)
print("inserted", len(pairs), "keys into vhcres.resx")
PY
```

Expected output: `inserted 35 keys into vhcres.resx`. (`GuiTitle`, reused for line 18, is deliberately absent from this script's `pairs` list — it already exists in the resx from an earlier stage.) Confirm:

```bash
grep -c '<data name="Gui' vHC/HC_Reporting/Resources/Localization/vhcres.resx
```

Expected: `84` (49 pre-existing `Gui`-prefixed keys from earlier stages + this task's 35 new ones).

- [ ] **Step 2: Add the accessors to `VbrLocalizationHelper.cs`, encoding-preserving**

```bash
python3 - <<'PY'
import io
p = "vHC/HC_Reporting/Resources/Localization/VbrLocalizationHelper.cs"
keys = [
    "GuiImportTooltip", "GuiThemeToggleTooltip", "GuiAboutButton", "GuiAdHocTab",
    "GuiMonitoringTab", "GuiServerCardTitle", "GuiProductTypeLabel", "GuiProductTypeTooltip",
    "GuiProductTypeAuto", "GuiProductTypeVbr", "GuiProductTypeVb365", "GuiProductTypeBoth",
    "GuiExportOptionsLabel", "GuiHtmlReportTooltip", "GuiDataCollectionLabel",
    "GuiCollectionPeriodLabel", "GuiRescanTooltip", "GuiSecurityPrivacyLabel",
    "GuiScrubTooltip", "GuiClearCredsTooltip", "GuiPdfUnavailableTooltip",
    "GuiMonitorStatusHeader", "GuiMonitorStatusLabel", "GuiMonitorQuickSetup",
    "GuiMonitorQuickSetupTooltip", "GuiMonitorVhcSetup", "GuiMonitorVhcSetupTooltip",
    "GuiMonitorRunNow", "GuiMonitorRunTooltip", "GuiAlertNotificationsHeader",
    "GuiMinSeverityLabel", "GuiNotifSeverityWarning", "GuiNotifSeverityCritical",
    "GuiNotifSeverityOk", "GuiProcessingText",
]
s = io.open(p, encoding="utf-16-le", newline="").read()
marker = "}}"
assert s.rstrip().endswith(marker), "unexpected helper tail; inspect before editing"
tail_at = s.rstrip().rfind(marker)
block = "".join('public static string %s = m4.GetString("%s");\r\n' % (k, k) for k in keys)
s = s[:tail_at] + block + s[tail_at:]
io.open(p, "w", encoding="utf-16-le", newline="").write(s)
print("inserted", len(keys), "accessors")
PY
```

Expected: `inserted 35 accessors`.

- [ ] **Step 3: Verify the encoding survived**

```bash
file vHC/HC_Reporting/Resources/Localization/VbrLocalizationHelper.cs
iconv -f UTF-16LE -t UTF-8 vHC/HC_Reporting/Resources/Localization/VbrLocalizationHelper.cs | grep -c 'GuiMonitor\|GuiProductType\|GuiNotifSeverity'
```

Expected: `Unicode text, UTF-16, little-endian text, with CRLF line terminators` and a count of `18` (8 `GuiMonitor*` + `GuiMonitoringTab`, which also matches the `GuiMonitor` pattern as a substring + 6 `GuiProductType*` + 3 `GuiNotifSeverity*`). **If `file` reports UTF-8, stop and `git checkout --` the file.**

- [ ] **Step 4: Append the matching entries to `vhcres.txt`**

```bash
python3 - <<'PY'
import io
p = "vHC/HC_Reporting/Resources/Localization/vhcres.txt"
pairs = [
    ("GuiImportTooltip", "Create a report from previously collected data"),
    ("GuiThemeToggleTooltip", "Toggle light/dark theme"),
    ("GuiAboutButton", "About / Disclaimer"),
    ("GuiAdHocTab", "Ad-hoc Health Check"),
    ("GuiMonitoringTab", "Continuous Monitoring"),
    ("GuiServerCardTitle", "Veeam Server"),
    ("GuiProductTypeLabel", "Product Type:"),
    ("GuiProductTypeTooltip", "For remote servers, specifying the product type avoids connection failures. If set to Auto-detect, the tool will try both connection types."),
    ("GuiProductTypeAuto", "Auto-detect"),
    ("GuiProductTypeVbr", "VBR (Backup & Replication)"),
    ("GuiProductTypeVb365", "VB365 (Backup for Microsoft 365)"),
    ("GuiProductTypeBoth", "Both"),
    ("GuiExportOptionsLabel", "Export Options"),
    ("GuiHtmlReportTooltip", "Display the HTML report in your default browser after generation"),
    ("GuiDataCollectionLabel", "Data Collection"),
    ("GuiCollectionPeriodLabel", "Collection Period:"),
    ("GuiRescanTooltip", "Rescan for Hardware change"),
    ("GuiSecurityPrivacyLabel", "Security & Privacy"),
    ("GuiScrubTooltip", "Anonymize sensitive data according to Veeam KB 2462"),
    ("GuiClearCredsTooltip", "Clear any previously saved credentials before running"),
    ("GuiPdfUnavailableTooltip", "PDF Export not available when both VB365 & VBR are detected on the same machine."),
    ("GuiMonitorStatusHeader", "Monitor Status"),
    ("GuiMonitorStatusLabel", "Status: "),
    ("GuiMonitorQuickSetup", "Quick Setup"),
    ("GuiMonitorQuickSetupTooltip", "Install monitor using selected VBR server and stored credentials"),
    ("GuiMonitorVhcSetup", "Setup from VHC"),
    ("GuiMonitorVhcSetupTooltip", "Configure monitor using data collected from the last health check run"),
    ("GuiMonitorRunNow", "Run Now"),
    ("GuiMonitorRunTooltip", "Trigger an immediate monitor check"),
    ("GuiAlertNotificationsHeader", "Alert Notifications"),
    ("GuiMinSeverityLabel", "Min severity: "),
    ("GuiNotifSeverityWarning", "warning"),
    ("GuiNotifSeverityCritical", "critical"),
    ("GuiNotifSeverityOk", "ok"),
    ("GuiProcessingText", "Processing health check..."),
]
s = io.open(p, encoding="utf-16-le", newline="").read()
if not s.endswith("\r\n"):
    s += "\r\n"
s += "".join("%s = %s\r\n" % kv for kv in pairs)
io.open(p, "w", encoding="utf-16-le", newline="").write(s)
print("appended", len(pairs), "entries")
PY
file vHC/HC_Reporting/Resources/Localization/vhcres.txt
```

Expected: `appended 35 entries` and still UTF-16LE.

- [ ] **Step 5: Update `VhcGui.axaml`**

**Why some items use `x:Static` instead of a `SetUiText()` assignment:** Avalonia's `ComboBox` does not live-bind its closed-box display to a selected item's `Content` — `UpdateSelectionBoxItem` (verified against the pinned `Avalonia` 11.3.20 source, `src/Avalonia.Controls/ComboBox.cs`) is only invoked from `OnAttachedToVisualTree` and from the `SelectedItemProperty` change handler, never from the item's own `Content` changing. Since `SelectedIndex="0"` is already set at XAML-load time, the box attaches and snapshots `Content` *before* `SetUiText()` ever runs (`SetUiText()` runs on `Loaded`, well after construction). Setting `Content` later via `SetUiText()`, as the rest of this sweep does, would leave `productTypeSelector` and `notifSeverityBox` permanently blank in their closed state. `{x:Static}` sidesteps this entirely: it resolves at XAML-load time, before attachment, so the correct text is already in place when the box takes its snapshot. Verified to compile against `VbrLocalizationHelper` (which is `internal`, no access-modifier change needed) with a scratch build before this plan was finalized.

Window root — find:

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        x:Class="VeeamHealthCheck.VhcGui"
        Title=""
        MinWidth="860" MinHeight="580"
        Width="900" Height="620"
        WindowStartupLocation="CenterScreen"
        CanResize="True">
```

Replace with:

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:loc="clr-namespace:VeeamHealthCheck.Resources.Localization;assembly=VeeamHealthCheck"
        x:Class="VeeamHealthCheck.VhcGui"
        Title=""
        MinWidth="860" MinHeight="580"
        Width="900" Height="620"
        WindowStartupLocation="CenterScreen"
        CanResize="True">
```

Header row — find:

```xml
            <TextBlock Grid.Column="0" Text="Veeam Health Check" FontSize="20" FontWeight="Bold" VerticalAlignment="Center" />
            <Button x:Name="importButton" Grid.Column="1" Classes="secondary"
                    Margin="0,0,12,0" Click="Import_click"
                    ToolTip.Tip="Create a report from previously collected data" />
            <Border x:Name="importDivider" Grid.Column="2" Width="1" Height="18" VerticalAlignment="Center" Margin="0,0,12,0"
                    Background="{DynamicResource CardBorderBrush}" />
            <Button x:Name="ThemeToggleButton" Grid.Column="3" Classes="secondary"
                    Margin="0,0,8,0" Click="ThemeToggleButton_Click" ToolTip.Tip="Toggle light/dark theme" />
            <Button x:Name="aboutButton" Grid.Column="4" Content="About / Disclaimer" Classes="secondary"
                    Click="AboutButton_Click" />
```

Replace with:

```xml
            <TextBlock x:Name="appHeaderText" Grid.Column="0" FontSize="20" FontWeight="Bold" VerticalAlignment="Center" />
            <Button x:Name="importButton" Grid.Column="1" Classes="secondary"
                    Margin="0,0,12,0" Click="Import_click" />
            <Border x:Name="importDivider" Grid.Column="2" Width="1" Height="18" VerticalAlignment="Center" Margin="0,0,12,0"
                    Background="{DynamicResource CardBorderBrush}" />
            <Button x:Name="ThemeToggleButton" Grid.Column="3" Classes="secondary"
                    Margin="0,0,8,0" Click="ThemeToggleButton_Click" />
            <Button x:Name="aboutButton" Grid.Column="4" Classes="secondary"
                    Click="AboutButton_Click" />
```

Tab strip row — find:

```xml
            <Button x:Name="AdHocTabButton" Content="Ad-hoc Health Check" Classes="tab tab-active"
                    Click="AdHocTabButton_Click" />
            <Button x:Name="MonitoringTabButton" Content="Continuous Monitoring" Classes="tab"
                    Click="MonitoringTabButton_Click" />
```

Replace with:

```xml
            <Button x:Name="AdHocTabButton" Classes="tab tab-active"
                    Click="AdHocTabButton_Click" />
            <Button x:Name="MonitoringTabButton" Classes="tab"
                    Click="MonitoringTabButton_Click" />
```

Server card — find:

```xml
                                <TextBlock Text="Veeam Server" Classes="card-title" />
```

Replace with:

```xml
                                <TextBlock x:Name="serverCardTitle" Classes="card-title" />
```

Find:

```xml
                                    <TextBlock Text="Product Type:" Classes="field-label" Margin="0,0,0,8" />
                                    <ComboBox x:Name="productTypeSelector"
                                              Classes="modern"
                                              Width="220" FontSize="12"
                                              SelectedIndex="0"
                                              HorizontalAlignment="Left"
                                              SelectionChanged="productTypeSelector_SelectionChanged"
                                              ToolTip.Tip="For remote servers, specifying the product type avoids connection failures. If set to Auto-detect, the tool will try both connection types.">
                                        <ComboBoxItem Content="Auto-detect" IsSelected="True" />
                                        <ComboBoxItem Content="VBR (Backup &amp; Replication)" />
                                        <ComboBoxItem Content="VB365 (Backup for Microsoft 365)" />
                                        <ComboBoxItem Content="Both" />
                                    </ComboBox>
```

Replace with:

```xml
                                    <TextBlock x:Name="productTypeLabel" Classes="field-label" Margin="0,0,0,8" />
                                    <ComboBox x:Name="productTypeSelector"
                                              Classes="modern"
                                              Width="220" FontSize="12"
                                              SelectedIndex="0"
                                              HorizontalAlignment="Left"
                                              SelectionChanged="productTypeSelector_SelectionChanged">
                                        <ComboBoxItem Content="{x:Static loc:VbrLocalizationHelper.GuiProductTypeAuto}" IsSelected="True" />
                                        <ComboBoxItem Content="{x:Static loc:VbrLocalizationHelper.GuiProductTypeVbr}" />
                                        <ComboBoxItem Content="{x:Static loc:VbrLocalizationHelper.GuiProductTypeVb365}" />
                                        <ComboBoxItem Content="{x:Static loc:VbrLocalizationHelper.GuiProductTypeBoth}" />
                                    </ComboBox>
```

`Content` is set directly via `{x:Static}` here rather than `x:Name` + `SetUiText()` — see the note at the top of this step. `productTypeSelector_SelectionChanged` already reads `SelectedIndex`, never `Content` (`VhcGui.axaml.cs:1013-1024`), so this is safe.

Options card — find:

```xml
                                    <TextBlock Text="Export Options" Classes="field-label" Margin="0,0,0,8" />
```

Replace with:

```xml
                                    <TextBlock x:Name="exportOptionsLabel" Classes="field-label" Margin="0,0,0,8" />
```

Find:

```xml
                                    <CheckBox Name="htmlCheckBox" Classes="modern"
                                              Checked="htmlChecked" Unchecked="htmlUnchecked"
                                              ToolTip.Tip="Display the HTML report in your default browser after generation" />

                                    <Separator Margin="0,12,0,12" />

                                    <TextBlock Text="Data Collection" Classes="field-label" Margin="0,0,0,8" />
                                    <StackPanel Orientation="Horizontal" Margin="0,8,0,0">
                                        <TextBlock Text="Collection Period:" VerticalAlignment="Center"
                                                   Classes="field-label" Margin="0,0,10,0" />
```

Replace with:

```xml
                                    <CheckBox Name="htmlCheckBox" Classes="modern"
                                              Checked="htmlChecked" Unchecked="htmlUnchecked" />

                                    <Separator Margin="0,12,0,12" />

                                    <TextBlock x:Name="dataCollectionLabel" Classes="field-label" Margin="0,0,0,8" />
                                    <StackPanel Orientation="Horizontal" Margin="0,8,0,0">
                                        <TextBlock x:Name="collectionPeriodLabel" VerticalAlignment="Center"
                                                   Classes="field-label" Margin="0,0,10,0" />
```

Find:

```xml
                                    <CheckBox x:Name="RescanBox" Classes="modern"
                                            Checked="RescanBox_Checked" Unchecked="RescanBox_Unchecked"
                                            Margin="0,8,0,0"
                                            ToolTip.Tip="Rescan for Hardware change" />

                                    <Separator Margin="0,12,0,12" />

                                    <TextBlock Text="Security &amp; Privacy" Classes="field-label" Margin="0,0,0,8" />
                                    <CheckBox x:Name="scrubBox" Classes="modern"
                                              Checked="HandleCheck" Unchecked="HandleUnchecked"
                                              ToolTip.Tip="Anonymize sensitive data according to Veeam KB 2462" />
                                    <CheckBox x:Name="clearCredsCheckBox" Classes="modern"
                                              Checked="clearCredsCheckBox_Checked" Unchecked="clearCredsCheckBox_Unchecked"
                                              IsChecked="False"
                                              ToolTip.Tip="Clear any previously saved credentials before running" />
```

Replace with:

```xml
                                    <CheckBox x:Name="RescanBox" Classes="modern"
                                            Checked="RescanBox_Checked" Unchecked="RescanBox_Unchecked"
                                            Margin="0,8,0,0" />

                                    <Separator Margin="0,12,0,12" />

                                    <TextBlock x:Name="securityPrivacyLabel" Classes="field-label" Margin="0,0,0,8" />
                                    <CheckBox x:Name="scrubBox" Classes="modern"
                                              Checked="HandleCheck" Unchecked="HandleUnchecked" />
                                    <CheckBox x:Name="clearCredsCheckBox" Classes="modern"
                                              Checked="clearCredsCheckBox_Checked" Unchecked="clearCredsCheckBox_Unchecked"
                                              IsChecked="False" />
```

Monitoring tab — find:

```xml
                            <TextBlock Text="Monitor Status" Classes="card-title" />
                            <StackPanel Margin="5,10,5,0">
                                <StackPanel Orientation="Horizontal" Margin="0,0,0,6">
                                    <TextBlock Text="Status: " Classes="secondary-text" VerticalAlignment="Center" />
                                    <TextBlock x:Name="monitorStatusText" FontSize="13" FontWeight="SemiBold"
                                               Foreground="{DynamicResource StatusNeutralBrush}" VerticalAlignment="Center" Text="Checking..." />
                                </StackPanel>

                                <TextBlock x:Name="monitorLastRunText" Classes="secondary-text" FontSize="11"
                                           Margin="0,0,0,10" IsVisible="False" TextWrapping="Wrap" />

                                <Grid ColumnDefinitions="*,8,*,8,*">
                                    <Button x:Name="monitorQuickSetupBtn" Grid.Column="0"
                                            Content="Quick Setup" Height="32" FontSize="11"
                                            Classes="primary" IsEnabled="False"
                                            Click="monitorQuickSetupBtn_Click"
                                            ToolTip.Tip="Install monitor using selected VBR server and stored credentials" />
                                    <Button x:Name="monitorVhcSetupBtn" Grid.Column="2"
                                            Content="Setup from VHC" Height="32" FontSize="11"
                                            Classes="secondary" IsEnabled="False"
                                            Click="monitorVhcSetupBtn_Click"
                                            ToolTip.Tip="Configure monitor using data collected from the last health check run" />
                                    <Button x:Name="monitorRunBtn" Grid.Column="4"
                                            Content="Run Now" Height="32" FontSize="11"
                                            Classes="secondary" IsEnabled="False"
                                            Click="monitorRunBtn_Click"
                                            ToolTip.Tip="Trigger an immediate monitor check" />
                                </Grid>
```

Replace with (the `Text="Checking..."` default is removed, not resx-backed — `InitializeMonitorStatus()` unconditionally overwrites `monitorStatusText.Text` synchronously in the constructor before the window is ever shown, so that literal is never actually visible to a user; see Task 3's note):

```xml
                            <TextBlock x:Name="monitorStatusHeader" Classes="card-title" />
                            <StackPanel Margin="5,10,5,0">
                                <StackPanel Orientation="Horizontal" Margin="0,0,0,6">
                                    <TextBlock x:Name="monitorStatusLabel" Classes="secondary-text" VerticalAlignment="Center" />
                                    <TextBlock x:Name="monitorStatusText" FontSize="13" FontWeight="SemiBold"
                                               Foreground="{DynamicResource StatusNeutralBrush}" VerticalAlignment="Center" />
                                </StackPanel>

                                <TextBlock x:Name="monitorLastRunText" Classes="secondary-text" FontSize="11"
                                           Margin="0,0,0,10" IsVisible="False" TextWrapping="Wrap" />

                                <Grid ColumnDefinitions="*,8,*,8,*">
                                    <Button x:Name="monitorQuickSetupBtn" Grid.Column="0"
                                            Height="32" FontSize="11"
                                            Classes="primary" IsEnabled="False"
                                            Click="monitorQuickSetupBtn_Click" />
                                    <Button x:Name="monitorVhcSetupBtn" Grid.Column="2"
                                            Height="32" FontSize="11"
                                            Classes="secondary" IsEnabled="False"
                                            Click="monitorVhcSetupBtn_Click" />
                                    <Button x:Name="monitorRunBtn" Grid.Column="4"
                                            Height="32" FontSize="11"
                                            Classes="secondary" IsEnabled="False"
                                            Click="monitorRunBtn_Click" />
                                </Grid>
```

Find:

```xml
                            <TextBlock Text="Alert Notifications" Classes="card-title" />
```

Replace with:

```xml
                            <TextBlock x:Name="alertNotificationsHeader" Classes="card-title" />
```

Find:

```xml
                                <StackPanel Orientation="Horizontal" Margin="0,0,0,12">
                                    <TextBlock Text="Min severity: " Classes="field-label"
                                               VerticalAlignment="Center" Margin="0,0,8,0" />
                                    <ComboBox x:Name="notifSeverityBox" Width="110"
                                              Classes="modern" Height="28" FontSize="12"
                                              SelectedIndex="0">
                                        <ComboBoxItem Content="warning" Tag="warning" IsSelected="True" />
                                        <ComboBoxItem Content="critical" Tag="critical" />
                                        <ComboBoxItem Content="ok" Tag="ok" />
                                    </ComboBox>
                                </StackPanel>
```

Replace with:

```xml
                                <StackPanel Orientation="Horizontal" Margin="0,0,0,12">
                                    <TextBlock x:Name="minSeverityLabel" Classes="field-label"
                                               VerticalAlignment="Center" Margin="0,0,8,0" />
                                    <ComboBox x:Name="notifSeverityBox" Width="110"
                                              Classes="modern" Height="28" FontSize="12"
                                              SelectedIndex="0">
                                        <ComboBoxItem Content="{x:Static loc:VbrLocalizationHelper.GuiNotifSeverityWarning}" Tag="warning" IsSelected="True" />
                                        <ComboBoxItem Content="{x:Static loc:VbrLocalizationHelper.GuiNotifSeverityCritical}" Tag="critical" />
                                        <ComboBoxItem Content="{x:Static loc:VbrLocalizationHelper.GuiNotifSeverityOk}" Tag="ok" />
                                    </ComboBox>
                                </StackPanel>
```

`Content` is set via `{x:Static}` for the same reason as `productTypeSelector` above — `Tag` (from Task 1) is untouched and still carries the fixed English protocol value the correctness fix depends on.

Bottom bar — find:

```xml
                    <TextBlock Name="progressText"
                               Classes="secondary-text"
                               Text="Processing health check..."
                               Margin="0,6,0,0"
                               HorizontalAlignment="Center"
                               Opacity="0" />
```

Replace with:

```xml
                    <TextBlock Name="progressText"
                               Classes="secondary-text"
                               Margin="0,6,0,0"
                               HorizontalAlignment="Center"
                               Opacity="0" />
```

- [ ] **Step 6: Update `SetUiText()`**

In `vHC/HC_Reporting/VhcGui.axaml.cs`, find the end of `SetUiText()`:

```csharp
            this.serverLabel.Text = VbrLocalizationHelper.GuiServerLabel;
            ToolTip.SetTip(this.manageServersBtn, VbrLocalizationHelper.GuiManageServersTooltip);
        }
```

Replace with:

```csharp
            this.serverLabel.Text = VbrLocalizationHelper.GuiServerLabel;
            ToolTip.SetTip(this.manageServersBtn, VbrLocalizationHelper.GuiManageServersTooltip);

            // Stage D: resx-back the strings VhcGui.axaml previously hardcoded.
            this.appHeaderText.Text = VbrLocalizationHelper.GuiTitle;
            ToolTip.SetTip(this.importButton, VbrLocalizationHelper.GuiImportTooltip);
            ToolTip.SetTip(this.ThemeToggleButton, VbrLocalizationHelper.GuiThemeToggleTooltip);
            this.aboutButton.Content = VbrLocalizationHelper.GuiAboutButton;
            this.AdHocTabButton.Content = VbrLocalizationHelper.GuiAdHocTab;
            this.MonitoringTabButton.Content = VbrLocalizationHelper.GuiMonitoringTab;
            this.serverCardTitle.Text = VbrLocalizationHelper.GuiServerCardTitle;
            this.productTypeLabel.Text = VbrLocalizationHelper.GuiProductTypeLabel;
            ToolTip.SetTip(this.productTypeSelector, VbrLocalizationHelper.GuiProductTypeTooltip);
            this.exportOptionsLabel.Text = VbrLocalizationHelper.GuiExportOptionsLabel;
            ToolTip.SetTip(this.htmlCheckBox, VbrLocalizationHelper.GuiHtmlReportTooltip);
            this.dataCollectionLabel.Text = VbrLocalizationHelper.GuiDataCollectionLabel;
            this.collectionPeriodLabel.Text = VbrLocalizationHelper.GuiCollectionPeriodLabel;
            ToolTip.SetTip(this.RescanBox, VbrLocalizationHelper.GuiRescanTooltip);
            this.securityPrivacyLabel.Text = VbrLocalizationHelper.GuiSecurityPrivacyLabel;
            ToolTip.SetTip(this.scrubBox, VbrLocalizationHelper.GuiScrubTooltip);
            ToolTip.SetTip(this.clearCredsCheckBox, VbrLocalizationHelper.GuiClearCredsTooltip);
            this.monitorStatusHeader.Text = VbrLocalizationHelper.GuiMonitorStatusHeader;
            this.monitorStatusLabel.Text = VbrLocalizationHelper.GuiMonitorStatusLabel;
            ToolTip.SetTip(this.monitorQuickSetupBtn, VbrLocalizationHelper.GuiMonitorQuickSetupTooltip);
            this.monitorVhcSetupBtn.Content = VbrLocalizationHelper.GuiMonitorVhcSetup;
            ToolTip.SetTip(this.monitorVhcSetupBtn, VbrLocalizationHelper.GuiMonitorVhcSetupTooltip);
            this.monitorRunBtn.Content = VbrLocalizationHelper.GuiMonitorRunNow;
            ToolTip.SetTip(this.monitorRunBtn, VbrLocalizationHelper.GuiMonitorRunTooltip);
            this.alertNotificationsHeader.Text = VbrLocalizationHelper.GuiAlertNotificationsHeader;
            this.minSeverityLabel.Text = VbrLocalizationHelper.GuiMinSeverityLabel;
            this.progressText.Text = VbrLocalizationHelper.GuiProcessingText;
        }
```

`productTypeAutoItem`/`productTypeVbrItem`/`productTypeVb365Item`/`productTypeBothItem` and `notifSeverityWarningItem`/`notifSeverityCriticalItem`/`notifSeverityOkItem` do **not** appear here — their `Content` is set via `{x:Static}` directly in the XAML (Step 5), not through `SetUiText()`. Do not add lines for them; doing so would just set `Content` to the same value a second time, uselessly, without fixing anything (the snapshot already happened at attach time using the `{x:Static}` value, which was already correct).

- [ ] **Step 7: Update the code-behind-set tooltip in `SetUiSync()`**

Find:

```csharp
            if (CGlobals.IsVb365 && CGlobals.IsVbr)
            {
                pdfCheckBox.IsEnabled = false;
                ToolTip.SetTip(pdfCheckBox, "PDF Export not available when both VB365 & VBR are detected on the same machine.");
            }
```

Replace with:

```csharp
            if (CGlobals.IsVb365 && CGlobals.IsVbr)
            {
                pdfCheckBox.IsEnabled = false;
                ToolTip.SetTip(pdfCheckBox, VbrLocalizationHelper.GuiPdfUnavailableTooltip);
            }
```

- [ ] **Step 8: Build and verify every key resolves**

```bash
dotnet build vHC/HC.sln --configuration Debug
git checkout -- vHC/HC_Reporting/VeeamHealthCheck.csproj
```

Expected: 0 errors. If `{x:Static loc:VbrLocalizationHelper.GuiXxx}` references a key that doesn't exist, this build step *does* catch it (unlike a `SetUiText()` typo, which only fails at runtime) — the XAML compiler resolves `x:Static` targets at compile time. A blank-label typo in a `SetUiText()`-driven control cannot be caught by the build — Task 6's guard test catches it going forward, but for *this* task, re-read every `x:Name` you just added in the XAML (the 11 plain `Text`/`Content`/tooltip targets, not the 7 `{x:Static}` `ComboBoxItem`s) against every reference in `SetUiText()` character-for-character now, while the diff is small: every `x:Name` introduced in Step 5 must appear exactly once on the left-hand side of an assignment in Step 6 or 7.

- [ ] **Step 9: Confirm the excluded strings are untouched**

```bash
grep -n 'Content="&#x2699;"\|Content="\.\.\."\|Content="ntfy"\|Content="Teams"\|Content="Slack"\|Content="PagerDuty"' vHC/HC_Reporting/VhcGui.axaml
```

Expected: 6 matches (gear icon, ellipsis, and the 4 notif-type brand names) — these must still be present as literal `Content` values; only their `Tag` (added in Task 1) changed.

- [ ] **Step 10: Commit**

```bash
git add vHC/HC_Reporting/VhcGui.axaml vHC/HC_Reporting/VhcGui.axaml.cs vHC/HC_Reporting/Resources/Localization/
git commit -m "feat(l10n): resx-back VhcGui.axaml's remaining hardcoded strings"
```

---

## Task 3: Code-behind sweep — resx-back `VhcGui.axaml.cs`'s hardcoded strings

**Files:**
- Modify: `vHC/HC_Reporting/VhcGui.axaml.cs` (`SetUiText()`, `ThemeLabelFor()`, `UpdateCollectionStatusText()`, `InitializeMonitorStatus()`, `monitorQuickSetupBtn_Click()`, `monitorVhcSetupBtn_Click()`, `monitorRunBtn_Click()`, `OfferMonitorSetupIfNeeded()`)
- Modify: `vHC/HC_Reporting/Resources/Localization/vhcres.resx`
- Modify: `vHC/HC_Reporting/Resources/Localization/VbrLocalizationHelper.cs` (UTF-16LE)
- Modify: `vHC/HC_Reporting/Resources/Localization/vhcres.txt` (UTF-16LE)

This task adds 17 keys: 14 for the 16 hardcoded sites (`"Setup failed — check log"` and `"Available — not set up"` each collapse two sites into one shared key) plus 3 for `ThemeLabelFor()`'s decorative-glyph-plus-word strings (only the word half is resx-backed; the emoji stays hardcoded per the spec's non-goals).

- [ ] **Step 1: Add 17 keys to `vhcres.resx`**

```bash
python3 - <<'PY'
import io

def esc(s):
    return s.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")

pairs = [
    ("GuiExportPdfLabel", "Export PDF"),
    ("GuiClearCredsLabel", "Clear Saved Credentials"),
    ("GuiCollectionCompleteWarnings", "Collection complete — {0} collector warning(s)"),
    ("GuiCollectionComplete", "Collection complete"),
    ("GuiMonitorNotBundled", "Not bundled"),
    ("GuiMonitorAvailableNotSetUp", "Available — not set up"),
    ("GuiMonitorRunningVersion", "Running ({0})"),
    ("GuiMonitorReconfigure", "Reconfigure"),
    ("GuiMonitorLastRun", "Last run: {0} — {1}"),
    ("GuiMonitorInstalling", "Installing..."),
    ("GuiMonitorSetupFailed", "Setup failed — check log"),
    ("GuiMonitorInstallingFromVhc", "Installing from VHC data..."),
    ("GuiMonitorCheckInProgress", "Running..."),
    ("GuiMonitorCompleteSetupPrompt", "Health check complete — click 'Setup from VHC' to configure continuous monitoring with auto-detected server settings."),
    ("GuiThemeDark", "Dark"),
    ("GuiThemeLight", "Light"),
    ("GuiThemeSystem", "System"),
]

p = "vHC/HC_Reporting/Resources/Localization/vhcres.resx"
s = io.open(p, encoding="utf-8").read()
block = "".join(
    '  <data name="%s" xml:space="preserve">\n    <value>%s</value>\n  </data>\n' % (k, esc(v))
    for k, v in pairs
)
assert "</root>" in s
s = s.replace("</root>", block + "</root>")
io.open(p, "w", encoding="utf-8").write(s)
print("inserted", len(pairs), "keys into vhcres.resx")
PY
```

Expected: `inserted 17 keys into vhcres.resx`.

- [ ] **Step 2: Add the accessors to `VbrLocalizationHelper.cs`, encoding-preserving**

```bash
python3 - <<'PY'
import io
p = "vHC/HC_Reporting/Resources/Localization/VbrLocalizationHelper.cs"
keys = [
    "GuiExportPdfLabel", "GuiClearCredsLabel", "GuiCollectionCompleteWarnings",
    "GuiCollectionComplete", "GuiMonitorNotBundled", "GuiMonitorAvailableNotSetUp",
    "GuiMonitorRunningVersion", "GuiMonitorReconfigure", "GuiMonitorLastRun",
    "GuiMonitorInstalling", "GuiMonitorSetupFailed", "GuiMonitorInstallingFromVhc",
    "GuiMonitorCheckInProgress", "GuiMonitorCompleteSetupPrompt",
    "GuiThemeDark", "GuiThemeLight", "GuiThemeSystem",
]
s = io.open(p, encoding="utf-16-le", newline="").read()
marker = "}}"
assert s.rstrip().endswith(marker), "unexpected helper tail; inspect before editing"
tail_at = s.rstrip().rfind(marker)
block = "".join('public static string %s = m4.GetString("%s");\r\n' % (k, k) for k in keys)
s = s[:tail_at] + block + s[tail_at:]
io.open(p, "w", encoding="utf-16-le", newline="").write(s)
print("inserted", len(keys), "accessors")
PY
```

Expected: `inserted 17 accessors`.

- [ ] **Step 3: Verify the encoding survived**

```bash
file vHC/HC_Reporting/Resources/Localization/VbrLocalizationHelper.cs
iconv -f UTF-16LE -t UTF-8 vHC/HC_Reporting/Resources/Localization/VbrLocalizationHelper.cs | grep -c 'GuiMonitor\|GuiTheme'
```

Expected: `Unicode text, UTF-16, little-endian text, with CRLF line terminators` and a count of `23` (19 lines matching `GuiMonitor` — Task 2's 8 `GuiMonitor*` plus `GuiMonitoringTab`, plus this task's 10 `GuiMonitor*` — + 4 lines matching `GuiTheme` — Task 2's `GuiThemeToggleTooltip` plus this task's 3 `GuiTheme*`). **If `file` reports UTF-8, stop and `git checkout --` the file.**

- [ ] **Step 4: Append the matching entries to `vhcres.txt`**

```bash
python3 - <<'PY'
import io
p = "vHC/HC_Reporting/Resources/Localization/vhcres.txt"
pairs = [
    ("GuiExportPdfLabel", "Export PDF"),
    ("GuiClearCredsLabel", "Clear Saved Credentials"),
    ("GuiCollectionCompleteWarnings", "Collection complete - {0} collector warning(s)"),
    ("GuiCollectionComplete", "Collection complete"),
    ("GuiMonitorNotBundled", "Not bundled"),
    ("GuiMonitorAvailableNotSetUp", "Available - not set up"),
    ("GuiMonitorRunningVersion", "Running ({0})"),
    ("GuiMonitorReconfigure", "Reconfigure"),
    ("GuiMonitorLastRun", "Last run: {0} - {1}"),
    ("GuiMonitorInstalling", "Installing..."),
    ("GuiMonitorSetupFailed", "Setup failed - check log"),
    ("GuiMonitorInstallingFromVhc", "Installing from VHC data..."),
    ("GuiMonitorCheckInProgress", "Running..."),
    ("GuiMonitorCompleteSetupPrompt", "Health check complete - click 'Setup from VHC' to configure continuous monitoring with auto-detected server settings."),
    ("GuiThemeDark", "Dark"),
    ("GuiThemeLight", "Light"),
    ("GuiThemeSystem", "System"),
]
s = io.open(p, encoding="utf-16-le", newline="").read()
if not s.endswith("\r\n"):
    s += "\r\n"
s += "".join("%s = %s\r\n" % kv for kv in pairs)
io.open(p, "w", encoding="utf-16-le", newline="").write(s)
print("appended", len(pairs), "entries")
PY
file vHC/HC_Reporting/Resources/Localization/vhcres.txt
```

Note: this file's `Key = Value` format uses a plain hyphen for the em-dash in the *source* pairs above deliberately — `vhcres.txt` is the (inert, see Context) ResGen source and is not what ships; `vhcres.resx` in Step 1 carries the real em-dash (`—`). Keeping `vhcres.txt` ASCII-only here avoids a second UTF-16LE em-dash round-trip to verify on a file nothing actually consumes. Expected: `appended 17 entries` and still UTF-16LE.

- [ ] **Step 5: Update `SetUiText()`'s two remaining hardcoded lines**

Find:

```csharp
            this.pdfCheckBox.Content = "Export PDF";
            // this.pptxCheckBox.Content = "Export PowerPoint";
            this.clearCredsCheckBox.Content = "Clear Saved Credentials";
```

Replace with:

```csharp
            this.pdfCheckBox.Content = VbrLocalizationHelper.GuiExportPdfLabel;
            // this.pptxCheckBox.Content = "Export PowerPoint";
            this.clearCredsCheckBox.Content = VbrLocalizationHelper.GuiClearCredsLabel;
```

- [ ] **Step 6: Update `ThemeLabelFor()`**

Find:

```csharp
        private static string ThemeLabelFor(ThemeVariant variant) =>
            variant == ThemeVariant.Dark ? "🌙 Dark" :
            variant == ThemeVariant.Light ? "☀ Light" : "🖥 System";
```

Replace with:

```csharp
        private static string ThemeLabelFor(ThemeVariant variant) =>
            variant == ThemeVariant.Dark ? $"🌙 {VbrLocalizationHelper.GuiThemeDark}" :
            variant == ThemeVariant.Light ? $"☀ {VbrLocalizationHelper.GuiThemeLight}" : $"🖥 {VbrLocalizationHelper.GuiThemeSystem}";
```

- [ ] **Step 7: Update `UpdateCollectionStatusText()`**

Find:

```csharp
        private void UpdateCollectionStatusText()
        {
            var failed = CGlobals.CollectionManifest?.Where(e => !e.Success).ToList();
            if (failed != null && failed.Count > 0)
            {
                Dispatcher.UIThread.Invoke(() =>
                {
                    progressText.Text = $"Collection complete — {failed.Count} collector warning(s)";
                    progressText.Foreground = GetStatusBrush("StatusWarningBrush");
                });
            }
            else
            {
                Dispatcher.UIThread.Invoke(() =>
                {
                    progressText.Text = "Collection complete";
                    progressText.Foreground = GetStatusBrush("StatusSuccessBrush");
                });
            }
        }
```

Replace with:

```csharp
        private void UpdateCollectionStatusText()
        {
            var failed = CGlobals.CollectionManifest?.Where(e => !e.Success).ToList();
            if (failed != null && failed.Count > 0)
            {
                Dispatcher.UIThread.Invoke(() =>
                {
                    progressText.Text = string.Format(VbrLocalizationHelper.GuiCollectionCompleteWarnings, failed.Count);
                    progressText.Foreground = GetStatusBrush("StatusWarningBrush");
                });
            }
            else
            {
                Dispatcher.UIThread.Invoke(() =>
                {
                    progressText.Text = VbrLocalizationHelper.GuiCollectionComplete;
                    progressText.Foreground = GetStatusBrush("StatusSuccessBrush");
                });
            }
        }
```

- [ ] **Step 8: Update `InitializeMonitorStatus()`**

Find:

```csharp
        private void InitializeMonitorStatus()
        {
            bool bundled = CVhcMonitorIntegration.IsExePresentInBundle();
            bool installed = CVhcMonitorIntegration.IsInstalled();
            bool taskActive = CVhcMonitorIntegration.IsTaskRegistered();

            if (!bundled)
            {
                monitorStatusText.Text = "Not bundled";
                monitorStatusText.Foreground = GetStatusBrush("StatusNeutralBrush");
                monitorQuickSetupBtn.IsEnabled = false;
                monitorVhcSetupBtn.IsEnabled = false;
                monitorRunBtn.IsEnabled = false;
            }
            else if (!installed || !taskActive)
            {
                monitorStatusText.Text = "Available — not set up";
                monitorStatusText.Foreground = GetStatusBrush("StatusWarningBrush");
                monitorQuickSetupBtn.IsEnabled = true;
                monitorRunBtn.IsEnabled = false;
            }
            else
            {
                string version = CVhcMonitorIntegration.GetInstalledVersion();
                monitorStatusText.Text = $"Running ({version})";
                monitorStatusText.Foreground = GetStatusBrush("StatusSuccessBrush");
                monitorQuickSetupBtn.Content = "Reconfigure";
                monitorQuickSetupBtn.IsEnabled = true;
                monitorRunBtn.IsEnabled = true;

                var status = CVhcMonitorIntegration.GetLastRunStatus();
                if (status != null)
                {
                    monitorLastRunText.Text = $"Last run: {status.Timestamp:g} — {status.Summary}";
                    monitorLastRunText.IsVisible = true;
                }
            }
        }
```

Replace with:

```csharp
        private void InitializeMonitorStatus()
        {
            bool bundled = CVhcMonitorIntegration.IsExePresentInBundle();
            bool installed = CVhcMonitorIntegration.IsInstalled();
            bool taskActive = CVhcMonitorIntegration.IsTaskRegistered();

            // Set before the branches below so every state has a correct label as
            // soon as this method runs (constructor time) - the "else" branch
            // overrides it to GuiMonitorReconfigure when the monitor is already
            // installed. This line moved here from SetUiText() (Task 2), which runs
            // later on Loaded and would otherwise either leave the button blank
            // until then (bundled/not-installed branches) or briefly show the
            // correct "Reconfigure" text and then wrongly revert it back to
            // "Quick Setup" once SetUiText() finally ran (already-installed branch).
            monitorQuickSetupBtn.Content = VbrLocalizationHelper.GuiMonitorQuickSetup;

            if (!bundled)
            {
                monitorStatusText.Text = VbrLocalizationHelper.GuiMonitorNotBundled;
                monitorStatusText.Foreground = GetStatusBrush("StatusNeutralBrush");
                monitorQuickSetupBtn.IsEnabled = false;
                monitorVhcSetupBtn.IsEnabled = false;
                monitorRunBtn.IsEnabled = false;
            }
            else if (!installed || !taskActive)
            {
                monitorStatusText.Text = VbrLocalizationHelper.GuiMonitorAvailableNotSetUp;
                monitorStatusText.Foreground = GetStatusBrush("StatusWarningBrush");
                monitorQuickSetupBtn.IsEnabled = true;
                monitorRunBtn.IsEnabled = false;
            }
            else
            {
                string version = CVhcMonitorIntegration.GetInstalledVersion();
                monitorStatusText.Text = string.Format(VbrLocalizationHelper.GuiMonitorRunningVersion, version);
                monitorStatusText.Foreground = GetStatusBrush("StatusSuccessBrush");
                monitorQuickSetupBtn.Content = VbrLocalizationHelper.GuiMonitorReconfigure;
                monitorQuickSetupBtn.IsEnabled = true;
                monitorRunBtn.IsEnabled = true;

                var status = CVhcMonitorIntegration.GetLastRunStatus();
                if (status != null)
                {
                    monitorLastRunText.Text = string.Format(VbrLocalizationHelper.GuiMonitorLastRun, status.Timestamp?.ToString("g") ?? string.Empty, status.Summary);
                    monitorLastRunText.IsVisible = true;
                }
            }
        }
```

Note: `monitorStatusText`'s XAML default (`Text="Checking..."`, removed in Task 2 Step 5) is never visible — every branch above unconditionally sets `monitorStatusText.Text`, and this method runs synchronously in the constructor (`this.InitializeMonitorStatus();`) before the window is shown. The same reasoning is why `monitorQuickSetupBtn.Content` is set unconditionally here rather than in `SetUiText()` — see Task 2 Step 6's note on `SetUiText()` for the general rule this follows: anything a constructor-time method (this one) also writes must be set no later than that method, not left to `SetUiText()`, which runs on `Loaded`.

- [ ] **Step 9: Update the three monitor button click handlers**

Find (`monitorQuickSetupBtn_Click`):

```csharp
            monitorQuickSetupBtn.IsEnabled = false;
            monitorStatusText.Text = "Installing...";

            var (notifType, notifUrl, minSeverity) = this.GetNotifSettings();

            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    CVhcMonitorIntegration.Install(server, username, password, notifType, notifUrl, minSeverity);
                    Dispatcher.UIThread.Post(this.InitializeMonitorStatus);
                }
                catch (Exception ex)
                {
                    CGlobals.Logger.Error($"Monitor setup failed: {ex.Message}", false);
                    Dispatcher.UIThread.Post(() =>
                    {
                        monitorStatusText.Text = "Setup failed — check log";
                        monitorStatusText.Foreground = GetStatusBrush("StatusErrorBrush");
                        monitorQuickSetupBtn.IsEnabled = true;
                    });
                }
            });
```

Replace with:

```csharp
            monitorQuickSetupBtn.IsEnabled = false;
            monitorStatusText.Text = VbrLocalizationHelper.GuiMonitorInstalling;

            var (notifType, notifUrl, minSeverity) = this.GetNotifSettings();

            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    CVhcMonitorIntegration.Install(server, username, password, notifType, notifUrl, minSeverity);
                    Dispatcher.UIThread.Post(this.InitializeMonitorStatus);
                }
                catch (Exception ex)
                {
                    CGlobals.Logger.Error($"Monitor setup failed: {ex.Message}", false);
                    Dispatcher.UIThread.Post(() =>
                    {
                        monitorStatusText.Text = VbrLocalizationHelper.GuiMonitorSetupFailed;
                        monitorStatusText.Foreground = GetStatusBrush("StatusErrorBrush");
                        monitorQuickSetupBtn.IsEnabled = true;
                    });
                }
            });
```

Find (`monitorVhcSetupBtn_Click`):

```csharp
            monitorVhcSetupBtn.IsEnabled = false;
            monitorStatusText.Text = "Installing from VHC data...";

            var (notifType, notifUrl, minSeverity) = this.GetNotifSettings();

            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    CVhcMonitorIntegration.InstallFromVhcData(notifType, notifUrl, minSeverity);
                    Dispatcher.UIThread.Post(this.InitializeMonitorStatus);
                }
                catch (Exception ex)
                {
                    CGlobals.Logger.Error($"Monitor VHC-assisted setup failed: {ex.Message}", false);
                    Dispatcher.UIThread.Post(() =>
                    {
                        monitorStatusText.Text = "Setup failed — check log";
                        monitorStatusText.Foreground = GetStatusBrush("StatusErrorBrush");
                        monitorVhcSetupBtn.IsEnabled = true;
                    });
                }
            });
```

Replace with:

```csharp
            monitorVhcSetupBtn.IsEnabled = false;
            monitorStatusText.Text = VbrLocalizationHelper.GuiMonitorInstallingFromVhc;

            var (notifType, notifUrl, minSeverity) = this.GetNotifSettings();

            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    CVhcMonitorIntegration.InstallFromVhcData(notifType, notifUrl, minSeverity);
                    Dispatcher.UIThread.Post(this.InitializeMonitorStatus);
                }
                catch (Exception ex)
                {
                    CGlobals.Logger.Error($"Monitor VHC-assisted setup failed: {ex.Message}", false);
                    Dispatcher.UIThread.Post(() =>
                    {
                        monitorStatusText.Text = VbrLocalizationHelper.GuiMonitorSetupFailed;
                        monitorStatusText.Foreground = GetStatusBrush("StatusErrorBrush");
                        monitorVhcSetupBtn.IsEnabled = true;
                    });
                }
            });
```

Find (`monitorRunBtn_Click`):

```csharp
            monitorRunBtn.IsEnabled = false;
            monitorLastRunText.Text = "Running...";
            monitorLastRunText.IsVisible = true;
```

Replace with:

```csharp
            monitorRunBtn.IsEnabled = false;
            monitorLastRunText.Text = VbrLocalizationHelper.GuiMonitorCheckInProgress;
            monitorLastRunText.IsVisible = true;
```

- [ ] **Step 10: Update `OfferMonitorSetupIfNeeded()`**

Find:

```csharp
            Dispatcher.UIThread.Invoke(() =>
            {
                monitorVhcSetupBtn.IsEnabled = true;
                monitorLastRunText.Text = "Health check complete — click 'Setup from VHC' to configure continuous monitoring with auto-detected server settings.";
                monitorLastRunText.IsVisible = true;
                monitorStatusText.Text = "Available — not set up";
                monitorStatusText.Foreground = GetStatusBrush("StatusWarningBrush");
            });
```

Replace with:

```csharp
            Dispatcher.UIThread.Invoke(() =>
            {
                monitorVhcSetupBtn.IsEnabled = true;
                monitorLastRunText.Text = VbrLocalizationHelper.GuiMonitorCompleteSetupPrompt;
                monitorLastRunText.IsVisible = true;
                monitorStatusText.Text = VbrLocalizationHelper.GuiMonitorAvailableNotSetUp;
                monitorStatusText.Foreground = GetStatusBrush("StatusWarningBrush");
            });
```

- [ ] **Step 11: Build and confirm no hardcoded strings remain at the swept sites**

```bash
dotnet build vHC/HC.sln --configuration Debug
git checkout -- vHC/HC_Reporting/VeeamHealthCheck.csproj
grep -n 'monitorStatusText.Text = "\|monitorLastRunText.Text = "\|progressText.Text = "\|monitorQuickSetupBtn.Content = "' vHC/HC_Reporting/VhcGui.axaml.cs
```

Expected: 0 build errors, and the `grep` returns **no matches** (every one of these assignment sites now reads from `VbrLocalizationHelper` or `string.Format`).

- [ ] **Step 12: Commit**

```bash
git add vHC/HC_Reporting/VhcGui.axaml.cs vHC/HC_Reporting/Resources/Localization/
git commit -m "feat(l10n): resx-back VhcGui.axaml.cs's remaining hardcoded strings"
```

---

## Task 4: Fix the pre-existing orphaned/mistranslated keys in the four locale files

**Files:**
- Modify: `vHC/HC_Reporting/Resources/Localization/vhcres.fR-FR.resx` (17 renames, 9 of which also get an English value fill — see below)
- Modify: `vHC/HC_Reporting/Resources/Localization/vhcres.ja.resx` (1 rename)
- Modify: `vHC/HC_Reporting/Resources/Localization/vhcres.zh-cn.resx` (1 rename)
- Modify: `vHC/HC_Reporting/Resources/Localization/vhcres.zh-tw.resx` (1 rename)

This is mostly a pure rename — no translation — but **9 of fR-FR's 17 keys need more than a rename**. `SbrExt8/10-14` and `Titre Sbr`→`SbrTitle` (8 keys) plus `HtmlIntroLine3`→`HtmlIntroLine3Original` are real, already-written French translations that are silently dead today because their `<data name="...">` doesn't match the neutral resx's key name — renaming alone recovers them. But `v365NavValeur0,1,2,3,4,5,7,8,9` (9 keys) have **empty `<value>` elements** — confirmed by direct inspection, not a rendering artifact. Renaming an empty value from a wrong key name to the correct one does not create a translation; it just moves the emptiness somewhere new, and an *empty* satellite value is worse than a *missing* one — `ResourceManager` falls back to the neutral English text for a missing key, but an empty string is itself a valid value and suppresses that fallback. Today these 9 show correct English (because the key is missing under its correct name); a naive rename would make them render blank in French. So for these 9, this step renames *and* fills the value with the current English neutral text (the same policy Task 5 uses for brand-new keys), and lists them in `untranslated-keys.txt` in Task 5.

`HtmlIntroLine3` (all four files) predates the neutral resx being split into `HtmlIntroLine3Anon`/`HtmlIntroLine3Original`; its old content (a single generic path, no Anonymous/Original distinction) is closest to `HtmlIntroLine3Original`, so that is the rename target. `HtmlIntroLine3Anon` remains genuinely new/untranslated in every locale — Task 5 covers it like any other new key.

- [ ] **Step 1: Rename fR-FR's 17 keys, and fill the 9 that are empty**

```bash
python3 - <<'PY'
import io
p = "vHC/HC_Reporting/Resources/Localization/vhcres.fR-FR.resx"
renames = [
    ("HtmlIntroLine3", "HtmlIntroLine3Original"),
    ("SbrExt 8", "SbrExt8"),
    ("SbrExt 10", "SbrExt10"),
    ("SbrExt 11", "SbrExt11"),
    ("SbrExt 12", "SbrExt12"),
    ("SbrExt 13", "SbrExt13"),
    ("SbrExt 14", "SbrExt14"),
    ("Titre Sbr", "SbrTitle"),
    ("v365NavValeur0", "v365NavValue0"),
    ("v365NavValeur1", "v365NavValue1"),
    ("v365NavValeur2", "v365NavValue2"),
    ("v365NavValeur3", "v365NavValue3"),
    ("v365NavValeur4", "v365NavValue4"),
    ("v365NavValeur5", "v365NavValue5"),
    ("v365NavValeur7", "v365NavValue7"),
    ("v365NavValeur8", "v365NavValue8"),
    ("v365NavValeur9", "v365NavValue9"),
]
# These 9 renamed keys have an EMPTY <value> today (confirmed by inspection) -
# fill them with the current English neutral text rather than shipping an empty
# string, which would suppress ResourceManager's normal missing-key fallback.
empty_key_english_fill = {
    "v365NavValue0": "Summary of license and global settings.",
    "v365NavValue1": "Summary of protected and unprotected users.",
    "v365NavValue2": "Details on the VB365 Server.",
    "v365NavValue3": "Details on the disks attached to the VB365 Server.",
    "v365NavValue4": "Details on proxies added to the current VB365 Server.",
    "v365NavValue5": "Details on reposiories added to the current VB365.",
    "v365NavValue7": "Summary of security settings relevant to VB365.",
    "v365NavValue8": "Summary of Role Based Access Control settings.",
    "v365NavValue9": "Summary of permissions granted &amp; used.",
}

s = io.open(p, encoding="utf-8").read()
for old, new in renames:
    needle = '<data name="%s" xml:space="preserve">' % old
    replacement = '<data name="%s" xml:space="preserve">' % new
    assert needle in s, "not found: %r" % old
    assert s.count(needle) == 1, "not unique: %r" % old
    s = s.replace(needle, replacement)

for key, english in empty_key_english_fill.items():
    needle = '<data name="%s" xml:space="preserve">\n    <value></value>\n  </data>' % key
    replacement = '<data name="%s" xml:space="preserve">\n    <value>%s</value>\n  </data>' % (key, english)
    assert needle in s, "empty-value block not found or format differs for %r - inspect the file before proceeding" % key
    s = s.replace(needle, replacement)

io.open(p, "w", encoding="utf-8").write(s)
print("renamed", len(renames), "keys,", "filled", len(empty_key_english_fill), "empty values")
PY
```

Expected: `renamed 17 keys, filled 9 empty values`. If the `assert` on an empty-value block fails, the actual whitespace/formatting of that `<data>` element differs from what's assumed here — open the file and check the exact block around that key name before adjusting the script; do not weaken the assertion to skip it silently.

- [ ] **Step 2: Rename `HtmlIntroLine3` in ja/zh-cn/zh-tw**

```bash
python3 - <<'PY'
import io
for locale in ["ja", "zh-cn", "zh-tw"]:
    p = "vHC/HC_Reporting/Resources/Localization/vhcres.%s.resx" % locale
    s = io.open(p, encoding="utf-8").read()
    needle = '<data name="HtmlIntroLine3" xml:space="preserve">'
    replacement = '<data name="HtmlIntroLine3Original" xml:space="preserve">'
    assert needle in s, "not found in %s" % locale
    assert s.count(needle) == 1, "not unique in %s" % locale
    s = s.replace(needle, replacement)
    io.open(p, "w", encoding="utf-8").write(s)
    print("renamed HtmlIntroLine3 in", locale)
PY
```

- [ ] **Step 3: Verify zero orphans remain**

```bash
for f in vhcres.fR-FR.resx vhcres.ja.resx vhcres.zh-cn.resx vhcres.zh-tw.resx; do
  echo "--- $f ---"
  grep -o '<data name="[^"]*"' vHC/HC_Reporting/Resources/Localization/$f | sed 's/<data name="//;s/"$//' | sort -u > /tmp/keys_after_$f.txt
  grep -o '<data name="[^"]*"' vHC/HC_Reporting/Resources/Localization/vhcres.resx | sed 's/<data name="//;s/"$//' | sort -u > /tmp/keys_neutral_now.txt
  comm -13 /tmp/keys_neutral_now.txt /tmp/keys_after_$f.txt
done
```

Expected: empty output under every `---` header (zero orphans in every locale). Note: run this *after* Tasks 2-3 have already added their new keys to `vhcres.resx` — those new keys are correctly absent from the locale files at this point (Task 5 adds them next) and will show up as "missing," not "orphan," which is expected and not what this check is for.

### Correction applied after review (commit `39be2be5`)

Step 2's `HtmlIntroLine3` → `HtmlIntroLine3Original` rename was reverted in all four locale files: `CHtmlCompiler.cs:270` interpolates the value into `<dd>{1}</dd>` with no closing tag (unlike `HtmlIntroLine1`, which gets `"</a>\n"` appended in code), and none of the four renamed locale values had a closing `</a>` of their own (fR-FR/ja/zh-cn end mid-sentence on an open `<a href="...">`; zh-tw is a bare `<a href` fragment with no `=`/quotes/`>` at all). Renaming them onto the live key activated malformed HTML in the report's About card. The revert restores `HtmlIntroLine3` as a deliberate, permanent orphan — the translated content is preserved under the old key name (not deleted) for a future translator to produce well-formed `HtmlIntroLine3Original`/`HtmlIntroLine3Anon` replacements with a correct closing tag and the current (post-path-split) `JobSessionReports` path.

**Corrected totals for Task 4: 16 renames (not 20) — 7 dead translations recovered (`SbrExt8/10/11/12/13/14`, `SbrTitle`) + 9 blank fR-FR `v365NavValue0/1/2/3/4/5/7/8/9` values filled with English text.** `HtmlIntroLine3` is intentionally excluded and remains orphaned in all four locale files.

**Step 3's expected output above is now stale.** Re-running that check after `39be2be5` correctly prints `HtmlIntroLine3` under all four `---` headers — this is the expected, deliberate exception, not a regression. Do not "fix" this by re-renaming or by deleting the four `<data name="HtmlIntroLine3">` blocks. See Task 6's `KnownDeliberateOrphans` allowlist below, which this exception feeds into.

- [ ] **Step 4: Build**

```bash
dotnet build vHC/HC.sln --configuration Debug
git checkout -- vHC/HC_Reporting/VeeamHealthCheck.csproj
```

Expected: 0 errors.

- [ ] **Step 5: Commit**

```bash
git add vHC/HC_Reporting/Resources/Localization/vhcres.fR-FR.resx vHC/HC_Reporting/Resources/Localization/vhcres.ja.resx vHC/HC_Reporting/Resources/Localization/vhcres.zh-cn.resx vHC/HC_Reporting/Resources/Localization/vhcres.zh-tw.resx
git commit -m "fix(l10n): rename 20 orphaned resx keys, recovering 11 dead translations and fixing 9 blank ones"
```

---

## Task 5: Key-parity for Stage D's new keys across the four locales

**Files:**
- Modify: `vHC/HC_Reporting/Resources/Localization/vhcres.fR-FR.resx`
- Modify: `vHC/HC_Reporting/Resources/Localization/vhcres.ja.resx`
- Modify: `vHC/HC_Reporting/Resources/Localization/vhcres.zh-cn.resx`
- Modify: `vHC/HC_Reporting/Resources/Localization/vhcres.zh-tw.resx`
- Create: `vHC/HC_Reporting/Resources/Localization/untranslated-keys.txt`

Per the spec (Design §4, per your decision on the translation-policy question): the 52 keys Tasks 2-3 added get copied into all four locale files with their **English** neutral value — not machine-translated — plus a checked-in to-do list for a future translator. This keeps every locale at full key-parity with neutral (so Task 6's orphan/missing checks pass cleanly) without shipping unaudited translation.

- [ ] **Step 1: Copy the 52 new keys into all four locale files**

```bash
python3 - <<'PY'
import io, re

new_keys = [
    "GuiImportTooltip", "GuiThemeToggleTooltip", "GuiAboutButton", "GuiAdHocTab",
    "GuiMonitoringTab", "GuiServerCardTitle", "GuiProductTypeLabel", "GuiProductTypeTooltip",
    "GuiProductTypeAuto", "GuiProductTypeVbr", "GuiProductTypeVb365", "GuiProductTypeBoth",
    "GuiExportOptionsLabel", "GuiHtmlReportTooltip", "GuiDataCollectionLabel",
    "GuiCollectionPeriodLabel", "GuiRescanTooltip", "GuiSecurityPrivacyLabel",
    "GuiScrubTooltip", "GuiClearCredsTooltip", "GuiPdfUnavailableTooltip",
    "GuiMonitorStatusHeader", "GuiMonitorStatusLabel", "GuiMonitorQuickSetup",
    "GuiMonitorQuickSetupTooltip", "GuiMonitorVhcSetup", "GuiMonitorVhcSetupTooltip",
    "GuiMonitorRunNow", "GuiMonitorRunTooltip", "GuiAlertNotificationsHeader",
    "GuiMinSeverityLabel", "GuiNotifSeverityWarning", "GuiNotifSeverityCritical",
    "GuiNotifSeverityOk", "GuiProcessingText",
    "GuiExportPdfLabel", "GuiClearCredsLabel", "GuiCollectionCompleteWarnings",
    "GuiCollectionComplete", "GuiMonitorNotBundled", "GuiMonitorAvailableNotSetUp",
    "GuiMonitorRunningVersion", "GuiMonitorReconfigure", "GuiMonitorLastRun",
    "GuiMonitorInstalling", "GuiMonitorSetupFailed", "GuiMonitorInstallingFromVhc",
    "GuiMonitorCheckInProgress", "GuiMonitorCompleteSetupPrompt",
    "GuiThemeDark", "GuiThemeLight", "GuiThemeSystem",
]
assert len(new_keys) == 52, len(new_keys)

neutral_path = "vHC/HC_Reporting/Resources/Localization/vhcres.resx"
neutral = io.open(neutral_path, encoding="utf-8").read()
values = dict(re.findall(r'<data name="([^"]+)"[^>]*>\s*<value>(.*?)</value>', neutral, re.S))

locales = ["fR-FR", "ja", "zh-cn", "zh-tw"]
todo_lines = []
for locale in locales:
    p = "vHC/HC_Reporting/Resources/Localization/vhcres.%s.resx" % locale
    s = io.open(p, encoding="utf-8").read()
    block_parts = []
    for key in new_keys:
        assert key in values, "missing from neutral: %s" % key
        block_parts.append('  <data name="%s" xml:space="preserve">\n    <value>%s</value>\n  </data>\n' % (key, values[key]))
        todo_lines.append("%s:%s" % (locale, key))
    block = "".join(block_parts)
    assert "</root>" in s
    s = s.replace("</root>", block + "</root>")
    io.open(p, "w", encoding="utf-8").write(s)
    print("added", len(new_keys), "keys to", locale)

# Task 4 filled these 9 fR-FR keys with English text (they were empty under the
# wrong key name before the rename) rather than a real translation - track them
# here too, fR-FR only, alongside this task's own 52 x 4 additions.
fr_fr_only_untranslated = [
    "v365NavValue0", "v365NavValue1", "v365NavValue2", "v365NavValue3", "v365NavValue4",
    "v365NavValue5", "v365NavValue7", "v365NavValue8", "v365NavValue9",
]
for key in fr_fr_only_untranslated:
    todo_lines.append("fR-FR:%s" % key)

todo_path = "vHC/HC_Reporting/Resources/Localization/untranslated-keys.txt"
with io.open(todo_path, "w", encoding="utf-8", newline="\n") as f:
    f.write("# Stage D keys added with English content, pending real translation.\n")
    f.write("# Format: Culture:KeyName\n")
    for line in todo_lines:
        f.write(line + "\n")
print("wrote", len(todo_lines), "lines to untranslated-keys.txt")
PY
```

Expected: `added 52 keys to fR-FR` / `ja` / `zh-cn` / `zh-tw`, and `wrote 217 lines to untranslated-keys.txt` (208 from this task's 52-keys-times-4-locales, plus the 9 fR-FR-only entries Task 4 introduced).

- [ ] **Step 2: Build and confirm the satellites pick up the new keys**

```bash
dotnet build vHC/HC.sln --configuration Debug
git checkout -- vHC/HC_Reporting/VeeamHealthCheck.csproj
ls -la vHC/HC_Reporting/bin/Debug/net8.0-windows7.0/fR-FR/VeeamHealthCheck.resources.dll
ls -la vHC/HC_Reporting/bin/Debug/net8.0-windows7.0/ja/VeeamHealthCheck.resources.dll
ls -la vHC/HC_Reporting/bin/Debug/net8.0-windows7.0/zh-CN/VeeamHealthCheck.resources.dll
ls -la vHC/HC_Reporting/bin/Debug/net8.0-windows7.0/zh-tw/VeeamHealthCheck.resources.dll
```

Expected: 0 build errors, all four `.dll` files exist and are newly regenerated (check the timestamp is from this build — the build can print a harmless `BuildCopy.sh`/SMB-mount warning unrelated to these files; don't rely on the build's overall exit code alone per the spec's Testing note).

- [ ] **Step 3: Commit**

```bash
git add vHC/HC_Reporting/Resources/Localization/vhcres.fR-FR.resx vHC/HC_Reporting/Resources/Localization/vhcres.ja.resx vHC/HC_Reporting/Resources/Localization/vhcres.zh-cn.resx vHC/HC_Reporting/Resources/Localization/vhcres.zh-tw.resx vHC/HC_Reporting/Resources/Localization/untranslated-keys.txt
git commit -m "feat(l10n): bring Stage D's 52 new keys to parity across all four locales (English, untranslated)"
```

---

## Task 6: Guard tests

**Files:**
- Create: `vHC/VhcXTests/VbrLocalizationHelperTests.cs`
- Create: `vHC/HC_Reporting/Resources/Localization/locale-known-missing.txt`
- Modify: `vHC/VhcXTests/VhcXTests.csproj`

Two independent checks, because they catch different failure classes (see spec Design §5): `ResourceManager.GetString()` silently returns `null` on a missing key rather than throwing, and a key missing from a *satellite* falls back to the neutral value, so a plain "resolves under one culture" check cannot see a satellite gap.

- [ ] **Step 1: Compute the current pre-existing missing-key allowlist**

This lists every key that is in the neutral resx but genuinely absent (not orphaned, not Stage D's concern) from each locale, **after** Tasks 4-5 have run. Run this to generate the file content rather than trusting the numbers in this plan — Task 4/5's scripts are deterministic, but regenerating from the actual post-Task-5 file state is the only way to be sure nothing drifted:

```bash
python3 - <<'PY'
import io, re

neutral = "vHC/HC_Reporting/Resources/Localization/vhcres.resx"
neutral_keys = set(re.findall(r'<data name="([^"]+)"', io.open(neutral, encoding="utf-8").read()))

locale_files = {
    "fR-FR": "vHC/HC_Reporting/Resources/Localization/vhcres.fR-FR.resx",
    "ja": "vHC/HC_Reporting/Resources/Localization/vhcres.ja.resx",
    "zh-CN": "vHC/HC_Reporting/Resources/Localization/vhcres.zh-cn.resx",
    "zh-tw": "vHC/HC_Reporting/Resources/Localization/vhcres.zh-tw.resx",
}

lines = ["# Culture:KeyName pairs known to be missing from a satellite resx.",
         "# Pre-existing translation debt unrelated to Stage D - not fixed by this stage.",
         "# Shrinks over time as a translator fills these in; the guard test in",
         "# VbrLocalizationHelperTests.cs fails if a satellite is missing any neutral",
         "# key NOT listed here, so removing a line here requires the translation to exist."]
total = 0
for culture, path in locale_files.items():
    locale_keys = set(re.findall(r'<data name="([^"]+)"', io.open(path, encoding="utf-8").read()))
    missing = sorted(neutral_keys - locale_keys)
    for key in missing:
        lines.append("%s:%s" % (culture, key))
        total += 1

out = "vHC/HC_Reporting/Resources/Localization/locale-known-missing.txt"
with io.open(out, "w", encoding="utf-8", newline="\n") as f:
    f.write("\n".join(lines) + "\n")
print("wrote", total, "known-missing entries")
PY
```

Expected (verified by directly running this exact script against the actual post-Task-5 file state, commit `b7e18bab`): `wrote 165 known-missing entries` (48 for fR-FR + 39 each for ja/zh-CN/zh-tw). **This is not the same as the spec's Context table** — that table's 64/39/39/39 are the *pre-Task-4* counts (before the rename fix). 165 is the correct *post-Task-4/5* state this step computes fresh; per the plan's own preamble, a mismatch against the spec would normally be a stop-and-report, but this one is expected — the spec and this step are describing the state of the resx files at two different points in the branch's history, not disagreeing about a fact.

### Correction applied after review (Task 4's HtmlIntroLine3 revert, commit `39be2be5`)

An earlier draft of this step expected `wrote 161 known-missing entries` (47/38/38/38), computed when Task 4 was still expected to fully eliminate the `HtmlIntroLine3` orphan via a rename to `HtmlIntroLine3Original`. That rename was reverted (see Task 4's own "Correction applied after review") because none of the four locale values had a closing `</a>` tag and renaming them onto the live key emitted malformed HTML. The revert means `HtmlIntroLine3Original` is now *also* missing from every locale (on top of `HtmlIntroLine3Anon`, which was already going to be missing either way), adding one missing entry per locale: 47→48 for fR-FR, 38→39 each for ja/zh-CN/zh-tw, 161→165 total. `HtmlIntroLine3` itself shows up as an *orphan*, not a missing key — see `KnownDeliberateOrphans` in Step 3 below, not this allowlist.

- [ ] **Step 2: Link the allowlist into the test project's output**

In `vHC/VhcXTests/VhcXTests.csproj`, find:

```xml
  <ItemGroup>
    <ProjectReference Include="..\HC_Reporting\VeeamHealthCheck.csproj" />
  </ItemGroup>
```

Replace with:

```xml
  <ItemGroup>
    <ProjectReference Include="..\HC_Reporting\VeeamHealthCheck.csproj" />
  </ItemGroup>

  <ItemGroup>
    <None Include="..\HC_Reporting\Resources\Localization\locale-known-missing.txt"
          Link="Resources\Localization\locale-known-missing.txt"
          CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>
```

This links the single source file into `VhcXTests`'s own build output without duplicating it — a `ProjectReference` alone does not guarantee a referenced project's `<None>` items propagate to the referencing project's output directory.

- [ ] **Step 3: Write the failing tests**

The neutral resx has 25 pre-existing keys whose `<value>` is already empty (confirmed by direct inspection — none are Stage D keys, none are new). A first cut of `AllStaticStrings_ResolveNonNullAndNonEmpty` that asserts blanket non-empty would fail against these 25 immediately, for reasons unrelated to anything this stage touches. `KnownEmptyNeutralKeys` below is that confirmed list — it exists so the test still catches a real typo (any *other* blank field) while not being poisoned by pre-existing debt.

Create `vHC/VhcXTests/VbrLocalizationHelperTests.cs`:

```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using VeeamHealthCheck.Resources.Localization;
using Xunit;

namespace VhcXTests
{
    public class VbrLocalizationHelperTests
    {
        private static readonly string[] SatelliteCultures = { "fR-FR", "ja", "zh-CN", "zh-tw" };

        // Pre-existing neutral resx keys whose <value> is genuinely empty - not
        // Stage D's concern, and not a missing/typo'd key. Confirmed by direct
        // inspection of vhcres.resx before this stage touched it. A key belongs
        // here only if its blank value is a pre-existing fact, not a new typo -
        // do not add a Stage D key to this list to make a failing test pass.
        private static readonly HashSet<string> KnownEmptyNeutralKeys = new()
        {
            "Prx10TT",
            "JobCon1TT", "JobCon2TT", "JobCon3TT", "JobCon4TT", "JobCon5TT", "JobCon6TT", "JobCon7TT",
            "TaskCon1TT", "TaskCon2TT", "TaskCon3TT", "TaskCon4TT", "TaskCon5TT", "TaskCon6TT", "TaskCon7TT",
            "Reg0TT", "Reg1TT",
            "JobInfo6TT", "JobInfo7TT", "JobInfo8TT", "JobInfo9TT",
            "JobInfo10TT", "JobInfo11TT", "JobInfo12TT", "JobInfo13TT",
        };

        // HtmlIntroLine3 is deliberately left orphaned in all four satellites (commit
        // 39be2be5). Its translated values predate the neutral key's split into
        // HtmlIntroLine3Anon/HtmlIntroLine3Original and are malformed HTML (no closing
        // </a>; zh-tw's is a truncated tag fragment) pointing at the pre-split
        // JobSessionReports path. Renaming them onto the live key emits broken HTML into
        // the report (CHtmlCompiler.cs interpolates the value into <dd>{1}</dd> with no
        // closing tag appended). They are kept under the old key name so a translator can
        // produce well-formed replacements later. DO NOT delete these keys or rename them
        // to make this test pass - either "fix" reintroduces the bug 39be2be5 resolved,
        // or destroys real translated content.
        private static readonly HashSet<(string Culture, string Key)> KnownDeliberateOrphans = new()
        {
            ("fR-FR", "HtmlIntroLine3"), ("ja", "HtmlIntroLine3"),
            ("zh-CN", "HtmlIntroLine3"), ("zh-tw", "HtmlIntroLine3"),
        };

        [Fact]
        public void AllStaticStrings_ResolveNonNullAndNonEmpty()
        {
            var fields = typeof(VbrLocalizationHelper)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.FieldType == typeof(string));

            var blank = new List<string>();
            foreach (var field in fields)
            {
                var value = (string)field.GetValue(null);
                if (string.IsNullOrEmpty(value) && !KnownEmptyNeutralKeys.Contains(field.Name))
                {
                    blank.Add(field.Name);
                }
            }

            Assert.True(blank.Count == 0,
                "Fields resolving null/empty under the neutral resx (missing or typo'd key, not in KnownEmptyNeutralKeys): " + string.Join(", ", blank));
        }

        [Fact]
        public void EverySatellite_HasNoOrphansAndNoUnexpectedMissingKeys()
        {
            var resourceManager = new ResourceManager(
                "VeeamHealthCheck.Resources.Localization.vhcres",
                typeof(VbrLocalizationHelper).Assembly);

            var neutralKeys = GetKeySet(resourceManager, CultureInfo.InvariantCulture);
            var knownMissing = LoadKnownMissing();

            foreach (var culture in SatelliteCultures)
            {
                var satelliteKeys = GetKeySet(resourceManager, new CultureInfo(culture));

                var orphans = satelliteKeys.Except(neutralKeys)
                    .Where(k => !KnownDeliberateOrphans.Contains((culture, k)))
                    .ToList();
                Assert.True(orphans.Count == 0,
                    $"{culture} has {orphans.Count} orphan key(s) absent from the neutral resx and not in KnownDeliberateOrphans: {string.Join(", ", orphans)}");

                var missing = neutralKeys.Except(satelliteKeys)
                    .Where(k => !knownMissing.Contains((culture, k)))
                    .ToList();
                Assert.True(missing.Count == 0,
                    $"{culture} is missing {missing.Count} key(s) not covered by locale-known-missing.txt: {string.Join(", ", missing)}");
            }
        }

        private static HashSet<string> GetKeySet(ResourceManager resourceManager, CultureInfo culture)
        {
            var set = resourceManager.GetResourceSet(culture, createIfNotExists: true, tryParents: false);
            var keys = new HashSet<string>();
            if (set != null)
            {
                foreach (DictionaryEntry entry in set)
                {
                    keys.Add((string)entry.Key);
                }
            }
            return keys;
        }

        private static HashSet<(string Culture, string Key)> LoadKnownMissing()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Resources", "Localization", "locale-known-missing.txt");
            var set = new HashSet<(string, string)>();
            foreach (var line in File.ReadAllLines(path))
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;
                var parts = line.Split(':', 2);
                set.Add((parts[0], parts[1]));
            }
            return set;
        }
    }
}
```

- [ ] **Step 4: Run the tests and confirm they pass**

```bash
dotnet test vHC/VhcXTests/VhcXTests.csproj --filter "FullyQualifiedName~VbrLocalizationHelperTests"
git checkout -- vHC/HC_Reporting/VeeamHealthCheck.csproj
```

Expected: `Passed! - Failed: 0, Passed: 2, Skipped: 0`. If `EverySatellite_HasNoOrphansAndNoUnexpectedMissingKeys` fails listing keys from Tasks 2/3 (not pre-existing ones), Task 5's copy step missed something — re-check its key list against Tasks 2-3's. If it fails listing pre-existing keys not in Step 1's generated allowlist, Step 1 was run before Task 5 finished, or against stale files — regenerate it. **If the failure lists `HtmlIntroLine3` as an orphan, that is deliberate** — see the `KnownDeliberateOrphans` comment above; the fix is to confirm the culture/key pair is already in that HashSet (it should be, from Task 4/`39be2be5`), not to delete the key from the resx or rename it back to `HtmlIntroLine3Original`.

- [ ] **Step 5: Run the full suite**

```bash
dotnet test vHC/VhcXTests/VhcXTests.csproj
git checkout -- vHC/HC_Reporting/VeeamHealthCheck.csproj
```

Expected: `910 passed, 0 failed, 12 skipped` (908 baseline + 2 new).

- [ ] **Step 6: Commit**

```bash
git add vHC/VhcXTests/VbrLocalizationHelperTests.cs vHC/VhcXTests/VhcXTests.csproj vHC/HC_Reporting/Resources/Localization/locale-known-missing.txt
git commit -m "test(l10n): guard against silent resx key regressions across all locales"
```

---

## Task 7: Final verification and handoff

**Files:**
- Create: `docs/plans/2026-09-15-gui-redesign-stage-d-verification.md`

- [ ] **Step 1: Full build and test run**

```bash
dotnet build vHC/HC.sln --configuration Debug
dotnet test vHC/VhcXTests/VhcXTests.csproj
git checkout -- vHC/HC_Reporting/VeeamHealthCheck.csproj
git status
```

Expected: 0 build errors, `910 passed, 0 failed, 12 skipped`, and `git status` clean (no stray `VeeamHealthCheck.csproj` diff, no leftover `.swp`/temp files).

- [ ] **Step 2: Confirm no XAML-declared hardcoded strings remain outside the documented non-goals**

```bash
grep -nE '(Text|Content)="[^"{][^"]*"' vHC/HC_Reporting/VhcGui.axaml
grep -n 'ToolTip.Tip="[^"{]' vHC/HC_Reporting/VhcGui.axaml
```

Expected: only the 6 documented non-goal literals (gear glyph, ellipsis, `ntfy`/`Teams`/`Slack`/`PagerDuty`) from the first command, and **zero** matches from the second (every `ToolTip.Tip` attribute was removed from XAML in Task 2 in favor of `ToolTip.SetTip(...)` in `SetUiText()`).

- [ ] **Step 3: Write the Windows verification checklist**

Create `docs/plans/2026-09-15-gui-redesign-stage-d-verification.md`:

```markdown
# Stage D — Windows verification checklist

This sandbox cannot render Avalonia (native platform bootstrap crash). Everything below
needs a real Windows machine with VBR and/or VB365 installed.

## Task 1 correctness fix (highest priority — verify first)

- [ ] Open Continuous Monitoring tab. Select each of the 4 notif types (ntfy/Teams/Slack/
      PagerDuty) and each of the 3 severities (warning/critical/ok) in turn, click Quick
      Setup or Setup from VHC, and confirm the generated monitor config
      (`CVhcMonitorIntegration`'s output — check its config file location) contains the
      exact English protocol string for both fields, matching the selected item, not a
      localized label. (Correct today regardless of OS UI culture — Tag values are English
      literals unconditionally. This step is about confirming no regression, since prior
      to Task 1 this happened to also read English text; the trap only fires under a
      non-English UI culture.)
- [ ] Switch Windows display language to French, Japanese, Simplified Chinese, or
      Traditional Chinese. Repeat the notif type/severity selection above. Confirm the
      generated config's `min_severity` and notif type are still the English protocol
      strings even though the dropdown labels themselves may now render in that language
      for `notifSeverityBox` (ok/warning/critical are localized `Content`; the brand names
      on `notifTypeBox` are intentionally not localized, so those labels stay English).

## XAML/code-behind sweep (Tasks 2-3)

**These two checks are the only verification for a defect class the build and both guard
tests cannot see** — a render-time ordering bug, not a missing/wrong resx value. Two
instances of it were found and fixed while writing this plan (a `ComboBox` never re-reading
an item's `Content` after attach, and a competing constructor-time writer overwritten by
`SetUiText()` running later on `Loaded`); check specifically for a *third* instance rather
than assuming the fix is complete, since neither this sandbox nor either guard test can
confirm that on its own:

- [ ] On first opening the app (default English), before touching either dropdown, confirm
      the Product Type box already shows "Auto-detect" and the Min severity box already
      shows "warning" — not blank. This is the specific symptom of the ComboBox-snapshot bug
      Task 2 fixes with `{x:Static}`; if it regresses (e.g. a future edit moves a `Content`
      value back into `SetUiText()`), both boxes render blank until the user manually opens
      and re-selects an item.
- [ ] On a machine with the monitor already installed and its scheduled task registered,
      confirm the "Quick Setup" button reads "Reconfigure" **immediately** on window open —
      not "Quick Setup" that then flips to "Reconfigure", and not blank. This is the specific
      symptom of Task 3's `InitializeMonitorStatus()`/`SetUiText()` ordering fix; a
      regression here means something reintroduced a `SetUiText()` write to
      `monitorQuickSetupBtn.Content` without removing the unconditional one now at the top
      of `InitializeMonitorStatus()`.

- [ ] With Windows display language set to French, Japanese, Simplified Chinese, or
      Traditional Chinese, open the app and confirm no blank labels, buttons, or tooltips
      appear anywhere in the main window or the Continuous Monitoring tab. A blank control
      here means a resx key typo the guard test's neutral-only check could not catch on its
      own machine (though the per-satellite guard test in Task 6 should have caught it at
      test time — this step is the real-hardware backstop).
- [ ] Toggle the theme button (sun/moon icon) and confirm the "Dark"/"Light"/"System" word
      is localized in that language while the emoji prefix is unchanged.
- [ ] Trigger a health check run and confirm "Processing health check..." (or its localized
      equivalent) shows correctly in the progress area, and that the collection-complete
      message (with or without a warning count) renders correctly in that language,
      including the `{0} collector warning(s)` count substitution.
- [ ] Exercise every Continuous Monitoring status transition (not bundled / available-not-
      set-up / installing / running / setup failed) by installing, uninstalling, and
      forcing a failure, and confirm each status string renders correctly in that language.
- [ ] Confirm the PDF export tooltip ("PDF Export not available when both VB365 & VBR are
      detected on the same machine.") renders correctly, on a machine with both products
      installed.

## Locale-file fixes (Task 4)

- [ ] With Windows display language set to French, navigate to the "SOBR" (Sbr) section of a
      generated HTML report (not the GUI window) and spot-check that the recovered
      translations (`SbrTitle`, `SbrExt8`/`10`-`14`) render in French, not English fallback.
- [ ] In the same report's "VB365 Nav" section, confirm `v365NavValue0`-`5`/`7`-`9` render in
      **English**, not French — these 9 keys had an empty French value under their old
      (wrong) key name, so Task 4 filled them with English text rather than inventing a
      translation. Seeing English here is the *correct*, expected outcome, not a bug — only
      flag it if one of these renders blank or in the wrong language entirely.

## Recorded, not fixed (informational only — no action needed)

- `notifUrlBox.Tag`'s write in `notifTypeBox_SelectionChanged` remains dead code (nothing
  reads `Tag` on that control; Avalonia's actual placeholder property is `Watermark`).
  Confirmed still true after this stage's changes — the URL hint still never renders.
- The vestigial VBR-side `ResGen.exe`/`VbrResFileBuilder.ps1` pipeline is untouched.
- No locale picker exists; the four satellites above are reachable only via the OS's own
  display-language setting.
- `vhcres.fR-FR.resx`'s `v365NavValue6`, `10`, `11`, `12`, and `13` are *already* blank in
  French today, via the same empty-`<value>` mechanism Task 4 fixed for 9 sibling keys —
  found while investigating those 9, out of scope for this stage, and unrelated to anything
  Stage D added or renamed.
```

- [ ] **Step 4: Commit**

```bash
git add docs/plans/2026-09-15-gui-redesign-stage-d-verification.md
git commit -m "docs(gui): add the Stage D Windows verification checklist"
```

- [ ] **Step 5: Push and open the PR**

```bash
git push -u origin stage-d/localization
gh pr create --base feature/gui-redesign-port --title "feat(gui): Stage D localization sweep" --body "$(cat <<'EOF'
## Summary
- Fixes a correctness trap where notifTypeBox/notifSeverityBox read their own displayed (soon-to-be-localized) Content as a backend protocol value; min_severity was written raw into a monitor YAML config.
- Resx-backs the remaining ~52 hardcoded strings across VhcGui.axaml and its code-behind that Stage C left as a known gap.
- Fixes 16 pre-existing orphaned/mistranslated keys across the four satellite locale files, recovering 7 dead French/Japanese/Chinese translations and filling 9 blank French ones. A 17th (`HtmlIntroLine3`) was found post-rename to carry malformed HTML with no closing tag and is deliberately left orphaned rather than renamed onto the live key or deleted; see Task 4's "Correction applied after review" and Task 6's `KnownDeliberateOrphans` allowlist.
- Brings all new keys to parity across the four locales (English content, tracked in untranslated-keys.txt for a future translator) rather than shipping unaudited machine translation.
- Adds a two-part guard test against silent resx regressions (neutral-key coverage + per-satellite orphan/missing-key parity).

## Test plan
- [ ] `dotnet test vHC/VhcXTests/VhcXTests.csproj` — 910 passed, 0 failed, 12 skipped
- [ ] Windows checklist in docs/plans/2026-09-15-gui-redesign-stage-d-verification.md
EOF
)"
```

**Only do Step 5 if the user asks you to push/open the PR — do not push or open a PR unprompted.**

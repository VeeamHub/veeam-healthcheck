# GUI Redesign Stage E: Cold-Start Recovery Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** On a machine with no local VBR/VB365 detected and no usable persisted remote server, let the user add a remote server from inside the existing cold-start dialog and continue into Remote Mode in the same launch, instead of the app hard-shutting-down with no way to reach Manage Servers.

**Architecture:** Turn the single OK-only error dialog in `VhcGui.axaml.cs`'s `SetUiAsync()` into a Yes/No offer (`IUiNotifier.ConfirmAsync`, already exists). Accepting opens the existing `ManageServersDialog` inline; committing a server re-runs the same "has a remote server → Remote Mode" resolution `SetUiSync()` already does for the pre-persisted case, via a new shared, unit-tested predicate (`CAppSettings.HasNonLocalhostServer`). Every non-success path (decline, cancel, net-zero commit) converges on today's exact shutdown behavior.

**Tech Stack:** .NET 8 (`net8.0-windows7.0`), Avalonia UI, xUnit, resx localization (4 satellite locales + neutral).

**Spec:** `docs/superpowers/specs/2026-09-25-gui-redesign-stage-e-cold-start-design.md` (already independently reviewed and corrected once — treat as authoritative).

**Baseline (verified empirically in this worktree before starting):** `dotnet test vHC/VhcXTests/VhcXTests.csproj` → `Passed! - Failed: 0, Passed: 910, Skipped: 12, Total: 922`.

---

## File Structure

| File | Role |
|---|---|
| `vHC/HC_Reporting/Startup/CAppSettings.cs` | Task 1: add `HasNonLocalhostServer(IEnumerable<string>)`, the shared, testable "is there a remote server" predicate. |
| `vHC/VhcXTests/CAppSettingsTests.cs` | Task 1: direct unit tests for the new predicate. |
| `vHC/HC_Reporting/VhcGui.axaml.cs` | Task 1: wire `SetUiSync()`'s existing inline check onto the shared predicate. Task 4: rewrite `SetUiAsync()`'s `_modeCheckFailed` branch — the actual recovery flow. |
| `vHC/HC_Reporting/Resources/Localization/vhcres.resx` | Task 2: add `GuiNoVeeamDetectedTitle`/`GuiNoVeeamDetectedMessage` (neutral/English). |
| `vHC/HC_Reporting/Resources/Localization/VbrLocalizationHelper.cs` | Task 2: add the two matching accessor fields (UTF-16LE/CRLF). |
| `vHC/HC_Reporting/Resources/Localization/vhcres.txt` | Task 2: matching ResGen source entries (UTF-16LE/CRLF). |
| `vHC/HC_Reporting/Resources/Localization/vhcres.fR-FR.resx`, `vhcres.ja.resx`, `vhcres.zh-cn.resx`, `vhcres.zh-tw.resx` | Task 3: copy the two new keys in with English content, for key parity. |
| `vHC/HC_Reporting/Resources/Localization/untranslated-keys.txt` | Task 3: append the 8 new to-do entries (2 keys × 4 locales). |
| `docs/plans/2026-09-25-gui-redesign-stage-e-verification.md` | Task 5: new Windows verification checklist. |

No new files beyond the verification checklist — this stage reuses `ManageServersDialog`, `ServerListEditor`, `ServerListCommitter`, and `IUiNotifier` exactly as they exist today.

---

## Task 1: Shared `HasNonLocalhostServer` predicate, wired into `SetUiSync()`

**Files:**
- Modify: `vHC/HC_Reporting/Startup/CAppSettings.cs`
- Modify: `vHC/HC_Reporting/VhcGui.axaml.cs` (`SetUiSync()`, around line 350)
- Test: `vHC/VhcXTests/CAppSettingsTests.cs`

`SetUiSync()` currently has its own inline check (`_persistedServers.Any(s => !s.Equals(LocalhostName, StringComparison.OrdinalIgnoreCase))`, `VhcGui.axaml.cs:350-351`). Task 4's cold-start recovery branch needs the identical check. This task adds it once, in the Avalonia-free `CAppSettings` class (alongside `NormalizeServers`/`LoadOrSeedServers`), so both call sites share one tested implementation.

- [ ] **Step 1: Write the failing tests**

Open `vHC/VhcXTests/CAppSettingsTests.cs`. Find the last test method and the class/namespace's two closing braces that immediately follow it:

```csharp
        [Fact]
        public void LoadOrSeedServers_WhenSettingsUnreadable_ReturnsEmptyAndDoesNotWrite()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CAppSettings.StorePath)!);
            File.WriteAllText(CAppSettings.StorePath, "{ not valid json ");
            var before = File.ReadAllText(CAppSettings.StorePath);

            var result = CAppSettings.LoadOrSeedServers(
                new[] { "vbr01" }, excludeLocalhost: true);

            // Seeding over an unreadable file would turn a transient read failure into
            // a permanent resurrection of removed servers. Better to show nothing this
            // session and leave the file alone so a retry can recover.
            Assert.Empty(result);
            Assert.Equal(before, File.ReadAllText(CAppSettings.StorePath));
        }
    }
}
```

**Replace it** (the whole block above, including both closing braces) **with** the same method unchanged, followed by 4 new `[Fact]` methods, followed by the same two closing braces — do not leave the original block in place and paste this below it, that duplicates the method and fails with `CS0111`:

```csharp
        [Fact]
        public void LoadOrSeedServers_WhenSettingsUnreadable_ReturnsEmptyAndDoesNotWrite()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CAppSettings.StorePath)!);
            File.WriteAllText(CAppSettings.StorePath, "{ not valid json ");
            var before = File.ReadAllText(CAppSettings.StorePath);

            var result = CAppSettings.LoadOrSeedServers(
                new[] { "vbr01" }, excludeLocalhost: true);

            // Seeding over an unreadable file would turn a transient read failure into
            // a permanent resurrection of removed servers. Better to show nothing this
            // session and leave the file alone so a retry can recover.
            Assert.Empty(result);
            Assert.Equal(before, File.ReadAllText(CAppSettings.StorePath));
        }

        [Fact]
        public void HasNonLocalhostServer_WithEmptyList_ReturnsFalse()
        {
            Assert.False(CAppSettings.HasNonLocalhostServer(System.Array.Empty<string>()));
        }

        [Fact]
        public void HasNonLocalhostServer_WithOnlyLocalhostAnyCasing_ReturnsFalse()
        {
            Assert.False(CAppSettings.HasNonLocalhostServer(
                new[] { "localhost", "LOCALHOST", "LocalHost" }));
        }

        [Fact]
        public void HasNonLocalhostServer_WithRealHostPresent_ReturnsTrue()
        {
            Assert.True(CAppSettings.HasNonLocalhostServer(new[] { "vbr01" }));
        }

        [Fact]
        public void HasNonLocalhostServer_WithMixedLocalhostAndRealHost_ReturnsTrue()
        {
            Assert.True(CAppSettings.HasNonLocalhostServer(new[] { "localhost", "vbr01" }));
        }
    }
}
```

- [ ] **Step 2: Run the tests to confirm they fail to compile**

```bash
dotnet test vHC/VhcXTests/VhcXTests.csproj --filter "FullyQualifiedName~HasNonLocalhostServer"
git checkout -- vHC/HC_Reporting/VeeamHealthCheck.csproj
```

Expected: build error `CS0117: 'CAppSettings' does not contain a definition for 'HasNonLocalhostServer'`.

- [ ] **Step 3: Implement `HasNonLocalhostServer` in `CAppSettings.cs`**

Find the end of `LoadOrSeedServers` and the start of `IsUsableServerName`:

```csharp
        return NormalizeServers(settings.Servers, excludeLocalhost);
    }

    // The single rule for "not a usable server name", shared by NormalizeServers
    // (read), SetServers (write), and AddServer's input guard, so the three paths
    // cannot drift into different definitions of the same rule.
    private static bool IsUsableServerName(string server) => !string.IsNullOrWhiteSpace(server);
```

Insert the new method between them:

```csharp
        return NormalizeServers(settings.Servers, excludeLocalhost);
    }

    /// <summary>
    /// True if <paramref name="servers"/> contains at least one entry other than
    /// "localhost" (case-insensitive). Shared by <c>VhcGui</c>'s <c>SetUiSync</c>
    /// and its cold-start recovery branch in <c>SetUiAsync</c> - both need the
    /// identical "is there a remote server to fall back to" check, and this is
    /// the only piece of that logic directly unit-testable outside the Avalonia
    /// code-behind.
    /// </summary>
    public static bool HasNonLocalhostServer(IEnumerable<string> servers) =>
        servers.Any(s => !string.Equals(s, LocalhostName, StringComparison.OrdinalIgnoreCase));

    // The single rule for "not a usable server name", shared by NormalizeServers
    // (read), SetServers (write), and AddServer's input guard, so the three paths
    // cannot drift into different definitions of the same rule.
    private static bool IsUsableServerName(string server) => !string.IsNullOrWhiteSpace(server);
```

- [ ] **Step 4: Run the tests to confirm they pass**

```bash
dotnet test vHC/VhcXTests/VhcXTests.csproj --filter "FullyQualifiedName~HasNonLocalhostServer"
git checkout -- vHC/HC_Reporting/VeeamHealthCheck.csproj
```

Expected: `Passed! - Failed: 0, Passed: 4, Skipped: 0, Total: 4`.

- [ ] **Step 5: Wire `SetUiSync()`'s inline check onto the shared predicate**

In `vHC/HC_Reporting/VhcGui.axaml.cs`, find (around line 346-351):

```csharp
                // The localhost filter is kept rather than relying on the "localhost is
                // never persisted" invariant: on a non-injecting machine localhost IS
                // legitimately persisted, and counting it as a remote server would put a
                // local-only box into Remote Mode.
                bool hasRemoteServers = _persistedServers
                    .Any(s => !s.Equals(LocalhostName, StringComparison.OrdinalIgnoreCase));
```

Replace with:

```csharp
                // The localhost filter is kept rather than relying on the "localhost is
                // never persisted" invariant: on a non-injecting machine localhost IS
                // legitimately persisted, and counting it as a remote server would put a
                // local-only box into Remote Mode. Shared with SetUiAsync's cold-start
                // recovery branch via CAppSettings.HasNonLocalhostServer, so both paths
                // agree on the definition.
                bool hasRemoteServers = CAppSettings.HasNonLocalhostServer(_persistedServers);
```

- [ ] **Step 6: Full build and targeted test run**

```bash
dotnet build vHC/HC.sln --configuration Debug
git checkout -- vHC/HC_Reporting/VeeamHealthCheck.csproj
dotnet test vHC/VhcXTests/VhcXTests.csproj --filter "FullyQualifiedName~CAppSettingsTests"
git checkout -- vHC/HC_Reporting/VeeamHealthCheck.csproj
```

Expected: 0 build errors. Test run: `Passed! - Failed: 0, Passed: 49, Skipped: 0` (45 pre-existing `CAppSettingsTests` + this task's 4 new ones).

- [ ] **Step 7: Commit**

```bash
git add vHC/HC_Reporting/Startup/CAppSettings.cs vHC/HC_Reporting/VhcGui.axaml.cs vHC/VhcXTests/CAppSettingsTests.cs
git commit -m "$(cat <<'EOF'
feat(gui): add CAppSettings.HasNonLocalhostServer, shared by SetUiSync and Stage E's cold-start recovery

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

## Task 2: New resx keys — neutral resx, helper accessors, ResGen source

**Files:**
- Modify: `vHC/HC_Reporting/Resources/Localization/vhcres.resx`
- Modify: `vHC/HC_Reporting/Resources/Localization/VbrLocalizationHelper.cs` (UTF-16LE/CRLF)
- Modify: `vHC/HC_Reporting/Resources/Localization/vhcres.txt` (UTF-16LE/CRLF)

Adds `GuiNoVeeamDetectedTitle` and `GuiNoVeeamDetectedMessage` with English content. Not yet consumed by any call site — Task 4 wires them in. This mirrors Stage D's own sequencing (resx keys land before their XAML/code-behind consumer).

The message must use **real line breaks** in the `.resx` `<value>`, not the two characters `\`+`n` — confirmed against the existing `GuiAcceptText` entry, whose value holds literal newlines. `vhcres.txt` is the opposite: it's ResGen source, and the existing `GuiAcceptText` entry there uses the literal two-character `\n` escape, which `ResGen.exe` expands on the rare occasion someone runs that (inert-for-the-real-build, per Stage D) pipeline by hand.

- [ ] **Step 1: Add the two keys to `vhcres.resx`**

```bash
python3 - <<'PY'
import io

def esc(s):
    return s.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")

message = (
    "No Veeam Software detected on this machine.\n\n"
    "This tool requires Veeam Backup & Replication (VBR) or Veeam Backup for Microsoft 365 "
    "(VB365) to be installed locally, or a remote server to connect to.\n\n"
    "Would you like to add a remote server now?\n\n"
    "Alternatively, close this window and run from the command line with:\n"
    "VeeamHealthCheck.exe /remote /host=your-vbr-server"
)

pairs = [
    ("GuiNoVeeamDetectedTitle", "Veeam Software Not Detected"),
    ("GuiNoVeeamDetectedMessage", message),
]

p = "vHC/HC_Reporting/Resources/Localization/vhcres.resx"
s = io.open(p, encoding="utf-8", newline="").read()
block = "".join(
    '  <data name="%s" xml:space="preserve">\n    <value>%s</value>\n  </data>\n' % (k, esc(v))
    for k, v in pairs
)
assert "</root>" in s
s = s.replace("</root>", block + "</root>")
io.open(p, "w", encoding="utf-8", newline="").write(s)
print("inserted", len(pairs), "keys into vhcres.resx")
PY
```

Expected: `inserted 2 keys into vhcres.resx`. Confirm:

```bash
grep -c 'name="GuiNoVeeamDetected' vHC/HC_Reporting/Resources/Localization/vhcres.resx
```

Expected: `2`.

- [ ] **Step 2: Add the accessors to `VbrLocalizationHelper.cs`, encoding-preserving**

```bash
python3 - <<'PY'
import io
p = "vHC/HC_Reporting/Resources/Localization/VbrLocalizationHelper.cs"
keys = ["GuiNoVeeamDetectedTitle", "GuiNoVeeamDetectedMessage"]
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

Expected: `inserted 2 accessors`.

- [ ] **Step 3: Verify the encoding survived**

```bash
file vHC/HC_Reporting/Resources/Localization/VbrLocalizationHelper.cs
iconv -f UTF-16LE -t UTF-8 vHC/HC_Reporting/Resources/Localization/VbrLocalizationHelper.cs | grep -c 'GuiNoVeeamDetected'
```

Expected: `Unicode text, UTF-16, little-endian text, with CRLF line terminators` and a count of `2`. **If `file` reports UTF-8, stop and `git checkout --` the file.**

- [ ] **Step 4: Append the matching entries to `vhcres.txt`**

```bash
python3 - <<'PY'
import io
p = "vHC/HC_Reporting/Resources/Localization/vhcres.txt"

# Literal two-character \n (not a real newline) - matches how the existing
# GuiAcceptText entry in this same file represents a multi-paragraph value;
# ResGen.exe expands it back into a real newline if this pipeline is ever run.
message_escaped = (
    "No Veeam Software detected on this machine.\\n\\n"
    "This tool requires Veeam Backup & Replication (VBR) or Veeam Backup for Microsoft 365 "
    "(VB365) to be installed locally, or a remote server to connect to.\\n\\n"
    "Would you like to add a remote server now?\\n\\n"
    "Alternatively, close this window and run from the command line with:\\n"
    "VeeamHealthCheck.exe /remote /host=your-vbr-server"
)

pairs = [
    ("GuiNoVeeamDetectedTitle", "Veeam Software Not Detected"),
    ("GuiNoVeeamDetectedMessage", message_escaped),
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

Expected: `appended 2 entries` and still `HTML document text, Unicode text, UTF-16, little-endian text, with very long lines (976), with CRLF line terminators` (this file's HTML-flavored heuristic in `file`'s magic detection produces this longer string both before and after the edit — the exact wording that matters is `UTF-16, little-endian` and `CRLF line terminators`, not the `HTML document text`/`very long lines (976)` prefix, which is `vhcres.txt`'s existing, unrelated file signature).

- [ ] **Step 5: Build and confirm the new field resolves**

```bash
dotnet build vHC/HC.sln --configuration Debug
git checkout -- vHC/HC_Reporting/VeeamHealthCheck.csproj
dotnet test vHC/VhcXTests/VhcXTests.csproj --filter "FullyQualifiedName~AllStaticStrings_ResolveNonNullAndNonEmpty"
git checkout -- vHC/HC_Reporting/VeeamHealthCheck.csproj
```

Expected: 0 build errors, `Passed! - Failed: 0, Passed: 1, Skipped: 0`. (Don't run `EverySatellite_HasNoOrphansAndNoUnexpectedMissingKeys` yet — the two new keys aren't in the four satellite locale files until Task 3, so it would fail as "missing" at this point. That's expected and gets fixed in Task 3, not this task.)

- [ ] **Step 6: Commit**

```bash
git add vHC/HC_Reporting/Resources/Localization/vhcres.resx vHC/HC_Reporting/Resources/Localization/VbrLocalizationHelper.cs vHC/HC_Reporting/Resources/Localization/vhcres.txt
git commit -m "$(cat <<'EOF'
feat(l10n): add GuiNoVeeamDetectedTitle/Message resx keys for Stage E

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

### Correction applied after review

Code-quality review on the resulting commit found the `GuiNoVeeamDetectedMessage` text
above had two real problems: the suggested CLI command
(`VeeamHealthCheck.exe /remote /host=your-vbr-server`) is a verified silent no-op against
`CArgsParser.ParseAllArgs` (neither `/remote` nor `/host=` sets the local `run`/`ui` flag the
dispatch at the end of that method requires — only `/run`/`/gui`/`/lite`/`/import`/`/security`
do), and the CLI-alternative paragraph sat after the "Would you like to add a remote server
now?" question instead of immediately before the Yes/No buttons that answer it. Both fixed in
a follow-up commit, before Task 3 could propagate the flawed text into four more locale files
— see `docs/superpowers/specs/2026-09-25-gui-redesign-stage-e-cold-start-design.md`'s matching
correction note for the corrected value. The Step 1/Step 4 scripts above are left as originally
written/executed for this task, since Task 2's own commit is unaffected — Task 3, below,
picks up the corrected neutral value automatically since it reads whatever is currently in
`vhcres.resx` rather than a hardcoded copy of the text.

---

## Task 3: Key-parity for Stage E's new keys across the four locales

**Files:**
- Modify: `vHC/HC_Reporting/Resources/Localization/vhcres.fR-FR.resx`
- Modify: `vHC/HC_Reporting/Resources/Localization/vhcres.ja.resx`
- Modify: `vHC/HC_Reporting/Resources/Localization/vhcres.zh-cn.resx`
- Modify: `vHC/HC_Reporting/Resources/Localization/vhcres.zh-tw.resx`
- Modify: `vHC/HC_Reporting/Resources/Localization/untranslated-keys.txt`

Same policy as every prior stage: the two new keys get copied into all four locale files with their **English** neutral value — not machine-translated — plus an appended to-do entry. `untranslated-keys.txt` already exists (Stage D created it); this task appends to it, not overwrites it.

- [ ] **Step 1: Copy the two new keys into all four locale files, append to the to-do list**

```bash
python3 - <<'PY'
import io, re

new_keys = ["GuiNoVeeamDetectedTitle", "GuiNoVeeamDetectedMessage"]

neutral_path = "vHC/HC_Reporting/Resources/Localization/vhcres.resx"
neutral = io.open(neutral_path, encoding="utf-8", newline="").read()
values = dict(re.findall(r'<data name="([^"]+)"[^>]*>\s*<value>(.*?)</value>', neutral, re.S))

locales = ["fR-FR", "ja", "zh-cn", "zh-tw"]
todo_lines = []
for locale in locales:
    p = "vHC/HC_Reporting/Resources/Localization/vhcres.%s.resx" % locale
    s = io.open(p, encoding="utf-8", newline="").read()
    block_parts = []
    for key in new_keys:
        assert key in values, "missing from neutral: %s" % key
        block_parts.append(
            '  <data name="%s" xml:space="preserve">\n    <value>%s</value>\n  </data>\n'
            % (key, values[key]))
        todo_lines.append("%s:%s" % (locale, key))
    block = "".join(block_parts)
    assert "</root>" in s
    s = s.replace("</root>", block + "</root>")
    io.open(p, "w", encoding="utf-8", newline="").write(s)
    print("added", len(new_keys), "keys to", locale)

todo_path = "vHC/HC_Reporting/Resources/Localization/untranslated-keys.txt"
with io.open(todo_path, "a", encoding="utf-8", newline="\n") as f:
    f.write("# Stage E keys added with English content, pending real translation.\n")
    for line in todo_lines:
        f.write(line + "\n")
print("appended", len(todo_lines), "lines to untranslated-keys.txt")
PY
```

Expected: `added 2 keys to fR-FR` / `ja` / `zh-cn` / `zh-tw`, and `appended 8 lines to untranslated-keys.txt`.

- [ ] **Step 2: Build and confirm the satellites pick up the new keys, both guard tests pass**

```bash
dotnet build vHC/HC.sln --configuration Debug
git checkout -- vHC/HC_Reporting/VeeamHealthCheck.csproj
dotnet test vHC/VhcXTests/VhcXTests.csproj --filter "FullyQualifiedName~VbrLocalizationHelperTests"
git checkout -- vHC/HC_Reporting/VeeamHealthCheck.csproj
```

Expected: 0 build errors, `Passed! - Failed: 0, Passed: 2, Skipped: 0` (both `AllStaticStrings_ResolveNonNullAndNonEmpty` and `EverySatellite_HasNoOrphansAndNoUnexpectedMissingKeys` now pass).

- [ ] **Step 3: Commit**

```bash
git add vHC/HC_Reporting/Resources/Localization/vhcres.fR-FR.resx vHC/HC_Reporting/Resources/Localization/vhcres.ja.resx vHC/HC_Reporting/Resources/Localization/vhcres.zh-cn.resx vHC/HC_Reporting/Resources/Localization/vhcres.zh-tw.resx vHC/HC_Reporting/Resources/Localization/untranslated-keys.txt
git commit -m "$(cat <<'EOF'
feat(l10n): bring Stage E's new keys to parity across all four locales (English, untranslated)

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

## Task 4: Rewrite `SetUiAsync()`'s cold-start recovery branch

**Files:**
- Modify: `vHC/HC_Reporting/VhcGui.axaml.cs` (`SetUiAsync()`, around line 386-423)

This is Avalonia code-behind — zero test coverage is possible in this sandbox (native platform bootstrap crashes here; confirmed empirically on every prior stage of this arc). Verification for this task is: exact-match review against this plan's code, a full build, and the Windows verification checklist Task 5 writes (this is the primary reason that checklist exists).

- [ ] **Step 1: Replace the `_modeCheckFailed` branch**

Find in `vHC/HC_Reporting/VhcGui.axaml.cs` (the full current `SetUiAsync()` method):

```csharp
        private async Task SetUiAsync()
        {
            // Stage D removed VhcGui.axaml's hardcoded English defaults from the ~15
            // controls this method resx-backs, so SetUiText() must run before the
            // _modeCheckFailed branch below, not after it - otherwise the window
            // briefly shows blank labels/buttons behind the error dialog on a machine
            // with no local Veeam software and no remote servers configured.
            this.SetUiText();

            if (_modeCheckFailed)
            {
                string errorMessage = "No Veeam Software detected on this machine.\n\n" +
                                     "This tool requires Veeam Backup & Replication (VBR) or Veeam Backup for Microsoft 365 (VB365) to be installed.\n\n" +
                                     "To connect to a remote Veeam server:\n" +
                                     "1. Close this window\n" +
                                     "2. Run from command line with: VeeamHealthCheck.exe /remote /host=your-vbr-server\n\n" +
                                     "For more information, see the documentation.";

                await CGlobals.Notifier.ShowErrorAsync(errorMessage, "Veeam Software Not Detected");

                if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    desktop.Shutdown();
                }
                return;
            }

            // PreRunCheck() stays synchronous (Part 1) but calls the notifier's
            // blocking wrapper (IUiNotifier.Confirm/ShowError) internally.
            // Calling that directly from the UI thread would deadlock, so it's
            // moved off the UI thread here, same as termsCheckBox_Checked below.
            await Task.Run(() => this.functions.PreRunCheck());
            scrubBox.IsChecked = true;
            RescanBox.IsChecked = false;
            Console.WriteLine("Value: " + VbrLocalizationHelper.GuiRescanHosts);
            this.hideProgressBar();
            run.IsEnabled = false;
        }
```

Replace the entire method with:

```csharp
        private async Task SetUiAsync()
        {
            // Stage D removed VhcGui.axaml's hardcoded English defaults from the ~15
            // controls this method resx-backs, so SetUiText() must run before the
            // _modeCheckFailed branch below, not after it - otherwise the window
            // briefly shows blank labels/buttons behind the error dialog on a machine
            // with no local Veeam software and no remote servers configured.
            this.SetUiText();

            if (_modeCheckFailed)
            {
                // SetUiSync()'s fail branch returns before reaching its own
                // run.IsEnabled/hideProgressBar tail. Stage D's final review already
                // documented pBar spinning behind the OK-only dialog here as a harmless
                // pre-existing quirk, harmless only because the app used to shut down
                // within a frame or two. Once the confirm + ManageServersDialog
                // interaction below can take real, human-paced time, leaving Run
                // enabled and the progress bar spinning for that whole interval would
                // no longer be harmless. Safe to set here regardless of which way this
                // branch resolves below.
                run.IsEnabled = false;
                this.hideProgressBar();

                bool wantsToAddServer = await CGlobals.Notifier.ConfirmAsync(
                    VbrLocalizationHelper.GuiNoVeeamDetectedMessage,
                    VbrLocalizationHelper.GuiNoVeeamDetectedTitle);

                if (wantsToAddServer)
                {
                    // Same initial/pinned shape manageServersBtn_Click uses when not
                    // injecting localhost (LocalhostIsInjected is always false on this
                    // path - ModeCheck()'s fail condition is !IsVb365 && !IsVbr, and
                    // IsVbrInstalled is set alongside IsVbr, so LocalhostIsInjected -
                    // IsVbrInstalled || IsVb365 - is false whenever this branch runs).
                    // initial MUST be _displayServers, not empty: ManageServersDialog's
                    // commit overwrites settings.json's server list wholesale from its
                    // own state, so an empty initial would silently drop whatever was
                    // already persisted, including a stray "localhost".
                    var dialog = new ManageServersDialog(
                        initial: _displayServers.ToList(),
                        pinned: Array.Empty<string>());
                    bool committed = await dialog.ShowDialog<bool>(this);

                    if (committed)
                    {
                        _persistedServers = CAppSettings.LoadOrSeedServers(
                            CredentialStore.GetAllServers(),
                            excludeLocalhost: LocalhostIsInjected);
                    }
                }

                if (!CAppSettings.HasNonLocalhostServer(_persistedServers))
                {
                    // Declined the confirm, cancelled ManageServersDialog, or committed
                    // with net zero non-localhost servers - every non-success route
                    // converges on the same shutdown this branch always had.
                    if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                    {
                        desktop.Shutdown();
                    }
                    return;
                }

                this.Title = "Veeam Health Check - Remote Mode";
                CGlobals.Logger.Info("No local Veeam detected, but remote servers configured.", false);

                this.InitializeServerList(preserveSelection: false);

                // InitializeServerList's own fallback selects "localhost" first when
                // present - correct for its other two call sites, but wrong here:
                // reaching this point means HasNonLocalhostServer is true, and
                // selecting a lingering "localhost" entry over the server just added
                // would set REMOTEEXEC = false and point the run straight back at the
                // local box that has no Veeam installed, defeating the point of this
                // recovery path. remoteServer cannot be null: _displayServers is built
                // from _persistedServers with LocalhostIsInjected false on this path,
                // so no injected-localhost row exists to interfere with the check just
                // made above.
                var remoteServer = _displayServers.FirstOrDefault(
                    s => !s.Equals(LocalhostName, StringComparison.OrdinalIgnoreCase));
                serverSelector.SelectedItem = remoteServer;
                UpdateSelectedServersGlobal();
            }

            // PreRunCheck() stays synchronous (Part 1) but calls the notifier's
            // blocking wrapper (IUiNotifier.Confirm/ShowError) internally.
            // Calling that directly from the UI thread would deadlock, so it's
            // moved off the UI thread here, same as termsCheckBox_Checked below.
            await Task.Run(() => this.functions.PreRunCheck());
            scrubBox.IsChecked = true;
            RescanBox.IsChecked = false;
            Console.WriteLine("Value: " + VbrLocalizationHelper.GuiRescanHosts);
            this.hideProgressBar();
            run.IsEnabled = false;
        }
```

- [ ] **Step 2: Build**

```bash
dotnet build vHC/HC.sln --configuration Debug
git checkout -- vHC/HC_Reporting/VeeamHealthCheck.csproj
```

Expected: 0 build errors. (No new warnings about unused `using` directives — `System.Linq`, `VeeamHealthCheck.Functions.ManageServers`, and `VeeamHealthCheck.Startup` are all already imported and already used elsewhere in this file.)

- [ ] **Step 3: Self-review against the spec before moving on**

Re-read `docs/superpowers/specs/2026-09-25-gui-redesign-stage-e-cold-start-design.md`'s "Control flow" section side by side with the method just written. Confirm every point (0 through 5) is present: the `run.IsEnabled`/`hideProgressBar` calls, the confirm, the exact `initial`/`pinned` values, the re-resolution after commit, the `HasNonLocalhostServer` check with fallthrough to shutdown, and the explicit non-localhost selection after `InitializeServerList`.

- [ ] **Step 4: Commit**

```bash
git add vHC/HC_Reporting/VhcGui.axaml.cs
git commit -m "$(cat <<'EOF'
feat(gui): recover from cold-start mode-check failure by offering to add a remote server

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

## Task 5: Final verification and handoff

**Files:**
- Create: `docs/plans/2026-09-25-gui-redesign-stage-e-verification.md`

- [ ] **Step 1: Full build and test run**

```bash
dotnet build vHC/HC.sln --configuration Debug
dotnet test vHC/VhcXTests/VhcXTests.csproj
git checkout -- vHC/HC_Reporting/VeeamHealthCheck.csproj
git status
```

Expected: 0 build errors, `Passed! - Failed: 0, Passed: 914, Skipped: 12, Total: 926` (baseline 910 + Task 1's 4 new tests), and `git status` clean (no stray `VeeamHealthCheck.csproj` diff, no leftover temp files).

- [ ] **Step 2: Write the Windows verification checklist**

Create `docs/plans/2026-09-25-gui-redesign-stage-e-verification.md`:

```markdown
# Stage E — Windows verification checklist

This sandbox cannot render Avalonia (native platform bootstrap crash). Everything below
needs a real Windows machine, and most items specifically need a machine (or a VM) with
**no local VBR/VB365 installed** to reach the cold-start path at all.

## Core recovery flow (highest priority — verify first)

- [ ] Cold start: no local Veeam, no persisted servers, no stored credentials in
      CredentialStore. Confirm dialog shows the new message/title. Confirm Run is
      disabled and the progress bar is not spinning behind the dialog (this used to be
      harmless because the app shut down within a frame or two — Stage E is what makes
      it visible for the first time).
- [ ] **Decline** the confirm → app shuts down exactly as before this stage. Confirm
      nothing was written to `settings.json`.
- [ ] Same cold start, **accept** the confirm → `ManageServersDialog` opens. Add a host,
      click Done → window enters Remote Mode: title changes to "Veeam Health Check -
      Remote Mode", the server picker shows and **selects** the new host (not
      "localhost"), Run becomes enabled, and a run completes without deadlocking on
      `PreRunCheck`.
- [ ] Accept, then **Cancel** the `ManageServersDialog` with nothing added → falls
      through to shutdown, same as declining. Confirm nothing was written to
      `settings.json`.
- [ ] Accept, then press **Done with nothing changed** (no host added, none removed) →
      falls through to shutdown, same outward behavior as Cancel — but confirm
      `settings.json` now has an explicit `"Servers": []`, unlike the Cancel case. This
      is pre-existing `ManageServersDialog` behavior, not new to this stage, but this is
      the first path that reaches it without the user having deliberately opened Manage
      Servers.
- [ ] Accept, add a host, then remove it again before clicking Done (net zero) → same
      outcome and same `"Servers": []` write as the item above.
- [ ] Seed `settings.json` (or the credential store) with a stray `"localhost"` entry
      before cold start (e.g. via a `/savecreds` run against the default host on a
      machine with no local Veeam, or by hand-editing `settings.json`). Confirm the
      cold-start dialog still appears (the trigger is "no *non-localhost* server", not
      "zero servers"). Accept, add a real host, commit — confirm the picker selects and
      targets the real host, **not** `"localhost"`, and that a run actually reaches the
      remote server rather than failing against the local, Veeam-less box.
- [ ] **Quit and relaunch** after the item above (still no local Veeam, `settings.json`
      now has `["localhost", "<the host you added>"]`). Confirm the picker still selects
      and targets the real host on this second launch too, not `"localhost"` — this
      exercises `SetUiSync`'s pre-existing "already has remote servers" branch, which
      this stage's own recovery path never runs on a second launch (`_modeCheckFailed`
      stays `false`), so `InitializeServerList`'s own fallback ordering is what has to
      get this right, not `SetUiAsync`'s explicit override. A code-quality review during
      implementation (Task 4) found and fixed a real regression here — this item exists
      specifically to catch it on real hardware if the fix regresses.
- [ ] After successfully adding a host via any of the above and starting a run, confirm
      the lazy credential prompt (`CredentialPromptWindow`) still appears at the
      expected point if that host has no stored credentials yet.

## Localization

- [ ] With Windows display language set to French, Japanese, Simplified Chinese, or
      Traditional Chinese, trigger the cold-start dialog and confirm the message and
      title render (English content is expected and correct — these two keys are not
      translated yet, same policy as every other Stage D/E key added this way) rather
      than falling back to a raw resx key name or throwing.

## Recorded, not fixed (informational only — no action needed)

- `NotifierDialog`'s Yes/No/OK button labels are hardcoded English, not resx-backed.
  Pre-existing, unrelated to this stage, deliberately out of scope (see the spec's "Out
  of scope" section).
- The `"Veeam Health Check - Remote Mode"` title string stays a raw C# literal,
  unlocalized — pre-existing, deliberately out of scope for the same reason.
- Still owed from Stage D, unrelated to this stage's changes: the notif type/severity
  correctness-trap fix under a non-English locale, and the French SOBR locale-file
  spot-check (`docs/plans/2026-09-15-gui-redesign-stage-d-verification.md`).
```

- [ ] **Step 3: Commit**

```bash
git add docs/plans/2026-09-25-gui-redesign-stage-e-verification.md
git commit -m "$(cat <<'EOF'
docs(gui): add the Stage E Windows verification checklist

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

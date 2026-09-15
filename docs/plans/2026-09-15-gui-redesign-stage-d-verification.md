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

## Known, deliberate translation debt (Task 4/6)

- [ ] `HtmlIntroLine3` is intentionally left orphaned (not wired up) in all four locale
      files (fR-FR, ja, zh-CN, zh-tw) — see commit `39be2be5` and Task 6's
      `KnownDeliberateOrphans` allowlist in `VbrLocalizationHelperTests.cs`. Its old
      translated content lacks a closing `</a>` tag and would render malformed HTML in the
      report's About card if wired to the live `HtmlIntroLine3Original`/`HtmlIntroLine3Anon`
      keys. All four locales correctly show the well-formed English fallback for that
      sentence today. No action needed unless a translator produces corrected replacement
      text for both `HtmlIntroLine3Original` and `HtmlIntroLine3Anon` (with a closing tag
      and the current, post-path-split `JobSessionReports` path) — that would be separate,
      future, content-only work, not a regression to fix now.

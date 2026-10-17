---
title: "Localization helpers have no missing-key fallback and a single-point-of-failure static initializer"
severity: Medium
labels: [reliability]
domain: analysis-monitor
files:
  - vHC/HC_Reporting/Resources/Localization/VbrLocalizationHelper.cs:6
  - vHC/HC_Reporting/Resources/Localization/VB365/Vb365ResourceHandler.cs:6
confidence: High
---

## Summary

Both localization classes eagerly initialize hundreds of `public static string` fields via `ResourceManager.GetString(...)` in field initializers (i.e., the type's static constructor). Two failure modes are unhandled:

1. **Missing key** — `GetString` returns `null` for an absent key. There is no fallback (no `?? "KeyName"`), so a single missing/renamed `.resx` entry silently injects `null` into report HTML/GUI text (rendering as blank labels, tooltips, or nav entries) with no log entry. With ~580 keys in `VbrLocalizationHelper` and ~200 in `Vb365ResourceHandler`, a drift between code and resx is realistic and invisible at compile time.
2. **Missing resource set** — if the embedded `vhcres`/`vb365_vhcres` resources fail to load (bad satellite assembly, renamed baseName, trimming), the first `GetString` throws `MissingManifestResourceException` inside the static initializer; every subsequent touch of *any* field on the class then throws `TypeInitializationException`, killing the entire report generation rather than degrading to key names.

Additionally, the fields are mutable `public static string` (not `readonly`), so any code can accidentally overwrite a label process-wide, and values are fixed at first touch — runtime culture changes are ignored.

## Impact

Wrong or blank text in customer-facing reports with no diagnostic trail (missing key), or a hard crash of report generation (missing manifest). Both are common-path: the helpers are touched by every VBR/VB365 HTML compile.

## Evidence

`vHC/HC_Reporting/Resources/Localization/VbrLocalizationHelper.cs:6-8` —
```csharp
private static readonly ResourceManager m4 = new("VeeamHealthCheck.Resources.Localization.vhcres", typeof(VbrLocalizationHelper).Assembly);

public static string GuiAcceptButton = m4.GetString("GuiAcceptButton");
```
(All ~580 fields follow this pattern; same in `Vb365ResourceHandler.cs:6-8` with `vb365_vhcres`.) No null-coalescing, no try/catch, fields not `readonly`.

## Suggested fix

Route lookups through a small helper: `private static string Get(string key) { try { return m4.GetString(key) ?? key; } catch (MissingManifestResourceException) { return key; } }` and make fields `static readonly` (or properties). That converts both failure modes into visible-but-harmless key names in the output and removes the TypeInitializationException cascade.

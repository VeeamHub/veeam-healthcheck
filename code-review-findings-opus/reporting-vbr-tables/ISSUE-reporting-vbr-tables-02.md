# DomainStatus cell computes danger shading but never applies it (copy-paste bug)

**Category:** reporting-vbr-tables
**Severity:** Medium
**Type:** Bug
**File(s):** `Functions/Reporting/Html/VBR/VbrTables/BackupServer/CVbrServerTableHelper.cs:135-143`

## Summary
`DomainStatus()` is a security row that should flag a domain-joined backup server (a hardening anti-pattern) in red. It computes the `shade` value but then calls the 2-argument `TableData` overload, discarding the shade — so the cell is never colored.

## Evidence
```csharp
public Tuple<string, string> DomainStatus()
{
    string result = CSecurityGlobalValues.IsDomainJoined;
    int shade = this.ParseFalseAsBadShade(result);     // computed...

    string header = this.form.TableHeader(VbrLocalizationHelper.BackupServerDomainJoined, string.Empty);
    string data = this.form.TableData(result, string.Empty);   // ...but shade is NOT passed
    return Tuple.Create(header, data);
}
```
Contrast `RdpStatus`-style rows above (line 131) which correctly call `this.form.TableData(result, string.Empty, shade)`.

Note also the logic: `ParseFalseAsBadShade` returns danger when the value is `"False"`. For "is domain joined", a *True* value is the security concern, so this row likely should use `ParseTrueAsBadShade` instead. Either way the shade is currently never rendered.

## Impact
The "Domain Joined" security indicator never highlights, so reviewers lose the visual cue for a real hardening finding. If the intended helper is also wrong (`ParseFalseAsBadShade` vs `ParseTrueAsBadShade`), the coloring would be inverted even once wired up.

## Suggested Fix
Pass the shade and use the correct polarity:
```csharp
int shade = this.ParseTrueAsBadShade(result);   // domain-joined == bad
string data = this.form.TableData(result, string.Empty, shade);
```
Confirm the intended polarity against the security guidance before committing.

## Labels
bug, security-indicator, copy-paste, conditional-formatting

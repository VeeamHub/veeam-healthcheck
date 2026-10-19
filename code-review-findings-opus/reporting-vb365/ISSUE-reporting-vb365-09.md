# Security table OS/throttling/RAM thresholds and shade reuse make cert severity fragile

**Category:** reporting-vb365
**Severity:** Low
**Type:** Maintainability
**File(s):** `vHC/HC_Reporting/Functions/Reporting/Html/VB365/CM365Tables.cs:597-642`

## Summary
The certificate-expiry coloring in `Vb365Security()` mixes assignment (`= 1` for expired/danger) with bitwise-or compound assignment (`|= 3` for expiring-soon/warning) on the same shade variables. The shade values are CSS-class selectors (1=danger, 2=success, 3=warning), not bit flags, so combining them with `|=` is semantically meaningless and only happens to work because the expired and expiring-soon conditions are mutually exclusive (`< Now` vs `> Now && < Now+60`). Any future edit that makes the branches overlap would yield `1 | 3 == 3`, silently **downgrading an expired cert (danger/red) to warning (yellow)**.

## Evidence
`CM365Tables.cs:591-600`:
```csharp
if (sCertExpiry < DateTime.Now)
{
    serverDateShade = 1;          // danger (assignment)
}
if (sCertExpiry > DateTime.Now && sCertExpiry < DateTime.Now.AddDays(60))
{
    serverDateShade |= 3;         // warning (bitwise-or on a non-flag enum-like value)
}
```
The same `= 1` / `|= 3` pattern repeats for API, Tenant, Portal, and Operator certs (lines 603-642). Note also `serverDateShade` etc. are computed but the cell renders `ServerCertSelfSigned` with `serverCertSignShade`, not these date shades (lines 672-703) — so the cert *expiry* dates (`g.ServerCertExpires`, etc.) are emitted with no coloring at all, meaning all this date-shade computation is currently dead.

## Impact
Two latent issues: (1) the `|=` idiom is a trap that will invert severity if the branches ever overlap; (2) the entire `*DateShade` computation is unused — certificate expiry dates are shown uncolored, so an expired or soon-to-expire cert is not visually flagged in the report at all, undermining the Security section's purpose.

## Suggested Fix
Use plain `else if` assignment (`= 3`) instead of `|= 3`, and actually pass the date shade into the corresponding `TableData(g.ServerCertExpires, ..., serverDateShade)` calls so expiry coloring takes effect.

## Labels
maintainability, dead-code, coloring, certificates, vb365, security

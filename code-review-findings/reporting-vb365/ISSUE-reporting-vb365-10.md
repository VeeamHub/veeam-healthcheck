---
title: "GetServerName(): dynamic '.Name' never matches lowercased CSV key — Priority-2 lookup always fails"
severity: Medium
labels: [bug]
domain: reporting-vb365
files:
  - vHC/HC_Reporting/Functions/Reporting/Html/VB365/CVb365HtmlCompiler.cs:177
confidence: Medium
---

## Summary

`GetServerName()` reads the VB365 server name from Proxies.csv via `proxies[0].Name`. The dynamic records produced by `CCsvParser`/CsvHelper are keyed by the `PrepareHeaderForMatch` result, which lowercases and strips punctuation (`CCsvReader.cs:76-84`) — i.e. the key is `name`, not `Name`. Dynamic member binding on the ExpandoObject is case-sensitive, so `.Name` throws `RuntimeBinderException`, which is caught at line 190 and the code always falls through to `Dns.GetHostName()`.

That the lowercase keys are correct is demonstrated by the working table code, which switches on `case "name":` / `case "internetproxy":` for the same CSV (`CM365Tables.cs:185, 203`).

## Impact

The "works for import and local" path (the comment at line 167 says this was the point of the change) never works: when an SE imports a customer's CSV bundle, the report file is named after the **SE's workstation hostname** instead of the customer's VB365 server. Misleading filenames, and possibly overwriting reports from different customers imported on the same machine.

## Evidence

`vHC/HC_Reporting/Functions/Reporting/Html/VB365/CVb365HtmlCompiler.cs:172-177`:

```csharp
var proxies = parser.GetDynamicVboProxies()?.ToList();

if (proxies != null && proxies.Count > 0)
{
    // Get first proxy's hostname - typically the VB365 server itself or primary proxy
    string serverName = proxies[0].Name?.ToString();
```

Header prep, `vHC/HC_Reporting/Functions/Reporting/CsvHandlers/CCsvReader.cs:76`:

```csharp
PrepareHeaderForMatch = args => args.Header.ToLower() ...
```

## Suggested fix

Use the lowercase key: cast the record to `IDictionary<string, object>` and read `["name"]` (matching how `CM365Tables` consumes these records), or change the access to `proxies[0].name`. Verify with an imported dataset that the filename picks up the proxy hostname.

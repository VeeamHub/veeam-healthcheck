# Monitor config hardcodes verify_ssl: false, sending credentials over unauthenticated TLS

**Category:** analysis-monitor
**Severity:** High
**Type:** Security
**File(s):** `vHC/HC_Reporting/Functions/Monitor/CVhcMonitorIntegration.cs:82-86`

## Summary
The generated monitor config always disables TLS certificate verification (`verify_ssl: false`) while pointing the monitor at the VBR REST API over HTTPS and authenticating with a real username/password. With certificate validation off, the monitor will trust any certificate presented on `https://{vbrServer}:9419`, so an attacker positioned on the network (ARP/DNS spoofing, rogue device on the backup VLAN) can impersonate the VBR server and harvest the credentials the monitor submits on every 5-minute run.

## Evidence
```csharp
// CVhcMonitorIntegration.cs:82-86
sb.AppendLine($"    url: \"https://{vbrServer}:9419\"");
sb.AppendLine($"    username: \"{EscapeYaml(username)}\"");
sb.AppendLine($"    password: \"{EscapeYaml(password)}\"");
sb.AppendLine("    api_version: \"1.3-rev1\"");
sb.AppendLine("    verify_ssl: false");   // <-- always disabled, not configurable
```
The scheduled task runs this every 5 minutes indefinitely:
```csharp
// CVhcMonitorIntegration.cs:138
$trigger = New-ScheduledTaskTrigger -RepetitionInterval (New-TimeSpan -Minutes 5) ...
```

## Impact
Recurring credential exposure to active MITM on the path between the monitor host and the VBR server. Because the task repeats every 5 minutes forever, the attacker has a continuous, low-effort interception window rather than a one-shot. The credential is frequently a privileged VBR account, so capture can lead to full control of the backup environment.

## Suggested Fix
- Make `verify_ssl` a setting that defaults to `true`. Allow opt-out only as an explicit, logged user choice (e.g., a checkbox / `--insecure` flag) with a clear warning.
- Where the VBR server uses a self-signed cert, prefer pinning the expected certificate thumbprint over disabling verification wholesale.

## Labels
security, tls, mitm, credentials, monitor

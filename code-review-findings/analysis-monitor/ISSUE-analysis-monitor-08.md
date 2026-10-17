---
title: "Unbounded log accumulation and per-line file open/close in CLogger"
severity: Low
labels: [performance, maintainability]
domain: analysis-monitor
files:
  - vHC/HC_Reporting/Common/Logging/CLogger.cs:17
  - vHC/HC_Reporting/Common/Logging/CLogger.cs:105
confidence: High
---

## Summary

Two related smells: (1) every `CLogger` instance creates a new timestamped log file under `{unsafeDir}\Log` and nothing ever prunes old files — repeated/scheduled runs accumulate logs forever; (2) `LogLine` opens and closes a `FileStream` + `StreamWriter` for every single line, which is wasteful for chatty collection phases and increases the window for share-mode collisions (the very condition the IOException retry at line 111 exists to paper over). Concurrent writers via `FileShare.Write` with `FileMode.Append` can also interleave partial lines since appends through separate handles are not atomic.

## Impact

- Disk creep on customer servers (`C:\temp\vHC\Original\Log` grows without bound), especially once vhc-monitor/scheduled usage makes runs frequent.
- Per-line open/close measurably slows large collections and produces the lock contention the retry logic compensates for.
- Possible garbled lines when GUI + background tasks log simultaneously.

## Evidence

`vHC/HC_Reporting/Common/Logging/CLogger.cs:30-33` — new file per instance, no retention:
```csharp
string logName = String.Format("Job.{1}_{0}_.log", dt.ToString("yyyy.MM.dd_HHmmss"), jobName);
return logDir + "\\" + logName;
```
`vHC/HC_Reporting/Common/Logging/CLogger.cs:105-109` — open/close per line:
```csharp
using (var fs = new FileStream(this.logFile, FileMode.Append, FileAccess.Write, FileShare.Write, bufferSize: 4096, useAsync: false))
using (var sw = new StreamWriter(fs))
{
    sw.WriteLine(line);
}
```

## Suggested fix

Keep a single shared `StreamWriter` (with `AutoFlush = true`) per logger guarded by a lock, or serialize writes through a `lock`ed static. Add simple retention at startup (delete `Job.*.log` older than N days or keep last N files).

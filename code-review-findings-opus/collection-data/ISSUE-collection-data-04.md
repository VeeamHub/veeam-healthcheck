# CLogParser opens waits.csv per-row under a lock during parallel parsing (open/close storm + serialized I/O)

**Category:** collection-data
**Severity:** Medium
**Type:** Performance
**File(s):** `vHC/HC_Reporting/Functions/Collection/LogParser/CLogParser.cs:83-99`, `vHC/HC_Reporting/Functions/Collection/LogParser/CLogParser.cs:266-285`

## Summary
`GetWaitsFromFiles` parses directories in parallel (`Parallel.ForEach`, `CLogParser.cs:133`), but every individual wait record calls `CalcTime` → `DumpWaitsToFile`, which acquires a global `fileLock` and **opens, appends to, and closes** `waits.csv` for a single line each time. Under parallelism this fully serializes all writers on one lock while paying a file open/flush/close on every record. On large environments (the code is explicitly built for "7 day timeout" scale) this is a substantial, avoidable bottleneck and largely negates the parallelism.

## Evidence
```csharp
// CLogParser.cs:86-92
lock (this.fileLock)
{
    using (StreamWriter sw = new StreamWriter(this.pathToCsv, append: true))
    {
        sw.WriteLine(JobName + "," + start + "," + end + "," + diff);
    }
}
```
Called once per matched wait, from inside the parallel body via `CheckFileWait` → `CalcTime` (`CLogParser.cs:252`, `CLogParser.cs:283`).

## Impact
For tens of thousands of wait events the program performs tens of thousands of open/close cycles, each serialized behind a single lock, while N worker threads stall. This dominates parse time on exactly the large deployments the parallelization was added to help.

## Suggested Fix
Collect rows per-directory in the parallel body (they already accumulate in `List<TimeSpan> waits` and `ConcurrentDictionary`), then write the CSV once after `Parallel.ForEach` completes. Alternatively keep a single long-lived `StreamWriter` guarded by the lock and flush once at the end, or buffer rows into a `ConcurrentQueue<string>` and drain after the parallel phase.

## Labels
performance, concurrency, io, collection

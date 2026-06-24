# CScrubHandler is a shared static singleton with non-thread-safe, IO-on-every-call mutation

**Category:** startup-cli
**Severity:** Medium
**Type:** Concurrency
**File(s):** `vHC/HC_Reporting/Common/CGlobals.cs:30,166`, `vHC/HC_Reporting/Common/Scrubber/CXmlHandler.cs:26-115`

## Summary
`CGlobals.scrubberMain` is a single static `CScrubHandler` exposed via `CGlobals.Scrubber` and used across all report rendering. Its `ScrubItem` path mutates a shared `Dictionary<string,string> matchDictionary` and `Dictionary<string,int> typeCounters` with no locking, and `AddItemToList` saves an `XDocument` to disk and rewrites a JSON file on **every** new item. With multi-server execution (`CGlobals.MaxParallelServers = 3`, `SelectedServers`) and the GUI/report layers potentially scrubbing concurrently, this is a data race.

## Evidence
```csharp
// CGlobals
private static readonly CScrubHandler scrubberMain = new();
public static CScrubHandler Scrubber { get { return scrubberMain; } }
```
```csharp
// CScrubHandler.ScrubItem — no lock around shared dictionary RMW
if (!this.matchDictionary.ContainsKey(item))
{
    if (!this.typeCounters.ContainsKey(type)) this.typeCounters[type] = 0;
    int counter = this.typeCounters[type]++;
    string newName = type + "_" + counter.ToString();
    this.matchDictionary.Add(item, newName);
    this.AddItemToList(type, item, newName);   // this.doc.Save(...) + WriteToText() on every call
}
```

## Impact
Concurrent scrubbing can throw `InvalidOperationException` (dictionary modified during enumeration in `WriteToText`'s `OrderBy`), corrupt the obfuscation map, or produce duplicate/colliding faux names — meaning the same real object maps to two different scrubbed names (or two real objects collide), silently breaking the anonymization guarantee. Saving the XML/JSON key file on every single item is also O(n²) IO that scales badly on large environments. Even single-threaded, the per-item full-file rewrite is a latent performance trap.

## Suggested Fix
- Wrap the `matchDictionary`/`typeCounters` read-modify-write and the file persistence in a `lock`, or replace with `ConcurrentDictionary` + an explicit atomic counter.
- Persist the key file once at end-of-run (or batched) rather than on every `AddItemToList`.
- If parallel multi-server scrubbing is intended, give each run its own handler/key file instead of a process-wide static.

## Labels
concurrency, thread-safety, scrub, performance, data-integrity, medium

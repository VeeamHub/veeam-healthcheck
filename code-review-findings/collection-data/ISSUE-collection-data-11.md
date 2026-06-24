---
title: "waits.csv written with unescaped fields and culture-sensitive DateTime formatting"
severity: Medium
labels: [bug, reliability]
domain: collection-data
files:
  - vHC/HC_Reporting/Functions/Collection/LogParser/CLogParser.cs:90
confidence: High
---

## Summary

`DumpWaitsToFile` builds CSV rows by naive string concatenation: `JobName + "," + start + "," + end + "," + diff`. Two problems:

1. **No CSV escaping.** A VBR job name containing a comma (legal in Veeam) shifts every subsequent column; quotes in a name corrupt the row. Downstream `CCsvParser.WaitsCsvReader` maps columns by index (`CWaitsCsv` uses `[Index(0..3)]`), so shifted columns silently load wrong values.
2. **Culture-sensitive DateTime/TimeSpan formatting.** `start` and `end` are `DateTime` formatted via implicit `ToString()` with the machine's current culture. The file is later parsed by CsvHelper configured with `CultureInfo.InvariantCulture`, so on non-en-US collection machines the StartTime/EndTime strings may be re-interpreted incorrectly or fail to parse (e.g., `dd.MM.yyyy` vs `M/d/yyyy` ambiguity).

Note the header row also says `JobName,StartTime,EndTime,Duration` while readers use positional indexes — escaping bugs go undetected.

## Impact

Silently wrong wait-time rows in reports generated from machines with non-US locales or comma-containing job names; the data loads without error but the columns are misaligned or misparsed.

## Evidence

`vHC/HC_Reporting/Functions/Collection/LogParser/CLogParser.cs:88-91` —

```csharp
using (StreamWriter sw = new StreamWriter(this.pathToCsv, append: true))
{
    sw.WriteLine(JobName + "," + start + "," + end + "," + diff);
}
```

`start`/`end` are `DateTime` parameters — implicit current-culture `ToString()`. `JobName` is the raw directory name with no quoting.

## Suggested fix

Write the file with CsvHelper (`CsvWriter` + `InvariantCulture`) or at minimum quote/escape `JobName` and format timestamps with an explicit round-trippable format (`start.ToString("o")` or `yyyy-MM-dd HH:mm:ss` + InvariantCulture) matching what the reader expects.

# PowerPoint Export Guide

## Quick Start

The Veeam Health Check tool can now export reports as PowerPoint presentations, making it easier to share findings in meetings and presentations.

## Enabling PowerPoint Export

### Option 1: Command Line
Add the `/pptx` flag when running the tool:

```powershell
# For VBR (Veeam Backup & Replication)
.\VeeamHealthCheck.exe /vbr /pptx

# For VB365 (Veeam Backup for Microsoft 365)
.\VeeamHealthCheck.exe /vb365 /pptx

# Export both PDF and PowerPoint
.\VeeamHealthCheck.exe /vbr /pdf /pptx
```

### Option 2: GUI
1. Launch VeeamHealthCheck.exe
2. In the **Export Options** section, check ☑ **Export PowerPoint**
3. Run your health check as normal

## What Gets Exported

The PowerPoint export includes:
- **Title Slide**: Overview of the health check report
- **Table Slides**: Each HTML table becomes one or more PowerPoint slides
- **Section Titles**: Preserved from the HTML report

### Example Slides
- License Summary
- Backup Server & Config DB Info
- Security Summary
- Job Summary
- Protected Workloads
- Proxy Info
- Repository Details
- And more...

## Table Pagination

Large tables are automatically split for readability:

**Before (30+ rows):**
```
License Summary
[30+ rows of data - hard to read on one slide]
```

**After (automatic pagination):**
```
Slide 1: License Summary (1/3)
[Rows 1-30]

Slide 2: License Summary (2/3)
[Rows 31-60]

Slide 3: License Summary (3/3)
[Rows 61-90]
```

## Output Location

PowerPoint files are saved in the same location as your HTML reports:

**Unscrubbed Reports:**
```
C:\VeeamHealthCheckReports\UNSCRUBBED\
  Veeam Health Check Report_VBR_ServerName_2024.12.09.183000.html
  Veeam Health Check Report_VBR_ServerName_2024.12.09.183000.pptx  ← PowerPoint file
  Veeam Health Check Report_VBR_ServerName_2024.12.09.183000.pdf   ← (if /pdf also used)
```

**Scrubbed Reports:**
```
C:\VeeamHealthCheckReports\SCRUBBED\
  Veeam Health Check Report_VBR_abc1234_2024.12.09.183000.html
  Veeam Health Check Report_VBR_abc1234_2024.12.09.183000.pptx
```

## Presentation Tips

### Customizing the PowerPoint
After export, you can:
1. Apply your organization's PowerPoint template
2. Add company logos and branding
3. Adjust colors and fonts
4. Add presenter notes
5. Reorder slides for your audience

### Using in Meetings
The PowerPoint format is ideal for:
- Executive briefings
- Technical reviews
- Audit presentations
- Status updates
- Change advisory board meetings

### Recommended Workflow
1. Run health check with `/pptx` export
2. Open the generated `.pptx` file
3. Review all slides and remove any you don't need
4. Add title slide with your company branding
5. Add summary/conclusion slides
6. Save as final presentation

## Formatting Details

### Fonts
- **Slide Titles**: 28pt, Bold
- **Table Headers**: 14pt, Bold
- **Table Data**: 11pt, Regular

### Special Characters
These are preserved in the export:
- ✓ Checkmarks (success/enabled)
- ☑ Checked boxes
- ☐ Unchecked boxes
- ⚠ Warning symbols

### Table Structure
- Native PowerPoint tables (not images)
- Editable after export
- Can be resized and reformatted
- Copy-paste friendly

## Troubleshooting

### No .pptx file generated
- Check that you used the `/pptx` flag or checked the GUI option
- Verify you're not using scrubbed mode (scrubbed exports skip PPTX by design)
- Look for error messages in the console output

### Slides look crowded
- This is expected for tables with many columns
- After export, rotate slide orientation to landscape if needed
- Consider splitting wide tables manually in PowerPoint

### Missing data
- Ensure all sections were expanded in the HTML (this happens automatically)
- Check the HTML report first to confirm data is present there

## Comparing Export Formats

| Feature | HTML | PDF | PowerPoint |
|---------|------|-----|------------|
| Interactive | ✓ | ✗ | ✗ |
| Searchable | ✓ | ✓ | ✓ |
| Editable | ✗ | ✗ | ✓ |
| Presentation-ready | ✗ | △ | ✓ |
| Print-friendly | △ | ✓ | ✓ |
| Meeting-friendly | ✗ | △ | ✓ |

**Recommendation**: Generate all three formats for maximum flexibility!
```bash
.\VeeamHealthCheck.exe /vbr /pdf /pptx
```

## Advanced Usage

### Custom Output Path
Combine with the `/path` parameter:
```powershell
.\VeeamHealthCheck.exe /vbr /pptx /path="D:\Reports"
```

### Scheduled Exports
Create a scheduled task that generates weekly PowerPoint reports:
```powershell
$trigger = New-ScheduledTaskTrigger -Weekly -DaysOfWeek Monday -At 6am
$action = New-ScheduledTaskAction -Execute "C:\Tools\VeeamHealthCheck.exe" -Argument "/vbr /pptx"
Register-ScheduledTask -TaskName "Weekly Veeam Report" -Trigger $trigger -Action $action
```

## Support

For issues or questions about PowerPoint export:
1. Check the main README.md in the repository
2. Review the POWERPOINT_EXPORT_SUMMARY.md for technical details
3. Submit an issue on the GitHub repository

## Version Information

PowerPoint export feature added in version 3.0+

Requires:
- .NET 8.0 or higher
- Windows 7 or higher
- PowerPoint not required (files can be opened in Google Slides, LibreOffice Impress, etc.)

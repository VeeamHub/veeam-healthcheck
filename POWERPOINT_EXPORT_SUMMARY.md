# PowerPoint Export Feature - Implementation Summary

## Overview
Added PowerPoint (PPTX) export capability to the Veeam Health Check tool, allowing users to export health check reports as PowerPoint presentations with automatic table pagination.

## Changes Made

### 1. New Files Created
- **`HtmlToPptxConverter.cs`** - Core PowerPoint converter class that:
  - Parses HTML content to extract tables
  - Creates PowerPoint slides with native table objects
  - Automatically splits tables exceeding 30 rows across multiple slides
  - Handles title slides and section headers
  - Cleans HTML formatting and preserves special characters

- **`README.md`** - Documentation for the exportable formats folder

### 2. Modified Files

#### `VeeamHealthCheck.csproj`
- Added `DocumentFormat.OpenXml` version 3.1.0 package reference

#### `CGlobals.cs`
- Added `EXPORTPPTX` global boolean flag (default: false)

#### `CHtmlExporter.cs`
- Added `ExportHtmlStringToPptx()` method
- Integrated PowerPoint export into both VBR and VB365 export workflows
- PowerPoint export triggers when `CGlobals.EXPORTPPTX` is true and not scrubbing

#### `CArgsParser.cs`
- Added `/pptx` command-line argument support

#### `VhcGui.xaml`
- Added `pptxCheckBox` UI element in Export Options section

#### `VhcGui.xaml.cs`
- Added `pptxCheckBox_Checked()` and `pptxCheckBox_Unchecked()` event handlers
- Set checkbox content to "Export PowerPoint"

## Features

### Table Pagination
- Tables with more than 30 rows are automatically split across multiple slides
- Each continuation slide is numbered (e.g., "License Summary (1/3)")
- Headers are included on the first slide
- Maintains data integrity across page breaks

### HTML Parsing
- Identifies table sections by CSS classes and IDs
- Extracts section titles from H2, H3, or button elements
- Parses table headers and data rows using regex
- Handles nested HTML structures

### Text Processing
- Removes HTML tags
- Decodes HTML entities
- Preserves Unicode symbols (✓, ☑, ☐, ⚠)
- Collapses whitespace

### Output Format
- Creates native PowerPoint table objects (not images)
- Uses consistent formatting:
  - Title: 2800pt font size, bold
  - Headers: 1400pt font size, bold
  - Body: 1100pt font size
- Files saved alongside HTML reports with `.pptx` extension

## Usage

### Command Line
```bash
# Export to PowerPoint only
VeeamHealthCheck.exe /vbr /pptx

# Export to both PDF and PowerPoint
VeeamHealthCheck.exe /vbr /pdf /pptx
```

### GUI
Check the "Export PowerPoint" checkbox in the Export Options section before running the health check.

## Technical Implementation

### Architecture
The implementation follows the existing pattern used for PDF export:
1. Global flag controls feature enablement
2. Command-line and GUI interfaces update the flag
3. HTML exporter checks flag and calls converter
4. Converter processes HTML string and generates output

### Dependencies
- **DocumentFormat.OpenXml 3.1.0**: Microsoft's official library for creating Office documents
- Compatible with .NET 8.0 Windows framework

### Customization Points
Constants in `HtmlToPptxConverter.cs` can be adjusted:
- `MaxRowsPerSlide`: Pagination threshold (default: 30)
- `TitleFontSize`: Slide title font size
- `HeaderFontSize`: Table header font size
- `BodyFontSize`: Table body font size

## Testing Recommendations

### Basic Functionality
1. Run health check with `/pptx` flag
2. Verify `.pptx` file is created
3. Open file in PowerPoint and verify content

### Table Pagination
1. Run health check on environment with large tables (30+ rows)
2. Verify tables split correctly across slides
3. Verify slide numbering is correct
4. Verify headers appear only on first slide

### Format Handling
1. Verify special characters render correctly (checkmarks, warnings)
2. Verify HTML entities are decoded properly
3. Verify collapsible sections are expanded

### Integration
1. Test with both VBR and VB365 reports
2. Test with scrubbed and unscrubbed data
3. Test combined `/pdf /pptx` export
4. Test GUI checkbox functionality

## Future Enhancements

### Potential Improvements
1. **Custom slide layouts**: Add support for master slides with branding
2. **Charts and graphs**: Convert data to PowerPoint charts
3. **Image support**: Include logos and icons from HTML
4. **Table styling**: Add color coding based on status/severity
5. **Compression**: Option to merge smaller tables onto single slides
6. **Notes**: Add slide notes with additional context
7. **Hyperlinks**: Preserve internal navigation links

### Configuration Options
Consider adding configuration file support for:
- Font families and sizes
- Color schemes
- Pagination thresholds per table type
- Slide layouts and templates

## Known Limitations

1. **Fixed pagination**: All tables use the same 30-row threshold
2. **Basic formatting**: Uses default PowerPoint styles
3. **No images**: HTML images are not exported
4. **Column sizing**: All columns use equal width
5. **No charts**: Data visualizations from HTML are not converted to PowerPoint charts

## Build Status
✅ Project builds successfully with only existing warnings (no new errors introduced)

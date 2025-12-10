# Export Formats

This folder contains converters for exporting Veeam Health Check reports to various formats.

## Available Exporters

### HtmlToPdfConverter
Exports the HTML report to PDF format using DinkToPdf library.
- Converts all visible sections
- Maintains table structure
- Uses A3 landscape orientation

### HtmlToPptxConverter
Exports the HTML report to PowerPoint (PPTX) format using DocumentFormat.OpenXml library.
- Creates a title slide
- Each table is converted to a PowerPoint slide
- **Automatic pagination**: Tables with more than 30 rows are automatically split across multiple slides
- Preserves table headers and data
- Cleans HTML formatting and special characters

## PowerPoint Export Features

### Table Handling
- Tables are extracted from HTML sections identified by their `class` and `id` attributes
- Section titles are extracted from `<h2>`, `<h3>`, or `<button>` elements
- Each table gets its own slide(s) with the section title as the slide title

### Pagination
When a table exceeds 30 rows, it's automatically split across multiple slides:
- Each continuation slide is numbered (e.g., "License Summary (1/3)", "License Summary (2/3)")
- Headers are preserved on the first slide only
- Page breaks occur at row boundaries to avoid splitting data

### Text Cleaning
The converter automatically:
- Removes HTML tags
- Decodes HTML entities (e.g., `&nbsp;`, `&#9989;`)
- Preserves Unicode symbols (✓, ☑, ☐, ⚠)
- Collapses multiple spaces

## Usage

### Command Line
To export to PowerPoint, add the `/pptx` flag:
```bash
VeeamHealthCheck.exe /vbr /pptx
```

You can combine PDF and PowerPoint export:
```bash
VeeamHealthCheck.exe /vbr /pdf /pptx
```

### GUI
In the GUI, check the "Export PowerPoint" checkbox in the Export Options section before running the health check.

## Output
PowerPoint files are saved alongside the HTML report with the `.pptx` extension:
```
C:\VeeamHealthCheckReports\UNSCRUBBED\Veeam Health Check Report_VBR_ServerName_2024.12.09.183000.pptx
```

## Customization

You can adjust the following constants in `HtmlToPptxConverter.cs`:
- `MaxRowsPerSlide`: Maximum rows per slide before pagination (default: 30)
- `TitleFontSize`: Font size for slide titles (default: 2800)
- `HeaderFontSize`: Font size for table headers (default: 1400)
- `BodyFontSize`: Font size for table body text (default: 1100)

## Technical Details

The PowerPoint converter:
1. Parses HTML to identify table sections
2. Extracts table headers and data rows using regex
3. Creates PowerPoint slides with appropriate layouts
4. Adds tables as native PowerPoint table objects (not images)
5. Handles pagination automatically for large tables

## Dependencies
- DocumentFormat.OpenXml 3.1.0 or higher

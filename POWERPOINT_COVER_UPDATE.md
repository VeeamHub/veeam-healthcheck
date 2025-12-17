# PowerPoint Cover Slide Update

## Overview
Updated the PowerPoint export cover slide to match the HTML report's cover design with a dark themed overlay containing company information.

## Changes Made

### Visual Design
The cover slide now features:
- **Dark gradient background** (#1A1F2E) matching HTML dark theme
- **Semi-transparent overlay box** positioned on the left side (5% from left, vertically centered)
- **Veeam green left border** (#00D15F) on the overlay box
- **White text** with appropriate opacity levels for hierarchy

### Content Structure
The cover slide displays the following information (matching HTML):
1. **Company Name** (extracted from license table)
   - Large, bold text (28pt)
   - Full white opacity
   
2. **Product Name** ("Veeam Backup & Recovery")
   - Medium text (20pt)
   - Full white opacity
   
3. **Report Type** ("Health Check Report")
   - Smaller text (14pt)
   - 80% white opacity
   
4. **Report Date & Duration** (e.g., "December 09 2025 | 7 day report")
   - Smallest text (12pt)
   - 80% white opacity

### Implementation Details

#### New Methods Added:
- `ExtractCompanyName(string htmlContent)` - Parses company name from the license table
- `ExtractReportDate(string htmlContent)` - Extracts report date and duration from HTML
- `CreateOverlayBox(long x, long y, long width, long height)` - Creates the semi-transparent box with Veeam green border
- `CreateCoverTextShape(...)` - Creates text shapes with proper styling

#### Modified Methods:
- `ConvertHtmlToPptx()` - Now extracts metadata before creating slides
- `CreateTitleSlide()` - Complete redesign to match HTML cover layout

### Color Scheme
- Background: #1A1F2E (dark blue-gray)
- Overlay: rgba(0, 0, 0, 0.85) (85% opacity black)
- Border: #00D15F (Veeam Green)
- Text: #FFFFFF with varying opacity (100% and 80%)

### Layout Positioning
- Overlay box: 35% width, positioned 5% from left edge, vertically centered
- Text padding: Consistent spacing within the overlay box
- Border: 4pt solid Veeam Green on left edge

## HTML Reference
The design matches the HTML `.text-overlay` section structure:
```html
<div class="text-overlay">
    <h1>Demo ACME</h1>
    <h3>Veeam Backup & Recovery</h3>
    <p>Health Check Report</p>
    <p>December 09 2025|7 day report</p>
</div>
```

## Testing
Build completed successfully with no errors. The PowerPoint export will now generate cover slides that visually match the HTML report cover design.

## Files Modified
- `vHC/HC_Reporting/Functions/Reporting/Html/Exportables/HtmlToPptxConverter.cs`

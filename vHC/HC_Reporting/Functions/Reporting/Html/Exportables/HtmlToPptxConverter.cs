using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace VeeamHealthCheck.Functions.Reporting.Html.Exportables
{
    public class HtmlToPptxConverter
    {
        // Layout constants
        private const int MaxRowsPerSlide = 12;
        private const int MaxColumnsForNormalLayout = 6;
        private const long SlideWidth = 12192000; // 16:9 widescreen
        private const long SlideHeight = 6858000;
        private const long Margin = 457200; // 0.5 inch
        private const long ContentWidth = 11277600; // SlideWidth - 2*Margin

        // Font sizes (in hundredths of a point)
        private const int TitleFontSize = 4400;
        private const int SubtitleFontSize = 2400;
        private const int SectionTitleFontSize = 3600;
        private const int HeaderFontSize = 1400;
        private const int BodyFontSize = 1100;
        private const int SmallFontSize = 900;
        private const int MetricValueFontSize = 3200;
        private const int MetricLabelFontSize = 1200;

        // Veeam brand colors
        private const string VeeamGreen = "00B336";
        private const string VeeamGreenDark = "009929";
        private const string VeeamGreenLight = "00D15F";
        private const string DarkBackground = "0D1117";
        private const string DarkCard = "161B22";
        private const string DarkCardHover = "21262D";
        private const string LightGray = "F6F8FA";
        private const string MediumGray = "8B949E";
        private const string TextWhite = "FFFFFF";
        private const string TextLight = "E6EDF3";
        private const string AccentBlue = "58A6FF";
        private const string AccentPurple = "A371F7";
        private const string AccentOrange = "D29922";
        private const string StatusGreen = "3FB950";
        private const string StatusYellow = "D29922";
        private const string StatusRed = "F85149";

        private int _slideIndex = 0;
        private uint _shapeId = 1;

        public void ConvertHtmlToPptx(string htmlContent, string outputPath)
        {
            using (PresentationDocument presentationDocument = PresentationDocument.Create(outputPath, PresentationDocumentType.Presentation))
            {
                PresentationPart presentationPart = presentationDocument.AddPresentationPart();
                presentationPart.Presentation = new Presentation();

                CreatePresentationParts(presentationDocument);

                // Extract metadata from HTML
                string companyName = ExtractCompanyName(htmlContent);
                string reportDate = ExtractReportDate(htmlContent);
                var summaryMetrics = ExtractSummaryMetrics(htmlContent);

                // Create title slide
                CreateModernTitleSlide(presentationPart, companyName, reportDate);

                // Create executive summary slide if we have metrics
                if (summaryMetrics.Count > 0)
                {
                    CreateExecutiveSummarySlide(presentationPart, summaryMetrics);
                }

                // Parse and create content slides
                List<TableData> tables = ParseHtmlTables(htmlContent);

                int tableIndex = 0;
                foreach (var table in tables)
                {
                    // Create section divider for major sections (every 4 tables)
                    if (tableIndex % 4 == 0)
                    {
                        CreateSectionDividerSlide(presentationPart, table.Title);
                    }

                    // Choose presentation style based on data characteristics
                    if (table.Rows.Count <= 3 && table.ColumnCount > MaxColumnsForNormalLayout)
                    {
                        // Wide table with few rows - use property grid layout (key-value pairs)
                        CreatePropertyGridSlide(presentationPart, table);
                    }
                    else if (table.Rows.Count <= 4 && table.ColumnCount <= 4)
                    {
                        // Small data - use card layout
                        CreateMetricCardsSlide(presentationPart, table);
                    }
                    else if (table.ColumnCount <= MaxColumnsForNormalLayout)
                    {
                        // Normal table - use modern table style
                        CreateModernTableSlides(presentationPart, table);
                    }
                    else
                    {
                        // Wide table with many rows - use compact table layout
                        CreateCompactWideTableSlides(presentationPart, table);
                    }

                    tableIndex++;
                }

                // Create closing slide
                CreateClosingSlide(presentationPart, companyName);

                SavePresentation(presentationPart);
            }
        }

        private SlideLayoutPart _slideLayoutPart;

        private void CreatePresentationParts(PresentationDocument presentationDocument)
        {
            PresentationPart presentationPart = presentationDocument.PresentationPart;

            presentationPart.Presentation.SlideIdList = new SlideIdList();
            presentationPart.Presentation.SlideSize = new SlideSize() { Cx = 12192000, Cy = 6858000, Type = SlideSizeValues.Screen16x9 };
            presentationPart.Presentation.NotesSize = new NotesSize() { Cx = 6858000, Cy = 9144000 };

            CreateThemePart(presentationPart);
            CreateSlideMasterPart(presentationPart);
        }

        private void CreateThemePart(PresentationPart presentationPart)
        {
            ThemePart themePart = presentationPart.AddNewPart<ThemePart>("rId5");

            A.Theme theme = new A.Theme() { Name = "VeeamTheme" };

            A.ThemeElements themeElements = new A.ThemeElements();

            // Color scheme
            A.ColorScheme colorScheme = new A.ColorScheme() { Name = "Veeam" };
            colorScheme.AppendChild(new A.Dark1Color(new A.RgbColorModelHex() { Val = DarkBackground }));
            colorScheme.AppendChild(new A.Light1Color(new A.RgbColorModelHex() { Val = TextWhite }));
            colorScheme.AppendChild(new A.Dark2Color(new A.RgbColorModelHex() { Val = DarkCard }));
            colorScheme.AppendChild(new A.Light2Color(new A.RgbColorModelHex() { Val = LightGray }));
            colorScheme.AppendChild(new A.Accent1Color(new A.RgbColorModelHex() { Val = VeeamGreen }));
            colorScheme.AppendChild(new A.Accent2Color(new A.RgbColorModelHex() { Val = AccentBlue }));
            colorScheme.AppendChild(new A.Accent3Color(new A.RgbColorModelHex() { Val = AccentPurple }));
            colorScheme.AppendChild(new A.Accent4Color(new A.RgbColorModelHex() { Val = AccentOrange }));
            colorScheme.AppendChild(new A.Accent5Color(new A.RgbColorModelHex() { Val = StatusGreen }));
            colorScheme.AppendChild(new A.Accent6Color(new A.RgbColorModelHex() { Val = StatusRed }));
            colorScheme.AppendChild(new A.Hyperlink(new A.RgbColorModelHex() { Val = AccentBlue }));
            colorScheme.AppendChild(new A.FollowedHyperlinkColor(new A.RgbColorModelHex() { Val = AccentPurple }));
            themeElements.AppendChild(colorScheme);

            // Font scheme
            A.FontScheme fontScheme = new A.FontScheme() { Name = "Veeam" };
            A.MajorFont majorFont = new A.MajorFont();
            majorFont.AppendChild(new A.LatinFont() { Typeface = "Segoe UI", Panose = "020B0502040204020203" });
            majorFont.AppendChild(new A.EastAsianFont() { Typeface = "" });
            majorFont.AppendChild(new A.ComplexScriptFont() { Typeface = "" });
            fontScheme.AppendChild(majorFont);

            A.MinorFont minorFont = new A.MinorFont();
            minorFont.AppendChild(new A.LatinFont() { Typeface = "Segoe UI", Panose = "020B0502040204020203" });
            minorFont.AppendChild(new A.EastAsianFont() { Typeface = "" });
            minorFont.AppendChild(new A.ComplexScriptFont() { Typeface = "" });
            fontScheme.AppendChild(minorFont);
            themeElements.AppendChild(fontScheme);

            // Format scheme
            A.FormatScheme formatScheme = new A.FormatScheme() { Name = "Veeam" };

            A.FillStyleList fillStyleList = new A.FillStyleList();
            fillStyleList.AppendChild(new A.SolidFill(new A.SchemeColor() { Val = A.SchemeColorValues.PhColor }));
            fillStyleList.AppendChild(new A.SolidFill(new A.SchemeColor() { Val = A.SchemeColorValues.PhColor }));
            fillStyleList.AppendChild(new A.SolidFill(new A.SchemeColor() { Val = A.SchemeColorValues.PhColor }));
            formatScheme.AppendChild(fillStyleList);

            A.LineStyleList lineStyleList = new A.LineStyleList();
            for (int i = 0; i < 3; i++)
            {
                A.Outline outline = new A.Outline() { Width = 9525, CapType = A.LineCapValues.Flat, CompoundLineType = A.CompoundLineValues.Single, Alignment = A.PenAlignmentValues.Center };
                outline.AppendChild(new A.SolidFill(new A.SchemeColor() { Val = A.SchemeColorValues.PhColor }));
                outline.AppendChild(new A.PresetDash() { Val = A.PresetLineDashValues.Solid });
                lineStyleList.AppendChild(outline);
            }
            formatScheme.AppendChild(lineStyleList);

            A.EffectStyleList effectStyleList = new A.EffectStyleList();
            for (int i = 0; i < 3; i++)
            {
                effectStyleList.AppendChild(new A.EffectStyle(new A.EffectList()));
            }
            formatScheme.AppendChild(effectStyleList);

            A.BackgroundFillStyleList bgFillStyleList = new A.BackgroundFillStyleList();
            bgFillStyleList.AppendChild(new A.SolidFill(new A.SchemeColor() { Val = A.SchemeColorValues.PhColor }));
            bgFillStyleList.AppendChild(new A.SolidFill(new A.SchemeColor() { Val = A.SchemeColorValues.PhColor }));
            bgFillStyleList.AppendChild(new A.SolidFill(new A.SchemeColor() { Val = A.SchemeColorValues.PhColor }));
            formatScheme.AppendChild(bgFillStyleList);

            themeElements.AppendChild(formatScheme);
            theme.AppendChild(themeElements);
            theme.AppendChild(new A.ObjectDefaults());
            theme.AppendChild(new A.ExtraColorSchemeList());

            themePart.Theme = theme;
        }

        private void CreateSlideMasterPart(PresentationPart presentationPart)
        {
            SlideMasterPart slideMasterPart = presentationPart.AddNewPart<SlideMasterPart>("rId1");

            // Link theme to slide master
            ThemePart themePart = presentationPart.GetPartById("rId5") as ThemePart;
            if (themePart != null)
            {
                slideMasterPart.AddPart(themePart, "rId1");
            }

            SlideMaster slideMaster = new SlideMaster(
                new P.CommonSlideData(
                    new P.ShapeTree(
                        new P.NonVisualGroupShapeProperties(
                            new P.NonVisualDrawingProperties() { Id = 1U, Name = "" },
                            new P.NonVisualGroupShapeDrawingProperties(),
                            new P.ApplicationNonVisualDrawingProperties()),
                        new P.GroupShapeProperties(
                            new A.TransformGroup(
                                new A.Offset() { X = 0L, Y = 0L },
                                new A.Extents() { Cx = 0L, Cy = 0L },
                                new A.ChildOffset() { X = 0L, Y = 0L },
                                new A.ChildExtents() { Cx = 0L, Cy = 0L })))),
                new P.ColorMap()
                {
                    Background1 = A.ColorSchemeIndexValues.Dark1,
                    Text1 = A.ColorSchemeIndexValues.Light1,
                    Background2 = A.ColorSchemeIndexValues.Dark2,
                    Text2 = A.ColorSchemeIndexValues.Light2,
                    Accent1 = A.ColorSchemeIndexValues.Accent1,
                    Accent2 = A.ColorSchemeIndexValues.Accent2,
                    Accent3 = A.ColorSchemeIndexValues.Accent3,
                    Accent4 = A.ColorSchemeIndexValues.Accent4,
                    Accent5 = A.ColorSchemeIndexValues.Accent5,
                    Accent6 = A.ColorSchemeIndexValues.Accent6,
                    Hyperlink = A.ColorSchemeIndexValues.Hyperlink,
                    FollowedHyperlink = A.ColorSchemeIndexValues.FollowedHyperlink
                });

            // Create slide layout
            _slideLayoutPart = slideMasterPart.AddNewPart<SlideLayoutPart>("rId2");
            SlideLayout slideLayout = new SlideLayout(
                new P.CommonSlideData(
                    new P.ShapeTree(
                        new P.NonVisualGroupShapeProperties(
                            new P.NonVisualDrawingProperties() { Id = 1U, Name = "" },
                            new P.NonVisualGroupShapeDrawingProperties(),
                            new P.ApplicationNonVisualDrawingProperties()),
                        new P.GroupShapeProperties(
                            new A.TransformGroup(
                                new A.Offset() { X = 0L, Y = 0L },
                                new A.Extents() { Cx = 0L, Cy = 0L },
                                new A.ChildOffset() { X = 0L, Y = 0L },
                                new A.ChildExtents() { Cx = 0L, Cy = 0L })))),
                new P.ColorMapOverride(new A.MasterColorMapping()));
            slideLayout.Type = SlideLayoutValues.Blank;
            _slideLayoutPart.SlideLayout = slideLayout;

            // Add SlideLayoutIdList to SlideMaster
            slideMaster.AppendChild(new P.SlideLayoutIdList(
                new P.SlideLayoutId() { Id = 2147483649U, RelationshipId = "rId2" }));

            slideMasterPart.SlideMaster = slideMaster;

            // Add SlideMasterIdList to Presentation
            presentationPart.Presentation.SlideMasterIdList = new SlideMasterIdList(
                new SlideMasterId() { Id = 2147483648U, RelationshipId = presentationPart.GetIdOfPart(slideMasterPart) });
        }

        private void CreateModernTitleSlide(PresentationPart presentationPart, string companyName, string reportDate)
        {
            _shapeId = 1;
            SlidePart slidePart = presentationPart.AddNewPart<SlidePart>();

            Slide slide = CreateBaseSlide();
            slidePart.Slide = slide;
            ShapeTree shapeTree = slide.CommonSlideData.ShapeTree;

            // Add gradient background
            AddGradientBackground(shapeTree, DarkBackground, DarkCard);

            // Add decorative accent line at top
            AddAccentLine(shapeTree, 0, 0, SlideWidth, 80000, VeeamGreen);

            // Add subtle pattern overlay (diagonal lines effect via shapes)
            AddPatternOverlay(shapeTree);

            // Left side content area with glass effect
            long contentBoxX = Margin;
            long contentBoxY = SlideHeight / 2 - 1500000;
            long contentBoxW = 5500000;
            long contentBoxH = 3000000;

            AddGlassCard(shapeTree, contentBoxX, contentBoxY, contentBoxW, contentBoxH);

            // Company name - large and bold
            long textY = contentBoxY + 300000;
            AddTextShape(shapeTree, companyName, contentBoxX + 200000, textY, contentBoxW - 400000, 800000,
                TitleFontSize, true, TextWhite, A.TextAlignmentTypeValues.Left);

            // "Veeam Backup & Replication" subtitle
            textY += 900000;
            AddTextShape(shapeTree, "Veeam Backup & Replication", contentBoxX + 200000, textY, contentBoxW - 400000, 500000,
                SubtitleFontSize, false, VeeamGreenLight, A.TextAlignmentTypeValues.Left);

            // "Health Check Report"
            textY += 600000;
            AddTextShape(shapeTree, "Health Check Report", contentBoxX + 200000, textY, contentBoxW - 400000, 400000,
                SubtitleFontSize - 200, false, TextLight, A.TextAlignmentTypeValues.Left);

            // Date
            textY += 500000;
            AddTextShape(shapeTree, reportDate, contentBoxX + 200000, textY, contentBoxW - 400000, 300000,
                MetricLabelFontSize, false, MediumGray, A.TextAlignmentTypeValues.Left);

            // Right side - decorative Veeam logo area (abstract geometric shapes)
            AddDecorativeGeometry(shapeTree, 7000000, 1500000);

            // Bottom branding bar
            AddBrandingBar(shapeTree);

            AddSlideToPresentation(presentationPart, slidePart);
        }

        private void CreateExecutiveSummarySlide(PresentationPart presentationPart, Dictionary<string, string> metrics)
        {
            _shapeId = 1;
            SlidePart slidePart = presentationPart.AddNewPart<SlidePart>();

            Slide slide = CreateBaseSlide();
            slidePart.Slide = slide;
            ShapeTree shapeTree = slide.CommonSlideData.ShapeTree;

            // Add gradient background
            AddGradientBackground(shapeTree, DarkBackground, DarkCard);
            AddAccentLine(shapeTree, 0, 0, SlideWidth, 60000, VeeamGreen);

            // Title
            AddTextShape(shapeTree, "Executive Summary", Margin, 400000, ContentWidth, 700000,
                SectionTitleFontSize, true, TextWhite, A.TextAlignmentTypeValues.Left);

            // Subtitle
            AddTextShape(shapeTree, "Key Infrastructure Metrics", Margin, 1000000, ContentWidth, 400000,
                MetricLabelFontSize, false, MediumGray, A.TextAlignmentTypeValues.Left);

            // Create metric cards in a grid
            var metricsList = metrics.Take(8).ToList(); // Max 8 metrics
            int columns = Math.Min(4, metricsList.Count);
            int rows = (int)Math.Ceiling(metricsList.Count / (double)columns);

            long cardWidth = (ContentWidth - (columns - 1) * 200000) / columns;
            long cardHeight = 1400000;
            long startY = 1600000;

            for (int i = 0; i < metricsList.Count; i++)
            {
                int col = i % columns;
                int row = i / columns;

                long x = Margin + col * (cardWidth + 200000);
                long y = startY + row * (cardHeight + 200000);

                string[] colors = { VeeamGreen, AccentBlue, AccentPurple, AccentOrange, StatusGreen, AccentBlue, VeeamGreen, AccentPurple };
                string accentColor = colors[i % colors.Length];

                AddMetricCard(shapeTree, x, y, cardWidth, cardHeight, metricsList[i].Key, metricsList[i].Value, accentColor);
            }

            AddSlideToPresentation(presentationPart, slidePart);
        }

        private void CreateSectionDividerSlide(PresentationPart presentationPart, string sectionTitle)
        {
            _shapeId = 1;
            SlidePart slidePart = presentationPart.AddNewPart<SlidePart>();

            Slide slide = CreateBaseSlide();
            slidePart.Slide = slide;
            ShapeTree shapeTree = slide.CommonSlideData.ShapeTree;

            // Gradient background
            AddGradientBackground(shapeTree, DarkCard, DarkBackground);

            // Large accent shape on the left
            AddShape(shapeTree, A.ShapeTypeValues.Rectangle, 0, 0, 300000, SlideHeight, VeeamGreen, null, 0);

            // Section title - centered vertically
            AddTextShape(shapeTree, sectionTitle, 600000, SlideHeight / 2 - 400000, SlideWidth - 1000000, 800000,
                SectionTitleFontSize + 400, true, TextWhite, A.TextAlignmentTypeValues.Left);

            // Decorative line under title
            AddAccentLine(shapeTree, 600000, SlideHeight / 2 + 400000, 4000000, 40000, VeeamGreen);

            AddSlideToPresentation(presentationPart, slidePart);
        }

        private void CreateMetricCardsSlide(PresentationPart presentationPart, TableData tableData)
        {
            if (tableData.Rows.Count == 0) return;

            _shapeId = 1;
            SlidePart slidePart = presentationPart.AddNewPart<SlidePart>();

            Slide slide = CreateBaseSlide();
            slidePart.Slide = slide;
            ShapeTree shapeTree = slide.CommonSlideData.ShapeTree;

            AddGradientBackground(shapeTree, DarkBackground, DarkCard);
            AddAccentLine(shapeTree, 0, 0, SlideWidth, 60000, VeeamGreen);

            // Title
            AddTextShape(shapeTree, tableData.Title, Margin, 400000, ContentWidth, 600000,
                SectionTitleFontSize - 400, true, TextWhite, A.TextAlignmentTypeValues.Left);

            // Create cards for each row
            int cardCount = Math.Min(tableData.Rows.Count, 6);
            int columns = Math.Min(3, cardCount);
            int rows = (int)Math.Ceiling(cardCount / (double)columns);

            long cardWidth = (ContentWidth - (columns - 1) * 200000) / columns;
            long cardHeight = rows == 1 ? 2000000 : 1600000;
            long startY = 1300000;

            string[] colors = { VeeamGreen, AccentBlue, AccentPurple, AccentOrange, StatusGreen, AccentBlue };

            for (int i = 0; i < cardCount; i++)
            {
                int col = i % columns;
                int row = i / columns;

                long x = Margin + col * (cardWidth + 200000);
                long y = startY + row * (cardHeight + 200000);

                var dataRow = tableData.Rows[i];
                string label = dataRow.Count > 0 ? dataRow[0] : "";
                string value = dataRow.Count > 1 ? dataRow[1] : "";

                // If we have headers, use first header as category and second as value label
                if (tableData.Headers.Count >= 2)
                {
                    AddDetailedCard(shapeTree, x, y, cardWidth, cardHeight, label, value,
                        tableData.Headers.Count > 2 && dataRow.Count > 2 ? dataRow[2] : "", colors[i % colors.Length]);
                }
                else
                {
                    AddMetricCard(shapeTree, x, y, cardWidth, cardHeight, label, value, colors[i % colors.Length]);
                }
            }

            AddSlideToPresentation(presentationPart, slidePart);
        }

        /// <summary>
        /// Creates a property grid layout for wide tables with few rows.
        /// Displays data as key-value pairs in a multi-column grid - much more visually appealing
        /// than splitting a 15-column table across multiple slides.
        /// </summary>
        private void CreatePropertyGridSlide(PresentationPart presentationPart, TableData tableData)
        {
            if (tableData.Rows.Count == 0 || tableData.Headers.Count == 0) return;

            // Calculate how many property pairs we have per row
            int propsPerRow = tableData.Headers.Count;
            int totalRows = tableData.Rows.Count;
            int totalProps = propsPerRow * totalRows;

            // Determine grid layout - aim for 3-4 columns of key-value pairs
            int gridColumns = totalProps <= 8 ? 2 : (totalProps <= 15 ? 3 : 4);
            int maxPropsPerSlide = gridColumns * 10; // 10 rows of pairs per slide max

            int slideCount = (int)Math.Ceiling((double)totalProps / maxPropsPerSlide);

            // Flatten all data into key-value pairs
            List<KeyValuePair<string, string>> allPairs = new List<KeyValuePair<string, string>>();
            for (int rowIdx = 0; rowIdx < tableData.Rows.Count; rowIdx++)
            {
                var row = tableData.Rows[rowIdx];
                string rowPrefix = tableData.Rows.Count > 1 ? $"[{rowIdx + 1}] " : "";

                for (int colIdx = 0; colIdx < tableData.Headers.Count && colIdx < row.Count; colIdx++)
                {
                    string key = rowPrefix + tableData.Headers[colIdx];
                    string value = row[colIdx];
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        allPairs.Add(new KeyValuePair<string, string>(key, value));
                    }
                }
            }

            // Create slides
            for (int slideIdx = 0; slideIdx < slideCount; slideIdx++)
            {
                _shapeId = 1;
                SlidePart slidePart = presentationPart.AddNewPart<SlidePart>();

                Slide slide = CreateBaseSlide();
                slidePart.Slide = slide;
                ShapeTree shapeTree = slide.CommonSlideData.ShapeTree;

                AddGradientBackground(shapeTree, DarkBackground, DarkCard);
                AddAccentLine(shapeTree, 0, 0, SlideWidth, 60000, VeeamGreen);

                string slideTitle = tableData.Title;
                if (slideCount > 1)
                {
                    slideTitle += $" ({slideIdx + 1}/{slideCount})";
                }

                AddTextShape(shapeTree, slideTitle, Margin, 350000, ContentWidth, 500000,
                    SectionTitleFontSize - 400, true, TextWhite, A.TextAlignmentTypeValues.Left);

                // Get pairs for this slide
                var slidePairs = allPairs.Skip(slideIdx * maxPropsPerSlide).Take(maxPropsPerSlide).ToList();

                // Calculate layout
                int pairsPerColumn = (int)Math.Ceiling((double)slidePairs.Count / gridColumns);
                long columnWidth = (ContentWidth - (gridColumns - 1) * 150000) / gridColumns;
                long startY = 1000000;
                long rowHeight = 420000;
                long keyWidth = (long)(columnWidth * 0.45);
                long valueWidth = (long)(columnWidth * 0.52);

                for (int i = 0; i < slidePairs.Count; i++)
                {
                    int col = i / pairsPerColumn;
                    int row = i % pairsPerColumn;

                    long x = Margin + col * (columnWidth + 150000);
                    long y = startY + row * rowHeight;

                    var pair = slidePairs[i];

                    // Add subtle background for the row
                    if (row % 2 == 0)
                    {
                        AddRoundedRectangle(shapeTree, x, y, columnWidth, rowHeight - 20000, DarkCardHover, 40);
                    }

                    // Key (label) - muted color
                    AddTextShape(shapeTree, TruncateText(pair.Key, 22), x + 50000, y + 50000, keyWidth, rowHeight - 100000,
                        SmallFontSize + 100, false, MediumGray, A.TextAlignmentTypeValues.Left);

                    // Value - bright color
                    AddTextShape(shapeTree, TruncateText(pair.Value, 25), x + keyWidth + 80000, y + 50000, valueWidth, rowHeight - 100000,
                        SmallFontSize + 100, true, TextLight, A.TextAlignmentTypeValues.Left);
                }

                AddSlideToPresentation(presentationPart, slidePart);
            }
        }

        /// <summary>
        /// Creates a more compact table layout for wide tables with many rows.
        /// Uses smaller fonts and tighter spacing to fit more data per slide.
        /// </summary>
        private void CreateCompactWideTableSlides(PresentationPart presentationPart, TableData tableData)
        {
            if (tableData.Rows.Count == 0) return;

            // For very wide tables, we'll use a compact horizontal scroll approach
            // Show all columns but use smaller text
            const int maxRowsPerSlide = 14;
            int totalRows = tableData.Rows.Count;
            int slideCount = (int)Math.Ceiling((double)totalRows / maxRowsPerSlide);

            for (int slideIndex = 0; slideIndex < slideCount; slideIndex++)
            {
                int startRow = slideIndex * maxRowsPerSlide;
                int endRow = Math.Min(startRow + maxRowsPerSlide, totalRows);

                _shapeId = 1;
                SlidePart slidePart = presentationPart.AddNewPart<SlidePart>();

                Slide slide = CreateBaseSlide();
                slidePart.Slide = slide;
                ShapeTree shapeTree = slide.CommonSlideData.ShapeTree;

                AddGradientBackground(shapeTree, DarkBackground, DarkCard);
                AddAccentLine(shapeTree, 0, 0, SlideWidth, 60000, VeeamGreen);

                string slideTitle = tableData.Title;
                if (slideCount > 1)
                {
                    slideTitle += $" ({slideIndex + 1}/{slideCount})";
                }

                AddTextShape(shapeTree, slideTitle, Margin, 300000, ContentWidth, 450000,
                    SectionTitleFontSize - 600, true, TextWhite, A.TextAlignmentTypeValues.Left);

                var rowsForSlide = tableData.Rows.Skip(startRow).Take(endRow - startRow).ToList();

                // Create compact styled table with all columns
                long tableX = Margin / 2;
                long tableY = 900000;
                long tableWidth = SlideWidth - Margin;
                long tableHeight = SlideHeight - tableY - 200000;

                A.Table table = CreateCompactStyledTable(tableData.Headers, rowsForSlide, tableData.ColumnCount);
                P.GraphicFrame graphicFrame = CreateGraphicFrame(table, tableX, tableY, tableWidth, tableHeight);
                shapeTree.AppendChild(graphicFrame);

                AddSlideToPresentation(presentationPart, slidePart);
            }
        }

        /// <summary>
        /// Creates a table with compact styling - smaller fonts and tighter spacing for wide tables.
        /// </summary>
        private A.Table CreateCompactStyledTable(List<string> headers, List<List<string>> rows, int columnCount)
        {
            A.Table table = new A.Table();

            A.TableProperties tableProperties = new A.TableProperties() { FirstRow = true, BandRow = true };
            table.AppendChild(tableProperties);

            A.TableGrid tableGrid = new A.TableGrid();
            long totalWidth = SlideWidth - Margin;
            long columnWidth = totalWidth / columnCount;
            for (int i = 0; i < columnCount; i++)
            {
                tableGrid.AppendChild(new A.GridColumn() { Width = columnWidth });
            }
            table.AppendChild(tableGrid);

            // Compact font sizes
            int headerSize = columnCount > 10 ? 800 : (columnCount > 7 ? 900 : 1000);
            int bodySize = columnCount > 10 ? 700 : (columnCount > 7 ? 800 : 900);

            if (headers != null && headers.Count > 0)
            {
                List<string> paddedHeaders = PadRowToColumnCount(headers, columnCount);
                A.TableRow headerRow = CreateCompactTableRow(paddedHeaders, true, headerSize, bodySize);
                table.AppendChild(headerRow);
            }

            int rowIndex = 0;
            foreach (var row in rows)
            {
                List<string> paddedRow = PadRowToColumnCount(row, columnCount);
                A.TableRow tableRow = CreateCompactTableRow(paddedRow, false, headerSize, bodySize, rowIndex % 2 == 1);
                table.AppendChild(tableRow);
                rowIndex++;
            }

            return table;
        }

        /// <summary>
        /// Creates a compact table row with smaller padding and fonts.
        /// </summary>
        private A.TableRow CreateCompactTableRow(List<string> cells, bool isHeader, int headerSize, int bodySize, bool isAlternate = false)
        {
            A.TableRow tableRow = new A.TableRow() { Height = isHeader ? 320000L : 280000L };

            foreach (string cellText in cells)
            {
                A.TableCell tableCell = new A.TableCell();

                A.TextBody textBody = new A.TextBody();
                A.BodyProperties bodyProperties = new A.BodyProperties()
                {
                    Wrap = A.TextWrappingValues.Square,
                    LeftInset = 36000,
                    RightInset = 36000,
                    TopInset = 18000,
                    BottomInset = 18000,
                    Anchor = A.TextAnchoringTypeValues.Center
                };
                textBody.AppendChild(bodyProperties);
                textBody.AppendChild(new A.ListStyle());

                A.Paragraph paragraph = new A.Paragraph();
                A.ParagraphProperties paragraphProperties = new A.ParagraphProperties()
                {
                    Alignment = A.TextAlignmentTypeValues.Center
                };
                paragraph.AppendChild(paragraphProperties);

                string cleanedText = CleanHtmlText(cellText);
                if (string.IsNullOrWhiteSpace(cleanedText))
                {
                    cleanedText = "-";
                }
                // More aggressive truncation for compact mode
                cleanedText = TruncateText(cleanedText, 18);

                A.Run run = new A.Run();
                A.RunProperties runProperties = new A.RunProperties()
                {
                    Language = "en-US",
                    FontSize = isHeader ? headerSize : bodySize,
                    Bold = isHeader,
                    Dirty = false
                };

                A.LatinFont latinFont = new A.LatinFont() { Typeface = "Segoe UI", PitchFamily = 34, CharacterSet = 0 };
                runProperties.AppendChild(latinFont);

                string textColor = isHeader ? TextWhite : TextLight;
                A.SolidFill textFill = new A.SolidFill(new A.RgbColorModelHex() { Val = textColor });
                runProperties.AppendChild(textFill);

                A.Text text = new A.Text() { Text = cleanedText };
                run.AppendChild(runProperties);
                run.AppendChild(text);
                paragraph.AppendChild(run);
                textBody.AppendChild(paragraph);

                A.TableCellProperties tableCellProperties = new A.TableCellProperties()
                {
                    Anchor = A.TextAnchoringTypeValues.Center
                };

                // Cell background
                string bgColor = isHeader ? VeeamGreen : (isAlternate ? DarkCardHover : DarkCard);
                tableCellProperties.Append(new A.SolidFill(new A.RgbColorModelHex() { Val = bgColor }));

                // Minimal borders
                string borderColor = DarkCardHover;
                int borderWidth = 6350;

                A.LeftBorderLineProperties leftBorder = new A.LeftBorderLineProperties() { Width = borderWidth };
                leftBorder.AppendChild(new A.SolidFill(new A.RgbColorModelHex() { Val = borderColor }));

                A.RightBorderLineProperties rightBorder = new A.RightBorderLineProperties() { Width = borderWidth };
                rightBorder.AppendChild(new A.SolidFill(new A.RgbColorModelHex() { Val = borderColor }));

                A.TopBorderLineProperties topBorder = new A.TopBorderLineProperties() { Width = borderWidth };
                topBorder.AppendChild(new A.SolidFill(new A.RgbColorModelHex() { Val = borderColor }));

                A.BottomBorderLineProperties bottomBorder = new A.BottomBorderLineProperties() { Width = borderWidth };
                bottomBorder.AppendChild(new A.SolidFill(new A.RgbColorModelHex() { Val = borderColor }));

                tableCellProperties.AppendChild(leftBorder);
                tableCellProperties.AppendChild(rightBorder);
                tableCellProperties.AppendChild(topBorder);
                tableCellProperties.AppendChild(bottomBorder);

                tableCell.AppendChild(textBody);
                tableCell.AppendChild(tableCellProperties);
                tableRow.AppendChild(tableCell);
            }

            return tableRow;
        }

        private void CreateModernTableSlides(PresentationPart presentationPart, TableData tableData)
        {
            if (tableData.Rows.Count == 0) return;

            int totalRows = tableData.Rows.Count;
            int slideCount = (int)Math.Ceiling((double)totalRows / MaxRowsPerSlide);

            for (int slideIndex = 0; slideIndex < slideCount; slideIndex++)
            {
                int startRow = slideIndex * MaxRowsPerSlide;
                int endRow = Math.Min(startRow + MaxRowsPerSlide, totalRows);

                _shapeId = 1;
                SlidePart slidePart = presentationPart.AddNewPart<SlidePart>();

                Slide slide = CreateBaseSlide();
                slidePart.Slide = slide;
                ShapeTree shapeTree = slide.CommonSlideData.ShapeTree;

                AddGradientBackground(shapeTree, DarkBackground, DarkCard);
                AddAccentLine(shapeTree, 0, 0, SlideWidth, 60000, VeeamGreen);

                string slideTitle = tableData.Title;
                if (slideCount > 1)
                {
                    slideTitle += $" ({slideIndex + 1}/{slideCount})";
                }

                AddTextShape(shapeTree, slideTitle, Margin, 350000, ContentWidth, 500000,
                    SectionTitleFontSize - 600, true, TextWhite, A.TextAlignmentTypeValues.Left);

                var rowsForSlide = tableData.Rows.Skip(startRow).Take(endRow - startRow).ToList();

                // Create modern styled table
                long tableX = Margin;
                long tableY = 1100000;
                long tableWidth = ContentWidth;
                long tableHeight = SlideHeight - tableY - Margin;

                A.Table table = CreateStyledTable(tableData.Headers, rowsForSlide, tableData.ColumnCount);
                P.GraphicFrame graphicFrame = CreateGraphicFrame(table, tableX, tableY, tableWidth, tableHeight);
                shapeTree.AppendChild(graphicFrame);

                AddSlideToPresentation(presentationPart, slidePart);
            }
        }

        private void CreateClosingSlide(PresentationPart presentationPart, string companyName)
        {
            _shapeId = 1;
            SlidePart slidePart = presentationPart.AddNewPart<SlidePart>();

            Slide slide = CreateBaseSlide();
            slidePart.Slide = slide;
            ShapeTree shapeTree = slide.CommonSlideData.ShapeTree;

            AddGradientBackground(shapeTree, DarkCard, DarkBackground);

            // Center content
            AddTextShape(shapeTree, "Thank You", Margin, SlideHeight / 2 - 800000, ContentWidth, 800000,
                TitleFontSize + 400, true, TextWhite, A.TextAlignmentTypeValues.Center);

            AddTextShape(shapeTree, companyName, Margin, SlideHeight / 2 + 100000, ContentWidth, 500000,
                SubtitleFontSize, false, VeeamGreenLight, A.TextAlignmentTypeValues.Center);

            AddTextShape(shapeTree, "Veeam Health Check Report", Margin, SlideHeight / 2 + 600000, ContentWidth, 400000,
                MetricLabelFontSize, false, MediumGray, A.TextAlignmentTypeValues.Center);

            // Bottom accent
            AddAccentLine(shapeTree, SlideWidth / 2 - 1500000, SlideHeight - 500000, 3000000, 40000, VeeamGreen);

            AddSlideToPresentation(presentationPart, slidePart);
        }

        private Slide CreateBaseSlide()
        {
            return new Slide(
                new CommonSlideData(
                    new ShapeTree(
                        new P.NonVisualGroupShapeProperties(
                            new P.NonVisualDrawingProperties() { Id = 1, Name = string.Empty },
                            new P.NonVisualGroupShapeDrawingProperties(),
                            new P.ApplicationNonVisualDrawingProperties()),
                        new P.GroupShapeProperties(
                            new A.TransformGroup(
                                new A.Offset() { X = 0, Y = 0 },
                                new A.Extents() { Cx = 0, Cy = 0 },
                                new A.ChildOffset() { X = 0, Y = 0 },
                                new A.ChildExtents() { Cx = 0, Cy = 0 })))),
                new P.ColorMapOverride(new A.MasterColorMapping()));
        }

        private void AddGradientBackground(ShapeTree shapeTree, string color1, string color2)
        {
            Shape shape = new Shape();

            P.NonVisualShapeProperties nvsp = new P.NonVisualShapeProperties(
                new P.NonVisualDrawingProperties() { Id = GetNextShapeId(), Name = "Background" },
                new P.NonVisualShapeDrawingProperties(new A.ShapeLocks() { NoGrouping = true }),
                new P.ApplicationNonVisualDrawingProperties());

            P.ShapeProperties sp = new P.ShapeProperties();
            A.Transform2D transform = new A.Transform2D();
            transform.AppendChild(new A.Offset() { X = 0, Y = 0 });
            transform.AppendChild(new A.Extents() { Cx = SlideWidth, Cy = SlideHeight });
            sp.AppendChild(transform);

            A.PresetGeometry presetGeometry = new A.PresetGeometry() { Preset = A.ShapeTypeValues.Rectangle };
            presetGeometry.AppendChild(new A.AdjustValueList());
            sp.AppendChild(presetGeometry);

            A.GradientFill gradientFill = new A.GradientFill() { RotateWithShape = true };
            A.GradientStopList gradientStopList = new A.GradientStopList();

            A.GradientStop stop1 = new A.GradientStop() { Position = 0 };
            stop1.AppendChild(new A.RgbColorModelHex() { Val = color1 });
            gradientStopList.AppendChild(stop1);

            A.GradientStop stop2 = new A.GradientStop() { Position = 100000 };
            stop2.AppendChild(new A.RgbColorModelHex() { Val = color2 });
            gradientStopList.AppendChild(stop2);

            gradientFill.AppendChild(gradientStopList);
            gradientFill.AppendChild(new A.LinearGradientFill() { Angle = 5400000 }); // 45 degrees
            sp.AppendChild(gradientFill);

            sp.AppendChild(new A.Outline(new A.NoFill()));

            P.TextBody textBody = CreateEmptyTextBody();

            shape.AppendChild(nvsp);
            shape.AppendChild(sp);
            shape.AppendChild(textBody);

            shapeTree.AppendChild(shape);
        }

        private void AddAccentLine(ShapeTree shapeTree, long x, long y, long width, long height, string color)
        {
            AddShape(shapeTree, A.ShapeTypeValues.Rectangle, x, y, width, height, color, null, 0);
        }

        private void AddPatternOverlay(ShapeTree shapeTree)
        {
            // Add subtle diagonal lines as decoration
            for (int i = 0; i < 5; i++)
            {
                long x = 8000000 + i * 400000;
                AddShape(shapeTree, A.ShapeTypeValues.Rectangle, x, 0, 20000, SlideHeight, DarkCard, null, 0, 20);
            }
        }

        private void AddGlassCard(ShapeTree shapeTree, long x, long y, long width, long height)
        {
            Shape shape = new Shape();

            P.NonVisualShapeProperties nvsp = new P.NonVisualShapeProperties(
                new P.NonVisualDrawingProperties() { Id = GetNextShapeId(), Name = "GlassCard" },
                new P.NonVisualShapeDrawingProperties(new A.ShapeLocks() { NoGrouping = true }),
                new P.ApplicationNonVisualDrawingProperties());

            P.ShapeProperties sp = new P.ShapeProperties();
            A.Transform2D transform = new A.Transform2D();
            transform.AppendChild(new A.Offset() { X = x, Y = y });
            transform.AppendChild(new A.Extents() { Cx = width, Cy = height });
            sp.AppendChild(transform);

            A.PresetGeometry presetGeometry = new A.PresetGeometry() { Preset = A.ShapeTypeValues.RoundRectangle };
            A.AdjustValueList adjustValueList = new A.AdjustValueList();
            adjustValueList.AppendChild(new A.ShapeGuide() { Name = "adj", Formula = "val 3000" });
            presetGeometry.AppendChild(adjustValueList);
            sp.AppendChild(presetGeometry);

            // Semi-transparent fill
            A.SolidFill solidFill = new A.SolidFill();
            A.RgbColorModelHex rgbColor = new A.RgbColorModelHex() { Val = DarkCard };
            rgbColor.AppendChild(new A.Alpha() { Val = 85000 });
            solidFill.AppendChild(rgbColor);
            sp.AppendChild(solidFill);

            // Border
            A.Outline outline = new A.Outline() { Width = 12700 };
            A.SolidFill outlineFill = new A.SolidFill();
            A.RgbColorModelHex outlineColor = new A.RgbColorModelHex() { Val = VeeamGreen };
            outlineColor.AppendChild(new A.Alpha() { Val = 60000 });
            outlineFill.AppendChild(outlineColor);
            outline.AppendChild(outlineFill);
            sp.AppendChild(outline);

            P.TextBody textBody = CreateEmptyTextBody();

            shape.AppendChild(nvsp);
            shape.AppendChild(sp);
            shape.AppendChild(textBody);

            shapeTree.AppendChild(shape);
        }

        private void AddDecorativeGeometry(ShapeTree shapeTree, long startX, long startY)
        {
            // Create abstract geometric shapes for visual interest
            string[] colors = { VeeamGreen, VeeamGreenDark, VeeamGreenLight };
            int[] alphas = { 40, 25, 15 };

            // Large circle
            AddShapeWithAlpha(shapeTree, A.ShapeTypeValues.Ellipse, startX, startY, 3500000, 3500000, VeeamGreen, 25);

            // Medium circle offset
            AddShapeWithAlpha(shapeTree, A.ShapeTypeValues.Ellipse, startX + 1500000, startY + 1000000, 2000000, 2000000, VeeamGreenLight, 20);

            // Small accent circles
            AddShapeWithAlpha(shapeTree, A.ShapeTypeValues.Ellipse, startX + 500000, startY + 2500000, 800000, 800000, AccentBlue, 30);
        }

        private void AddShapeWithAlpha(ShapeTree shapeTree, A.ShapeTypeValues shapeType, long x, long y, long width, long height, string color, int alphaPercent)
        {
            Shape shape = new Shape();

            P.NonVisualShapeProperties nvsp = new P.NonVisualShapeProperties(
                new P.NonVisualDrawingProperties() { Id = GetNextShapeId(), Name = "DecorShape" },
                new P.NonVisualShapeDrawingProperties(),
                new P.ApplicationNonVisualDrawingProperties());

            P.ShapeProperties sp = new P.ShapeProperties();
            A.Transform2D transform = new A.Transform2D();
            transform.AppendChild(new A.Offset() { X = x, Y = y });
            transform.AppendChild(new A.Extents() { Cx = width, Cy = height });
            sp.AppendChild(transform);

            A.PresetGeometry presetGeometry = new A.PresetGeometry() { Preset = shapeType };
            presetGeometry.AppendChild(new A.AdjustValueList());
            sp.AppendChild(presetGeometry);

            A.SolidFill solidFill = new A.SolidFill();
            A.RgbColorModelHex rgbColor = new A.RgbColorModelHex() { Val = color };
            rgbColor.AppendChild(new A.Alpha() { Val = alphaPercent * 1000 });
            solidFill.AppendChild(rgbColor);
            sp.AppendChild(solidFill);

            sp.AppendChild(new A.Outline(new A.NoFill()));

            P.TextBody textBody = CreateEmptyTextBody();

            shape.AppendChild(nvsp);
            shape.AppendChild(sp);
            shape.AppendChild(textBody);

            shapeTree.AppendChild(shape);
        }

        /// <summary>
        /// Creates an empty TextBody with required paragraph element to avoid PowerPoint repair errors.
        /// </summary>
        private P.TextBody CreateEmptyTextBody()
        {
            P.TextBody textBody = new P.TextBody();
            textBody.AppendChild(new A.BodyProperties());
            textBody.AppendChild(new A.ListStyle());
            // PowerPoint requires at least one paragraph in TextBody
            A.Paragraph paragraph = new A.Paragraph();
            paragraph.AppendChild(new A.EndParagraphRunProperties() { Language = "en-US" });
            textBody.AppendChild(paragraph);
            return textBody;
        }

        private void AddBrandingBar(ShapeTree shapeTree)
        {
            // Bottom bar
            AddShape(shapeTree, A.ShapeTypeValues.Rectangle, 0, SlideHeight - 150000, SlideWidth, 150000, DarkCard, null, 0, 90);

            // Powered by Veeam text
            AddTextShape(shapeTree, "Powered by Veeam Health Check", Margin, SlideHeight - 130000, 3000000, 100000,
                SmallFontSize, false, MediumGray, A.TextAlignmentTypeValues.Left);
        }

        private void AddTextShape(ShapeTree shapeTree, string text, long x, long y, long width, long height,
            int fontSize, bool bold, string color, A.TextAlignmentTypeValues alignment)
        {
            Shape shape = new Shape();

            P.NonVisualShapeProperties nvsp = new P.NonVisualShapeProperties(
                new P.NonVisualDrawingProperties() { Id = GetNextShapeId(), Name = "TextBox" },
                new P.NonVisualShapeDrawingProperties(new A.ShapeLocks() { NoGrouping = true }),
                new P.ApplicationNonVisualDrawingProperties());

            P.ShapeProperties sp = new P.ShapeProperties();
            A.Transform2D transform = new A.Transform2D();
            transform.AppendChild(new A.Offset() { X = x, Y = y });
            transform.AppendChild(new A.Extents() { Cx = width, Cy = height });
            sp.AppendChild(transform);

            A.PresetGeometry presetGeometry = new A.PresetGeometry() { Preset = A.ShapeTypeValues.Rectangle };
            presetGeometry.AppendChild(new A.AdjustValueList());
            sp.AppendChild(presetGeometry);
            sp.AppendChild(new A.NoFill());
            sp.AppendChild(new A.Outline(new A.NoFill()));

            P.TextBody textBody = new P.TextBody();
            A.BodyProperties bodyProperties = new A.BodyProperties()
            {
                Wrap = A.TextWrappingValues.Square,
                Anchor = A.TextAnchoringTypeValues.Center
            };
            textBody.AppendChild(bodyProperties);
            textBody.AppendChild(new A.ListStyle());

            A.Paragraph paragraph = new A.Paragraph();
            A.ParagraphProperties paragraphProperties = new A.ParagraphProperties() { Alignment = alignment };
            paragraph.AppendChild(paragraphProperties);

            A.Run run = new A.Run();
            A.RunProperties runProperties = new A.RunProperties()
            {
                Language = "en-US",
                FontSize = fontSize,
                Bold = bold,
                Dirty = false
            };

            // Add font
            A.LatinFont latinFont = new A.LatinFont() { Typeface = "Segoe UI", PitchFamily = 34, CharacterSet = 0 };
            runProperties.AppendChild(latinFont);

            A.SolidFill textFill = new A.SolidFill(new A.RgbColorModelHex() { Val = color });
            runProperties.AppendChild(textFill);

            A.Text textElement = new A.Text() { Text = text };
            run.AppendChild(runProperties);
            run.AppendChild(textElement);
            paragraph.AppendChild(run);
            textBody.AppendChild(paragraph);

            shape.AppendChild(nvsp);
            shape.AppendChild(sp);
            shape.AppendChild(textBody);

            shapeTree.AppendChild(shape);
        }

        private void AddShape(ShapeTree shapeTree, A.ShapeTypeValues shapeType, long x, long y, long width, long height,
            string fillColor, string outlineColor, int outlineWidth, int alphaPercent = 100)
        {
            Shape shape = new Shape();

            P.NonVisualShapeProperties nvsp = new P.NonVisualShapeProperties(
                new P.NonVisualDrawingProperties() { Id = GetNextShapeId(), Name = "Shape" },
                new P.NonVisualShapeDrawingProperties(),
                new P.ApplicationNonVisualDrawingProperties());

            P.ShapeProperties sp = new P.ShapeProperties();
            A.Transform2D transform = new A.Transform2D();
            transform.AppendChild(new A.Offset() { X = x, Y = y });
            transform.AppendChild(new A.Extents() { Cx = width, Cy = height });
            sp.AppendChild(transform);

            A.PresetGeometry presetGeometry = new A.PresetGeometry() { Preset = shapeType };
            presetGeometry.AppendChild(new A.AdjustValueList());
            sp.AppendChild(presetGeometry);

            if (!string.IsNullOrEmpty(fillColor))
            {
                A.SolidFill solidFill = new A.SolidFill();
                A.RgbColorModelHex rgbColor = new A.RgbColorModelHex() { Val = fillColor };
                if (alphaPercent < 100)
                {
                    rgbColor.AppendChild(new A.Alpha() { Val = alphaPercent * 1000 });
                }
                solidFill.AppendChild(rgbColor);
                sp.AppendChild(solidFill);
            }
            else
            {
                sp.AppendChild(new A.NoFill());
            }

            if (!string.IsNullOrEmpty(outlineColor) && outlineWidth > 0)
            {
                A.Outline outline = new A.Outline() { Width = outlineWidth * 12700 };
                outline.AppendChild(new A.SolidFill(new A.RgbColorModelHex() { Val = outlineColor }));
                sp.AppendChild(outline);
            }
            else
            {
                sp.AppendChild(new A.Outline(new A.NoFill()));
            }

            P.TextBody textBody = CreateEmptyTextBody();

            shape.AppendChild(nvsp);
            shape.AppendChild(sp);
            shape.AppendChild(textBody);

            shapeTree.AppendChild(shape);
        }

        private void AddMetricCard(ShapeTree shapeTree, long x, long y, long width, long height, string label, string value, string accentColor)
        {
            // Card background
            AddRoundedRectangle(shapeTree, x, y, width, height, DarkCardHover, 60);

            // Accent bar on left
            AddShape(shapeTree, A.ShapeTypeValues.Rectangle, x, y + 100000, 40000, height - 200000, accentColor, null, 0);

            // Value (large)
            string displayValue = TruncateText(value, 15);
            AddTextShape(shapeTree, displayValue, x + 100000, y + height / 2 - 500000, width - 150000, 600000,
                MetricValueFontSize, true, TextWhite, A.TextAlignmentTypeValues.Left);

            // Label (smaller, below)
            string displayLabel = TruncateText(label, 30);
            AddTextShape(shapeTree, displayLabel, x + 100000, y + height / 2 + 200000, width - 150000, 400000,
                MetricLabelFontSize, false, MediumGray, A.TextAlignmentTypeValues.Left);
        }

        private void AddDetailedCard(ShapeTree shapeTree, long x, long y, long width, long height,
            string title, string value, string detail, string accentColor)
        {
            // Card background
            AddRoundedRectangle(shapeTree, x, y, width, height, DarkCardHover, 60);

            // Accent bar
            AddShape(shapeTree, A.ShapeTypeValues.Rectangle, x, y + 100000, 40000, height - 200000, accentColor, null, 0);

            // Title
            string displayTitle = TruncateText(title, 25);
            AddTextShape(shapeTree, displayTitle, x + 100000, y + 150000, width - 150000, 350000,
                HeaderFontSize, true, TextLight, A.TextAlignmentTypeValues.Left);

            // Value
            string displayValue = TruncateText(value, 20);
            AddTextShape(shapeTree, displayValue, x + 100000, y + height / 2 - 200000, width - 150000, 500000,
                MetricValueFontSize - 400, true, TextWhite, A.TextAlignmentTypeValues.Left);

            // Detail
            if (!string.IsNullOrEmpty(detail))
            {
                string displayDetail = TruncateText(detail, 35);
                AddTextShape(shapeTree, displayDetail, x + 100000, y + height - 450000, width - 150000, 300000,
                    SmallFontSize, false, MediumGray, A.TextAlignmentTypeValues.Left);
            }
        }

        private void AddRoundedRectangle(ShapeTree shapeTree, long x, long y, long width, long height, string color, int alphaPercent)
        {
            Shape shape = new Shape();

            P.NonVisualShapeProperties nvsp = new P.NonVisualShapeProperties(
                new P.NonVisualDrawingProperties() { Id = GetNextShapeId(), Name = "RoundedRect" },
                new P.NonVisualShapeDrawingProperties(),
                new P.ApplicationNonVisualDrawingProperties());

            P.ShapeProperties sp = new P.ShapeProperties();
            A.Transform2D transform = new A.Transform2D();
            transform.AppendChild(new A.Offset() { X = x, Y = y });
            transform.AppendChild(new A.Extents() { Cx = width, Cy = height });
            sp.AppendChild(transform);

            A.PresetGeometry presetGeometry = new A.PresetGeometry() { Preset = A.ShapeTypeValues.RoundRectangle };
            A.AdjustValueList adjustValueList = new A.AdjustValueList();
            adjustValueList.AppendChild(new A.ShapeGuide() { Name = "adj", Formula = "val 5000" });
            presetGeometry.AppendChild(adjustValueList);
            sp.AppendChild(presetGeometry);

            A.SolidFill solidFill = new A.SolidFill();
            A.RgbColorModelHex rgbColor = new A.RgbColorModelHex() { Val = color };
            if (alphaPercent < 100)
            {
                rgbColor.AppendChild(new A.Alpha() { Val = alphaPercent * 1000 });
            }
            solidFill.AppendChild(rgbColor);
            sp.AppendChild(solidFill);

            sp.AppendChild(new A.Outline(new A.NoFill()));

            P.TextBody textBody = CreateEmptyTextBody();

            shape.AppendChild(nvsp);
            shape.AppendChild(sp);
            shape.AppendChild(textBody);

            shapeTree.AppendChild(shape);
        }

        private A.Table CreateStyledTable(List<string> headers, List<List<string>> rows, int columnCount)
        {
            A.Table table = new A.Table();

            A.TableProperties tableProperties = new A.TableProperties() { FirstRow = true, BandRow = true };
            table.AppendChild(tableProperties);

            A.TableGrid tableGrid = new A.TableGrid();
            long columnWidth = ContentWidth / columnCount;
            for (int i = 0; i < columnCount; i++)
            {
                tableGrid.AppendChild(new A.GridColumn() { Width = columnWidth });
            }
            table.AppendChild(tableGrid);

            int headerSize = columnCount > 5 ? HeaderFontSize - 200 : HeaderFontSize;
            int bodySize = columnCount > 5 ? BodyFontSize - 100 : BodyFontSize;

            if (headers != null && headers.Count > 0)
            {
                List<string> paddedHeaders = PadRowToColumnCount(headers, columnCount);
                A.TableRow headerRow = CreateStyledTableRow(paddedHeaders, true, headerSize, bodySize);
                table.AppendChild(headerRow);
            }

            int rowIndex = 0;
            foreach (var row in rows)
            {
                List<string> paddedRow = PadRowToColumnCount(row, columnCount);
                A.TableRow tableRow = CreateStyledTableRow(paddedRow, false, headerSize, bodySize, rowIndex % 2 == 1);
                table.AppendChild(tableRow);
                rowIndex++;
            }

            return table;
        }

        private A.TableRow CreateStyledTableRow(List<string> cells, bool isHeader, int headerSize, int bodySize, bool isAlternate = false)
        {
            A.TableRow tableRow = new A.TableRow() { Height = isHeader ? 450000L : 380000L };

            foreach (string cellText in cells)
            {
                A.TableCell tableCell = new A.TableCell();

                A.TextBody textBody = new A.TextBody();
                A.BodyProperties bodyProperties = new A.BodyProperties()
                {
                    Wrap = A.TextWrappingValues.Square,
                    LeftInset = 91440,
                    RightInset = 91440,
                    TopInset = 45720,
                    BottomInset = 45720,
                    Anchor = A.TextAnchoringTypeValues.Center
                };
                textBody.AppendChild(bodyProperties);
                textBody.AppendChild(new A.ListStyle());

                A.Paragraph paragraph = new A.Paragraph();
                A.ParagraphProperties paragraphProperties = new A.ParagraphProperties()
                {
                    Alignment = isHeader ? A.TextAlignmentTypeValues.Center : A.TextAlignmentTypeValues.Left
                };
                paragraph.AppendChild(paragraphProperties);

                string cleanedText = CleanHtmlText(cellText);
                if (string.IsNullOrWhiteSpace(cleanedText))
                {
                    cleanedText = "-";
                }
                cleanedText = TruncateText(cleanedText, 50);

                A.Run run = new A.Run();
                A.RunProperties runProperties = new A.RunProperties()
                {
                    Language = "en-US",
                    FontSize = isHeader ? headerSize : bodySize,
                    Bold = isHeader,
                    Dirty = false
                };

                A.LatinFont latinFont = new A.LatinFont() { Typeface = "Segoe UI", PitchFamily = 34, CharacterSet = 0 };
                runProperties.AppendChild(latinFont);

                string textColor = isHeader ? TextWhite : TextLight;
                A.SolidFill textFill = new A.SolidFill(new A.RgbColorModelHex() { Val = textColor });
                runProperties.AppendChild(textFill);

                A.Text text = new A.Text() { Text = cleanedText };
                run.AppendChild(runProperties);
                run.AppendChild(text);
                paragraph.AppendChild(run);
                textBody.AppendChild(paragraph);

                A.TableCellProperties tableCellProperties = new A.TableCellProperties()
                {
                    Anchor = A.TextAnchoringTypeValues.Center
                };

                // Cell background
                string bgColor = isHeader ? VeeamGreen : (isAlternate ? DarkCardHover : DarkCard);
                tableCellProperties.Append(new A.SolidFill(new A.RgbColorModelHex() { Val = bgColor }));

                // Subtle borders
                string borderColor = isHeader ? VeeamGreenDark : DarkCardHover;
                int borderWidth = 9525;

                A.LeftBorderLineProperties leftBorder = new A.LeftBorderLineProperties() { Width = borderWidth };
                leftBorder.AppendChild(new A.SolidFill(new A.RgbColorModelHex() { Val = borderColor }));

                A.RightBorderLineProperties rightBorder = new A.RightBorderLineProperties() { Width = borderWidth };
                rightBorder.AppendChild(new A.SolidFill(new A.RgbColorModelHex() { Val = borderColor }));

                A.TopBorderLineProperties topBorder = new A.TopBorderLineProperties() { Width = borderWidth };
                topBorder.AppendChild(new A.SolidFill(new A.RgbColorModelHex() { Val = borderColor }));

                A.BottomBorderLineProperties bottomBorder = new A.BottomBorderLineProperties() { Width = borderWidth };
                bottomBorder.AppendChild(new A.SolidFill(new A.RgbColorModelHex() { Val = borderColor }));

                tableCellProperties.AppendChild(leftBorder);
                tableCellProperties.AppendChild(rightBorder);
                tableCellProperties.AppendChild(topBorder);
                tableCellProperties.AppendChild(bottomBorder);

                tableCell.AppendChild(textBody);
                tableCell.AppendChild(tableCellProperties);
                tableRow.AppendChild(tableCell);
            }

            return tableRow;
        }

        private P.GraphicFrame CreateGraphicFrame(A.Table table, long x, long y, long width, long height)
        {
            P.GraphicFrame graphicFrame = new P.GraphicFrame();

            P.NonVisualGraphicFrameProperties nonVisualGraphicFrameProperties = new P.NonVisualGraphicFrameProperties(
                new P.NonVisualDrawingProperties() { Id = GetNextShapeId(), Name = "Table" },
                new P.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks() { NoGrouping = true }),
                new P.ApplicationNonVisualDrawingProperties());

            P.Transform transform = new P.Transform();
            transform.AppendChild(new A.Offset() { X = x, Y = y });
            transform.AppendChild(new A.Extents() { Cx = width, Cy = height });

            A.Graphic graphic = new A.Graphic();
            A.GraphicData graphicData = new A.GraphicData() { Uri = "http://schemas.openxmlformats.org/drawingml/2006/table" };
            graphicData.AppendChild(table);
            graphic.AppendChild(graphicData);

            graphicFrame.AppendChild(nonVisualGraphicFrameProperties);
            graphicFrame.AppendChild(transform);
            graphicFrame.AppendChild(graphic);

            return graphicFrame;
        }

        private List<string> PadRowToColumnCount(List<string> row, int targetCount)
        {
            List<string> paddedRow = new List<string>(row);
            while (paddedRow.Count < targetCount)
            {
                paddedRow.Add(string.Empty);
            }
            if (paddedRow.Count > targetCount)
            {
                paddedRow = paddedRow.Take(targetCount).ToList();
            }
            return paddedRow;
        }

        private void AddSlideToPresentation(PresentationPart presentationPart, SlidePart slidePart)
        {
            // Link slide to slide layout
            if (_slideLayoutPart != null)
            {
                slidePart.AddPart(_slideLayoutPart, "rId1");
            }

            SlideIdList slideIdList = presentationPart.Presentation.SlideIdList;

            uint maxSlideId = 256;
            if (slideIdList.Elements<SlideId>().Count() > 0)
            {
                maxSlideId = slideIdList.Elements<SlideId>().Select(x => x.Id.Value).Max();
            }

            SlideId slideId = new SlideId();
            slideId.Id = ++maxSlideId;
            slideId.RelationshipId = presentationPart.GetIdOfPart(slidePart);
            slideIdList.AppendChild(slideId);

            _slideIndex++;
        }

        private void SavePresentation(PresentationPart presentationPart)
        {
            presentationPart.Presentation.Save();
        }

        private uint GetNextShapeId()
        {
            return ++_shapeId;
        }

        private string TruncateText(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text)) return text;
            if (text.Length <= maxLength) return text;
            return text.Substring(0, maxLength - 3) + "...";
        }

        #region HTML Parsing

        private List<TableData> ParseHtmlTables(string htmlContent)
        {
            List<TableData> tables = new List<TableData>();
            HashSet<string> processedTableSignatures = new HashSet<string>();

            // Method 1: Find all tables directly and get their containing div's id for the title
            var allTableMatches = Regex.Matches(htmlContent, @"<table[^>]*class=""[^""]*content-table[^""]*""[^>]*>(.*?)</table>",
                RegexOptions.Singleline | RegexOptions.IgnoreCase);

            foreach (Match tableMatch in allTableMatches)
            {
                string tableHtml = tableMatch.Groups[1].Value;

                // Find the div that contains this table by looking backwards in the HTML
                int tableStart = tableMatch.Index;
                string beforeTable = htmlContent.Substring(Math.Max(0, tableStart - 500), Math.Min(500, tableStart));

                // Look for the most recent id= in a div with card class
                string title = "Data";
                var idMatch = Regex.Match(beforeTable, @"id=""([^""]+)""[^>]*>\s*$", RegexOptions.RightToLeft);
                if (idMatch.Success)
                {
                    title = FormatSectionTitle(idMatch.Groups[1].Value);
                }

                TableData tableData = ParseTable(tableHtml, title);

                if (tableData.Rows.Count > 0)
                {
                    // Create signature to avoid duplicates
                    string signature = $"{tableData.Title}_{tableData.ColumnCount}_{tableData.Rows.Count}";
                    if (!processedTableSignatures.Contains(signature))
                    {
                        processedTableSignatures.Add(signature);
                        tables.Add(tableData);
                    }
                }
            }

            // Method 2: Look for card divs with tables - improved regex
            var cardDivMatches = Regex.Matches(htmlContent,
                @"<div[^>]*(?:class=""[^""]*card[^""]*""[^>]*id=""([^""]+)""|id=""([^""]+)""[^>]*class=""[^""]*card[^""]*"")[^>]*>",
                RegexOptions.Singleline | RegexOptions.IgnoreCase);

            foreach (Match cardMatch in cardDivMatches)
            {
                string cardId = !string.IsNullOrEmpty(cardMatch.Groups[1].Value)
                    ? cardMatch.Groups[1].Value
                    : cardMatch.Groups[2].Value;

                // Find the table content after this div opening
                int divStart = cardMatch.Index + cardMatch.Length;
                int searchEnd = Math.Min(divStart + 50000, htmlContent.Length);
                string afterDiv = htmlContent.Substring(divStart, searchEnd - divStart);

                // Find the first table in this section
                var tableMatch = Regex.Match(afterDiv, @"<table[^>]*>(.*?)</table>",
                    RegexOptions.Singleline | RegexOptions.IgnoreCase);

                if (tableMatch.Success)
                {
                    string title = FormatSectionTitle(cardId);
                    TableData tableData = ParseTable(tableMatch.Groups[1].Value, title);

                    if (tableData.Rows.Count > 0)
                    {
                        string signature = $"{tableData.Title}_{tableData.ColumnCount}_{tableData.Rows.Count}";
                        if (!processedTableSignatures.Contains(signature))
                        {
                            processedTableSignatures.Add(signature);
                            tables.Add(tableData);
                        }
                    }
                }
            }

            // Method 3: Match collapsible sections with tables
            var sectionMatches = Regex.Matches(htmlContent,
                @"<button[^>]*class=""[^""]*collapsible[^""]*""[^>]*>([^<]+)</button>",
                RegexOptions.Singleline | RegexOptions.IgnoreCase);

            foreach (Match sectionMatch in sectionMatches)
            {
                string sectionTitle = CleanHtmlText(sectionMatch.Groups[1].Value);

                // Find tables after this button
                int buttonEnd = sectionMatch.Index + sectionMatch.Length;
                int searchEnd = Math.Min(buttonEnd + 100000, htmlContent.Length);
                string afterButton = htmlContent.Substring(buttonEnd, searchEnd - buttonEnd);

                // Look for next button to bound our search
                var nextButtonMatch = Regex.Match(afterButton, @"<button[^>]*class=""[^""]*collapsible");
                string sectionContent = nextButtonMatch.Success
                    ? afterButton.Substring(0, nextButtonMatch.Index)
                    : afterButton;

                var tableMatches = Regex.Matches(sectionContent, @"<table[^>]*>(.*?)</table>",
                    RegexOptions.Singleline | RegexOptions.IgnoreCase);

                foreach (Match tableMatch in tableMatches)
                {
                    TableData tableData = ParseTable(tableMatch.Groups[1].Value, sectionTitle);

                    if (tableData.Rows.Count > 0)
                    {
                        string signature = $"{tableData.Title}_{tableData.ColumnCount}_{tableData.Rows.Count}";
                        if (!processedTableSignatures.Contains(signature))
                        {
                            processedTableSignatures.Add(signature);
                            tables.Add(tableData);
                        }
                    }
                }
            }

            return tables;
        }

        private string FormatSectionTitle(string id)
        {
            if (string.IsNullOrEmpty(id)) return "Data";

            // Convert camelCase or PascalCase to Title Case with spaces
            string result = Regex.Replace(id, @"([a-z])([A-Z])", "$1 $2");
            result = Regex.Replace(result, @"([A-Z]+)([A-Z][a-z])", "$1 $2");
            result = result.Replace("_", " ").Replace("-", " ");

            // Title case
            result = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(result.ToLower());

            // Common abbreviations - keep uppercase
            result = Regex.Replace(result, @"\bVbr\b", "VBR", RegexOptions.IgnoreCase);
            result = Regex.Replace(result, @"\bSobr\b", "SOBR", RegexOptions.IgnoreCase);
            result = Regex.Replace(result, @"\bVm\b", "VM", RegexOptions.IgnoreCase);
            result = Regex.Replace(result, @"\bDb\b", "DB", RegexOptions.IgnoreCase);
            result = Regex.Replace(result, @"\bSql\b", "SQL", RegexOptions.IgnoreCase);

            return result.Trim();
        }

        private string ExtractSectionTitle(string sectionContent, string fallbackId)
        {
            var h2Match = Regex.Match(sectionContent, @"<h2[^>]*>(.*?)</h2>", RegexOptions.Singleline);
            if (h2Match.Success)
            {
                return CleanHtmlText(h2Match.Groups[1].Value);
            }

            var h3Match = Regex.Match(sectionContent, @"<h3[^>]*>(.*?)</h3>", RegexOptions.Singleline);
            if (h3Match.Success)
            {
                return CleanHtmlText(h3Match.Groups[1].Value);
            }

            return FormatSectionTitle(fallbackId);
        }

        private TableData ParseTable(string tableHtml, string title)
        {
            TableData tableData = new TableData { Title = title };

            var headerMatch = Regex.Match(tableHtml, @"<thead>(.*?)</thead>", RegexOptions.Singleline);
            if (headerMatch.Success)
            {
                string headerHtml = headerMatch.Groups[1].Value;
                var headerCells = Regex.Matches(headerHtml, @"<th[^>]*>(.*?)</th>", RegexOptions.Singleline);

                foreach (Match cell in headerCells)
                {
                    tableData.Headers.Add(CleanHtmlText(cell.Groups[1].Value));
                }
            }

            tableData.ColumnCount = tableData.Headers.Count > 0 ? tableData.Headers.Count : 1;

            var bodyMatch = Regex.Match(tableHtml, @"<tbody>(.*?)</tbody>", RegexOptions.Singleline);
            string rowsHtml = bodyMatch.Success ? bodyMatch.Groups[1].Value : tableHtml;

            var rowMatches = Regex.Matches(rowsHtml, @"<tr[^>]*>(.*?)</tr>", RegexOptions.Singleline);

            foreach (Match rowMatch in rowMatches)
            {
                string rowHtml = rowMatch.Groups[1].Value;
                var cellMatches = Regex.Matches(rowHtml, @"<t[dh][^>]*>(.*?)</t[dh]>", RegexOptions.Singleline);

                if (cellMatches.Count == 0)
                {
                    continue;
                }

                List<string> row = new List<string>();
                foreach (Match cellMatch in cellMatches)
                {
                    row.Add(CleanHtmlText(cellMatch.Groups[1].Value));
                }

                if (row.Count > tableData.ColumnCount)
                {
                    tableData.ColumnCount = row.Count;
                }

                tableData.Rows.Add(row);
            }

            return tableData;
        }

        private Dictionary<string, string> ExtractSummaryMetrics(string htmlContent)
        {
            Dictionary<string, string> metrics = new Dictionary<string, string>();

            // Try to extract key metrics from the HTML
            // Look for license info
            var licenseMatch = Regex.Match(htmlContent, @"License\s*Type[^<]*</t[dh]>\s*<t[dh][^>]*>([^<]+)</t[dh]>", RegexOptions.IgnoreCase);
            if (licenseMatch.Success)
            {
                metrics["License Type"] = CleanHtmlText(licenseMatch.Groups[1].Value);
            }

            // Look for VBR version
            var versionMatch = Regex.Match(htmlContent, @"(?:VBR|Version)[^<]*</t[dh]>\s*<t[dh][^>]*>(\d+\.\d+[^<]*)</t[dh]>", RegexOptions.IgnoreCase);
            if (versionMatch.Success)
            {
                metrics["VBR Version"] = CleanHtmlText(versionMatch.Groups[1].Value);
            }

            // Look for job counts
            var jobMatch = Regex.Match(htmlContent, @"(?:Total\s+)?Jobs?[^<]*</t[dh]>\s*<t[dh][^>]*>(\d+)</t[dh]>", RegexOptions.IgnoreCase);
            if (jobMatch.Success)
            {
                metrics["Total Jobs"] = CleanHtmlText(jobMatch.Groups[1].Value);
            }

            // Look for repository info
            var repoMatch = Regex.Match(htmlContent, @"Repositor(?:y|ies)[^<]*</t[dh]>\s*<t[dh][^>]*>(\d+)</t[dh]>", RegexOptions.IgnoreCase);
            if (repoMatch.Success)
            {
                metrics["Repositories"] = CleanHtmlText(repoMatch.Groups[1].Value);
            }

            // Look for proxy count
            var proxyMatch = Regex.Match(htmlContent, @"Prox(?:y|ies)[^<]*</t[dh]>\s*<t[dh][^>]*>(\d+)</t[dh]>", RegexOptions.IgnoreCase);
            if (proxyMatch.Success)
            {
                metrics["Proxies"] = CleanHtmlText(proxyMatch.Groups[1].Value);
            }

            // Look for protected VMs
            var vmMatch = Regex.Match(htmlContent, @"(?:Protected\s+)?VMs?[^<]*</t[dh]>\s*<t[dh][^>]*>(\d+)</t[dh]>", RegexOptions.IgnoreCase);
            if (vmMatch.Success)
            {
                metrics["Protected VMs"] = CleanHtmlText(vmMatch.Groups[1].Value);
            }

            return metrics;
        }

        private string ExtractCompanyName(string htmlContent)
        {
            // Try to get from license table
            var licenseMatch = Regex.Match(htmlContent, @"<td[^>]*>([^<]+)</td>\s*<td[^>]*>EnterprisePlus</td>", RegexOptions.Singleline);
            if (licenseMatch.Success)
            {
                return CleanHtmlText(licenseMatch.Groups[1].Value);
            }

            // Try from h1
            var h1Match = Regex.Match(htmlContent, @"<h1[^>]*>\s*([^<]+)\s*</h1>", RegexOptions.Singleline);
            if (h1Match.Success)
            {
                string title = CleanHtmlText(h1Match.Groups[1].Value);
                if (!string.IsNullOrWhiteSpace(title) && !title.Contains("Veeam") && !title.Contains("Health"))
                {
                    return title;
                }
            }

            // Try from text overlay
            var overlayMatch = Regex.Match(htmlContent, @"text-overlay[^>]*>\s*<h1[^>]*>\s*([^<]+)\s*</h1>", RegexOptions.Singleline);
            if (overlayMatch.Success)
            {
                string name = CleanHtmlText(overlayMatch.Groups[1].Value);
                if (!string.IsNullOrWhiteSpace(name))
                {
                    return name;
                }
            }

            return "Health Check Report";
        }

        private string ExtractReportDate(string htmlContent)
        {
            // Try to extract from text-overlay
            var overlayMatch = Regex.Match(htmlContent, @"<p>([^<|]+)\|([^<]+)</p>\s*</div>\s*<img", RegexOptions.Singleline);
            if (overlayMatch.Success)
            {
                string datePart = overlayMatch.Groups[1].Value.Trim();
                string reportPart = overlayMatch.Groups[2].Value.Trim();
                return $"{datePart} | {reportPart}";
            }

            // Try from title
            var titleMatch = Regex.Match(htmlContent, @"<title>([^<]+)</title>", RegexOptions.Singleline);
            if (titleMatch.Success)
            {
                string title = titleMatch.Groups[1].Value;
                var dateMatch = Regex.Match(title, @"(\d{4}\.\d{2}\.\d{2}\.\d{6})");
                if (dateMatch.Success)
                {
                    string dateStr = dateMatch.Groups[1].Value;
                    if (DateTime.TryParseExact(dateStr, "yyyy.MM.dd.HHmmss", null, System.Globalization.DateTimeStyles.None, out DateTime reportDateTime))
                    {
                        return $"{reportDateTime:MMMM dd, yyyy} | 7 Day Report";
                    }
                }
            }

            return $"{DateTime.Now:MMMM dd, yyyy} | Health Check Report";
        }

        private string CleanHtmlText(string htmlText)
        {
            if (string.IsNullOrEmpty(htmlText))
            {
                return string.Empty;
            }

            string text = Regex.Replace(htmlText, @"<[^>]+>", " ");
            text = System.Net.WebUtility.HtmlDecode(text);
            text = Regex.Replace(text, @"\s+", " ");
            text = text.Trim();

            return text;
        }

        #endregion

        public void Dispose()
        {
        }

        private class TableData
        {
            public string Title { get; set; }
            public List<string> Headers { get; set; } = new List<string>();
            public List<List<string>> Rows { get; set; } = new List<List<string>>();
            public int ColumnCount { get; set; }
        }
    }
}

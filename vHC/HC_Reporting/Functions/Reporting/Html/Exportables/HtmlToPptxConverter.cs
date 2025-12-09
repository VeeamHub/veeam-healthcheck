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
        private const int MaxRowsPerSlide = 18; // Optimal for readability
        private const int MaxColumnsForNormalLayout = 8; // If more, consider special handling
        private const int TitleFontSize = 3200;
        private const int HeaderFontSize = 1600;
        private const int BodyFontSize = 1200;
        private const long SlideWidth = 9144000;
        private const long SlideHeight = 6858000;
        
        // Veeam brand colors from CSS
        private const string VeeamGreen = "00D15F";
        private const string VeeamGreenDark = "00A847";
        private const string VeeamGreenLight = "4AFF80";

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

                SlidePart titleSlidePart = CreateTitleSlide(presentationPart, companyName, reportDate);

                List<TableData> tables = ParseHtmlTables(htmlContent);

                foreach (var table in tables)
                {
                    CreateTableSlides(presentationPart, table);
                }

                SavePresentation(presentationPart);
            }
        }

        private void CreatePresentationParts(PresentationDocument presentationDocument)
        {
            PresentationPart presentationPart = presentationDocument.PresentationPart;
            
            presentationPart.Presentation.SlideIdList = new SlideIdList();
            presentationPart.Presentation.SlideSize = new SlideSize() { Cx = 9144000, Cy = 6858000 };
            presentationPart.Presentation.NotesSize = new NotesSize() { Cx = 6858000, Cy = 9144000 };
            
            CreateSlideMasterPart(presentationPart);
        }

        private void CreateSlideMasterPart(PresentationPart presentationPart)
        {
            SlideMasterPart slideMasterPart1 = presentationPart.AddNewPart<SlideMasterPart>("rId1");
            
            SlideMaster slideMaster = new SlideMaster(
                new P.CommonSlideData(
                    new P.Background(
                        new P.BackgroundProperties(
                            new A.SolidFill(new A.SchemeColor() { Val = A.SchemeColorValues.Background1 })
                        )
                    ),
                    new P.ShapeTree(
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
                new P.ColorMap()
                {
                    Background1 = A.ColorSchemeIndexValues.Light1,
                    Text1 = A.ColorSchemeIndexValues.Dark1,
                    Background2 = A.ColorSchemeIndexValues.Light2,
                    Text2 = A.ColorSchemeIndexValues.Dark2,
                    Accent1 = A.ColorSchemeIndexValues.Accent1,
                    Accent2 = A.ColorSchemeIndexValues.Accent2,
                    Accent3 = A.ColorSchemeIndexValues.Accent3,
                    Accent4 = A.ColorSchemeIndexValues.Accent4,
                    Accent5 = A.ColorSchemeIndexValues.Accent5,
                    Accent6 = A.ColorSchemeIndexValues.Accent6,
                    Hyperlink = A.ColorSchemeIndexValues.Hyperlink,
                    FollowedHyperlink = A.ColorSchemeIndexValues.FollowedHyperlink
                });
            
            slideMasterPart1.SlideMaster = slideMaster;
            
            SlideLayoutPart slideLayoutPart = slideMasterPart1.AddNewPart<SlideLayoutPart>("rId1");
            SlideLayout slideLayout = new SlideLayout(
                new P.CommonSlideData(
                    new P.ShapeTree(
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
            
            slideLayout.Type = SlideLayoutValues.Blank;
            slideLayoutPart.SlideLayout = slideLayout;
            
            presentationPart.Presentation.SlideMasterIdList = new SlideMasterIdList(
                new SlideMasterId() { Id = 2147483648U, RelationshipId = presentationPart.GetIdOfPart(slideMasterPart1) });
        }

        private SlidePart CreateTitleSlide(PresentationPart presentationPart, string companyName, string reportDate)
        {
            SlidePart slidePart = presentationPart.AddNewPart<SlidePart>();
            
            Slide slide = new Slide(
                new CommonSlideData(
                    new P.Background(
                        new P.BackgroundProperties(
                            new A.SolidFill(new A.RgbColorModelHex() { Val = "1A1F2E" }))),
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
            
            slidePart.Slide = slide;
            ShapeTree shapeTree = slide.CommonSlideData.ShapeTree;

            // Create text overlay box matching HTML .text-overlay style
            // Position: left 5%, vertically centered
            long boxX = 457200; // 5% of slide width
            long boxY = 1714500; // Vertically centered
            long boxWidth = 3200000; // 35% of slide width
            long boxHeight = 2400000;

            // Background box with semi-transparent dark background and Veeam green border
            Shape overlayBox = CreateOverlayBox(boxX, boxY, boxWidth, boxHeight);
            shapeTree.AppendChild(overlayBox);

            // Company name (H1 style from HTML)
            Shape companyShape = CreateCoverTextShape(companyName, boxX + 100000, boxY + 200000, boxWidth - 200000, 600000, 2800, true, true);
            shapeTree.AppendChild(companyShape);
            
            // "Veeam Backup & Recovery" (H3 style)
            Shape productShape = CreateCoverTextShape("Veeam Backup & Recovery", boxX + 100000, boxY + 900000, boxWidth - 200000, 500000, 2000, false, true);
            shapeTree.AppendChild(productShape);

            // "Health Check Report" (P style)
            Shape reportTypeShape = CreateCoverTextShape("Health Check Report", boxX + 100000, boxY + 1450000, boxWidth - 200000, 350000, 1400, false, false);
            shapeTree.AppendChild(reportTypeShape);

            // Date and report range (P style)
            Shape dateShape = CreateCoverTextShape(reportDate, boxX + 100000, boxY + 1850000, boxWidth - 200000, 350000, 1200, false, false);
            shapeTree.AppendChild(dateShape);

            AddSlideToPresentation(presentationPart, slidePart);

            return slidePart;
        }

        private Shape CreateStyledTitleShape(string text, long x, long y, long width, long height, int fontSize, bool isMainTitle)
        {
            Shape shape = new Shape();

            P.NonVisualShapeProperties nonVisualShapeProperties = new P.NonVisualShapeProperties(
                new P.NonVisualDrawingProperties() { Id = (UInt32Value)2U, Name = "Title" },
                new P.NonVisualShapeDrawingProperties(new A.ShapeLocks() { NoGrouping = true }),
                new P.ApplicationNonVisualDrawingProperties(new P.PlaceholderShape()));

            P.ShapeProperties shapeProperties = new P.ShapeProperties();
            A.Transform2D transform2D = new A.Transform2D();
            A.Offset offset = new A.Offset() { X = x, Y = y };
            A.Extents extents = new A.Extents() { Cx = width, Cy = height };
            transform2D.AppendChild(offset);
            transform2D.AppendChild(extents);
            shapeProperties.AppendChild(transform2D);

            P.TextBody textBody = new P.TextBody();
            A.BodyProperties bodyProperties = new A.BodyProperties() { Anchor = A.TextAnchoringTypeValues.Center };
            A.ListStyle listStyle = new A.ListStyle();

            A.Paragraph paragraph = new A.Paragraph();
            A.ParagraphProperties paragraphProperties = new A.ParagraphProperties() { Alignment = A.TextAlignmentTypeValues.Center };
            paragraph.AppendChild(paragraphProperties);

            A.Run run = new A.Run();
            A.RunProperties runProperties = new A.RunProperties() 
            { 
                Language = "en-US", 
                FontSize = fontSize, 
                Bold = isMainTitle,
                Dirty = false
            };
            A.SolidFill solidFill = new A.SolidFill(new A.RgbColorModelHex() { Val = "FFFFFF" });
            runProperties.AppendChild(solidFill);
            
            A.Text textElement = new A.Text() { Text = text };

            run.AppendChild(runProperties);
            run.AppendChild(textElement);
            paragraph.AppendChild(run);

            textBody.AppendChild(bodyProperties);
            textBody.AppendChild(listStyle);
            textBody.AppendChild(paragraph);

            shape.AppendChild(nonVisualShapeProperties);
            shape.AppendChild(shapeProperties);
            shape.AppendChild(textBody);

            return shape;
        }

        private void CreateTableSlides(PresentationPart presentationPart, TableData tableData)
        {
            if (tableData.Rows.Count == 0)
            {
                return;
            }

            // Detect if table is too wide for normal layout
            if (tableData.ColumnCount > MaxColumnsForNormalLayout)
            {
                CreateWideTableSlides(presentationPart, tableData);
                return;
            }

            int totalRows = tableData.Rows.Count;
            int slideCount = (int)Math.Ceiling((double)totalRows / MaxRowsPerSlide);

            for (int slideIndex = 0; slideIndex < slideCount; slideIndex++)
            {
                int startRow = slideIndex * MaxRowsPerSlide;
                int endRow = Math.Min(startRow + MaxRowsPerSlide, totalRows);

                SlidePart slidePart = presentationPart.AddNewPart<SlidePart>();
                
                Slide slide = new Slide(
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
                
                slidePart.Slide = slide;
                ShapeTree shapeTree = slide.CommonSlideData.ShapeTree;

                string slideTitle = tableData.Title;
                if (slideCount > 1)
                {
                    slideTitle += $" (Page {slideIndex + 1} of {slideCount})";
                }

                Shape titleShape = CreateModernTextShape(slideTitle, 457200, 300000, 8229600, 900000, TitleFontSize - 400, true);
                shapeTree.AppendChild(titleShape);

                var rowsForSlide = tableData.Rows.Skip(startRow).Take(endRow - startRow).ToList();
                A.Table table = CreatePowerPointTable(tableData.Headers, rowsForSlide, tableData.ColumnCount);

                // Adjusted position and size for better layout
                P.GraphicFrame graphicFrame = CreateGraphicFrame(table, 300000, 1400000, 8544000, 5100000);
                shapeTree.AppendChild(graphicFrame);

                AddSlideToPresentation(presentationPart, slidePart);
            }
        }

        private void CreateWideTableSlides(PresentationPart presentationPart, TableData tableData)
        {
            // For wide tables, split into column groups
            const int columnsPerSlide = 6; // Show 6 columns at a time
            int columnGroups = (int)Math.Ceiling((double)tableData.ColumnCount / columnsPerSlide);

            for (int groupIndex = 0; groupIndex < columnGroups; groupIndex++)
            {
                int startCol = groupIndex * columnsPerSlide;
                int endCol = Math.Min(startCol + columnsPerSlide, tableData.ColumnCount);
                int colsInGroup = endCol - startCol;

                // Always include first column (usually the identifier) plus the current group
                List<string> slideHeaders = new List<string>();
                if (tableData.Headers.Count > 0)
                {
                    if (startCol > 0)
                    {
                        // Include first column as reference
                        slideHeaders.Add(tableData.Headers[0]);
                    }
                    slideHeaders.AddRange(tableData.Headers.Skip(startCol).Take(colsInGroup));
                }

                List<List<string>> slideRows = new List<List<string>>();
                foreach (var row in tableData.Rows)
                {
                    List<string> slideRow = new List<string>();
                    if (startCol > 0 && row.Count > 0)
                    {
                        slideRow.Add(row[0]); // Include first column
                    }
                    slideRow.AddRange(row.Skip(startCol).Take(colsInGroup));
                    slideRows.Add(slideRow);
                }

                // Paginate rows for this column group
                int totalRows = slideRows.Count;
                int slideCount = (int)Math.Ceiling((double)totalRows / MaxRowsPerSlide);

                for (int slideIndex = 0; slideIndex < slideCount; slideIndex++)
                {
                    int startRow = slideIndex * MaxRowsPerSlide;
                    int endRow = Math.Min(startRow + MaxRowsPerSlide, totalRows);

                    SlidePart slidePart = presentationPart.AddNewPart<SlidePart>();
                    Slide slide = new Slide(
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

                    slidePart.Slide = slide;
                    ShapeTree shapeTree = slide.CommonSlideData.ShapeTree;

                    string slideTitle = $"{tableData.Title} - Columns {startCol + 1}-{endCol}";
                    if (slideCount > 1)
                    {
                        slideTitle += $" (Page {slideIndex + 1} of {slideCount})";
                    }

                    Shape titleShape = CreateModernTextShape(slideTitle, 457200, 300000, 8229600, 900000, TitleFontSize - 400, true);
                    shapeTree.AppendChild(titleShape);

                    var rowsForSlide = slideRows.Skip(startRow).Take(endRow - startRow).ToList();
                    A.Table table = CreatePowerPointTable(slideHeaders, rowsForSlide, slideHeaders.Count);

                    P.GraphicFrame graphicFrame = CreateGraphicFrame(table, 300000, 1400000, 8544000, 5100000);
                    shapeTree.AppendChild(graphicFrame);

                    AddSlideToPresentation(presentationPart, slidePart);
                }
            }
        }

        private A.Table CreatePowerPointTable(List<string> headers, List<List<string>> rows, int columnCount)
        {
            A.Table table = new A.Table();

            A.TableProperties tableProperties = new A.TableProperties() { FirstRow = true, BandRow = true };
            table.AppendChild(tableProperties);

            A.TableGrid tableGrid = new A.TableGrid();
            long columnWidth = 8544000 / columnCount; // Use full width for table
            for (int i = 0; i < columnCount; i++)
            {
                tableGrid.AppendChild(new A.GridColumn() { Width = columnWidth });
            }
            table.AppendChild(tableGrid);

            // Adjust font sizes based on column count for better fit
            int headerSize = columnCount > 6 ? HeaderFontSize - 200 : HeaderFontSize;
            int bodySize = columnCount > 6 ? BodyFontSize - 100 : BodyFontSize;

            if (headers != null && headers.Count > 0)
            {
                List<string> paddedHeaders = PadRowToColumnCount(headers, columnCount);
                A.TableRow headerRow = CreateTableRow(paddedHeaders, true, headerSize, bodySize);
                table.AppendChild(headerRow);
            }

            foreach (var row in rows)
            {
                List<string> paddedRow = PadRowToColumnCount(row, columnCount);
                A.TableRow tableRow = CreateTableRow(paddedRow, false, headerSize, bodySize);
                table.AppendChild(tableRow);
            }

            return table;
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

        private A.TableRow CreateTableRow(List<string> cells, bool isHeader, int headerSize, int bodySize)
        {
            A.TableRow tableRow = new A.TableRow() { Height = isHeader ? 457200L : 400000L }; // Taller rows

            foreach (string cellText in cells)
            {
                A.TableCell tableCell = new A.TableCell();

                A.TextBody textBody = new A.TextBody();
                A.BodyProperties bodyProperties = new A.BodyProperties() 
                { 
                    Wrap = A.TextWrappingValues.Square,
                    LeftInset = 45720, // 0.05 inch padding for more compact fit
                    RightInset = 45720,
                    TopInset = 45720,
                    BottomInset = 45720,
                    Anchor = A.TextAnchoringTypeValues.Center
                };
                A.ListStyle listStyle = new A.ListStyle();

                A.Paragraph paragraph = new A.Paragraph();
                A.ParagraphProperties paragraphProperties = new A.ParagraphProperties() 
                { 
                    Alignment = isHeader ? A.TextAlignmentTypeValues.Center : A.TextAlignmentTypeValues.Left 
                };
                paragraph.AppendChild(paragraphProperties);

                string cleanedText = CleanHtmlText(cellText);
                if (string.IsNullOrWhiteSpace(cleanedText))
                {
                    cleanedText = " ";
                }

                A.Run run = new A.Run();
                A.RunProperties runProperties = new A.RunProperties() 
                { 
                    Language = "en-US", 
                    FontSize = isHeader ? headerSize : bodySize, 
                    Bold = isHeader,
                    Dirty = false
                };
                
                // Text color: white for headers, dark for body
                if (isHeader)
                {
                    A.SolidFill textFill = new A.SolidFill(new A.RgbColorModelHex() { Val = "FFFFFF" });
                    runProperties.AppendChild(textFill);
                }
                else
                {
                    A.SolidFill textFill = new A.SolidFill(new A.SchemeColor() { Val = A.SchemeColorValues.Text1 });
                    runProperties.AppendChild(textFill);
                }
                
                A.Text text = new A.Text() { Text = cleanedText };

                run.AppendChild(runProperties);
                run.AppendChild(text);
                paragraph.AppendChild(run);

                textBody.AppendChild(bodyProperties);
                textBody.AppendChild(listStyle);
                textBody.AppendChild(paragraph);

                A.TableCellProperties tableCellProperties = new A.TableCellProperties() 
                { 
                    Anchor = A.TextAnchoringTypeValues.Center
                };
                
                // Cell background color
                if (isHeader)
                {
                    // Veeam brand green
                    tableCellProperties.Append(new A.SolidFill(new A.RgbColorModelHex() { Val = VeeamGreen }));
                }
                else
                {
                    // Simple light gray for readability
                    tableCellProperties.Append(new A.SolidFill(new A.RgbColorModelHex() { Val = "F8F9FA" }));
                }
                
                // Add borders to cells
                A.LeftBorderLineProperties leftBorder = new A.LeftBorderLineProperties() { Width = 12700 };
                leftBorder.AppendChild(new A.SolidFill(new A.RgbColorModelHex() { Val = "D0D0D0" }));
                
                A.RightBorderLineProperties rightBorder = new A.RightBorderLineProperties() { Width = 12700 };
                rightBorder.AppendChild(new A.SolidFill(new A.RgbColorModelHex() { Val = "D0D0D0" }));
                
                A.TopBorderLineProperties topBorder = new A.TopBorderLineProperties() { Width = 12700 };
                topBorder.AppendChild(new A.SolidFill(new A.RgbColorModelHex() { Val = "D0D0D0" }));
                
                A.BottomBorderLineProperties bottomBorder = new A.BottomBorderLineProperties() { Width = 12700 };
                bottomBorder.AppendChild(new A.SolidFill(new A.RgbColorModelHex() { Val = "D0D0D0" }));
                
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
                new P.NonVisualDrawingProperties() { Id = (UInt32Value)5U, Name = "Table" },
                new P.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks() { NoGrouping = true }),
                new P.ApplicationNonVisualDrawingProperties());

            P.Transform transform = new P.Transform();
            A.Offset offset = new A.Offset() { X = x, Y = y };
            A.Extents extents = new A.Extents() { Cx = width, Cy = height };
            transform.AppendChild(offset);
            transform.AppendChild(extents);

            A.Graphic graphic = new A.Graphic();
            A.GraphicData graphicData = new A.GraphicData() { Uri = "http://schemas.openxmlformats.org/drawingml/2006/table" };
            graphicData.AppendChild(table);
            graphic.AppendChild(graphicData);

            graphicFrame.AppendChild(nonVisualGraphicFrameProperties);
            graphicFrame.AppendChild(transform);
            graphicFrame.AppendChild(graphic);

            return graphicFrame;
        }

        private Shape CreateModernTextShape(string text, long x, long y, long width, long height, int fontSize, bool bold)
        {
            Shape shape = new Shape();

            P.NonVisualShapeProperties nonVisualShapeProperties = new P.NonVisualShapeProperties(
                new P.NonVisualDrawingProperties() { Id = (UInt32Value)2U, Name = "Title" },
                new P.NonVisualShapeDrawingProperties(new A.ShapeLocks() { NoGrouping = true }),
                new P.ApplicationNonVisualDrawingProperties(new P.PlaceholderShape()));

            P.ShapeProperties shapeProperties = new P.ShapeProperties();
            A.Transform2D transform2D = new A.Transform2D();
            A.Offset offset = new A.Offset() { X = x, Y = y };
            A.Extents extents = new A.Extents() { Cx = width, Cy = height };
            transform2D.AppendChild(offset);
            transform2D.AppendChild(extents);
            shapeProperties.AppendChild(transform2D);
            
            // Simple light background
            A.SolidFill solidFill = new A.SolidFill(new A.RgbColorModelHex() { Val = "F0F0F0" });
            shapeProperties.AppendChild(solidFill);

            P.TextBody textBody = new P.TextBody();
            A.BodyProperties bodyProperties = new A.BodyProperties() 
            { 
                Anchor = A.TextAnchoringTypeValues.Center,
                LeftInset = 91440, // 0.1 inch padding
                RightInset = 91440
            };
            A.ListStyle listStyle = new A.ListStyle();

            A.Paragraph paragraph = new A.Paragraph();
            A.ParagraphProperties paragraphProperties = new A.ParagraphProperties() { Alignment = A.TextAlignmentTypeValues.Left };
            paragraph.AppendChild(paragraphProperties);
            
            A.Run run = new A.Run();
            A.RunProperties runProperties = new A.RunProperties() 
            { 
                Language = "en-US", 
                FontSize = fontSize, 
                Bold = bold,
                Dirty = false
            };
            A.SolidFill textColor = new A.SolidFill(new A.RgbColorModelHex() { Val = VeeamGreen });
            runProperties.AppendChild(textColor);
            
            A.Text textElement = new A.Text() { Text = text };

            run.AppendChild(runProperties);
            run.AppendChild(textElement);
            paragraph.AppendChild(run);

            textBody.AppendChild(bodyProperties);
            textBody.AppendChild(listStyle);
            textBody.AppendChild(paragraph);

            shape.AppendChild(nonVisualShapeProperties);
            shape.AppendChild(shapeProperties);
            shape.AppendChild(textBody);

            return shape;
        }

        private P.GroupShapeProperties CreateNonVisualGroupShapeProperties()
        {
            P.GroupShapeProperties groupShapeProperties = new P.GroupShapeProperties();
            A.TransformGroup transformGroup = new A.TransformGroup();
            A.Offset offset = new A.Offset() { X = 0L, Y = 0L };
            A.Extents extents = new A.Extents() { Cx = 0L, Cy = 0L };
            A.ChildOffset childOffset = new A.ChildOffset() { X = 0L, Y = 0L };
            A.ChildExtents childExtents = new A.ChildExtents() { Cx = 0L, Cy = 0L };

            transformGroup.AppendChild(offset);
            transformGroup.AppendChild(extents);
            transformGroup.AppendChild(childOffset);
            transformGroup.AppendChild(childExtents);
            groupShapeProperties.AppendChild(transformGroup);

            return groupShapeProperties;
        }

        private void AddSlideToPresentation(PresentationPart presentationPart, SlidePart slidePart)
        {
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
        }

        private void SavePresentation(PresentationPart presentationPart)
        {
            presentationPart.Presentation.Save();
        }

        private List<TableData> ParseHtmlTables(string htmlContent)
        {
            List<TableData> tables = new List<TableData>();

            var sectionMatches = Regex.Matches(htmlContent, @"<div[^>]*class=""[^""]*\b(card|table-sortable)\b[^""]*""[^>]*id=""([^""]+)""[^>]*>(.*?)</div>(?=\s*</div>)", RegexOptions.Singleline);

            foreach (Match sectionMatch in sectionMatches)
            {
                string sectionId = sectionMatch.Groups[2].Value;
                string sectionContent = sectionMatch.Groups[3].Value;

                string sectionTitle = ExtractSectionTitle(sectionContent, sectionId);

                var tableMatches = Regex.Matches(sectionContent, @"<table[^>]*>(.*?)</table>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

                foreach (Match tableMatch in tableMatches)
                {
                    string tableHtml = tableMatch.Groups[1].Value;
                    TableData tableData = ParseTable(tableHtml, sectionTitle);

                    if (tableData.Rows.Count > 0)
                    {
                        tables.Add(tableData);
                    }
                }
            }

            return tables;
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

            var buttonMatch = Regex.Match(sectionContent, @"<button[^>]*>(.*?)</button>", RegexOptions.Singleline);
            if (buttonMatch.Success)
            {
                return CleanHtmlText(buttonMatch.Groups[1].Value);
            }

            return fallbackId.Replace("_", " ");
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

        private string ExtractCompanyName(string htmlContent)
        {
            var licenseMatch = Regex.Match(htmlContent, @"<td[^>]*>([^<]+)</td>\s*<td[^>]*>EnterprisePlus</td>", RegexOptions.Singleline);
            if (licenseMatch.Success)
            {
                return CleanHtmlText(licenseMatch.Groups[1].Value);
            }

            var titleMatch = Regex.Match(htmlContent, @"<h1>\s*([^<]+)\s*</h1>", RegexOptions.Singleline);
            if (titleMatch.Success)
            {
                return CleanHtmlText(titleMatch.Groups[1].Value);
            }

            return "Company Name";
        }

        private string ExtractReportDate(string htmlContent)
        {
            // First try to extract from the image-container section
            var overlayMatch = Regex.Match(htmlContent, @"<p>([^<]+)\|([^<]+)</p>\s*</div>\s*<img", RegexOptions.Singleline);
            if (overlayMatch.Success)
            {
                string datePart = overlayMatch.Groups[1].Value.Trim();
                string reportPart = overlayMatch.Groups[2].Value.Trim();
                return $"{datePart} | {reportPart}";
            }

            // Fallback: extract from title
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
                        return $"{reportDateTime:MMMM dd yyyy} | 7 day report";
                    }
                }
            }

            return $"{DateTime.Now:MMMM dd yyyy} | 7 day report";
        }

        private Shape CreateOverlayBox(long x, long y, long width, long height)
        {
            Shape shape = new Shape();

            P.NonVisualShapeProperties nonVisualShapeProperties = new P.NonVisualShapeProperties(
                new P.NonVisualDrawingProperties() { Id = (UInt32Value)1U, Name = "OverlayBox" },
                new P.NonVisualShapeDrawingProperties(new A.ShapeLocks() { NoGrouping = true }),
                new P.ApplicationNonVisualDrawingProperties());

            P.ShapeProperties shapeProperties = new P.ShapeProperties();
            A.Transform2D transform2D = new A.Transform2D();
            A.Offset offset = new A.Offset() { X = x, Y = y };
            A.Extents extents = new A.Extents() { Cx = width, Cy = height };
            transform2D.AppendChild(offset);
            transform2D.AppendChild(extents);

            A.PresetGeometry presetGeometry = new A.PresetGeometry() { Preset = A.ShapeTypeValues.Rectangle };
            presetGeometry.AppendChild(new A.AdjustValueList());

            // Semi-transparent dark background
            A.SolidFill solidFill = new A.SolidFill();
            A.RgbColorModelHex rgbColor = new A.RgbColorModelHex() { Val = "000000" };
            rgbColor.AppendChild(new A.Alpha() { Val = 85000 }); // 85% opacity
            solidFill.AppendChild(rgbColor);

            // Veeam green left border
            A.Outline outline = new A.Outline() { Width = 38100 }; // 4pt border
            A.SolidFill outlineFill = new A.SolidFill(new A.RgbColorModelHex() { Val = VeeamGreen });
            outline.AppendChild(outlineFill);

            shapeProperties.AppendChild(transform2D);
            shapeProperties.AppendChild(presetGeometry);
            shapeProperties.AppendChild(solidFill);
            shapeProperties.AppendChild(outline);

            P.TextBody textBody = new P.TextBody();
            A.BodyProperties bodyProperties = new A.BodyProperties();
            A.ListStyle listStyle = new A.ListStyle();
            textBody.AppendChild(bodyProperties);
            textBody.AppendChild(listStyle);

            shape.AppendChild(nonVisualShapeProperties);
            shape.AppendChild(shapeProperties);
            shape.AppendChild(textBody);

            return shape;
        }

        private Shape CreateCoverTextShape(string text, long x, long y, long width, long height, int fontSize, bool isBold, bool isLarger)
        {
            Shape shape = new Shape();

            P.NonVisualShapeProperties nonVisualShapeProperties = new P.NonVisualShapeProperties(
                new P.NonVisualDrawingProperties() { Id = (UInt32Value)2U, Name = "CoverText" },
                new P.NonVisualShapeDrawingProperties(new A.ShapeLocks() { NoGrouping = true }),
                new P.ApplicationNonVisualDrawingProperties());

            P.ShapeProperties shapeProperties = new P.ShapeProperties();
            A.Transform2D transform2D = new A.Transform2D();
            A.Offset offset = new A.Offset() { X = x, Y = y };
            A.Extents extents = new A.Extents() { Cx = width, Cy = height };
            transform2D.AppendChild(offset);
            transform2D.AppendChild(extents);
            shapeProperties.AppendChild(transform2D);

            // No fill for text shapes
            shapeProperties.AppendChild(new A.NoFill());

            P.TextBody textBody = new P.TextBody();
            A.BodyProperties bodyProperties = new A.BodyProperties() 
            { 
                Anchor = A.TextAnchoringTypeValues.Top,
                Wrap = A.TextWrappingValues.None
            };
            A.ListStyle listStyle = new A.ListStyle();

            A.Paragraph paragraph = new A.Paragraph();
            A.ParagraphProperties paragraphProperties = new A.ParagraphProperties() { Alignment = A.TextAlignmentTypeValues.Left };
            paragraph.AppendChild(paragraphProperties);

            A.Run run = new A.Run();
            A.RunProperties runProperties = new A.RunProperties() 
            { 
                Language = "en-US", 
                FontSize = fontSize,
                Bold = isBold,
                Dirty = false
            };
            
            // White text color
            A.SolidFill solidFill = new A.SolidFill(new A.RgbColorModelHex() { Val = "FFFFFF" });
            if (!isLarger)
            {
                // Slightly transparent for smaller text (matching HTML rgba(255, 255, 255, 0.8))
                var rgbColor = new A.RgbColorModelHex() { Val = "FFFFFF" };
                rgbColor.AppendChild(new A.Alpha() { Val = 80000 });
                solidFill = new A.SolidFill(rgbColor);
            }
            runProperties.AppendChild(solidFill);
            
            A.Text textElement = new A.Text() { Text = text };

            run.AppendChild(runProperties);
            run.AppendChild(textElement);
            paragraph.AppendChild(run);

            textBody.AppendChild(bodyProperties);
            textBody.AppendChild(listStyle);
            textBody.AppendChild(paragraph);

            shape.AppendChild(nonVisualShapeProperties);
            shape.AppendChild(shapeProperties);
            shape.AppendChild(textBody);

            return shape;
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

            text = text.Replace("✓", "✓")
                       .Replace("☑", "☑")
                       .Replace("☐", "☐")
                       .Replace("⚠", "⚠");

            return text;
        }

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

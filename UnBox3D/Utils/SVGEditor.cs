using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using Svg;
using Svg.Transforms;
using System.Diagnostics;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;    // For ImageFormat
/* Values are in pixels
// SOMEONE GET ON THIS
// Margins don't work? It seems to crop out rather than provide that 2in buffer
// Needs a proper buffer instead of translating/offsetting the view
// OPTIMIZATION
// Rotation and eliminating empty boxes are nice to implement but not urgent 
*/

namespace UnBox3D.Utils
{
    public class SVGEditor
    {
        private const float MmToPx = 3.779527f;

        public static void ExportSvgPanels(string inputSvgPath, string outputDirectory, string filename, int pageIndex, float panelWidthMm, float panelHeightMm, float marginMm = 0f)
        {
            Debug.WriteLine($"panelWidthMM: {panelWidthMm} - panelHeightMM: {panelHeightMm}");

            Debug.WriteLine($"Processing Page: {pageIndex} - Filename: {inputSvgPath}");
            SvgDocument svgDocument = SvgDocument.Open(inputSvgPath);

            try
            {
                if (File.Exists(inputSvgPath))
                {
                    File.Delete(inputSvgPath);
                    Debug.WriteLine($"Deleted SVG file: {inputSvgPath}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to delete {inputSvgPath}: {ex.Message}");
            }

            float panelWidth = panelWidthMm * MmToPx;
            float panelHeight = panelHeightMm * MmToPx;
            float margin = marginMm * MmToPx;

            float svgWidth = svgDocument.Width.Value * MmToPx;
            float svgHeight = svgDocument.Height.Value * MmToPx;

            int numPanelsX = (int)Math.Ceiling((svgWidth - 2 * margin) / panelWidth);
            int numPanelsY = (int)Math.Ceiling((svgHeight - 2 * margin) / panelHeight);

            for (int x = 0; x < numPanelsX; x++)
            {
                for (int y = 0; y < numPanelsY; y++)
                {
                    float xOffset = x * panelWidth + margin;
                    float yOffset = y * panelHeight + margin;

                    SvgDocument panelDoc = new SvgDocument
                    {
                        Width = new SvgUnit(panelWidth),
                        Height = new SvgUnit(panelHeight)
                    };

                    foreach (SvgElement element in svgDocument.Children)
                    {
                        SvgElement clonedElement = (SvgElement)element.DeepCopy();
                        clonedElement.Transforms = new SvgTransformCollection
                        {
                            new SvgScale(MmToPx),
                            new SvgTranslate(-xOffset / MmToPx, -yOffset / MmToPx)
                        };
                        panelDoc.Children.Add(clonedElement);
                    }

                    string outputFilePath = Path.Combine(outputDirectory, $"{filename}_panel_page{pageIndex}_{x}_{y}.svg");
                    panelDoc.Write(outputFilePath);
                    Debug.WriteLine($"Exported panel to {outputFilePath} with x-offset: {xOffset}, y-offset: {yOffset}");
                }
            }
        }

        /// <summary>
        /// Overlays a dashed grid onto an SVG file showing corrugated cardboard sheet boundaries.
        /// The grid lines help plan cuts and reduce waste on the rotary cutter.
        /// </summary>
        public static void AddCardboardGrid(string svgFilePath, float sheetWidthMm, float sheetHeightMm)
        {
            if (sheetWidthMm <= 0 || sheetHeightMm <= 0)
                return;

            SvgDocument svgDoc = SvgDocument.Open(svgFilePath);

            // SVG document dimensions are in mm (as exported by Blender paper model addon)
            float docWidthMm = svgDoc.Width.Value;
            float docHeightMm = svgDoc.Height.Value;

            Debug.WriteLine($"AddCardboardGrid: doc={docWidthMm}x{docHeightMm}mm, sheet={sheetWidthMm}x{sheetHeightMm}mm");

            var gridGroup = new SvgGroup();
            gridGroup.ID = "cardboard-grid";

            // Vertical dashed lines at each sheet boundary
            int numSheetsX = (int)Math.Ceiling(docWidthMm / sheetWidthMm);
            for (int i = 1; i < numSheetsX; i++)
            {
                float xMm = i * sheetWidthMm;
                var line = new SvgLine
                {
                    StartX = new SvgUnit(SvgUnitType.Millimeter, xMm),
                    StartY = new SvgUnit(SvgUnitType.Millimeter, 0),
                    EndX = new SvgUnit(SvgUnitType.Millimeter, xMm),
                    EndY = new SvgUnit(SvgUnitType.Millimeter, docHeightMm),
                    Stroke = new SvgColourServer(Color.Blue),
                    StrokeWidth = new SvgUnit(SvgUnitType.Millimeter, 0.5f),
                    StrokeDashArray = new SvgUnitCollection { new SvgUnit(SvgUnitType.Millimeter, 5), new SvgUnit(SvgUnitType.Millimeter, 3) },
                    Opacity = 0.6f
                };
                gridGroup.Children.Add(line);
            }

            // Horizontal dashed lines at each sheet boundary
            int numSheetsY = (int)Math.Ceiling(docHeightMm / sheetHeightMm);
            for (int j = 1; j < numSheetsY; j++)
            {
                float yMm = j * sheetHeightMm;
                var line = new SvgLine
                {
                    StartX = new SvgUnit(SvgUnitType.Millimeter, 0),
                    StartY = new SvgUnit(SvgUnitType.Millimeter, yMm),
                    EndX = new SvgUnit(SvgUnitType.Millimeter, docWidthMm),
                    EndY = new SvgUnit(SvgUnitType.Millimeter, yMm),
                    Stroke = new SvgColourServer(Color.Blue),
                    StrokeWidth = new SvgUnit(SvgUnitType.Millimeter, 0.5f),
                    StrokeDashArray = new SvgUnitCollection { new SvgUnit(SvgUnitType.Millimeter, 5), new SvgUnit(SvgUnitType.Millimeter, 3) },
                    Opacity = 0.6f
                };
                gridGroup.Children.Add(line);
            }

            // Add sheet count labels in the corner of each sheet cell
            for (int ix = 0; ix < numSheetsX; ix++)
            {
                for (int iy = 0; iy < numSheetsY; iy++)
                {
                    float labelX = ix * sheetWidthMm + 2;
                    float labelY = iy * sheetHeightMm + 6;
                    var label = new SvgText($"Sheet {ix + 1},{iy + 1}")
                    {
                        X = new SvgUnitCollection { new SvgUnit(SvgUnitType.Millimeter, labelX) },
                        Y = new SvgUnitCollection { new SvgUnit(SvgUnitType.Millimeter, labelY) },
                        FontSize = new SvgUnit(SvgUnitType.Millimeter, 4),
                        Fill = new SvgColourServer(Color.Blue),
                        Opacity = 0.5f
                    };
                    gridGroup.Children.Add(label);
                }
            }

            svgDoc.Children.Add(gridGroup);
            svgDoc.Write(svgFilePath);

            int totalSheets = numSheetsX * numSheetsY;
            Debug.WriteLine($"AddCardboardGrid: {numSheetsX}x{numSheetsY} = {totalSheets} sheets needed");
        }

        /// <summary>
        /// Crops the SVG viewBox and dimensions to tightly fit the actual drawn content,
        /// removing empty whitespace. Adds a small padding margin around the content.
        /// Uses SvgVisualElement.Bounds which properly accounts for group transforms.
        /// Skips the cardboard grid overlay so we crop to just the model geometry.
        /// </summary>
        public static void CropToContent(string svgFilePath, float paddingMm = 10f)
        {
            SvgDocument svgDoc = SvgDocument.Open(svgFilePath);

            float docW = svgDoc.Width.Value;
            float docH = svgDoc.Height.Value;

            Debug.WriteLine($"CropToContent: original document size = {docW}x{docH}");

            // Walk all visual elements and collect their bounds.
            // SvgVisualElement.Bounds accounts for parent group transforms,
            // which is critical because Blender nests paths inside <g> groups.
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            bool foundContent = false;

            foreach (var element in svgDoc.Descendants())
            {
                // Skip our cardboard grid overlay — crop to model content only
                if (element is SvgGroup grp && grp.ID == "cardboard-grid")
                    continue;

                // Only process visual elements that have geometry (paths, lines, polygons, etc.)
                if (element is not SvgVisualElement visual)
                    continue;

                // Skip groups themselves — we want leaf elements with actual geometry
                if (element is SvgGroup)
                    continue;

                try
                {
                    var bounds = visual.Bounds;

                    // Skip degenerate or empty bounds
                    if (bounds.IsEmpty || (bounds.Width <= 0 && bounds.Height <= 0))
                        continue;

                    foundContent = true;
                    if (bounds.Left < minX) minX = bounds.Left;
                    if (bounds.Top < minY) minY = bounds.Top;
                    if (bounds.Right > maxX) maxX = bounds.Right;
                    if (bounds.Bottom > maxY) maxY = bounds.Bottom;
                }
                catch
                {
                    // Some elements may not support Bounds — skip them
                }
            }

            if (!foundContent)
            {
                Debug.WriteLine("CropToContent: No drawable content found, skipping crop");
                return;
            }

            Debug.WriteLine($"CropToContent: raw content bounds = ({minX:F1},{minY:F1})-({maxX:F1},{maxY:F1})");

            // Add padding around the content
            minX = Math.Max(0, minX - paddingMm);
            minY = Math.Max(0, minY - paddingMm);
            maxX = maxX + paddingMm;
            maxY = maxY + paddingMm;

            float cropW = maxX - minX;
            float cropH = maxY - minY;

            // Only crop if it actually reduces the size meaningfully
            if (cropW >= docW * 0.95f && cropH >= docH * 0.95f)
            {
                Debug.WriteLine($"CropToContent: content fills most of page, skipping crop");
                return;
            }

            Debug.WriteLine($"CropToContent: cropping from {docW:F0}x{docH:F0} to {cropW:F0}x{cropH:F0} (viewBox origin: {minX:F1},{minY:F1})");

            // Set viewBox to the content area and resize the document
            svgDoc.ViewBox = new SvgViewBox(minX, minY, cropW, cropH);
            svgDoc.Width = new SvgUnit(SvgUnitType.Millimeter, cropW);
            svgDoc.Height = new SvgUnit(SvgUnitType.Millimeter, cropH);

            svgDoc.Write(svgFilePath);
            Debug.WriteLine($"CropToContent: saved cropped SVG ({cropW:F0}x{cropH:F0}mm)");
        }

        public static bool ExportToPdf(string svgFile, PdfDocument pdf)
        {
            Debug.WriteLine($"Combining SVG: {svgFile}");

            try
            {
                SvgDocument svgDoc;
                using (var fs = File.OpenRead(svgFile))
                {
                    svgDoc = SvgDocument.Open<SvgDocument>(fs);
                }

                using var bmp = svgDoc.Draw();
                using var ms = new MemoryStream();
                bmp.Save(ms, ImageFormat.Png);
                ms.Position = 0;

                var page = pdf.AddPage();
                page.Width = XUnit.FromPoint(bmp.Width);
                page.Height = XUnit.FromPoint(bmp.Height);

                using var gfx = XGraphics.FromPdfPage(page);
                using var xImage = XImage.FromStream(() => ms);
                gfx.DrawImage(xImage, 0, 0);

                return true;
            }
            catch (Svg.Exceptions.SvgMemoryException ex)
            {
                Debug.WriteLine($"Caught SvgMemoryException: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error processing SVG: {ex.Message}");
                return false;
            }
        }
    }
}
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using Svg;
using Svg.Transforms;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;    // For ImageFormat
using System.Text.RegularExpressions;
using System.Xml.Linq;
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
        /// removing empty whitespace. Parses path coordinates directly from the SVG XML
        /// to compute bounds — does not rely on the SVG library's Bounds property.
        /// </summary>
        public static void CropToContent(string svgFilePath, float paddingMm = 10f)
        {
            // Parse as raw XML — much more reliable than the SVG library for bounds detection
            XNamespace ns = "http://www.w3.org/2000/svg";
            XDocument xdoc = XDocument.Load(svgFilePath);
            XElement root = xdoc.Root!;

            // Read current document dimensions from the root <svg> element
            string? widthAttr = root.Attribute("width")?.Value;
            string? heightAttr = root.Attribute("height")?.Value;
            if (widthAttr == null || heightAttr == null) return;

            // Strip "mm" suffix to get numeric values
            float docW = float.Parse(widthAttr.Replace("mm", ""), CultureInfo.InvariantCulture);
            float docH = float.Parse(heightAttr.Replace("mm", ""), CultureInfo.InvariantCulture);

            Debug.WriteLine($"CropToContent: original SVG = {docW}x{docH}mm");

            // Extract all numeric coordinates from <path d="..."> attributes.
            // The d attribute contains commands like "M0 38113 L50000 38113 Z"
            // We just need to find the min/max of all numbers to get the bounding box.
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            bool foundContent = false;

            // Regex to extract number pairs from SVG path d attributes
            // Matches patterns like: M100.5 200.3, L300 400, etc.
            var numberRegex = new Regex(@"-?\d+\.?\d*", RegexOptions.Compiled);

            foreach (var pathEl in xdoc.Descendants(ns + "path"))
            {
                string? d = pathEl.Attribute("d")?.Value;
                if (string.IsNullOrEmpty(d)) continue;

                // Skip sticker/tab paths (they extend beyond the actual model outline)
                string? cls = pathEl.Attribute("class")?.Value;
                if (cls == "sticker") continue;

                var matches = numberRegex.Matches(d);

                // Path coordinates come in X,Y pairs after move/line commands
                for (int i = 0; i < matches.Count - 1; i += 2)
                {
                    if (float.TryParse(matches[i].Value, CultureInfo.InvariantCulture, out float x) &&
                        float.TryParse(matches[i + 1].Value, CultureInfo.InvariantCulture, out float y))
                    {
                        foundContent = true;
                        if (x < minX) minX = x;
                        if (y < minY) minY = y;
                        if (x > maxX) maxX = x;
                        if (y > maxY) maxY = y;
                    }
                }
            }

            if (!foundContent)
            {
                Debug.WriteLine("CropToContent: No path content found, skipping crop");
                return;
            }

            Debug.WriteLine($"CropToContent: content bounds = ({minX:F1},{minY:F1})-({maxX:F1},{maxY:F1})");

            // Add padding
            minX = Math.Max(0, minX - paddingMm);
            minY = Math.Max(0, minY - paddingMm);
            maxX = maxX + paddingMm;
            maxY = maxY + paddingMm;

            float cropW = maxX - minX;
            float cropH = maxY - minY;

            // Only crop if it reduces size by at least 5%
            if (cropW >= docW * 0.95f && cropH >= docH * 0.95f)
            {
                Debug.WriteLine("CropToContent: content fills most of page, skipping crop");
                return;
            }

            Debug.WriteLine($"CropToContent: cropping {docW:F0}x{docH:F0} → {cropW:F0}x{cropH:F0}mm");

            // Rewrite the SVG root attributes directly in XML
            root.SetAttributeValue("width", $"{cropW:F2}mm");
            root.SetAttributeValue("height", $"{cropH:F2}mm");
            root.SetAttributeValue("viewBox", $"{minX:F2} {minY:F2} {cropW:F2} {cropH:F2}");

            xdoc.Save(svgFilePath);
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
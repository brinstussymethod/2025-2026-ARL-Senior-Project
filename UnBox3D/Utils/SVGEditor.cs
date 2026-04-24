using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using Svg;
using Svg.Transforms;
using System.Diagnostics;
using System.IO;
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

        // Safety cap — refuse to emit more than this many panels from a single export.
        // Guards against tiny board sizes entered against a huge unfolded net
        // (e.g. 50 mm boards against a 7 m net => ~20k files).
        private const int MaxPanels = 64;

        public static void ExportSvgPanels(string inputSvgPath, string outputDirectory, string filename, int pageIndex, float panelWidthMm, float panelHeightMm, float marginMm = 0f)
        {
            Debug.WriteLine($"panelWidthMM: {panelWidthMm} - panelHeightMM: {panelHeightMm}");

            Debug.WriteLine($"Processing Page: {pageIndex} - Filename: {inputSvgPath}");
            SvgDocument svgDocument = SvgDocument.Open(inputSvgPath);

            // Blender's paper_model addon emits width/height/viewBox in millimetres.
            // Stay in mm all the way through so the panel viewBox matches the source coordinate space.
            //
            // We tile the drawn content's bounding box rather than the Blender page itself.
            // The page grows via the retry loop in MainViewModel until the net fits, so it
            // almost always contains empty whitespace around the net — tiling the page
            // directly produced sliver panels over that whitespace.
            var contentBounds = ComputeContentBounds(svgDocument);
            float netWidthMm, netHeightMm, netOriginXMm, netOriginYMm;
            if (contentBounds.Width > 0 && contentBounds.Height > 0)
            {
                netWidthMm = contentBounds.Width;
                netHeightMm = contentBounds.Height;
                netOriginXMm = contentBounds.X;
                netOriginYMm = contentBounds.Y;
                Debug.WriteLine($"Content bbox: origin=({netOriginXMm:0.##}, {netOriginYMm:0.##}), size=({netWidthMm:0.##} x {netHeightMm:0.##}) mm");
            }
            else
            {
                Debug.WriteLine("No drawable content found; falling back to page dimensions.");
                netWidthMm = svgDocument.Width.Value;
                netHeightMm = svgDocument.Height.Value;
                netOriginXMm = 0f;
                netOriginYMm = 0f;
            }

            int numPanelsX = (int)Math.Ceiling((netWidthMm - 2 * marginMm) / panelWidthMm);
            int numPanelsY = (int)Math.Ceiling((netHeightMm - 2 * marginMm) / panelHeightMm);

            // Soft cap — if the requested board size would explode into too many panels,
            // emit the source SVG untiled instead of silently writing thousands of files.
            if (numPanelsX * numPanelsY > MaxPanels)
            {
                string fallbackPath = Path.Combine(outputDirectory, $"{filename}_panel_page{pageIndex}_0_0.svg");
                Debug.WriteLine($"Panel count {numPanelsX}x{numPanelsY}={numPanelsX * numPanelsY} exceeds cap of {MaxPanels}. Falling back to a single untiled SVG at {fallbackPath}.");

                try
                {
                    if (File.Exists(fallbackPath)) File.Delete(fallbackPath);
                    File.Move(inputSvgPath, fallbackPath);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Fallback copy failed: {ex.Message}");
                }
                return;
            }

            // Commit to tiling — the source SVG is consumed by this export.
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

            for (int x = 0; x < numPanelsX; x++)
            {
                for (int y = 0; y < numPanelsY; y++)
                {
                    // ViewBox is anchored at the content's origin so each panel shows a
                    // real slice of the net rather than a slice of the empty page.
                    float xOffsetMm = netOriginXMm + x * panelWidthMm + marginMm;
                    float yOffsetMm = netOriginYMm + y * panelHeightMm + marginMm;

                    SvgDocument panelDoc = new SvgDocument
                    {
                        Width = new SvgUnit(SvgUnitType.Millimeter, panelWidthMm),
                        Height = new SvgUnit(SvgUnitType.Millimeter, panelHeightMm),
                        ViewBox = new SvgViewBox(xOffsetMm, yOffsetMm, panelWidthMm, panelHeightMm),
                        Overflow = SvgOverflow.Hidden
                    };

                    foreach (SvgElement element in svgDocument.Children)
                    {
                        SvgElement clonedElement = (SvgElement)element.DeepCopy();
                        panelDoc.Children.Add(clonedElement);
                    }

                    string outputFilePath = Path.Combine(outputDirectory, $"{filename}_panel_page{pageIndex}_{x}_{y}.svg");
                    panelDoc.Write(outputFilePath);
                    Debug.WriteLine($"Exported panel to {outputFilePath} with x-offset: {xOffsetMm}mm, y-offset: {yOffsetMm}mm");
                }
            }
        }

        // Walk the SVG tree and union the bounding boxes of every drawable leaf
        // (paths, text, shapes). Containers (groups, the svg root) and non-visual
        // nodes (style, defs) are skipped because their own .Bounds would either
        // double-count descendants or be meaningless.
        private static System.Drawing.RectangleF ComputeContentBounds(SvgElement root)
        {
            System.Drawing.RectangleF acc = System.Drawing.RectangleF.Empty;
            AccumulateBounds(root, ref acc);
            return acc;
        }

        private static void AccumulateBounds(SvgElement element, ref System.Drawing.RectangleF acc)
        {
            if (element is SvgVisualElement visual
                && !(element is SvgGroup)
                && !(element is SvgFragment))
            {
                var b = visual.Bounds;
                if (b.Width > 0 || b.Height > 0)
                {
                    acc = acc.IsEmpty ? b : System.Drawing.RectangleF.Union(acc, b);
                }
            }

            foreach (var child in element.Children)
            {
                AccumulateBounds(child, ref acc);
            }
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
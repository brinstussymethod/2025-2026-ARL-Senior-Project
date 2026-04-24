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
            Debug.WriteLine($"panelWidthMM: {panelWidthMm} - panelHeightMM: {panelHeightMm} - marginMM: {marginMm}");

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

            // Whitespace semantics: marginMm is an empty (uncut) border on every side
            // of the physical board. Content lives only in the inner usable rectangle
            // (panelWidthMm - 2*marginMm) × (panelHeightMm - 2*marginMm), so the panel
            // grid tiles the net by usable area, not by physical board size.
            float usableWidthMm = panelWidthMm - 2 * marginMm;
            float usableHeightMm = panelHeightMm - 2 * marginMm;

            // Guard: margin too large leaves no room for content. Fall back to untiled
            // instead of hitting a divide-by-zero or emitting infinite panels.
            if (usableWidthMm <= 0f || usableHeightMm <= 0f)
            {
                string fallbackPath = Path.Combine(outputDirectory, $"{filename}_panel_page{pageIndex}_0_0.svg");
                Debug.WriteLine($"Margin {marginMm}mm leaves no usable area on a {panelWidthMm}x{panelHeightMm}mm board. Falling back to untiled SVG at {fallbackPath}.");

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

            int numPanelsX = (int)Math.Ceiling(netWidthMm / usableWidthMm);
            int numPanelsY = (int)Math.Ceiling(netHeightMm / usableHeightMm);

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
                    // Artboard spans the physical board (panelWidthMm × panelHeightMm).
                    // ViewBox is anchored so the usable region's top-left net coord
                    // lands at artboard position (marginMm, marginMm), leaving an
                    // empty marginMm band on every side.
                    float usableOriginXMm = netOriginXMm + x * usableWidthMm;
                    float usableOriginYMm = netOriginYMm + y * usableHeightMm;
                    float viewBoxXMm = usableOriginXMm - marginMm;
                    float viewBoxYMm = usableOriginYMm - marginMm;

                    SvgDocument panelDoc = new SvgDocument
                    {
                        Width = new SvgUnit(SvgUnitType.Millimeter, panelWidthMm),
                        Height = new SvgUnit(SvgUnitType.Millimeter, panelHeightMm),
                        ViewBox = new SvgViewBox(viewBoxXMm, viewBoxYMm, panelWidthMm, panelHeightMm),
                        Overflow = SvgOverflow.Hidden
                    };

                    // ClipPath constrains rendered content to the inner usable rectangle.
                    // Without this, features from neighbouring panels that fall inside
                    // this panel's viewBox would render into the margin band and end up
                    // as cuts on the physical board's edge area.
                    string clipId = $"panelClip_{x}_{y}";
                    var clipPath = new SvgClipPath { ID = clipId };
                    clipPath.Children.Add(new SvgRectangle
                    {
                        X = new SvgUnit(SvgUnitType.User, usableOriginXMm),
                        Y = new SvgUnit(SvgUnitType.User, usableOriginYMm),
                        Width = new SvgUnit(SvgUnitType.User, usableWidthMm),
                        Height = new SvgUnit(SvgUnitType.User, usableHeightMm)
                    });
                    var defs = new SvgDefinitionList();
                    defs.Children.Add(clipPath);
                    panelDoc.Children.Add(defs);

                    var contentGroup = new SvgGroup();
                    contentGroup.CustomAttributes["clip-path"] = $"url(#{clipId})";
                    foreach (SvgElement element in svgDocument.Children)
                    {
                        SvgElement clonedElement = (SvgElement)element.DeepCopy();
                        contentGroup.Children.Add(clonedElement);
                    }
                    panelDoc.Children.Add(contentGroup);

                    string outputFilePath = Path.Combine(outputDirectory, $"{filename}_panel_page{pageIndex}_{x}_{y}.svg");
                    panelDoc.Write(outputFilePath);
                    Debug.WriteLine($"Exported panel to {outputFilePath}: viewBox=({viewBoxXMm:0.##}, {viewBoxYMm:0.##}, {panelWidthMm:0.##}, {panelHeightMm:0.##}) mm, usable=({usableOriginXMm:0.##}, {usableOriginYMm:0.##}, {usableWidthMm:0.##}, {usableHeightMm:0.##}) mm");
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
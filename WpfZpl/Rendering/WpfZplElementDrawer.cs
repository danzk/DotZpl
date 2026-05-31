using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using BinaryKits.Zpl.Label;
using BinaryKits.Zpl.Label.Elements;
using BinaryKits.Zpl.Viewer;

using WpfZpl.ElementDrawers;

namespace WpfZpl.Rendering
{
    /// <summary>
    /// WPF analogue of <c>BinaryKits.Zpl.Viewer.ZplElementDrawer</c>. Builds the label as
    /// <see cref="Geometry"/> (so Field Reverse can be reproduced via geometry XOR) and rasterises
    /// to a PNG through a <see cref="RenderTargetBitmap"/> at a fixed 96 dpi (1 ZPL dot = 1 pixel).
    ///
    /// <para>Must be invoked on an STA thread (WPF requirement). The unit-test harness uses
    /// <c>[STATestMethod]</c>.</para>
    /// </summary>
    public class WpfZplElementDrawer
    {
        /// <summary>The registered element drawers (populated incrementally per implementation phase).</summary>
        public static IWpfElementDrawer[] ElementDrawers { get; } =
        [
            new GraphicBoxWpfElementDrawer(),
            new GraphicCircleWpfElementDrawer(),
            new GraphicEllipseWpfElementDrawer(),
            new GraphicDiagonalLineWpfElementDrawer(),
            new TextFieldWpfElementDrawer(),
            new FieldBlockWpfElementDrawer(),
            new GraphicSymbolWpfElementDrawer(),
            new Barcode128WpfElementDrawer(),
            new Barcode39WpfElementDrawer(),
            new Barcode93WpfElementDrawer(),
            new BarcodeAnsiCodabarWpfElementDrawer(),
            new Interleaved2of5WpfElementDrawer(),
            new BarcodeEAN13WpfElementDrawer(),
            new BarcodeUpcAWpfElementDrawer(),
            new BarcodeUpcEWpfElementDrawer(),
            new BarcodeUpcExtensionWpfElementDrawer(),
            new QrCodeWpfElementDrawer(),
            new DataMatrixWpfElementDrawer(),
            new AztecBarcodeWpfElementDrawer(),
            new Pdf417WpfElementDrawer(),
            new MaxiCodeWpfElementDrawer(),
            new GraphicFieldWpfElementDrawer(),
            new ImageMoveWpfElementDrawer(),
            new RecallGraphicWpfElementDrawer(),
        ];

        private readonly WpfDrawerOptions _options;
        private readonly IPrinterStorage _printerStorage;

        public WpfZplElementDrawer(IPrinterStorage printerStorage, WpfDrawerOptions? options = null)
        {
            _printerStorage = printerStorage;
            _options = options ?? new WpfDrawerOptions();
        }

        /// <summary>
        /// Build the label as a reusable, freezable WPF <see cref="DrawingGroup"/> — pure vector, no
        /// rasterisation. Render it in a control's <c>OnRender</c> via <c>dc.DrawDrawing(group)</c>,
        /// wrap it in a <see cref="DrawingImage"/> for an <c>Image.Source</c>, or rasterise it yourself.
        /// Coordinates are in ZPL dots (1 dot = 1 device-independent unit); apply a transform to scale.
        /// </summary>
        /// <param name="elements">Zpl elements</param>
        /// <param name="labelWidth">Label width in millimetres</param>
        /// <param name="labelHeight">Label height in millimetres</param>
        /// <param name="printDensityDpmm">Dots per millimetre</param>
        public DrawingGroup CreateDrawing(
            IEnumerable<ZplElementBase> elements,
            double labelWidth = 101.6,
            double labelHeight = 152.4,
            int printDensityDpmm = 8)
        {
            (int width, int height) = LabelSize(labelWidth, labelHeight, printDensityDpmm);
            (Geometry? background, Geometry? whiteRegion, List<WpfImageOp> images) =
                BuildContent(elements, width, height, printDensityDpmm);

            var group = new DrawingGroup();
#if WPF
            // Aliased edge mode is an attached property on the Drawing in WPF; on Avalonia it is applied
            // at rasterisation time (see Compat.RenderToPng) since it attaches to a Visual, not a Drawing.
            if (!_options.Antialias)
            {
                RenderOptions.SetEdgeMode(group, EdgeMode.Aliased);
            }
#endif

            using (DrawingContext dc = group.Open())
            {
                RenderContent(dc, width, height, background, whiteRegion, images);
            }

#if WPF
            // WPF Drawings are Freezable; freezing makes the vector content immutable and cheaper to
            // reuse. Avalonia has no Freezable, so the group is returned as-is.
            if (group.CanFreeze)
            {
                group.Freeze();
            }
#endif

            return group;
        }

        /// <summary>
        /// Draw the label directly into an existing <see cref="DrawingContext"/> — e.g. from a custom
        /// control's <c>OnRender</c>. Coordinates are in ZPL dots; apply a transform on your visual to scale.
        /// </summary>
        public void Draw(
            DrawingContext drawingContext,
            IEnumerable<ZplElementBase> elements,
            double labelWidth = 101.6,
            double labelHeight = 152.4,
            int printDensityDpmm = 8)
        {
            (int width, int height) = LabelSize(labelWidth, labelHeight, printDensityDpmm);
            (Geometry? background, Geometry? whiteRegion, List<WpfImageOp> images) =
                BuildContent(elements, width, height, printDensityDpmm);
            RenderContent(drawingContext, width, height, background, whiteRegion, images);
        }

        /// <summary>
        /// Convenience: rasterise the label to a PNG byte array via <see cref="RenderTargetBitmap"/> at
        /// 96 dpi. Prefer <see cref="CreateDrawing"/> / <see cref="Draw(DrawingContext, IEnumerable{ZplElementBase}, double, double, int)"/>
        /// for live WPF rendering; this is for file export and image-based testing. Must run on an STA thread.
        /// </summary>
        public byte[] DrawPng(
            IEnumerable<ZplElementBase> elements,
            double labelWidth = 101.6,
            double labelHeight = 152.4,
            int printDensityDpmm = 8)
        {
            (int width, int height) = LabelSize(labelWidth, labelHeight, printDensityDpmm);
            DrawingGroup drawing = CreateDrawing(elements, labelWidth, labelHeight, printDensityDpmm);
            return Compat.RenderToPng(drawing, width, height, _options.Antialias);
        }

        private static (int width, int height) LabelSize(double labelWidth, double labelHeight, int printDensityDpmm)
        {
            // Match Skia's Convert.ToInt32 (banker's) rounding so both backends produce identical dimensions.
            return (Convert.ToInt32(labelWidth * printDensityDpmm), Convert.ToInt32(labelHeight * printDensityDpmm));
        }

        /// <summary>Run the drawer pipeline, accumulating the black/white geometry regions and images.</summary>
        private (Geometry? background, Geometry? whiteRegion, List<WpfImageOp> images) BuildContent(
            IEnumerable<ZplElementBase> elements, int width, int height, int printDensityDpmm)
        {
            var context = new WpfDrawContext(width, height);

            Geometry? background = null;   // running black region
            Geometry? whiteRegion = null;  // running explicit-white region
            var images = new List<WpfImageOp>();

            InternationalFont internationalFont = InternationalFont.ZCP850;
            Point currentDefaultPosition = new(0, 0);

            foreach (ZplElementBase element in elements)
            {
                if (element is ZplChangeInternationalFont changeFont)
                {
                    internationalFont = changeFont.InternationalFont;
                    continue;
                }

                IWpfElementDrawer? drawer = ElementDrawers.SingleOrDefault(o => o.CanDraw(element));
                if (drawer == null)
                {
                    continue;
                }

                try
                {
                    drawer.Prepare(_printerStorage, context);
                    currentDefaultPosition = drawer.Draw(element, _options, currentDefaultPosition, internationalFont, printDensityDpmm);

                    Geometry? black = context.TakeBlack();
                    Geometry? white = context.TakeWhite();
                    images.AddRange(context.TakeImages());

                    // Two regions are maintained in document order: `background` (black pixels) and
                    // `whiteRegion` (explicit white pixels, needed only when the final background is
                    // transparent). Every element mutually excludes itself from the *other* region so
                    // that a later element correctly paints over an earlier one — e.g. a black border
                    // drawn last must show through white shapes drawn before it.
                    if (drawer.IsReverseDraw(element))
                    {
                        // Field Reverse (^FR) is a geometry XOR against the painted background.
                        // Skia's InvertDrawWhite normalises a white-drawn reverse shape to black before
                        // the XOR, so white vs black reverse have the identical geometric effect here.
                        background = Xor(background, black);
                        whiteRegion = Exclude(whiteRegion, black);
                    }
                    else
                    {
                        if (black != null)
                        {
                            background = Union(background, black);
                            whiteRegion = Exclude(whiteRegion, black);
                        }

                        if (white != null)
                        {
                            background = Exclude(background, white);
                            whiteRegion = Union(whiteRegion, white);
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw DescribeElementError(element, ex);
                }
            }

            return (background, whiteRegion, images);
        }

        /// <summary>Paint the accumulated regions/images into a drawing context (vector).</summary>
        private void RenderContent(DrawingContext dc, int width, int height, Geometry? background, Geometry? whiteRegion, List<WpfImageOp> images)
        {
            // Always paint the full label rectangle so the resulting Drawing's bounds are the whole
            // label (white when opaque; otherwise an invisible Transparent fill just to set bounds, so
            // a DrawingImage keeps the correct size/aspect instead of collapsing to the inked content).
            dc.DrawRectangle(_options.OpaqueBackground ? Brushes.White : Brushes.Transparent, null, new Rect(0, 0, width, height));

            if (background != null)
            {
                dc.DrawGeometry(Brushes.Black, null, background);
            }

            if (whiteRegion != null)
            {
                dc.DrawGeometry(Brushes.White, null, whiteRegion);
            }

            foreach (WpfImageOp op in images)
            {
                if (op.Transform.IsIdentity)
                {
                    dc.DrawImage(op.Image, op.Destination);
                }
                else
                {
                    using (dc.PushTransform(op.Transform))
                    {
                        dc.DrawImage(op.Image, op.Destination);
                    }
                }
            }
        }

        private static Geometry? Union(Geometry? a, Geometry? b)
        {
            if (a == null) return b;
            if (b == null) return a;
            return new CombinedGeometry(GeometryCombineMode.Union, a, b);
        }

        private static Geometry? Xor(Geometry? a, Geometry? b)
        {
            if (a == null) return b;
            if (b == null) return a;
            return new CombinedGeometry(GeometryCombineMode.Xor, a, b);
        }

        private static Geometry? Exclude(Geometry? a, Geometry? b)
        {
            if (a == null) return null;
            if (b == null) return a;
            return new CombinedGeometry(GeometryCombineMode.Exclude, a, b);
        }

        private static Exception DescribeElementError(ZplElementBase element, Exception ex)
        {
            return element switch
            {
                ZplBarcode barcode => new Exception($"Error on zpl element \"{barcode.Content}\": {ex.Message}", ex),
                ZplDataMatrix dataMatrix => new Exception($"Error on zpl element \"{dataMatrix.Content}\": {ex.Message}", ex),
                _ => ex,
            };
        }
    }
}

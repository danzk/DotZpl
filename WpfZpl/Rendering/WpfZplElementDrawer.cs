using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

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
        /// Draw the label to a PNG byte array.
        /// </summary>
        /// <param name="elements">Zpl elements</param>
        /// <param name="labelWidth">Label width in millimetres</param>
        /// <param name="labelHeight">Label height in millimetres</param>
        /// <param name="printDensityDpmm">Dots per millimetre</param>
        public byte[] Draw(
            IEnumerable<ZplElementBase> elements,
            double labelWidth = 101.6,
            double labelHeight = 152.4,
            int printDensityDpmm = 8)
        {
            // Match Skia's Convert.ToInt32 (banker's) rounding so both backends produce identical dimensions.
            int width = Convert.ToInt32(labelWidth * printDensityDpmm);
            int height = Convert.ToInt32(labelHeight * printDensityDpmm);

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

            return Rasterize(width, height, background, whiteRegion, images);
        }

        private byte[] Rasterize(int width, int height, Geometry? background, Geometry? whiteRegion, List<WpfImageOp> images)
        {
            var visual = new DrawingVisual();
            if (!_options.Antialias)
            {
                RenderOptions.SetEdgeMode(visual, EdgeMode.Aliased);
            }

            using (DrawingContext dc = visual.RenderOpen())
            {
                if (_options.OpaqueBackground)
                {
                    dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));
                }

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
                    bool pushed = op.Transform != null && op.Transform != Transform.Identity;
                    if (pushed)
                    {
                        dc.PushTransform(op.Transform);
                    }

                    dc.DrawImage(op.Image, op.Destination);

                    if (pushed)
                    {
                        dc.Pop();
                    }
                }
            }

            var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(visual);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));
            using var ms = new MemoryStream();
            encoder.Save(ms);
            return ms.ToArray();
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

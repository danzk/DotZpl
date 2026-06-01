using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using BinaryKits.Zpl.Label;
using BinaryKits.Zpl.Label.Elements;
using BinaryKits.Zpl.Analyzer;

using DotZpl.ElementDrawers;

namespace DotZpl.Rendering
{
    /// <summary>
    /// WPF analogue of <c>BinaryKits.Zpl.Viewer.ZplRenderer</c>. Builds the label as
    /// <see cref="Geometry"/> (so Field Reverse can be reproduced via geometry XOR) and rasterises
    /// to a PNG through a <see cref="RenderTargetBitmap"/> at a fixed 96 dpi (1 ZPL dot = 1 pixel).
    ///
    /// <para>Must be invoked on an STA thread (WPF requirement). The unit-test harness uses
    /// <c>[STATestMethod]</c>.</para>
    /// </summary>
    public class ZplRenderer
    {
        /// <summary>The registered element drawers (populated incrementally per implementation phase).</summary>
        public static IElementDrawer[] ElementDrawers { get; } =
        [
            new GraphicBoxElementDrawer(),
            new GraphicCircleElementDrawer(),
            new GraphicEllipseElementDrawer(),
            new GraphicDiagonalLineElementDrawer(),
            new TextFieldElementDrawer(),
            new FieldBlockElementDrawer(),
            new GraphicSymbolElementDrawer(),
            new Barcode128ElementDrawer(),
            new Barcode39ElementDrawer(),
            new Barcode93ElementDrawer(),
            new BarcodeAnsiCodabarElementDrawer(),
            new Interleaved2of5ElementDrawer(),
            new BarcodeEAN13ElementDrawer(),
            new BarcodeUpcAElementDrawer(),
            new BarcodeUpcEElementDrawer(),
            new BarcodeUpcExtensionElementDrawer(),
            new QrCodeElementDrawer(),
            new DataMatrixElementDrawer(),
            new AztecBarcodeElementDrawer(),
            new Pdf417ElementDrawer(),
            new MaxiCodeElementDrawer(),
            new GraphicFieldElementDrawer(),
            new ImageMoveElementDrawer(),
            new RecallGraphicElementDrawer(),
        ];

        private readonly ZplRendererOptions _options;
        private readonly IPrinterStorage _printerStorage;

        public ZplRenderer(IPrinterStorage printerStorage, ZplRendererOptions? options = null)
        {
            _printerStorage = printerStorage;
            _options = options ?? new ZplRendererOptions();
        }

        /// <summary>
        /// Build the label into a reusable <see cref="LabelDrawing"/> — the canonical, framework-
        /// agnostic builder. Hold onto the result and call its <c>Draw</c> per render pass; coordinates
        /// are in ZPL dots (1 dot = 1 device-independent unit). Apply a transform to scale.
        /// </summary>
        /// <param name="elements">Zpl elements</param>
        /// <param name="labelWidth">Label width in millimetres</param>
        /// <param name="labelHeight">Label height in millimetres</param>
        /// <param name="printDensityDpmm">Dots per millimetre</param>
        public LabelDrawing CreateLabelDrawing(
            IEnumerable<ZplElementBase> elements,
            double labelWidth = 101.6,
            double labelHeight = 152.4,
            int printDensityDpmm = 8)
        {
            (int width, int height) = LabelSize(labelWidth, labelHeight, printDensityDpmm);
            List<LabelOp> ops = BuildContent(elements, width, height, printDensityDpmm);
            return new LabelDrawing(width, height, ops, _options.OpaqueBackground);
        }

#if WPF
        /// <summary>
        /// Build the label as a reusable, freezable WPF <see cref="DrawingGroup"/> — pure vector,
        /// no rasterisation. Render it in a control's <c>OnRender</c> via <c>dc.DrawDrawing(group)</c>,
        /// wrap it in a <see cref="DrawingImage"/> for an <c>Image.Source</c>, or rasterise it yourself.
        /// Coordinates are in ZPL dots (1 dot = 1 device-independent unit); apply a transform to scale.
        ///
        /// <para>WPF only. The equivalent on Avalonia is <see cref="CreateLabelDrawing"/>, which is
        /// also a fine fit for WPF — <see cref="DrawingGroup"/>-shaped content is the only reason
        /// you'd reach for this method specifically.</para>
        /// </summary>
        public DrawingGroup CreateDrawing(
            IEnumerable<ZplElementBase> elements,
            double labelWidth = 101.6,
            double labelHeight = 152.4,
            int printDensityDpmm = 8)
        {
            LabelDrawing label = CreateLabelDrawing(elements, labelWidth, labelHeight, printDensityDpmm);
            var group = new DrawingGroup();
            if (!_options.Antialias)
            {
                RenderOptions.SetEdgeMode(group, EdgeMode.Aliased);
            }

            using (DrawingContext dc = group.Open())
            {
                label.Draw(dc);
            }

            // Freezable: makes the vector content immutable and cheaper to reuse.
            if (group.CanFreeze)
            {
                group.Freeze();
            }
            return group;
        }
#endif

        /// <summary>
        /// Draw the label directly into an existing <see cref="DrawingContext"/> — e.g. from a custom
        /// control's render pass. Coordinates are in ZPL dots; apply a transform on your visual to scale.
        /// Equivalent to <c>CreateLabelDrawing(...).Draw(drawingContext)</c>; cache the
        /// <see cref="LabelDrawing"/> yourself if the same label renders repeatedly.
        /// </summary>
        public void Draw(
            DrawingContext drawingContext,
            IEnumerable<ZplElementBase> elements,
            double labelWidth = 101.6,
            double labelHeight = 152.4,
            int printDensityDpmm = 8)
            => CreateLabelDrawing(elements, labelWidth, labelHeight, printDensityDpmm).Draw(drawingContext);

        /// <summary>
        /// Convenience: rasterise the label to a PNG byte array via <see cref="RenderTargetBitmap"/> at
        /// 96 dpi. For live rendering prefer <see cref="CreateLabelDrawing"/> and its <c>Draw</c>; this
        /// is for file export and image-based testing. On WPF this must run on an STA thread.
        /// </summary>
        public byte[] DrawPng(
            IEnumerable<ZplElementBase> elements,
            double labelWidth = 101.6,
            double labelHeight = 152.4,
            int printDensityDpmm = 8)
        {
            LabelDrawing label = CreateLabelDrawing(elements, labelWidth, labelHeight, printDensityDpmm);
            return Compat.RenderToPng(label, _options.Antialias);
        }

        private static (int width, int height) LabelSize(double labelWidth, double labelHeight, int printDensityDpmm)
        {
            // Match Skia's Convert.ToInt32 (banker's) rounding so both backends produce identical dimensions.
            return (Convert.ToInt32(labelWidth * printDensityDpmm), Convert.ToInt32(labelHeight * printDensityDpmm));
        }

        /// <summary>
        /// Run the drawer pipeline, emitting an ordered display list of fill / image ops. Drawing the
        /// list in document order reproduces ZPL compositing by the painter's algorithm: additive
        /// black unions visually (no boolean geometry, no winding cancellation), white over black
        /// erases, images layer in order.
        ///
        /// <para>Field Reverse (<c>^FR</c>) is the one op that needs geometry boolean math, because a
        /// DrawingContext can't raster-XOR: a reverse field XORs against everything painted under it.
        /// We reproduce that as two paint ops — erase (white) where the field overlaps the visible
        /// black, and add (black) where it doesn't — using the visible-black region at that point. That
        /// region is folded lazily from the ops emitted so far the first time a reverse element appears
        /// and then maintained incrementally, so labels without <c>^FR</c> (the overwhelming majority)
        /// run boolean-op-free.</para>
        /// </summary>
        private List<LabelOp> BuildContent(
            IEnumerable<ZplElementBase> elements, int width, int height, int printDensityDpmm)
        {
            var context = new DrawContext(width, height);
            var ops = new List<LabelOp>();

            Geometry? visibleBlack = null;   // composite black region, only tracked once a reverse needs it
            bool trackingBlack = false;

            InternationalFont internationalFont = InternationalFont.ZCP850;
            Point currentDefaultPosition = new(0, 0);

            foreach (ZplElementBase element in elements)
            {
                if (element is ZplChangeInternationalFont changeFont)
                {
                    internationalFont = changeFont.InternationalFont;
                    continue;
                }

                IElementDrawer? drawer = ElementDrawers.SingleOrDefault(o => o.CanDraw(element));
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

                    if (drawer.IsReverseDraw(element))
                    {
                        // ^FR: XOR the field against the visible black under it. Skia's InvertDrawWhite
                        // normalises a white-drawn reverse to black before the XOR, so white vs black
                        // reverse have the identical effect here — both go through `black`.
                        if (black != null)
                        {
                            if (!trackingBlack)
                            {
                                visibleBlack = FoldVisibleBlack(ops);
                                trackingBlack = true;
                            }

                            if (visibleBlack == null)
                            {
                                ops.Add(LabelOp.Black(black));     // nothing underneath: reverse is all black
                                visibleBlack = black;
                            }
                            else
                            {
                                Geometry erase = Compat.Combine(black, visibleBlack, GeometryCombineMode.Intersect);
                                Geometry add = Compat.Combine(black, visibleBlack, GeometryCombineMode.Exclude);
                                ops.Add(LabelOp.WhiteFill(erase));  // erase where the field overlaps black
                                ops.Add(LabelOp.Black(add));        // add black where it doesn't
                                visibleBlack = Xor(visibleBlack, black);
                            }
                        }
                    }
                    else
                    {
                        if (black != null)
                        {
                            ops.Add(LabelOp.Black(black));
                            if (trackingBlack) visibleBlack = Union(visibleBlack, black);
                        }

                        if (white != null)
                        {
                            ops.Add(LabelOp.WhiteFill(white));
                            if (trackingBlack) visibleBlack = Exclude(visibleBlack, white);
                        }
                    }

                    foreach (ImageOp image in context.TakeImages())
                    {
                        ops.Add(LabelOp.Img(image));
                    }
                }
                catch (Exception ex)
                {
                    throw DescribeElementError(element, ex);
                }
            }

            return ops;
        }

        /// <summary>Replay the ops emitted so far to recover the visible black region (black fills union, white fills erase).</summary>
        private static Geometry? FoldVisibleBlack(List<LabelOp> ops)
        {
            Geometry? black = null;
            foreach (LabelOp op in ops)
            {
                if (op.IsImage)
                {
                    continue;
                }

                black = op.White ? Exclude(black, op.Fill) : Union(black, op.Fill);
            }

            return black;
        }

        // Boolean helpers — used only on the rare ^FR path (the additive pipeline paints instead).
        // Compat.Combine evaluates eagerly on WPF (flat PathGeometry) and lazily on Avalonia.
        private static Geometry? Union(Geometry? a, Geometry? b)
        {
            if (a == null) return b;
            if (b == null) return a;
            return Compat.Combine(a, b, GeometryCombineMode.Union);
        }

        private static Geometry? Xor(Geometry? a, Geometry? b)
        {
            if (a == null) return b;
            if (b == null) return a;
            return Compat.Combine(a, b, GeometryCombineMode.Xor);
        }

        private static Geometry? Exclude(Geometry? a, Geometry? b)
        {
            if (a == null) return null;
            if (b == null) return a;
            return Compat.Combine(a, b, GeometryCombineMode.Exclude);
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

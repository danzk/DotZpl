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
            (Geometry? background, Geometry? whiteRegion, List<ImageOp> images) =
                BuildContent(elements, width, height, printDensityDpmm);
            return new LabelDrawing(width, height, background, whiteRegion, images, _options.OpaqueBackground);
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

        /// <summary>Run the drawer pipeline, accumulating the black/white geometry regions and images.</summary>
        private (Geometry? background, Geometry? whiteRegion, List<ImageOp> images) BuildContent(
            IEnumerable<ZplElementBase> elements, int width, int height, int printDensityDpmm)
        {
            var context = new DrawContext(width, height);

            Geometry? background = null;   // running black region
            Geometry? whiteRegion = null;  // running explicit-white region
            var images = new List<ImageOp>();

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

        /// <summary>
        /// Accumulate <paramref name="b"/> additively into <paramref name="a"/>.
        ///
        /// <para><b>WPF:</b> uses a flat <see cref="GeometryGroup"/> with NonZero fill rather than
        /// nesting another <see cref="CombinedGeometry"/> — the boolean Union evaluator is wasted
        /// work for the common case of non-overlapping label elements, and WPF correctly flattens
        /// child geometries' path data so children with their own holes (a CombinedGeometry border
        /// ring, a multi-figure text glyph) keep them under the group's FillRule.</para>
        ///
        /// <para><b>Avalonia:</b> stays with <see cref="CombinedGeometry"/>. Avalonia 12's
        /// GeometryGroup rasterises each child as an independently-filled shape and unions the
        /// painted regions; a child's own holes (CombinedGeometry's Boolean op, a glyph path's
        /// reverse-winding sub-figures) are lost, so a hollow GraphicBox border or the inside of
        /// the letter 'o' re-fills. Until Avalonia's rasteriser flattens children the same way WPF
        /// does, the safe path is the boolean Union — slower but correct.</para>
        /// </summary>
        private static Geometry? Union(Geometry? a, Geometry? b)
        {
            if (a == null) return b;
            if (b == null) return a;

#if WPF
            if (a is GeometryGroup group && group.FillRule == Compat.NonZeroFill && IsMutableAccumulator(group))
            {
                group.Children.Add(b);
                return group;
            }

            var fresh = new GeometryGroup { FillRule = Compat.NonZeroFill };
            fresh.Children.Add(a);
            fresh.Children.Add(b);
            return fresh;
#else
            return new CombinedGeometry(GeometryCombineMode.Union, a, b);
#endif
        }

#if WPF
        /// <summary>
        /// Whether <paramref name="g"/> can safely accept more <see cref="GeometryGroup.Children"/>
        /// — i.e. not frozen, and no enclosing transform that would then incorrectly apply to the
        /// newly appended child.
        /// </summary>
        private static bool IsMutableAccumulator(GeometryGroup g)
        {
            if (g.Transform != null && !g.Transform.Value.IsIdentity) return false;
            return !g.IsFrozen;
        }
#endif

        // Xor / Exclude genuinely need the boolean op — the GeometryGroup trick above doesn't apply.
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

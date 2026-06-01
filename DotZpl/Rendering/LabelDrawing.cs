using System.Collections.Generic;

namespace DotZpl.Rendering
{
    /// <summary>
    /// One entry in a <see cref="LabelDrawing"/>'s display list: either a geometry fill (black or
    /// white) or an image draw. Replayed in document order, which reproduces ZPL compositing by the
    /// painter's algorithm — black over black unions, white over black erases, images layer in order —
    /// with no boolean geometry ops.
    /// </summary>
    internal readonly struct LabelOp
    {
        /// <summary>Fill geometry; <c>null</c> marks an image op (see <see cref="Image"/>).</summary>
        public Geometry? Fill { get; }

        /// <summary>Fill colour for a geometry op: <c>true</c> white, <c>false</c> black.</summary>
        public bool White { get; }

        /// <summary>The image to draw; valid only when <see cref="Fill"/> is <c>null</c>.</summary>
        public ImageOp Image { get; }

        private LabelOp(Geometry? fill, bool white, ImageOp image)
        {
            Fill = fill;
            White = white;
            Image = image;
        }

        public bool IsImage => Fill == null;

        public static LabelOp Black(Geometry fill) => new(fill, false, default);
        public static LabelOp WhiteFill(Geometry fill) => new(fill, true, default);
        public static LabelOp Img(ImageOp image) => new(null, false, image);
    }

    /// <summary>
    /// A label baked down to an ordered display list of fill / image ops plus the label's pixel
    /// dimensions. Decoupled from any UI-framework geometry container, so the same built artifact can
    /// be drawn straight into a live <see cref="DrawingContext"/> (a control's render pass, a
    /// <see cref="RenderTargetBitmap"/>, etc.) on both backends.
    ///
    /// <para>Drawing replays the ops in document order — the painter's algorithm — so overlapping
    /// additive elements simply union visually (no boolean geometry, no fill-rule winding cancellation),
    /// and white-over-black / image layering follow from draw order.</para>
    ///
    /// <para>Build once via <see cref="ZplRenderer.CreateLabelDrawing"/>, reuse across renders. Pure
    /// data — no caching, no thread affinity.</para>
    /// </summary>
    public sealed class LabelDrawing
    {
        /// <summary>Label width in dots (1 dot = 1 device-independent unit).</summary>
        public int Width { get; }

        /// <summary>Label height in dots.</summary>
        public int Height { get; }

        private readonly IReadOnlyList<LabelOp> _ops;
        private readonly bool _opaqueBackground;

        internal LabelDrawing(int width, int height, IReadOnlyList<LabelOp> ops, bool opaqueBackground)
        {
            Width = width;
            Height = height;
            _ops = ops;
            _opaqueBackground = opaqueBackground;
        }

        /// <summary>
        /// Draw the label into a live <see cref="DrawingContext"/> — a control's render pass or a
        /// <see cref="RenderTargetBitmap"/> drawing context. Must not be called against a recording
        /// context (e.g. <c>DrawingGroup.Open()</c> on Avalonia), which doesn't implement image draws.
        /// </summary>
        public void Draw(DrawingContext context)
        {
            // Always paint the full label rectangle so the rendered region is the whole label (white
            // when opaque; otherwise an invisible Transparent fill that establishes bounds without
            // tinting the content).
            context.DrawRectangle(_opaqueBackground ? Brushes.White : Brushes.Transparent, null,
                new Rect(0, 0, Width, Height));

            foreach (LabelOp op in _ops)
            {
                if (op.IsImage)
                {
                    ImageOp img = op.Image;
                    if (img.Transform.IsIdentity)
                    {
                        context.DrawImage(img.Image, img.Destination);
                    }
                    else
                    {
                        using (context.PushTransform(img.Transform))
                        {
                            context.DrawImage(img.Image, img.Destination);
                        }
                    }
                }
                else
                {
                    context.DrawGeometry(op.White ? Brushes.White : Brushes.Black, null, op.Fill!);
                }
            }
        }
    }
}

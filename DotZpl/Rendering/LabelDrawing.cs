using System.Collections.Generic;

namespace DotZpl.Rendering
{
    /// <summary>
    /// A label baked down to its rendered form: the composed black/white geometry regions, the
    /// image ops with their transforms, and the label's pixel dimensions. Decoupled from the
    /// UI-framework <c>DrawingGroup</c> so the same built artifact can be drawn straight into a
    /// live <see cref="DrawingContext"/> (a control's render pass, a <see cref="RenderTargetBitmap"/>,
    /// etc.) on both backends — including Avalonia, whose <c>DrawingGroup</c> recording context
    /// does not implement <c>DrawBitmap</c>.
    ///
    /// <para>Build once via <see cref="ZplRenderer.CreateLabelDrawing"/>, reuse across renders.
    /// Pure data — no caching, no thread affinity.</para>
    /// </summary>
    public sealed class LabelDrawing
    {
        /// <summary>Label width in dots (1 dot = 1 device-independent unit).</summary>
        public int Width { get; }

        /// <summary>Label height in dots.</summary>
        public int Height { get; }

        private readonly Geometry? _background;
        private readonly Geometry? _whiteRegion;
        private readonly IReadOnlyList<ImageOp> _images;
        private readonly bool _opaqueBackground;

        internal LabelDrawing(int width, int height, Geometry? background, Geometry? whiteRegion,
                              IReadOnlyList<ImageOp> images, bool opaqueBackground)
        {
            Width = width;
            Height = height;
            _background = background;
            _whiteRegion = whiteRegion;
            _images = images;
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

            if (_background != null)
            {
                context.DrawGeometry(Brushes.Black, null, _background);
            }

            if (_whiteRegion != null)
            {
                context.DrawGeometry(Brushes.White, null, _whiteRegion);
            }

            foreach (ImageOp op in _images)
            {
                if (op.Transform.IsIdentity)
                {
                    context.DrawImage(op.Image, op.Destination);
                }
                else
                {
                    using (context.PushTransform(op.Transform))
                    {
                        context.DrawImage(op.Image, op.Destination);
                    }
                }
            }
        }
    }
}

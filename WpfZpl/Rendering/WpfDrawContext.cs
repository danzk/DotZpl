using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WpfZpl.Rendering
{
    /// <summary>
    /// A single image draw operation (for <c>^GF</c> / <c>^XG</c> / <c>~DG</c> elements that
    /// have no geometry equivalent).
    /// </summary>
    public readonly struct WpfImageOp
    {
        public WpfImageOp(BitmapSource image, Rect destination, Transform transform)
        {
            Image = image;
            Destination = destination;
            Transform = transform;
        }

        public BitmapSource Image { get; }
        public Rect Destination { get; }
        public Transform Transform { get; }
    }

    /// <summary>
    /// Replaces Skia's <c>SKCanvas</c>. Element drawers do not paint immediately; they accumulate
    /// <see cref="Geometry"/> into per-element buckets so the orchestrator can union, exclude
    /// (white-over-black) or XOR (Field Reverse <c>^FR</c>) them against the running label.
    /// A transform stack mirrors <c>SKCanvas.Concat</c> + <c>SKAutoCanvasRestore</c>.
    /// </summary>
    public sealed class WpfDrawContext
    {
        private readonly List<Geometry> _black = new();
        private readonly List<Geometry> _white = new();
        private readonly List<WpfImageOp> _images = new();

        private readonly Stack<Matrix> _transformStack = new();
        private Matrix _current = Matrix.Identity;

        public WpfDrawContext(int pixelWidth, int pixelHeight)
        {
            PixelWidth = pixelWidth;
            PixelHeight = pixelHeight;
        }

        public int PixelWidth { get; }
        public int PixelHeight { get; }

        /// <summary>The composed transform currently in effect (innermost push applied first).</summary>
        public Transform CurrentTransform =>
            _current.IsIdentity ? Transform.Identity : new MatrixTransform(_current);

        /// <summary>Push a transform; mirrors <c>SKCanvas.Concat(matrix)</c>.</summary>
        public void PushTransform(Transform t)
        {
            _transformStack.Push(_current);
            Matrix m = _current;
            m.Prepend(t.Value); // t becomes innermost (applied first to geometry coordinates)
            _current = m;
        }

        /// <summary>Pop the most recently pushed transform; mirrors leaving an <c>SKAutoCanvasRestore</c> scope.</summary>
        public void Pop()
        {
            _current = _transformStack.Count > 0 ? _transformStack.Pop() : Matrix.Identity;
        }

        /// <summary>Add a black (foreground) geometry, baking in the current transform.</summary>
        public void AddBlack(Geometry g)
        {
            if (ReferenceEquals(g, Geometry.Empty))
            {
                return;
            }

            _black.Add(ApplyCurrent(g));
        }

        /// <summary>Add an explicit white geometry (e.g. <c>^GB...,W</c> without <c>^FR</c>).</summary>
        public void AddWhite(Geometry g)
        {
            if (ReferenceEquals(g, Geometry.Empty))
            {
                return;
            }

            _white.Add(ApplyCurrent(g));
        }

        /// <summary>Add a raster image draw operation, baking in the current transform.</summary>
        public void AddImage(BitmapSource image, Rect destination)
        {
            _images.Add(new WpfImageOp(image, destination, CurrentTransform));
        }

        private Geometry ApplyCurrent(Geometry g)
        {
            if (_current.IsIdentity)
            {
                return g;
            }

            var ct = new MatrixTransform(_current);

            // Fast path: a mutable geometry with no transform of its own takes the transform directly.
            if (!g.IsFrozen && (g.Transform == null || g.Transform.Value.IsIdentity))
            {
                g.Transform = ct;
                return g;
            }

            // Frozen (e.g. Geometry.Empty) or already-transformed (e.g. scaleX): wrap without mutating.
            // The wrapper applies the inner transform first, then the current one (rotation).
            var group = new GeometryGroup();
            group.Children.Add(g);
            group.Transform = ct;
            return group;
        }

        // --- consumed by the orchestrator after each element's Draw() ---

        /// <summary>Combine and clear the current element's black geometry (null when none).</summary>
        public Geometry? TakeBlack() => Combine(_black);

        /// <summary>Combine and clear the current element's white geometry (null when none).</summary>
        public Geometry? TakeWhite() => Combine(_white);

        /// <summary>Return and clear the current element's image operations.</summary>
        public IReadOnlyList<WpfImageOp> TakeImages()
        {
            if (_images.Count == 0)
            {
                return System.Array.Empty<WpfImageOp>();
            }

            var copy = _images.ToArray();
            _images.Clear();
            return copy;
        }

        private static Geometry? Combine(List<Geometry> parts)
        {
            if (parts.Count == 0)
            {
                return null;
            }

            Geometry result;
            if (parts.Count == 1)
            {
                result = parts[0];
            }
            else
            {
                var group = new GeometryGroup { FillRule = FillRule.Nonzero };
                foreach (Geometry g in parts)
                {
                    group.Children.Add(g);
                }

                result = group;
            }

            parts.Clear();
            return result;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Windows.Input;

using BinaryKits.Zpl.Label.Elements;
using BinaryKits.Zpl.Analyzer;
using BinaryKits.Zpl.Analyzer.Models;

using DotZpl.Rendering;

namespace DotZpl.Controls
{
    /// <summary>
    /// A WPF control that renders a ZPL string as native, scalable vector content via
    /// <see cref="ZplRenderer"/>. Supports fit-to-control scaling (<see cref="Stretch"/>) and
    /// arbitrary <see cref="RotationAngle"/> of the whole label.
    ///
    /// <para>The label is rendered in ZPL dots (1 dot = 1 device-independent unit) and then scaled /
    /// rotated to fit the control, so it stays crisp at any size. Drag with the left mouse button to
    /// pan and use the wheel to zoom toward the cursor (<see cref="Zoom"/>, <see cref="OffsetX"/>,
    /// <see cref="OffsetY"/>). Runs on the UI (STA) thread.</para>
    /// </summary>
    public class ZplLabelView : FrameworkElement
    {
        private const double MinZoom = 0.1;
        private const double MaxZoom = 20.0;
        private const double ZoomStep = 1.15;   // per wheel notch

        private DrawingGroup? _drawing;     // cached vector label (rebuilt only on content change)
        private bool _contentDirty = true;

        private bool _panning;
        private Point _panStart;
        private double _panStartOffsetX;
        private double _panStartOffsetY;

        #region Dependency properties

        /// <summary>The ZPL string to render.</summary>
        public static readonly DependencyProperty ZplProperty = DependencyProperty.Register(
            nameof(Zpl), typeof(string), typeof(ZplLabelView),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender, OnContentChanged));

        /// <summary>Label width in millimetres (default 101.6 = 4 in).</summary>
        public static readonly DependencyProperty LabelWidthProperty = DependencyProperty.Register(
            nameof(LabelWidth), typeof(double), typeof(ZplLabelView),
            new FrameworkPropertyMetadata(101.6, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender, OnContentChanged));

        /// <summary>Label height in millimetres (default 152.4 = 6 in).</summary>
        public static readonly DependencyProperty LabelHeightProperty = DependencyProperty.Register(
            nameof(LabelHeight), typeof(double), typeof(ZplLabelView),
            new FrameworkPropertyMetadata(152.4, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender, OnContentChanged));

        /// <summary>Print density in dots per millimetre (default 8 = 203 dpi).</summary>
        public static readonly DependencyProperty PrintDensityDpmmProperty = DependencyProperty.Register(
            nameof(PrintDensityDpmm), typeof(int), typeof(ZplLabelView),
            new FrameworkPropertyMetadata(8, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender, OnContentChanged));

        /// <summary>Rotation of the whole label, in degrees clockwise (any angle; commonly 0/90/180/270).</summary>
        public static readonly DependencyProperty RotationAngleProperty = DependencyProperty.Register(
            nameof(RotationAngle), typeof(double), typeof(ZplLabelView),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>How the label is scaled to fit the control (default <see cref="System.Windows.Media.Stretch.Uniform"/>).</summary>
        public static readonly DependencyProperty StretchProperty = DependencyProperty.Register(
            nameof(Stretch), typeof(Stretch), typeof(ZplLabelView),
            new FrameworkPropertyMetadata(Stretch.Uniform, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>Whether the label is rendered over an opaque white background (default true).</summary>
        public static readonly DependencyProperty OpaqueBackgroundProperty = DependencyProperty.Register(
            nameof(OpaqueBackground), typeof(bool), typeof(ZplLabelView),
            new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender, OnContentChanged));

        /// <summary>Optional renderer options (fonts, antialias, text backend). When null, options are built from <see cref="OpaqueBackground"/>.</summary>
        public static readonly DependencyProperty OptionsProperty = DependencyProperty.Register(
            nameof(Options), typeof(ZplRendererOptions), typeof(ZplLabelView),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender, OnContentChanged));

        /// <summary>Horizontal pan of the rendered label, in device-independent units (applied after scaling).</summary>
        public static readonly DependencyProperty OffsetXProperty = DependencyProperty.Register(
            nameof(OffsetX), typeof(double), typeof(ZplLabelView),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>Vertical pan of the rendered label, in device-independent units (applied after scaling).</summary>
        public static readonly DependencyProperty OffsetYProperty = DependencyProperty.Register(
            nameof(OffsetY), typeof(double), typeof(ZplLabelView),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>User zoom factor applied on top of the <see cref="Stretch"/> fit (1.0 = fit).</summary>
        public static readonly DependencyProperty ZoomProperty = DependencyProperty.Register(
            nameof(Zoom), typeof(double), typeof(ZplLabelView),
            new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender, null,
                static (_, v) => Math.Clamp((double)v, MinZoom, MaxZoom)));

        public string? Zpl { get => (string?)GetValue(ZplProperty); set => SetValue(ZplProperty, value); }
        public double LabelWidth { get => (double)GetValue(LabelWidthProperty); set => SetValue(LabelWidthProperty, value); }
        public double LabelHeight { get => (double)GetValue(LabelHeightProperty); set => SetValue(LabelHeightProperty, value); }
        public int PrintDensityDpmm { get => (int)GetValue(PrintDensityDpmmProperty); set => SetValue(PrintDensityDpmmProperty, value); }
        public double RotationAngle { get => (double)GetValue(RotationAngleProperty); set => SetValue(RotationAngleProperty, value); }
        public Stretch Stretch { get => (Stretch)GetValue(StretchProperty); set => SetValue(StretchProperty, value); }
        public bool OpaqueBackground { get => (bool)GetValue(OpaqueBackgroundProperty); set => SetValue(OpaqueBackgroundProperty, value); }
        public ZplRendererOptions? Options { get => (ZplRendererOptions?)GetValue(OptionsProperty); set => SetValue(OptionsProperty, value); }
        public double OffsetX { get => (double)GetValue(OffsetXProperty); set => SetValue(OffsetXProperty, value); }
        public double OffsetY { get => (double)GetValue(OffsetYProperty); set => SetValue(OffsetYProperty, value); }
        public double Zoom { get => (double)GetValue(ZoomProperty); set => SetValue(ZoomProperty, value); }

        #endregion

        private static void OnContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((ZplLabelView)d)._contentDirty = true;

        /// <summary>Builds (and caches) the vector label whenever the content inputs change.</summary>
        private void EnsureDrawing()
        {
            if (!_contentDirty)
            {
                return;
            }

            _contentDirty = false;
            _drawing = null;

            string? zpl = Zpl;
            if (string.IsNullOrEmpty(zpl))
            {
                return;
            }

            try
            {
                var storage = new PrinterStorage();
                var analyzer = new ZplAnalyzer(storage);
                AnalyzeInfo info = analyzer.Analyze(zpl);
                if (info.LabelInfos.Length == 0)
                {
                    return;
                }

                IList<ZplElementBase> elements = info.LabelInfos[0].ZplElements;
                ZplRendererOptions options = Options ?? new ZplRendererOptions { OpaqueBackground = OpaqueBackground };
                _drawing = new ZplRenderer(storage, options)
                    .CreateDrawing(elements, LabelWidth, LabelHeight, PrintDensityDpmm);
            }
            catch
            {
                // Invalid ZPL (or render failure): show nothing rather than throwing during layout.
                _drawing = null;
            }
        }

        /// <summary>Unrotated label size in dots, matching the renderer's pixel dimensions.</summary>
        private Size LabelSizeDots()
            => new(Convert.ToInt32(LabelWidth * PrintDensityDpmm), Convert.ToInt32(LabelHeight * PrintDensityDpmm));

        /// <summary>Axis-aligned bounding box of the label after rotation about its centre.</summary>
        private Rect RotatedBounds()
        {
            Size s = LabelSizeDots();
            var rotate = new RotateTransform(RotationAngle, s.Width / 2, s.Height / 2);
            return rotate.TransformBounds(new Rect(0, 0, s.Width, s.Height));
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            EnsureDrawing();

            // Act as a viewport: fill the available space when constrained, and fall back to the
            // natural (rotated) label size only when a dimension is unconstrained. The label is then
            // scaled/centred/panned within that viewport in OnRender, so panning never resizes it.
            Size natural = RotatedBounds().Size;
            double width = double.IsInfinity(availableSize.Width) ? natural.Width : availableSize.Width;
            double height = double.IsInfinity(availableSize.Height) ? natural.Height : availableSize.Height;
            return new Size(width, height);
        }

        protected override Size ArrangeOverride(Size finalSize) => finalSize;

        /// <summary>
        /// The content→viewport matrix at a given zoom, <em>without</em> the pan offset (rotate about
        /// the label centre → move the rotated bbox to the origin → fit-scale × zoom → centre in the
        /// viewport). The pan is a pure post-translation, so keeping it out lets the wheel handler map
        /// the cursor to a content point and back across a zoom change to anchor the zoom there.
        /// </summary>
        private bool TryBuildBaseMatrix(double zoom, out Matrix matrix)
        {
            matrix = Matrix.Identity;
            Rect rb = RotatedBounds();
            if (rb.Width <= 0 || rb.Height <= 0 || RenderSize.Width <= 0 || RenderSize.Height <= 0)
            {
                return false;
            }

            double sx = RenderSize.Width / rb.Width;
            double sy = RenderSize.Height / rb.Height;
            (double fitX, double fitY) = Stretch switch
            {
                Stretch.None => (1.0, 1.0),
                Stretch.Fill => (sx, sy),
                Stretch.UniformToFill => (Math.Max(sx, sy), Math.Max(sx, sy)),
                _ => (Math.Min(sx, sy), Math.Min(sx, sy)),   // Uniform
            };

            double scaleX = fitX * zoom;
            double scaleY = fitY * zoom;
            double centreX = (RenderSize.Width - rb.Width * scaleX) / 2;
            double centreY = (RenderSize.Height - rb.Height * scaleY) / 2;

            Size label = LabelSizeDots();
            var group = new TransformGroup();
            group.Children.Add(new RotateTransform(RotationAngle, label.Width / 2, label.Height / 2)); // rotate about label centre
            group.Children.Add(new TranslateTransform(-rb.X, -rb.Y));                                  // rotated box to origin
            group.Children.Add(new ScaleTransform(scaleX, scaleY));                                    // fit-scale x zoom
            group.Children.Add(new TranslateTransform(centreX, centreY));                              // centre in viewport
            matrix = group.Value;
            return true;
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            EnsureDrawing();

            // Fill the viewport with a transparent rect so the whole control is hit-testable (drag to
            // pan anywhere, including the letterbox around the label), not just the inked content.
            drawingContext.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));

            if (_drawing == null || !TryBuildBaseMatrix(Zoom, out Matrix matrix))
            {
                return;
            }

            matrix.Translate(OffsetX, OffsetY);   // pan, applied in screen space after the base transform

            drawingContext.PushClip(new RectangleGeometry(new Rect(RenderSize)));
            drawingContext.PushTransform(new MatrixTransform(matrix));
            drawingContext.DrawDrawing(_drawing);
            drawingContext.Pop();
            drawingContext.Pop();
        }

        /// <summary>
        /// Multiply <see cref="Zoom"/> by <paramref name="factor"/> (clamped) while keeping the content
        /// point under <paramref name="center"/> (a point in control coordinates) fixed — i.e. zoom
        /// toward that point, adjusting <see cref="OffsetX"/> / <see cref="OffsetY"/> to compensate.
        /// </summary>
        public void ZoomBy(double factor, Point center)
        {
            double oldZoom = Zoom;
            double newZoom = Math.Clamp(oldZoom * factor, MinZoom, MaxZoom);
            if (newZoom == oldZoom)
            {
                return;
            }

            if (TryBuildBaseMatrix(oldZoom, out Matrix oldMatrix) && oldMatrix.HasInverse
                && TryBuildBaseMatrix(newZoom, out Matrix newMatrix))
            {
                Matrix inverse = oldMatrix;
                inverse.Invert();
                Point content = inverse.Transform(new Point(center.X - OffsetX, center.Y - OffsetY));
                Point screen = newMatrix.Transform(content);
                Zoom = newZoom;
                OffsetX = center.X - screen.X;
                OffsetY = center.Y - screen.Y;
            }
            else
            {
                Zoom = newZoom;
            }
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);
            ZoomBy(e.Delta >= 0 ? ZoomStep : 1.0 / ZoomStep, e.GetPosition(this));
            e.Handled = true;
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);

            _panning = true;
            _panStart = e.GetPosition(this);
            _panStartOffsetX = OffsetX;
            _panStartOffsetY = OffsetY;
            CaptureMouse();
            Cursor = Cursors.SizeAll;
            e.Handled = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (_panning)
            {
                Point p = e.GetPosition(this);
                OffsetX = _panStartOffsetX + (p.X - _panStart.X);
                OffsetY = _panStartOffsetY + (p.Y - _panStart.Y);
            }
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);

            if (_panning)
            {
                _panning = false;
                ReleaseMouseCapture();
                Cursor = Cursors.Arrow;
                e.Handled = true;
            }
        }
    }
}

using System;
using System.Collections.Generic;

using BinaryKits.Zpl.Label.Elements;
using BinaryKits.Zpl.Viewer.Models;

using DotZpl.Rendering;

// PrinterStorage / ZplAnalyzer collide with no DotZpl types, but they live in BinaryKits.Zpl.Viewer
// alongside ZplElementDrawer / DrawerOptions / FontManager (the Skia reference). Pull them in by
// alias so the unqualified colliding names below resolve to DotZpl's types.
using PrinterStorage = BinaryKits.Zpl.Viewer.PrinterStorage;
using ZplAnalyzer = BinaryKits.Zpl.Viewer.ZplAnalyzer;

namespace DotZpl.Controls
{
    /// <summary>
    /// A WPF control that renders a ZPL string as native, scalable vector content via
    /// <see cref="ZplElementDrawer"/>. Supports fit-to-control scaling (<see cref="Stretch"/>) and
    /// arbitrary <see cref="RotationAngle"/> of the whole label.
    ///
    /// <para>The label is rendered in ZPL dots (1 dot = 1 device-independent unit) and then scaled /
    /// rotated to fit the control, so it stays crisp at any size. Runs on the UI (STA) thread.</para>
    /// </summary>
    public class ZplLabelView : FrameworkElement
    {
        private DrawingGroup? _drawing;     // cached vector label (rebuilt only on content change)
        private bool _contentDirty = true;

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
            nameof(Options), typeof(DrawerOptions), typeof(ZplLabelView),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender, OnContentChanged));

        /// <summary>Horizontal pan of the rendered label, in device-independent units (applied after scaling).</summary>
        public static readonly DependencyProperty OffsetXProperty = DependencyProperty.Register(
            nameof(OffsetX), typeof(double), typeof(ZplLabelView),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>Vertical pan of the rendered label, in device-independent units (applied after scaling).</summary>
        public static readonly DependencyProperty OffsetYProperty = DependencyProperty.Register(
            nameof(OffsetY), typeof(double), typeof(ZplLabelView),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public string? Zpl { get => (string?)GetValue(ZplProperty); set => SetValue(ZplProperty, value); }
        public double LabelWidth { get => (double)GetValue(LabelWidthProperty); set => SetValue(LabelWidthProperty, value); }
        public double LabelHeight { get => (double)GetValue(LabelHeightProperty); set => SetValue(LabelHeightProperty, value); }
        public int PrintDensityDpmm { get => (int)GetValue(PrintDensityDpmmProperty); set => SetValue(PrintDensityDpmmProperty, value); }
        public double RotationAngle { get => (double)GetValue(RotationAngleProperty); set => SetValue(RotationAngleProperty, value); }
        public Stretch Stretch { get => (Stretch)GetValue(StretchProperty); set => SetValue(StretchProperty, value); }
        public bool OpaqueBackground { get => (bool)GetValue(OpaqueBackgroundProperty); set => SetValue(OpaqueBackgroundProperty, value); }
        public DrawerOptions? Options { get => (DrawerOptions?)GetValue(OptionsProperty); set => SetValue(OptionsProperty, value); }
        public double OffsetX { get => (double)GetValue(OffsetXProperty); set => SetValue(OffsetXProperty, value); }
        public double OffsetY { get => (double)GetValue(OffsetYProperty); set => SetValue(OffsetYProperty, value); }

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
                DrawerOptions options = Options ?? new DrawerOptions { OpaqueBackground = OpaqueBackground };
                _drawing = new ZplElementDrawer(storage, options)
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

        protected override void OnRender(DrawingContext drawingContext)
        {
            EnsureDrawing();
            if (_drawing == null)
            {
                return;
            }

            Rect rb = RotatedBounds();
            if (rb.Width <= 0 || rb.Height <= 0 || RenderSize.Width <= 0 || RenderSize.Height <= 0)
            {
                return;
            }

            // Scale the label to the viewport per Stretch (independent of the pan offset).
            double sx = RenderSize.Width / rb.Width;
            double sy = RenderSize.Height / rb.Height;
            (double scaleX, double scaleY) = Stretch switch
            {
                Stretch.None => (1.0, 1.0),
                Stretch.Fill => (sx, sy),
                Stretch.UniformToFill => (Math.Max(sx, sy), Math.Max(sx, sy)),
                _ => (Math.Min(sx, sy), Math.Min(sx, sy)),   // Uniform
            };

            // Centre the scaled label in the viewport, then apply the pan (the pan only translates —
            // it does not affect the scale, so the label keeps its size).
            double scaledWidth = rb.Width * scaleX;
            double scaledHeight = rb.Height * scaleY;
            double centreAndPanX = (RenderSize.Width - scaledWidth) / 2 + OffsetX;
            double centreAndPanY = (RenderSize.Height - scaledHeight) / 2 + OffsetY;

            Size label = LabelSizeDots();
            var transform = new TransformGroup();
            transform.Children.Add(new RotateTransform(RotationAngle, label.Width / 2, label.Height / 2)); // rotate about label centre
            transform.Children.Add(new TranslateTransform(-rb.X, -rb.Y));                                  // rotated box to origin
            transform.Children.Add(new ScaleTransform(scaleX, scaleY));                                    // scale to viewport
            transform.Children.Add(new TranslateTransform(centreAndPanX, centreAndPanY));                  // centre + pan

            drawingContext.PushClip(new RectangleGeometry(new Rect(RenderSize)));
            drawingContext.PushTransform(transform);
            drawingContext.DrawDrawing(_drawing);
            drawingContext.Pop();
            drawingContext.Pop();
        }
    }
}

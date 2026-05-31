using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

using BinaryKits.Zpl.Label.Elements;
using BinaryKits.Zpl.Viewer;
using BinaryKits.Zpl.Viewer.Models;

using WpfZpl.Rendering;

namespace WpfZpl.Controls
{
    /// <summary>
    /// A WPF control that renders a ZPL string as native, scalable vector content via
    /// <see cref="WpfZplElementDrawer"/>. Supports fit-to-control scaling (<see cref="Stretch"/>) and
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
            nameof(Options), typeof(WpfDrawerOptions), typeof(ZplLabelView),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender, OnContentChanged));

        public string? Zpl { get => (string?)GetValue(ZplProperty); set => SetValue(ZplProperty, value); }
        public double LabelWidth { get => (double)GetValue(LabelWidthProperty); set => SetValue(LabelWidthProperty, value); }
        public double LabelHeight { get => (double)GetValue(LabelHeightProperty); set => SetValue(LabelHeightProperty, value); }
        public int PrintDensityDpmm { get => (int)GetValue(PrintDensityDpmmProperty); set => SetValue(PrintDensityDpmmProperty, value); }
        public double RotationAngle { get => (double)GetValue(RotationAngleProperty); set => SetValue(RotationAngleProperty, value); }
        public Stretch Stretch { get => (Stretch)GetValue(StretchProperty); set => SetValue(StretchProperty, value); }
        public bool OpaqueBackground { get => (bool)GetValue(OpaqueBackgroundProperty); set => SetValue(OpaqueBackgroundProperty, value); }
        public WpfDrawerOptions? Options { get => (WpfDrawerOptions?)GetValue(OptionsProperty); set => SetValue(OptionsProperty, value); }

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
                WpfDrawerOptions options = Options ?? new WpfDrawerOptions { OpaqueBackground = OpaqueBackground };
                _drawing = new WpfZplElementDrawer(storage, options)
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
            return ScaleToFit(RotatedBounds().Size, availableSize);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            return ScaleToFit(RotatedBounds().Size, finalSize);
        }

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

            double scaleX = RenderSize.Width / rb.Width;
            double scaleY = RenderSize.Height / rb.Height;

            Size label = LabelSizeDots();
            var transform = new TransformGroup();
            transform.Children.Add(new RotateTransform(RotationAngle, label.Width / 2, label.Height / 2)); // rotate about label centre
            transform.Children.Add(new TranslateTransform(-rb.X, -rb.Y));                                  // move rotated box to origin
            transform.Children.Add(new ScaleTransform(scaleX, scaleY));                                    // scale to the arranged size

            // Clip to our render bounds (matters for Stretch.UniformToFill / None overflow).
            drawingContext.PushClip(new RectangleGeometry(new Rect(RenderSize)));
            drawingContext.PushTransform(transform);
            drawingContext.DrawDrawing(_drawing);
            drawingContext.Pop();
            drawingContext.Pop();
        }

        /// <summary>Mirror of WPF's Viewbox/Image stretch maths, honouring infinite constraints.</summary>
        private Size ScaleToFit(Size natural, Size available)
        {
            if (natural.Width <= 0 || natural.Height <= 0)
            {
                return new Size(0, 0);
            }

            bool infW = double.IsInfinity(available.Width);
            bool infH = double.IsInfinity(available.Height);

            double sx = infW ? 1 : available.Width / natural.Width;
            double sy = infH ? 1 : available.Height / natural.Height;

            switch (Stretch)
            {
                case Stretch.None:
                    sx = sy = 1;
                    break;
                case Stretch.Fill:
                    // independent x/y; already 1 where unconstrained
                    break;
                case Stretch.Uniform:
                    sx = sy = (infW && infH) ? 1 : infW ? sy : infH ? sx : Math.Min(sx, sy);
                    break;
                case Stretch.UniformToFill:
                    sx = sy = (infW && infH) ? 1 : infW ? sy : infH ? sx : Math.Max(sx, sy);
                    break;
            }

            return new Size(natural.Width * sx, natural.Height * sy);
        }
    }
}

using System;
using System.Collections.Generic;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

using BinaryKits.Zpl.Label.Elements;
using BinaryKits.Zpl.Analyzer;
using BinaryKits.Zpl.Analyzer.Models;

using DotZpl.Rendering;

namespace DotZpl.Controls
{
    /// <summary>
    /// An Avalonia control that renders a ZPL string as native, scalable vector content via
    /// <see cref="ZplRenderer"/>. Mirrors the WPF <c>ZplLabelView</c> in
    /// <c>ZplLabelView.Wpf.cs</c> with the Avalonia property and rendering APIs.
    ///
    /// <para>The label is rendered in ZPL dots (1 dot = 1 device-independent unit) and then scaled /
    /// rotated to fit the control, so it stays crisp at any size.</para>
    /// </summary>
    public class ZplLabelView : Control
    {
        // Cached label tuple (background/whiteRegion/images) — rebuilt only on content change.
        // We hold a LabelDrawing rather than a DrawingGroup because Avalonia 12's DrawingGroup
        // recording context can't carry image draws and its declarative children carry no per-child
        // transform; building straight into the live render-pass DrawingContext per Render call
        // sidesteps both limitations and removes a layer of caching.
        private LabelDrawing? _drawing;
        private bool _contentDirty = true;

        #region Styled properties

        public static readonly StyledProperty<string?> ZplProperty =
            AvaloniaProperty.Register<ZplLabelView, string?>(nameof(Zpl));

        public static readonly StyledProperty<double> LabelWidthProperty =
            AvaloniaProperty.Register<ZplLabelView, double>(nameof(LabelWidth), 101.6);

        public static readonly StyledProperty<double> LabelHeightProperty =
            AvaloniaProperty.Register<ZplLabelView, double>(nameof(LabelHeight), 152.4);

        public static readonly StyledProperty<int> PrintDensityDpmmProperty =
            AvaloniaProperty.Register<ZplLabelView, int>(nameof(PrintDensityDpmm), 8);

        public static readonly StyledProperty<double> RotationAngleProperty =
            AvaloniaProperty.Register<ZplLabelView, double>(nameof(RotationAngle), 0.0);

        public static readonly StyledProperty<Stretch> StretchProperty =
            AvaloniaProperty.Register<ZplLabelView, Stretch>(nameof(Stretch), Stretch.Uniform);

        public static readonly StyledProperty<bool> OpaqueBackgroundProperty =
            AvaloniaProperty.Register<ZplLabelView, bool>(nameof(OpaqueBackground), true);

        public static readonly StyledProperty<ZplRendererOptions?> OptionsProperty =
            AvaloniaProperty.Register<ZplLabelView, ZplRendererOptions?>(nameof(Options));

        public static readonly StyledProperty<double> OffsetXProperty =
            AvaloniaProperty.Register<ZplLabelView, double>(nameof(OffsetX), 0.0);

        public static readonly StyledProperty<double> OffsetYProperty =
            AvaloniaProperty.Register<ZplLabelView, double>(nameof(OffsetY), 0.0);

        static ZplLabelView()
        {
            // Properties that change the *content* of the cached drawing — must be invalidated and
            // re-measured. Property assignments mark _contentDirty in OnPropertyChanged below.
            AffectsMeasure<ZplLabelView>(
                ZplProperty, LabelWidthProperty, LabelHeightProperty, PrintDensityDpmmProperty,
                RotationAngleProperty, StretchProperty, OpaqueBackgroundProperty, OptionsProperty);

            // Pan only affects the visual, not the natural size.
            AffectsRender<ZplLabelView>(OffsetXProperty, OffsetYProperty);
        }

        public string? Zpl { get => GetValue(ZplProperty); set => SetValue(ZplProperty, value); }
        public double LabelWidth { get => GetValue(LabelWidthProperty); set => SetValue(LabelWidthProperty, value); }
        public double LabelHeight { get => GetValue(LabelHeightProperty); set => SetValue(LabelHeightProperty, value); }
        public int PrintDensityDpmm { get => GetValue(PrintDensityDpmmProperty); set => SetValue(PrintDensityDpmmProperty, value); }
        public double RotationAngle { get => GetValue(RotationAngleProperty); set => SetValue(RotationAngleProperty, value); }
        public Stretch Stretch { get => GetValue(StretchProperty); set => SetValue(StretchProperty, value); }
        public bool OpaqueBackground { get => GetValue(OpaqueBackgroundProperty); set => SetValue(OpaqueBackgroundProperty, value); }
        public ZplRendererOptions? Options { get => GetValue(OptionsProperty); set => SetValue(OptionsProperty, value); }
        public double OffsetX { get => GetValue(OffsetXProperty); set => SetValue(OffsetXProperty, value); }
        public double OffsetY { get => GetValue(OffsetYProperty); set => SetValue(OffsetYProperty, value); }

        #endregion

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            // Any content-altering property mark the cached drawing stale; AffectsMeasure has already
            // arranged for a re-measure and re-render.
            if (change.Property == ZplProperty ||
                change.Property == LabelWidthProperty ||
                change.Property == LabelHeightProperty ||
                change.Property == PrintDensityDpmmProperty ||
                change.Property == OpaqueBackgroundProperty ||
                change.Property == OptionsProperty)
            {
                _contentDirty = true;
            }
        }

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
                    .CreateLabelDrawing(elements, LabelWidth, LabelHeight, PrintDensityDpmm);
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
            var rect = new Rect(0, 0, s.Width, s.Height);
            return rect.TransformToAABB(RotationMatrix(s));
        }

        /// <summary>Matrix that rotates about the label's centre by <see cref="RotationAngle"/> degrees.</summary>
        private Matrix RotationMatrix(Size labelSize)
        {
            double cx = labelSize.Width / 2;
            double cy = labelSize.Height / 2;
            double rad = RotationAngle * Math.PI / 180.0;
            return Matrix.CreateTranslation(-cx, -cy)
                 * Matrix.CreateRotation(rad)
                 * Matrix.CreateTranslation(cx, cy);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            EnsureDrawing();

            // Act as a viewport: fill the available space when constrained, and fall back to the
            // natural (rotated) label size only when a dimension is unconstrained. The label is then
            // scaled/centred/panned within that viewport in Render, so panning never resizes it.
            Size natural = RotatedBounds().Size;
            double width = double.IsInfinity(availableSize.Width) ? natural.Width : availableSize.Width;
            double height = double.IsInfinity(availableSize.Height) ? natural.Height : availableSize.Height;
            return new Size(width, height);
        }

        public override void Render(DrawingContext context)
        {
            EnsureDrawing();
            if (_drawing == null)
            {
                return;
            }

            Rect rb = RotatedBounds();
            Size viewport = Bounds.Size;
            if (rb.Width <= 0 || rb.Height <= 0 || viewport.Width <= 0 || viewport.Height <= 0)
            {
                return;
            }

            // Scale the label to the viewport per Stretch (independent of the pan offset).
            double sx = viewport.Width / rb.Width;
            double sy = viewport.Height / rb.Height;
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
            double centreAndPanX = (viewport.Width - scaledWidth) / 2 + OffsetX;
            double centreAndPanY = (viewport.Height - scaledHeight) / 2 + OffsetY;

            Size label = LabelSizeDots();
            // Compose: rotate about label centre -> translate rotated-bbox to origin -> scale -> centre+pan.
            Matrix transform = RotationMatrix(label)
                             * Matrix.CreateTranslation(-rb.X, -rb.Y)
                             * Matrix.CreateScale(scaleX, scaleY)
                             * Matrix.CreateTranslation(centreAndPanX, centreAndPanY);

            using (context.PushClip(new Rect(viewport)))
            using (context.PushTransform(transform))
            {
                // LabelDrawing.Draw paints straight into the live render-pass DrawingContext,
                // so image elements work natively (no DrawingGroup recording, no RTB baking).
                _drawing.Draw(context);
            }
        }
    }
}

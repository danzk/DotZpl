using DotZpl.Text;

namespace DotZpl.Rendering
{
    /// <summary>
    /// WPF analogue of <c>BinaryKits.Zpl.Viewer.ElementDrawers.ZplRendererOptions</c>.
    /// Drops the Skia-specific render format/quality and the PDF path (out of scope for v1).
    /// </summary>
    public class ZplRendererOptions
    {
        /// <summary>
        /// Applies the label over a white background after rendering all elements.
        /// </summary>
        public bool OpaqueBackground { get; set; } = false;

        /// <summary>
        /// Replace dashes with en dash for the proportional "0" font (mirrors Skia option).
        /// </summary>
        public bool ReplaceDashWithEnDash { get; set; } = true;

        /// <summary>
        /// Replace underscores with en space for the proportional "0" font (mirrors Skia option).
        /// </summary>
        public bool ReplaceUnderscoreWithEnSpace { get; set; } = false;

        /// <summary>
        /// Whether antialiasing is enabled. When false, geometry is rendered with
        /// <c>EdgeMode.Aliased</c> (used for barcodes / MaxiCode where Skia disables AA).
        /// </summary>
        public bool Antialias { get; set; } = true;

        /// <summary>
        /// Which WPF text primitive drives rendering. <see cref="TextBackend.GlyphRun"/> is the
        /// default (baseline-origin, matches Skia); <see cref="TextBackend.FormattedText"/> is
        /// available for the comparison spike. Both go through geometry fill, which is the lightest
        /// (closest-to-Skia) WPF text path. WPF text renders marginally heavier than Skia at small
        /// sizes (a rasteriser-gamma difference, not a weight/font difference); this is inherent and
        /// does not affect SSIM — see the note in <c>TextRenderer</c>.
        /// </summary>
        public TextBackend TextBackend { get; set; } = TextBackend.GlyphRun;

        public ZplFontManager FontManager { get; }

        public ZplRendererOptions() : this(new ZplFontManager()) { }

        public ZplRendererOptions(ZplFontManager fontManager)
        {
            FontManager = fontManager;
        }
    }
}

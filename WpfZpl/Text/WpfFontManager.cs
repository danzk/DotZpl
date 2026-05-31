using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;

namespace WpfZpl.Text
{
    /// <summary>
    /// WPF analogue of <c>BinaryKits.Zpl.Viewer.FontManager</c>. Resolves the same family-name
    /// stacks to a WPF <see cref="Typeface"/> / <see cref="GlyphTypeface"/> (instead of an
    /// <c>SKTypeface</c>) and exposes the embedded graphic-symbol font used by <c>^GS</c>.
    /// </summary>
    public class WpfFontManager
    {
        /// <summary>Primary (proportional) font stack for ZPL font "0". Ordered by preference.</summary>
        public List<string> FontStack0 { get; set; } = new()
        {
            "Swis721 Cn BT",
            "TeX Gyre Heros Cn",
            "Nimbus Sans Narrow",
            "Roboto Condensed",
            "Helvetica",
            "Helvetica Neue",
            "Arial",
        };

        /// <summary>Monospace font stack for ZPL font "A" (and the default).</summary>
        public List<string> FontStackA { get; set; } = new()
        {
            "DejaVu Sans Mono",
            "Lucida Console",
            "Andale Mono",
            "Droid Sans Mono",
        };

        /// <summary>Loads a <see cref="GlyphTypeface"/> for a ZPL font name.</summary>
        public Func<string, GlyphTypeface> FontLoader { get; set; }

        /// <summary>Loads the WPF <see cref="Typeface"/> (family-based) for a ZPL font name.</summary>
        public Func<string, Typeface> TypefaceLoader { get; set; }

        private Typeface? _typeface0;
        private Typeface? _typefaceA;
        private GlyphTypeface? _glyph0;
        private GlyphTypeface? _glyphA;
        private GlyphTypeface? _glyphGs;
        private GlyphTypeface? _glyphFontA;
        private GlyphTypeface? _glyphFontC;

        internal Typeface Typeface0 => _typeface0 ??= ResolveTypeface("0");
        internal Typeface TypefaceA => _typefaceA ??= ResolveTypeface("A");
        internal GlyphTypeface Glyph0 => _glyph0 ??= ToGlyph(Typeface0);
        internal GlyphTypeface GlyphA => _glyphA ??= ToGlyph(TypefaceA);
        internal GlyphTypeface GlyphGS => _glyphGs ??= LoadEmbedded("WpfZpl.ZplGS.ttf", "WpfZpl_ZplGS.ttf");

        /// <summary>Embedded pixel font matching Zebra Font A (9 x 5 dot matrix).</summary>
        public GlyphTypeface GlyphFontA => _glyphFontA ??= LoadEmbedded("WpfZpl.font-a.ttf", "WpfZpl_font-a.ttf");

        /// <summary>Embedded pixel font matching Zebra Font C/D (18 x 10 dot matrix).</summary>
        public GlyphTypeface GlyphFontC => _glyphFontC ??= LoadEmbedded("WpfZpl.font-c.ttf", "WpfZpl_font-c.ttf");

        /// <summary>
        /// Whether a ZPL font name is rendered with an embedded fixed-cell pixel font (which is sized
        /// by its matrix cell height, not the proportional ×1.1 correction). Defaults to A / C / D
        /// (the fonts shipped in Resources). Set to <c>_ => false</c> to render everything with the
        /// system font stacks (used by the Skia-vs-WPF comparison harness).
        /// </summary>
        public Func<string, bool> IsPixelFont { get; set; } = name => name is "A" or "C" or "D";

        public WpfFontManager()
        {
            FontLoader = fontName => fontName switch
            {
                "0" => Glyph0,
                "A" => GlyphFontA,
                "C" or "D" => GlyphFontC,
                _ => GlyphA,
            };
            TypefaceLoader = fontName => fontName == "0" ? Typeface0 : TypefaceA;
        }

        private Typeface ResolveTypeface(string fontType)
        {
            List<string> stack = fontType == "0" ? FontStack0 : FontStackA;

            // Mirror Skia's font styles: "0" => Bold / SemiCondensed, "A" => Normal / Normal.
            (FontWeight weight, FontStretch stretch) = fontType == "0"
                ? (FontWeights.Bold, FontStretches.SemiCondensed)
                : (FontWeights.Normal, FontStretches.Normal);

            foreach (string family in stack)
            {
                var typeface = new Typeface(new FontFamily(family), FontStyles.Normal, weight, stretch);
                if (typeface.TryGetGlyphTypeface(out _))
                {
                    return typeface;
                }
            }

            return new Typeface(new FontFamily("Arial"), FontStyles.Normal, weight, stretch);
        }

        private static GlyphTypeface ToGlyph(Typeface typeface)
        {
            if (typeface.TryGetGlyphTypeface(out GlyphTypeface gt))
            {
                return gt;
            }

            throw new InvalidOperationException($"No GlyphTypeface for typeface '{typeface.FontFamily}'.");
        }

        private static GlyphTypeface LoadEmbedded(string logicalName, string tempFileName)
        {
            Assembly assembly = typeof(WpfFontManager).Assembly;
            using Stream? stream = assembly.GetManifestResourceStream(logicalName)
                ?? throw new InvalidOperationException($"Embedded font resource '{logicalName}' not found.");

            // GlyphTypeface can only be constructed from a URI, so spill the font to a stable temp file.
            string path = Path.Combine(Path.GetTempPath(), tempFileName);
            using (var ms = new MemoryStream())
            {
                stream.CopyTo(ms);
                byte[] bytes = ms.ToArray();
                if (!File.Exists(path) || new FileInfo(path).Length != bytes.Length)
                {
                    File.WriteAllBytes(path, bytes);
                }
            }

            return new GlyphTypeface(new Uri(path));
        }
    }
}

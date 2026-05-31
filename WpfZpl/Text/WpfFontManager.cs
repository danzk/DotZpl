using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
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
        private GlyphTypeface? _glyphFontB;
        private GlyphTypeface? _glyphFontC;

        internal Typeface Typeface0 => _typeface0 ??= ResolveTypeface("0");
        internal Typeface TypefaceA => _typefaceA ??= ResolveTypeface("A");
        internal GlyphTypeface Glyph0 => _glyph0 ??= ToGlyph(Typeface0);
        internal GlyphTypeface GlyphA => _glyphA ??= ToGlyph(TypefaceA);
        internal GlyphTypeface GlyphGS => _glyphGs ??= LoadEmbedded("WpfZpl.ZplGS.ttf", "WpfZpl_ZplGS.ttf");

        /// <summary>Embedded pixel font matching Zebra Font A (9 x 5 dot matrix).</summary>
        public GlyphTypeface GlyphFontA => _glyphFontA ??= LoadEmbedded("WpfZpl.font-a.ttf", "WpfZpl_font-a.ttf");

        /// <summary>Embedded pixel font matching Zebra Font B (11 x 7 dot matrix; bold, caps-only).</summary>
        public GlyphTypeface GlyphFontB => _glyphFontB ??= LoadEmbedded("WpfZpl.font-b.ttf", "WpfZpl_font-b.ttf");

        /// <summary>Embedded pixel font matching Zebra Font C/D (18 x 10 dot matrix).</summary>
        public GlyphTypeface GlyphFontC => _glyphFontC ??= LoadEmbedded("WpfZpl.font-c.ttf", "WpfZpl_font-c.ttf");

        /// <summary>
        /// Whether a ZPL font name is rendered with an embedded fixed-cell pixel font (which is sized
        /// by its matrix cell height, not the proportional ×1.1 correction). Defaults to A / B / C / D
        /// (the fonts shipped in Resources). Set to <c>_ => false</c> to render everything with the
        /// system font stacks (used by the Skia-vs-WPF comparison harness).
        /// </summary>
        public Func<string, bool> IsPixelFont { get; set; } = name => name is "A" or "B" or "C" or "D";

        /// <summary>
        /// Horizontal scale applied to ZPL font "0" when it resolves to a <em>non-condensed</em> face
        /// (i.e. the Arial fallback on machines without one of the <see cref="FontStack0"/> condensed
        /// fonts installed). The real Zebra font 0 is condensed (~0.86 of Arial's width), so plain Arial
        /// renders too wide; this squeezes it to approximate font 0. When a genuinely condensed font is
        /// resolved (Roboto Condensed, Swis721 Cn, ...) no extra scaling is applied. Set to 1.0 to disable
        /// (e.g. the comparison harness, which pins both backends to raw Arial).
        /// </summary>
        public double Font0FallbackCondense { get; set; } = 0.86;

        public WpfFontManager()
        {
            FontLoader = fontName => fontName switch
            {
                "0" => Glyph0,
                "A" => GlyphFontA,
                "B" => GlyphFontB,
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

        /// <summary>
        /// Extra horizontal scale to compose onto a font's <c>scaleX</c>. For font "0" this condenses the
        /// non-condensed Arial fallback (see <see cref="Font0FallbackCondense"/>); 1.0 for everything else.
        /// </summary>
        public double GetHorizontalScale(string fontName)
        {
            if (fontName != "0")
            {
                return 1.0;
            }

            return IsCondensedFamily(FontLoader("0")) ? 1.0 : Font0FallbackCondense;
        }

        private static bool IsCondensedFamily(GlyphTypeface gt)
        {
            foreach (string name in gt.FamilyNames.Values)
            {
                string n = name.ToLowerInvariant();
                if (n.Contains("condensed") || n.Contains("narrow") || Regex.IsMatch(n, @"\bcn\b"))
                {
                    return true;
                }
            }

            return false;
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

            byte[] bytes;
            using (var ms = new MemoryStream())
            {
                stream.CopyTo(ms);
                bytes = ms.ToArray();
            }

            // GlyphTypeface can only be constructed from a URI, so spill the font to a temp file. Make the
            // file name content-addressed (a hash of the bytes): an edit that doesn't change the byte count
            // — e.g. re-centring glyphs — must still invalidate the cache, which a length check would miss.
            string hash = Convert.ToHexString(SHA256.HashData(bytes))[..16];
            string name = $"{Path.GetFileNameWithoutExtension(tempFileName)}.{hash}{Path.GetExtension(tempFileName)}";
            string path = Path.Combine(Path.GetTempPath(), name);
            if (!File.Exists(path))
            {
                // Write to a unique sibling then move into place, so concurrent loaders (e.g. parallel
                // STA tests, or a multi-threaded host) never read a half-written file or collide on the
                // same write. A content-addressed name means an existing file is already the right bytes.
                string tmp = $"{path}.{Guid.NewGuid():N}.tmp";
                File.WriteAllBytes(tmp, bytes);
                try
                {
                    File.Move(tmp, path);
                }
                catch (IOException)
                {
                    // Another loader created it first — use theirs.
                }
                finally
                {
                    if (File.Exists(tmp)) File.Delete(tmp);
                }
            }

            return new GlyphTypeface(new Uri(path));
        }
    }
}

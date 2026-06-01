using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

using BinaryKits.Zpl.Analyzer;
using BinaryKits.Zpl.Label.Elements;
using BinaryKits.Zpl.Viewer;
using BinaryKits.Zpl.Viewer.ElementDrawers;

using DotZpl.Rendering;
using DotZpl.Text;

namespace DotZpl.Avalonia.UnitTest.Support
{
    /// <summary>
    /// Renders the same ZPL through the Skia reference (<see cref="ZplElementDrawer"/>) and the
    /// DotZpl candidate (<see cref="ZplRenderer"/>) so the two can be compared per element.
    /// Mirrors <c>DotZpl.UnitTest.RenderHarness</c> — same Skia configuration, same label corpus,
    /// same pinned-fonts logic, so results are apples-to-apples with the WPF suite.
    ///
    /// <para>Avalonia rendering must run on the Avalonia UI thread — tests should call into here
    /// from <c>[AvaloniaFact]</c> bodies, which the runtime schedules on that thread.</para>
    /// </summary>
    internal static class RenderHarness
    {
        public static readonly string LabelsRoot = Path.Combine("Labels");

        public static (byte[] skiaPng, byte[] dotPng) RenderBoth(
            string zpl, double width, double height, int dpmm, TextBackend textBackend = TextBackend.GlyphRun)
        {
            var storage = new PrinterStorage();
            var analyzer = new ZplAnalyzer(storage);
            IList<ZplElementBase> elements = analyzer.Analyze(zpl).LabelInfos[0].ZplElements;

            (DrawerOptions skiaOptions, ZplRendererOptions dotOptions) = BuildPinnedOptions(textBackend);

            byte[] skiaPng = new ZplElementDrawer(storage, skiaOptions).Draw(elements, width, height, dpmm);
            byte[] dotPng = new ZplRenderer(storage, dotOptions).DrawPng(elements, width, height, dpmm);

            return (skiaPng, dotPng);
        }

        public static RenderComparer.Result RenderAndCompare(
            string zpl, double width, double height, int dpmm, TextBackend textBackend = TextBackend.GlyphRun)
        {
            (byte[] skiaPng, byte[] dotPng) = RenderBoth(zpl, width, height, dpmm, textBackend);
            return RenderComparer.Compare(skiaPng, dotPng);
        }

        // Pinned to explicit font files for determinism (mirrors DotZpl.UnitTest.RenderHarness). When
        // they're missing (non-Windows host), the Skia side falls back to its own font lookup.
        private static readonly System.Lazy<(string font0, string fontA)?> PinnedFonts = new(() =>
        {
            string dir = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Fonts);
            string font0 = Path.Combine(dir, "arialbd.ttf");
            string fontA = Path.Combine(dir, "lucon.ttf");
            return File.Exists(font0) && File.Exists(fontA) ? (font0, fontA) : null;
        });

        private static (DrawerOptions, ZplRendererOptions) BuildPinnedOptions(TextBackend textBackend)
        {
            if (PinnedFonts.Value is not (string font0, string fontA))
            {
                return (new DrawerOptions { OpaqueBackground = true },
                        new ZplRendererOptions { OpaqueBackground = true, TextBackend = textBackend });
            }

            var skiaManager = new FontManager();
            SkiaSharp.SKTypeface skia0 = SkiaSharp.SKTypeface.FromFile(font0);
            SkiaSharp.SKTypeface skiaA = SkiaSharp.SKTypeface.FromFile(fontA);
            skiaManager.FontLoader = name => name == "0" ? skia0 : skiaA;
            var skiaOptions = new DrawerOptions(skiaManager) { OpaqueBackground = true };

            // Match the Skia side exactly: comparison stays on system fonts (so font A doesn't
            // diverge to the embedded pixel font), font "0" pins to Arial Bold with no condensation
            // (Skia doesn't condense), and font "A" pins to Lucida Console. Avalonia 12 has no
            // public "load this TTF from a file path" entry; instead we drive ResolveTypeface via
            // the FontStack and let FontManager.Current pick the same system files Skia loaded.
            var dotManager = new ZplFontManager
            {
                FontStack0 = new System.Collections.Generic.List<string> { "Arial" },
                FontStackA = new System.Collections.Generic.List<string> { "Lucida Console" },
                IsPixelFont = _ => false,
                Font0FallbackCondense = 1.0,
            };
            var dotOptions = new ZplRendererOptions(dotManager)
            {
                OpaqueBackground = true,
                TextBackend = textBackend,
            };

            return (skiaOptions, dotOptions);
        }

        public static IEnumerable<string> EnumerateTestLabels()
        {
            string dir = Path.Combine(LabelsRoot, "Test");
            if (!Directory.Exists(dir))
            {
                yield break;
            }

            foreach (string file in Directory.EnumerateFiles(dir, "*.zpl2").OrderBy(f => f))
            {
                yield return file;
            }
        }

        public static IEnumerable<string> EnumerateExampleLabels()
        {
            string dir = Path.Combine(LabelsRoot, "Example");
            if (!Directory.Exists(dir))
            {
                yield break;
            }

            foreach (string file in Directory.EnumerateFiles(dir, "*.zpl2").OrderBy(f => f))
            {
                yield return file;
            }
        }

        public static (double width, double height) SizeFromFileName(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            Match m = Regex.Match(name, @"(\d+)x(\d+)$");
            if (m.Success)
            {
                return (double.Parse(m.Groups[1].Value), double.Parse(m.Groups[2].Value));
            }

            return (101.6, 152.4);
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Media;

using BinaryKits.Zpl.Analyzer;
using BinaryKits.Zpl.Label.Elements;
using BinaryKits.Zpl.Viewer;
using BinaryKits.Zpl.Viewer.ElementDrawers;

using SkiaSharp;

using DotZpl.Rendering;
using DotZpl.Text;

namespace DotZpl.UnitTest
{
    /// <summary>
    /// Renders the same ZPL through the Skia reference (<see cref="ZplElementDrawer"/>) and the DotZpl
    /// candidate (<see cref="ZplRenderer"/>) so the two can be compared per element.
    /// </summary>
    internal static class RenderHarness
    {
        public static readonly string LabelsRoot = Path.Combine("Labels");

        /// <summary>Parse the elements of the first label in a ZPL string.</summary>
        public static IList<ZplElementBase> Parse(string zpl)
        {
            var storage = new PrinterStorage();
            var analyzer = new ZplAnalyzer(storage);
            var info = analyzer.Analyze(zpl);
            return info.LabelInfos[0].ZplElements;
        }

        /// <summary>Render both backends. Must be invoked on an STA thread (WPF requirement).</summary>
        public static (byte[] skiaPng, byte[] dotPng) RenderBoth(
            string zpl, double width, double height, int dpmm, TextBackend textBackend = TextBackend.GlyphRun)
        {
            // A single storage instance: analysis populates downloaded graphics (~DG/~DY) that the
            // recall/image-move elements then read at render time (mirrors Common.DefaultPrint).
            var storage = new PrinterStorage();
            var analyzer = new ZplAnalyzer(storage);
            IList<ZplElementBase> elements = analyzer.Analyze(zpl).LabelInfos[0].ZplElements;

            (DrawerOptions skiaOptions, ZplRendererOptions dotOptions) = BuildPinnedOptions(textBackend);

            byte[] skiaPng = new ZplElementDrawer(storage, skiaOptions).Draw(elements, width, height, dpmm);
            byte[] dotPng = new ZplRenderer(storage, dotOptions).DrawPng(elements, width, height, dpmm);

            return (skiaPng, dotPng);
        }

        // The comparison checks WPF == Skia, so both backends must use the *same* font. Pin them to
        // explicit font files (the faces both engines resolved before any extra fonts were installed),
        // so the result is deterministic regardless of which fonts are present on the machine.
        private static readonly Lazy<(string font0, string fontA)?> PinnedFonts = new(() =>
        {
            string dir = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
            string font0 = Path.Combine(dir, "arialbd.ttf");   // Arial Bold  -> ZPL font "0"
            string fontA = Path.Combine(dir, "lucon.ttf");     // Lucida Console -> ZPL font "A"
            return File.Exists(font0) && File.Exists(fontA) ? (font0, fontA) : null;
        });

        private static (DrawerOptions, ZplRendererOptions) BuildPinnedOptions(TextBackend textBackend)
        {
            if (PinnedFonts.Value is not (string font0, string fontA))
            {
                // Fallback: system font resolution (still works, just not font-install-proof).
                return (new DrawerOptions { OpaqueBackground = true },
                        new ZplRendererOptions { OpaqueBackground = true, TextBackend = textBackend });
            }

            var skiaManager = new FontManager();
            SKTypeface skia0 = SKTypeface.FromFile(font0);
            SKTypeface skiaA = SKTypeface.FromFile(fontA);
            skiaManager.FontLoader = name => name == "0" ? skia0 : skiaA;
            var skiaOptions = new DrawerOptions(skiaManager) { OpaqueBackground = true };

            var dotManager = new ZplFontManager();
            var dot0 = new GlyphTypeface(new Uri(font0));
            var dotA = new GlyphTypeface(new Uri(fontA));
            dotManager.FontLoader = name => name == "0" ? dot0 : dotA;
            dotManager.IsPixelFont = _ => false;       // comparison stays on system fonts (matches the Skia side)
            dotManager.Font0FallbackCondense = 1.0;    // pinned to raw Arial; Skia doesn't condense, so neither do we here
            var dotOptions = new ZplRendererOptions(dotManager) { OpaqueBackground = true, TextBackend = textBackend };

            return (skiaOptions, dotOptions);
        }

        /// <summary>Render-and-compare on an STA thread; returns the similarity result.</summary>
        public static RenderComparer.Result RenderAndCompare(
            string zpl, double width, double height, int dpmm, TextBackend textBackend = TextBackend.GlyphRun)
        {
            (byte[] skiaPng, byte[] dotPng) = StaRunner.Run(() => RenderBoth(zpl, width, height, dpmm, textBackend));
            return RenderComparer.Compare(skiaPng, dotPng);
        }

        /// <summary>Load a label file from the copied corpus by file name (with or without subfolder).</summary>
        public static string LoadLabel(string relativePath)
        {
            string path = Path.Combine(LabelsRoot, relativePath);
            return File.ReadAllText(path);
        }

        /// <summary>Enumerate the Test/ corpus for data-driven tests.</summary>
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

        /// <summary>
        /// Parse a label size (mm) from a `...-WxH` filename suffix; defaults to 4x6 inch (101.6x152.4mm).
        /// </summary>
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

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

using BinaryKits.Zpl.Label.Elements;
using BinaryKits.Zpl.Viewer;
using BinaryKits.Zpl.Viewer.ElementDrawers;

using WpfZpl.Rendering;
using WpfZpl.Text;

namespace WpfZpl.UnitTest
{
    /// <summary>
    /// Renders the same ZPL through the Skia reference (<see cref="ZplElementDrawer"/>) and the WPF
    /// candidate (<see cref="WpfZplElementDrawer"/>) so the two can be compared per element.
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
        public static (byte[] skiaPng, byte[] wpfPng) RenderBoth(
            string zpl, double width, double height, int dpmm, TextBackend textBackend = TextBackend.GlyphRun)
        {
            // A single storage instance: analysis populates downloaded graphics (~DG/~DY) that the
            // recall/image-move elements then read at render time (mirrors Common.DefaultPrint).
            var storage = new PrinterStorage();
            var analyzer = new ZplAnalyzer(storage);
            IList<ZplElementBase> elements = analyzer.Analyze(zpl).LabelInfos[0].ZplElements;

            byte[] skiaPng = new ZplElementDrawer(storage, new DrawerOptions { OpaqueBackground = true })
                .Draw(elements, width, height, dpmm);

            byte[] wpfPng = new WpfZplElementDrawer(storage, new WpfDrawerOptions { OpaqueBackground = true, TextBackend = textBackend })
                .DrawPng(elements, width, height, dpmm);

            return (skiaPng, wpfPng);
        }

        /// <summary>Render-and-compare on an STA thread; returns the similarity result.</summary>
        public static RenderComparer.Result RenderAndCompare(
            string zpl, double width, double height, int dpmm, TextBackend textBackend = TextBackend.GlyphRun)
        {
            (byte[] skiaPng, byte[] wpfPng) = StaRunner.Run(() => RenderBoth(zpl, width, height, dpmm, textBackend));
            return RenderComparer.Compare(skiaPng, wpfPng);
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

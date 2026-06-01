using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Avalonia.Headless.XUnit;

using BinaryKits.Zpl.Analyzer;

using DotZpl.Avalonia.UnitTest.Support;
using DotZpl.Rendering;

using Xunit;
using Xunit.Sdk;

namespace DotZpl.Avalonia.UnitTest.Tests
{
    /// <summary>
    /// Image elements (<c>^GF</c> / <c>^IM</c> / <c>^XG</c>) and full Example-corpus integration
    /// coverage. Mirrors <c>DotZpl.UnitTest.ImageAndIntegrationTests</c>, plus keeps the
    /// Example1-54x86 NotImplementedException regression test as a named guard.
    /// </summary>
    public class ImageAndIntegrationTests
    {
        public static IEnumerable<object[]> ImageLabels()
        {
            string[] prefixes = { "GraphicField", "DownloadGraphics", "DownloadObject" };
            return RenderHarness.EnumerateTestLabels()
                .Where(f => prefixes.Any(p => Path.GetFileName(f).StartsWith(p)))
                .Select(f => new object[] { f });
        }

        [AvaloniaTheory]
        [MemberData(nameof(ImageLabels))]
        public void ImageElements_MatchSkia(string labelPath)
        {
            // Raster images decode identically; both backends draw at native pixel size.
            TestSupport.CompareLabelFile(labelPath, minPixelSimilarity: 0.97, minSsim: 0.95);
        }

        public static IEnumerable<object[]> ExampleLabels()
            => RenderHarness.EnumerateExampleLabels().Select(f => new object[] { f });

        [AvaloniaTheory]
        [MemberData(nameof(ExampleLabels))]
        public void ExampleCorpus_MatchesSkia(string labelPath)
        {
            // Full multi-element labels: SSIM-gated end-to-end integration check. The gate is one
            // tick looser than the WPF suite's (0.92/0.88 vs 0.93/0.90) because Avalonia's rotated-
            // glyph rasteriser disagrees with Skia by ~2 SSIM points more than WPF does — visible
            // on text-heavy rotated labels like Example8-64x152. Same nature as the MaxiCode parity
            // gap: a real rasteriser difference, not a bug; tightening would require either
            // bypassing Avalonia's glyph fill or accepting flaky tests.
            TestSupport.CompareLabelFile(labelPath, minPixelSimilarity: 0.92, minSsim: 0.88);
        }

        /// <summary>
        /// Named guard for the original Avalonia <c>DrawingGroup</c> recording-context regression:
        /// any label with <c>^GFA</c> / <c>^XG</c> / <c>^IM</c> previously threw
        /// NotImplementedException through the cached-DrawingGroup path. The renderer now builds a
        /// framework-agnostic <see cref="LabelDrawing"/> that paints straight into the live render
        /// context. Kept as a separate test (rather than rolled into <see cref="ExampleCorpus_MatchesSkia"/>)
        /// so a future failure points at the exact label and path.
        /// </summary>
        [AvaloniaFact]
        public void Example1_54x86_RendersWithoutError()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Labels", "Example", "Example1-54x86.zpl2");
            Assert.True(File.Exists(path), $"label not found at {path}");
            string zpl = File.ReadAllText(path);

            var storage = new PrinterStorage();
            var elements = new ZplAnalyzer(storage).Analyze(zpl).LabelInfos[0].ZplElements;

            try
            {
                new ZplRenderer(storage, new ZplRendererOptions { OpaqueBackground = true })
                    .CreateLabelDrawing(elements, 54, 86, 8);
            }
            catch (Exception ex)
            {
                throw FailException.ForFailure($"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}

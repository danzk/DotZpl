using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using WpfZpl.Text;

namespace WpfZpl.UnitTest
{
    [TestClass]
    public class TextTests
    {
        public TestContext TestContext { get; set; } = null!;

        public static IEnumerable<object[]> TextLabels()
        {
            return RenderHarness.EnumerateTestLabels()
                .Where(f =>
                {
                    string n = Path.GetFileName(f);
                    return n.StartsWith("TextPosition")
                        || n.StartsWith("TextRotation")
                        || n.StartsWith("FieldDataText")
                        || n.StartsWith("GraphicSymbol");
                })
                .Select(f => new object[] { f });
        }

        [DataTestMethod]
        [DynamicData(nameof(TextLabels), DynamicDataSourceType.Method)]
        public void Text_MatchesSkia(string labelPath)
        {
            // Text rasterises differently across engines (hinting/AA); gate on SSIM, looser on pixels.
            TestSupport.CompareLabelFile(labelPath, TestContext, minPixelSimilarity: 0.90, minSsim: 0.85);
        }

        /// <summary>
        /// Spike comparing the two WPF text backends against the Skia reference. Reports SSIM for each
        /// so the better-matching primitive is evident. (Requested "example comparing both approaches".)
        /// </summary>
        [TestMethod]
        public void TextBackendSpike_GlyphRunVsFormattedText()
        {
            string[] samples =
            {
                "Test/TextPosition1-54x86.zpl2",
                "Test/FieldDataText1-102x152.zpl2",
                "Test/TextRotation1-102x152.zpl2",
            };

            double glyphRunTotal = 0, formattedTotal = 0;
            int count = 0;

            foreach (string rel in samples)
            {
                string path = Path.Combine(RenderHarness.LabelsRoot, rel);
                if (!File.Exists(path))
                {
                    continue;
                }

                string zpl = File.ReadAllText(path);
                (double w, double h) = RenderHarness.SizeFromFileName(path);

                RenderComparer.Result glyphRun = RenderHarness.RenderAndCompare(zpl, w, h, 8, TextBackend.GlyphRun);
                RenderComparer.Result formatted = RenderHarness.RenderAndCompare(zpl, w, h, 8, TextBackend.FormattedText);

                TestContext.WriteLine(
                    $"{Path.GetFileName(path)}: GlyphRun SSIM={glyphRun.Ssim:F4}/px={glyphRun.PixelSimilarity:F4} | " +
                    $"FormattedText SSIM={formatted.Ssim:F4}/px={formatted.PixelSimilarity:F4}");

                glyphRunTotal += glyphRun.Ssim;
                formattedTotal += formatted.Ssim;
                count++;
            }

            Assert.IsTrue(count > 0, "no text spike samples found");
            TestContext.WriteLine(
                $"MEAN SSIM => GlyphRun={glyphRunTotal / count:F4}, FormattedText={formattedTotal / count:F4}");

            // Both should be usable; we expect GlyphRun (baseline-origin) to be at least as good.
            Assert.IsTrue(glyphRunTotal / count >= 0.85, "GlyphRun backend should be a strong match");
        }
    }
}

using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotZpl.UnitTest
{
    [TestClass]
    public class ImageAndIntegrationTests
    {
        public TestContext TestContext { get; set; } = null!;

        public static IEnumerable<object[]> ImageLabels()
        {
            string[] prefixes = { "GraphicField", "DownloadGraphics", "DownloadObject" };
            return RenderHarness.EnumerateTestLabels()
                .Where(f => prefixes.Any(p => Path.GetFileName(f).StartsWith(p)))
                .Select(f => new object[] { f });
        }

        [DataTestMethod]
        [DynamicData(nameof(ImageLabels), DynamicDataSourceType.Method)]
        public void ImageElements_MatchSkia(string labelPath)
        {
            // Raster images decode identically; both backends draw at native pixel size.
            TestSupport.CompareLabelFile(labelPath, TestContext, minPixelSimilarity: 0.97, minSsim: 0.95);
        }

        public static IEnumerable<object[]> ExampleLabels()
        {
            string dir = Path.Combine(RenderHarness.LabelsRoot, "Example");
            if (!Directory.Exists(dir))
            {
                yield break;
            }

            foreach (string f in Directory.EnumerateFiles(dir, "*.zpl2").OrderBy(f => f))
            {
                yield return new object[] { f };
            }
        }

        [DataTestMethod]
        [DynamicData(nameof(ExampleLabels), DynamicDataSourceType.Method)]
        public void ExampleCorpus_MatchesSkia(string labelPath)
        {
            // Full multi-element labels: SSIM-gated end-to-end integration check.
            TestSupport.CompareLabelFile(labelPath, TestContext, minPixelSimilarity: 0.93, minSsim: 0.90);
        }
    }
}

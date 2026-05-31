using System.IO;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace WpfZpl.UnitTest
{
    [TestClass]
    public class MaxiCodeTests
    {
        public TestContext TestContext { get; set; } = null!;

        [TestMethod]
        public void MaxiCode_MatchesSkia()
        {
            string path = Path.Combine(RenderHarness.LabelsRoot, "Test", "MaxiCode-102x152.zpl2");
            // Tiny hexagons + finder rings; AA differs (Skia draws aliased), so gate loosely on SSIM.
            TestSupport.CompareLabelFile(path, TestContext, minPixelSimilarity: 0.90, minSsim: 0.85);
        }
    }
}

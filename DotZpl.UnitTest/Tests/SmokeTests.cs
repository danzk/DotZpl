using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotZpl.UnitTest
{
    [TestClass]
    public class SmokeTests
    {
        /// <summary>
        /// An empty label must render to an identical (all-white, opaque background) image on both
        /// backends — proves the RenderTargetBitmap + PNG encode + STA + comparison plumbing works.
        /// </summary>
        [TestMethod]
        public void EmptyLabel_BothBackendsIdentical()
        {
            const string zpl = "^XA^XZ";

            RenderComparer.Result result = RenderHarness.RenderAndCompare(zpl, 50, 30, 8);

            Assert.AreEqual(400, result.Width, "width = 50mm * 8dpmm");
            Assert.AreEqual(240, result.Height, "height = 30mm * 8dpmm");
            Assert.IsTrue(result.PixelSimilarity > 0.999, $"empty label should be ~identical: {result}");
            Assert.IsTrue(result.Ssim > 0.999, $"empty label SSIM should be ~1: {result}");
        }
    }
}

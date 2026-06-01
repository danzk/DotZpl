using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotZpl.UnitTest
{
    [TestClass]
    public class BarcodeTests
    {
        public TestContext TestContext { get; set; } = null!;

        public static IEnumerable<object[]> Barcode1DLabels()
        {
            string[] prefixes =
            {
                "Barcode128", "Barcode39", "Barcode93", "BarcodeEAN13",
                "BarcodeI2of5", "BarcodeUpcA", "BarcodeUpcE", "BarcodeOrientation",
            };

            return RenderHarness.EnumerateTestLabels()
                .Where(f => prefixes.Any(p => Path.GetFileName(f).StartsWith(p)))
                .Select(f => new object[] { f });
        }

        [DataTestMethod]
        [DynamicData(nameof(Barcode1DLabels), DynamicDataSourceType.Method)]
        public void Barcode1D_MatchesSkia(string labelPath)
        {
            // Module geometry should be near-exact; interpretation text adds minor AA differences.
            TestSupport.CompareLabelFile(labelPath, TestContext, minPixelSimilarity: 0.95, minSsim: 0.93);
        }

        public static IEnumerable<object[]> Barcode2DLabels()
        {
            string[] prefixes = { "QrCode", "DataMatrix", "BarcodePDF417" };
            return RenderHarness.EnumerateTestLabels()
                .Where(f => prefixes.Any(p => Path.GetFileName(f).StartsWith(p)))
                .Select(f => new object[] { f });
        }

        [DataTestMethod]
        [DynamicData(nameof(Barcode2DLabels), DynamicDataSourceType.Method)]
        public void Barcode2D_MatchesSkia(string labelPath)
        {
            // Module-exact geometry; with EdgeMode aliased these should be very close to Skia.
            TestSupport.CompareLabelFile(labelPath, TestContext, minPixelSimilarity: 0.97, minSsim: 0.95);
        }
    }
}

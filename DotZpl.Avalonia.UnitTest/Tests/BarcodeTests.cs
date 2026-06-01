using System.Collections.Generic;
using System.IO;
using System.Linq;

using Avalonia.Headless.XUnit;

using DotZpl.Avalonia.UnitTest.Support;

using Xunit;

namespace DotZpl.Avalonia.UnitTest.Tests
{
    /// <summary>1D + 2D barcode parity against the Skia reference. Mirrors <c>DotZpl.UnitTest.BarcodeTests</c>.</summary>
    public class BarcodeTests
    {
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

        [AvaloniaTheory]
        [MemberData(nameof(Barcode1DLabels))]
        public void Barcode1D_MatchesSkia(string labelPath)
        {
            // Module geometry should be near-exact; interpretation text adds minor AA differences.
            TestSupport.CompareLabelFile(labelPath, minPixelSimilarity: 0.95, minSsim: 0.93);
        }

        public static IEnumerable<object[]> Barcode2DLabels()
        {
            string[] prefixes = { "QrCode", "DataMatrix", "BarcodePDF417" };
            return RenderHarness.EnumerateTestLabels()
                .Where(f => prefixes.Any(p => Path.GetFileName(f).StartsWith(p)))
                .Select(f => new object[] { f });
        }

        [AvaloniaTheory]
        [MemberData(nameof(Barcode2DLabels))]
        public void Barcode2D_MatchesSkia(string labelPath)
        {
            // Module-exact geometry; with EdgeMode aliased these should be very close to Skia.
            TestSupport.CompareLabelFile(labelPath, minPixelSimilarity: 0.97, minSsim: 0.95);
        }
    }
}

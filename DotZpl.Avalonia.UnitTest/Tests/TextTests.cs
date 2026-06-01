using System.Collections.Generic;
using System.IO;
using System.Linq;

using Avalonia.Headless.XUnit;

using DotZpl.Avalonia.UnitTest.Support;

using Xunit;

namespace DotZpl.Avalonia.UnitTest.Tests
{
    /// <summary>Text-element parity against the Skia reference. Mirrors <c>DotZpl.UnitTest.TextTests</c>.</summary>
    public class TextTests
    {
        public static IEnumerable<object[]> TextLabels()
            => RenderHarness.EnumerateTestLabels()
                .Where(f =>
                {
                    string n = Path.GetFileName(f);
                    return n.StartsWith("TextPosition")
                        || n.StartsWith("TextRotation")
                        || n.StartsWith("FieldDataText")
                        || n.StartsWith("GraphicSymbol");
                })
                .Select(f => new object[] { f });

        [AvaloniaTheory]
        [MemberData(nameof(TextLabels))]
        public void Text_MatchesSkia(string labelPath)
        {
            // Text rasterises differently across engines (hinting/AA); gate on SSIM, looser on pixels.
            TestSupport.CompareLabelFile(labelPath, minPixelSimilarity: 0.90, minSsim: 0.85);
        }
    }
}

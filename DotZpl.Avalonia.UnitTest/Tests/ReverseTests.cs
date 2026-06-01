using System.Collections.Generic;
using System.IO;
using System.Linq;

using Avalonia.Headless.XUnit;

using DotZpl.Avalonia.UnitTest.Support;

using Xunit;

namespace DotZpl.Avalonia.UnitTest.Tests
{
    /// <summary>
    /// Field Reverse (<c>^FR</c>) and invert-draw coverage — exercises the Xor/Exclude paths of
    /// the running background composition. These are the paths most sensitive to the recent
    /// switch from <c>CombinedGeometry(Union, …)</c> to <c>GeometryGroup</c> in the orchestrator,
    /// since the Xor/Exclude operators now have a GeometryGroup operand rather than a deep
    /// CombinedGeometry tree.
    /// </summary>
    public class ReverseTests
    {
        public static IEnumerable<object[]> ReverseLabels()
            => RenderHarness.EnumerateTestLabels()
                .Where(f => Path.GetFileName(f).StartsWith("FieldReversePrint"))
                .Select(f => new object[] { f });

        [AvaloniaTheory]
        [MemberData(nameof(ReverseLabels))]
        public void FieldReverse_MatchesSkia(string labelPath)
        {
            TestSupport.CompareLabelFile(labelPath, minPixelSimilarity: 0.95, minSsim: 0.92);
        }
    }
}

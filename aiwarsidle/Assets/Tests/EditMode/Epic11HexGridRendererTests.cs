using System.Collections.Generic;
using AIWarsIdle.UI.Pvp;
using NUnit.Framework;

namespace AIWarsIdle.Tests.EditMode
{
    public sealed class Epic11HexGridRendererTests
    {
        [TestCase(0, 1)]
        [TestCase(1, 7)]
        [TestCase(2, 19)]
        [TestCase(3, 37)]
        [TestCase(4, 61)]
        public void HexGridMath_EnumerateAxial_ReturnsExpectedCellCount(int radius, int expectedCount)
        {
            var coords = HexGridMath.EnumerateAxial(radius);

            Assert.AreEqual(expectedCount, coords.Count);
        }

        [Test]
        public void HexGridMath_EnumerateAxial_ReturnsUniqueCoords_ForRadius4()
        {
            var coords = HexGridMath.EnumerateAxial(4);
            var unique = new HashSet<HexCoord>(coords);

            Assert.AreEqual(61, coords.Count);
            Assert.AreEqual(coords.Count, unique.Count);
        }

        [Test]
        public void HexGridMath_GetCornerCoords_ReturnsSixCorners_ForRadius4()
        {
            var corners = HexGridMath.GetCornerCoords(4);

            CollectionAssert.AreEquivalent(
                new[]
                {
                    new HexCoord(4, 0),
                    new HexCoord(0, 4),
                    new HexCoord(-4, 4),
                    new HexCoord(-4, 0),
                    new HexCoord(0, -4),
                    new HexCoord(4, -4)
                },
                corners);
        }

        [TestCase(1, 0, true)]
        [TestCase(7, 1, true)]
        [TestCase(19, 2, true)]
        [TestCase(61, 4, true)]
        [TestCase(62, 0, false)]
        public void HexGridRenderer_TryInferRadiusFromCellCount_Works(int count, int expectedRadius, bool expected)
        {
            var ok = HexGridRenderer.TryInferRadiusFromCellCount(count, out var radius);

            Assert.AreEqual(expected, ok);
            if (expected)
            {
                Assert.AreEqual(expectedRadius, radius);
            }
        }
    }
}

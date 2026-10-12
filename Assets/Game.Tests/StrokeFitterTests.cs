using System.Collections.Generic;
using Game.Client.Stroke;
using Game.Sim.Stroke;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public class StrokeFitterTests
    {
        private const float Eps = 1e-4f;

        private static void AssertPoint(Vector2 expected, Vector2 actual)
        {
            Assert.AreEqual(expected.x, actual.x, Eps);
            Assert.AreEqual(expected.y, actual.y, Eps);
        }

        [Test]
        public void EmptyOrNull_ReturnsEmpty()
        {
            Assert.IsEmpty(StrokeFitter.FitToUnitSquare(null));
            Assert.IsEmpty(StrokeFitter.FitToUnitSquare(new StrokePoint[0]));
        }

        [Test]
        public void LongerSideSpansUnit_ShorterSideCentred()
        {
            // 4 x 2 box offset from the origin.
            List<Vector2> fitted = StrokeFitter.FitToUnitSquare(new[]
            {
                StrokePoint.FromInt(10, 20), StrokePoint.FromInt(14, 22)
            });
            AssertPoint(new Vector2(0f, 0.25f), fitted[0]);
            AssertPoint(new Vector2(1f, 0.75f), fitted[1]);
        }

        [Test]
        public void SameShape_InDifferentUnits_FitsIdentically()
        {
            // Frame-centimetre stroke vs the same shape at 100x in canvas units.
            var cm = new[] { new StrokePoint(-1500, 0), new StrokePoint(0, 1000), new StrokePoint(1500, -500) };
            var canvas = new[] { StrokePoint.FromInt(-150, 0), StrokePoint.FromInt(0, 100), StrokePoint.FromInt(150, -50) };
            List<Vector2> a = StrokeFitter.FitToUnitSquare(cm);
            List<Vector2> b = StrokeFitter.FitToUnitSquare(canvas);
            for (int i = 0; i < a.Count; i++)
                AssertPoint(a[i], b[i]);
        }

        [Test]
        public void StraightHorizontalLine_IsCentredVertically()
        {
            List<Vector2> fitted = StrokeFitter.FitToUnitSquare(new[] { StrokePoint.FromInt(0, 5), StrokePoint.FromInt(8, 5) });
            AssertPoint(new Vector2(0f, 0.5f), fitted[0]);
            AssertPoint(new Vector2(1f, 0.5f), fitted[1]);
        }

        [Test]
        public void AllPointsEqual_CentredWithoutNaN()
        {
            List<Vector2> fitted = StrokeFitter.FitToUnitSquare(new[] { StrokePoint.FromInt(3, 3), StrokePoint.FromInt(3, 3) });
            foreach (Vector2 p in fitted)
                AssertPoint(new Vector2(0.5f, 0.5f), p);
        }
    }
}

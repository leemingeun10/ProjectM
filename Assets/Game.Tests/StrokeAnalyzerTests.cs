using System;
using System.Collections.Generic;
using Game.Sim.Stroke;
using NUnit.Framework;

namespace Game.Tests
{
    public class StrokeAnalyzerTests
    {
        [Test]
        public void StraightLine_IsClassifiedAsSlash()
        {
            var points = new List<StrokePoint>();
            for (int i = 0; i < 20; i++)
                points.Add(StrokePoint.FromInt(i * 50, 0));

            StrokeResult result = StrokeAnalyzer.Analyze(points);

            Assert.AreEqual(StrokeType.Slash, result.Type);
        }

        [Test]
        public void NearlyClosedLoop_IsClassifiedAsThrust()
        {
            // A circle arc that stops just short of where it started, leaving a small gap
            // that is well within SelfContactThreshold (3% of total path length).
            const int count = 32;
            const double radius = 500;
            double sweep = Math.PI * 2 * 0.99;

            var points = new List<StrokePoint>();
            for (int i = 0; i < count; i++)
            {
                double t = (double)i / (count - 1) * sweep;
                long x = (long)Math.Round(Math.Cos(t) * radius);
                long y = (long)Math.Round(Math.Sin(t) * radius);
                points.Add(StrokePoint.FromInt(x, y));
            }

            StrokeResult result = StrokeAnalyzer.Analyze(points);

            Assert.AreEqual(StrokeType.Thrust, result.Type);
        }

        [Test]
        public void FigureEight_MidSegmentCrossing_IsClassifiedAsThrust()
        {
            // Two diagonals crossing at the origin, in the middle of each segment
            // (not at a shared vertex), like a figure-eight stroke.
            var points = new List<StrokePoint>
            {
                StrokePoint.FromInt(-300, 300),
                StrokePoint.FromInt(300, -300),
                StrokePoint.FromInt(300, 300),
                StrokePoint.FromInt(-300, -300),
            };

            StrokeResult result = StrokeAnalyzer.Analyze(points);

            Assert.AreEqual(StrokeType.Thrust, result.Type);
        }

        [Test]
        public void Zigzag_WithoutSelfContact_IsClassifiedAsSlashWithoutThrowing()
        {
            var points = new List<StrokePoint>();
            for (int i = 0; i < 20; i++)
            {
                long y = (i % 2 == 0) ? 0 : 50;
                points.Add(StrokePoint.FromInt(i * 100, y));
            }

            StrokeResult result = default;
            Assert.DoesNotThrow(() => result = StrokeAnalyzer.Analyze(points));
            Assert.AreEqual(StrokeType.Slash, result.Type);
        }
    }
}

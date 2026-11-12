using System;
using System.Collections.Generic;
using Game.Sim.Common;
using Game.Sim.Shape;
using NUnit.Framework;

namespace Game.Tests
{
    public class ShapeRecognizerTests
    {
        private const int Threshold = ShapeRecognizer.DefaultThreshold;

        // ---- Shape builders (tests may use double; only Game.sim may not) ----

        // Equilateral triangle with circumradius `radius`, apex at `rotationDegrees` + 90°,
        // traced anticlockwise from the apex back to it with `stepsPerEdge` points per edge.
        private static List<ShapePoint> Triangle(double cx = 0, double cy = 0, double radius = 3000,
            double rotationDegrees = 0, int stepsPerEdge = 200, double jitter = 0, int seed = 0)
        {
            var rng = new Random(seed);
            var corners = new (double x, double y)[4];
            for (int k = 0; k < 3; k++)
            {
                double a = (90 + rotationDegrees + k * 120) * Math.PI / 180;
                corners[k] = (cx + radius * Math.Cos(a), cy + radius * Math.Sin(a));
            }
            corners[3] = corners[0];

            var points = new List<ShapePoint>();
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < stepsPerEdge; j++)
                {
                    double f = (double)j / stepsPerEdge;
                    double x = corners[i].x * (1 - f) + corners[i + 1].x * f;
                    double y = corners[i].y * (1 - f) + corners[i + 1].y * f;
                    points.Add(Point(x + Jitter(rng, jitter), y + Jitter(rng, jitter)));
                }
            }
            points.Add(Point(corners[0].x, corners[0].y));
            return points;
        }

        // Five-petal outline r = radius * (1 + 0.5 cos 5θ), first petal tip at rotationDegrees + 90°.
        // The pen starts startDegrees round from that tip (36 = between two petals) and covers
        // `coverage` of the loop.
        private static List<ShapePoint> Flower(double radius = 1500, double rotationDegrees = 0,
            int samples = 400, double jitter = 0, int seed = 0, double startDegrees = 0, double coverage = 1)
        {
            var rng = new Random(seed);
            var points = new List<ShapePoint>();
            for (int i = 0; i <= (int)(samples * coverage); i++)
            {
                double theta = 2 * Math.PI * i / samples + startDegrees * Math.PI / 180;
                double r = radius * (1 + 0.5 * Math.Cos(5 * theta));
                double a = theta + (90 + rotationDegrees) * Math.PI / 180;
                points.Add(Point(r * Math.Cos(a) + Jitter(rng, jitter), r * Math.Sin(a) + Jitter(rng, jitter)));
            }
            return points;
        }

        // Straight sword outline, tip up, traced from the tip down the left of the blade, round the
        // guard and grip and back up the right (ShapeTemplates' sword proportions, 300 units each).
        private static List<ShapePoint> Sword(double blade = 5.5, double bladeWidth = 1.2, double guard = 3,
            double gripLength = 2, double rotationDegrees = 0, double jitter = 0, int seed = 0)
        {
            const double scale = 300, grip = 0.6, guardHeight = 1;
            double g0 = gripLength, g1 = gripLength + guardHeight;
            var corners = new (double x, double y)[]
            {
                (0, g1 + blade), (-bladeWidth, g1), (-guard, g1), (-guard, g0), (-grip, g0), (-grip, 0),
                (grip, 0), (grip, g0), (guard, g0), (guard, g1), (bladeWidth, g1), (0, g1 + blade),
            };

            var rng = new Random(seed);
            double a = rotationDegrees * Math.PI / 180;
            bool exact = rotationDegrees == 0 && jitter == 0;
            var points = new List<ShapePoint>();
            for (int i = 0; i < corners.Length; i++)
            {
                // The exact template shape is just its corners; a moved or shaky one gets points
                // along each edge so the jitter applies all the way round.
                int steps = exact || i == corners.Length - 1 ? 1 : 12;
                (double x0, double y0) = corners[i];
                (double x1, double y1) = i + 1 < corners.Length ? corners[i + 1] : corners[i];
                for (int j = 0; j < steps; j++)
                {
                    double f = (double)j / steps;
                    double x = (x0 + (x1 - x0) * f) * scale, y = (y0 + (y1 - y0) * f) * scale;
                    points.Add(Point(x * Math.Cos(a) - y * Math.Sin(a) + Jitter(rng, jitter),
                        x * Math.Sin(a) + y * Math.Cos(a) + Jitter(rng, jitter)));
                }
            }
            return points;
        }

        // Crescent opening to the right: outer arc of radius `radius` from openDegrees anticlockwise
        // round to -openDegrees, then back along the circle through (inner * radius, 0) and both ends.
        private static List<ShapePoint> Crescent(double inner = -0.6, double openDegrees = 50, double radius = 1500,
            double rotationDegrees = 0, double jitter = 0, int seed = 0, int samples = 200)
        {
            double open = openDegrees * Math.PI / 180;
            double ex = Math.Cos(open), ey = Math.Sin(open);
            double c = (ex * ex + ey * ey - inner * inner) / (2 * (ex - inner));
            double r = c - inner;

            var unit = new List<(double x, double y)>();
            for (int i = 0; i <= samples; i++)
            {
                double t = open + (2 * Math.PI - 2 * open) * i / samples;
                unit.Add((Math.Cos(t), Math.Sin(t)));
            }
            double t0 = Math.Atan2(-ey, ex - c) + 2 * Math.PI;
            double t1 = Math.Atan2(ey, ex - c);
            for (int i = 1; i <= samples; i++)
            {
                double t = t0 - (t0 - t1) * i / samples;
                unit.Add((c + r * Math.Cos(t), r * Math.Sin(t)));
            }

            var rng = new Random(seed);
            double a = rotationDegrees * Math.PI / 180;
            var points = new List<ShapePoint>();
            foreach ((double x, double y) in unit)
            {
                if (rotationDegrees == 0 && jitter == 0)
                {
                    // Truncated like the generator that produced the template data.
                    points.Add(new ShapePoint((int)(x * radius), (int)(y * radius)));
                    continue;
                }
                points.Add(Point((x * Math.Cos(a) - y * Math.Sin(a)) * radius + Jitter(rng, jitter),
                    (x * Math.Sin(a) + y * Math.Cos(a)) * radius + Jitter(rng, jitter)));
            }
            return points;
        }

        // Arrow pointing up: a sword-like silhouette that must not count as one.
        private static List<ShapePoint> Arrow()
        {
            var corners = new[] { (0, 6), (-3, 3), (-1, 3), (-1, -4), (1, -4), (1, 3), (3, 3), (0, 6) };
            var points = new List<ShapePoint>();
            foreach ((int x, int y) in corners)
                points.Add(new ShapePoint(x * 300, y * 300));
            return points;
        }

        private static List<ShapePoint> Circle(double radius = 1500, int samples = 200)
        {
            var points = new List<ShapePoint>();
            for (int i = 0; i <= samples; i++)
            {
                double a = 2 * Math.PI * i / samples;
                points.Add(Point(radius * Math.Cos(a), radius * Math.Sin(a)));
            }
            return points;
        }

        private static List<ShapePoint> Zigzag(int seed)
        {
            var rng = new Random(seed);
            var points = new List<ShapePoint>();
            for (int i = 0; i < 8; i++)
                points.Add(new ShapePoint(rng.Next(-3000, 3001), rng.Next(-3000, 3001)));
            return points;
        }

        private static double Jitter(Random rng, double amount) => amount == 0 ? 0 : (rng.NextDouble() * 2 - 1) * amount;

        private static ShapePoint Point(double x, double y) => new ShapePoint((int)Math.Round(x), (int)Math.Round(y));

        private static ShapePoint[] Prepare(IReadOnlyList<ShapePoint> raw)
            => ShapeRecognizer.Normalize(ShapeRecognizer.Resample(raw, ShapeRecognizer.PointCount));

        // Same as Recognize: forward and reversed drawing order, better of the two.
        private static int Score(IReadOnlyList<ShapePoint> raw, ShapeTemplate template)
        {
            ShapePoint[] candidate = Prepare(raw);
            var reversed = (ShapePoint[])candidate.Clone();
            Array.Reverse(reversed);
            return Math.Max(
                ShapeRecognizer.BestRotationScore(candidate, template.NormalizedPoints),
                ShapeRecognizer.BestRotationScore(reversed, template.NormalizedPoints));
        }

        // ---- Resample ----

        [TestCase(2)]
        [TestCase(5)]
        [TestCase(64)]
        [TestCase(300)]
        public void Resample_ReturnsExactlyNPoints(int n)
        {
            Assert.AreEqual(n, ShapeRecognizer.Resample(Triangle(stepsPerEdge: 3), n).Length);
            Assert.AreEqual(n, ShapeRecognizer.Resample(Flower(), n).Length);
            Assert.AreEqual(n, ShapeRecognizer.Resample(new[] { new ShapePoint(0, 0), new ShapePoint(1000, 0) }, n).Length);
        }

        [Test]
        public void Resample_KeepsEndpointsAndSpacesPointsEvenly()
        {
            var line = new[] { new ShapePoint(0, 0), new ShapePoint(100, 0), new ShapePoint(6300, 0) };

            ShapePoint[] result = ShapeRecognizer.Resample(line, 64);

            for (int i = 0; i < 64; i++)
            {
                Assert.AreEqual(i * 100, result[i].X, $"point {i}");
                Assert.AreEqual(0, result[i].Y, $"point {i}");
            }
        }

        [Test]
        public void Resample_ZeroLengthPath_RepeatsThePoint()
        {
            var dot = new[] { new ShapePoint(7, -3), new ShapePoint(7, -3) };

            foreach (ShapePoint p in ShapeRecognizer.Resample(dot, 10))
                Assert.AreEqual((7, -3), (p.X, p.Y));
        }

        [Test]
        public void Resample_InvalidArguments_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => ShapeRecognizer.Resample(null, 64));
            Assert.Throws<ArgumentException>(() => ShapeRecognizer.Resample(new ShapePoint[0], 64));
            Assert.Throws<ArgumentOutOfRangeException>(() => ShapeRecognizer.Resample(Circle(), 1));
        }

        // ---- Normalize ----

        [Test]
        public void Normalize_CentresOnOriginAtTheReferenceSize()
        {
            ShapePoint[] normalized = Prepare(Triangle(cx: 4000, cy: -2500, radius: 1700));

            long sumX = 0, sumY = 0, sumSquares = 0;
            foreach (ShapePoint p in normalized)
            {
                sumX += p.X;
                sumY += p.Y;
                sumSquares += (long)p.X * p.X + (long)p.Y * p.Y;
            }
            Assert.LessOrEqual(Math.Abs(sumX / normalized.Length), 1);
            Assert.LessOrEqual(Math.Abs(sumY / normalized.Length), 1);
            long rms = IntegerMath.IntegerSqrt(sumSquares / normalized.Length);
            Assert.AreEqual(ShapeRecognizer.NormalizedRadius, rms, 2);
        }

        [Test]
        public void Normalize_SameShapeAtAnotherPositionAndSize_GivesNearlyTheSamePoints()
        {
            ShapePoint[] small = Prepare(Triangle(cx: -800, cy: 300, radius: 900));
            ShapePoint[] large = Prepare(Triangle(cx: 5000, cy: 7000, radius: 4200));
            ShapePoint[] flowerSmall = Prepare(Flower(radius: 600));
            ShapePoint[] flowerLarge = Prepare(Flower(radius: 3000));

            for (int i = 0; i < ShapeRecognizer.PointCount; i++)
            {
                Assert.AreEqual(small[i].X, large[i].X, 5, $"triangle point {i} X");
                Assert.AreEqual(small[i].Y, large[i].Y, 5, $"triangle point {i} Y");
                Assert.AreEqual(flowerSmall[i].X, flowerLarge[i].X, 5, $"flower point {i} X");
                Assert.AreEqual(flowerSmall[i].Y, flowerLarge[i].Y, 5, $"flower point {i} Y");
            }
        }

        [Test]
        public void Normalize_CoincidentPoints_ComeBackAsZero()
        {
            ShapePoint[] result = ShapeRecognizer.Normalize(new[] { new ShapePoint(5, 5), new ShapePoint(5, 5) });

            foreach (ShapePoint p in result)
                Assert.AreEqual((0, 0), (p.X, p.Y));
        }

        // ---- Templates ----

        [Test]
        public void Templates_HaveExactlyPointCountPoints()
        {
            foreach (ShapeTemplate template in ShapeTemplates.All)
                Assert.AreEqual(ShapeRecognizer.PointCount, template.NormalizedPoints.Length, template.Id);
        }

        // The hardcoded data must be what Resample + Normalize produces from the ideal shapes, so
        // editing the recognizer without regenerating the templates is caught here.
        [Test]
        public void Templates_MatchTheirIdealShapesRegenerated()
        {
            AssertPointsClose(Prepare(Triangle()), ShapeTemplates.Triangle.NormalizedPoints, "triangle");
            AssertPointsClose(Prepare(Flower()), ShapeTemplates.Flower.NormalizedPoints, "flower");
            AssertPointsClose(Prepare(Sword(5.5, 1.2)), ShapeTemplates.SwordShort.NormalizedPoints, "sword short");
            AssertPointsClose(Prepare(Sword(8, 0.9)), ShapeTemplates.SwordLong.NormalizedPoints, "sword long");
            AssertPointsClose(Prepare(Crescent(-0.6, 50)), ShapeTemplates.CrescentFull.NormalizedPoints, "crescent full");
            AssertPointsClose(Prepare(Crescent(-0.15, 55)), ShapeTemplates.CrescentThin.NormalizedPoints, "crescent thin");
        }

        private static void AssertPointsClose(ShapePoint[] expected, ShapePoint[] actual, string name)
        {
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i].X, actual[i].X, 2, $"{name} point {i} X");
                Assert.AreEqual(expected[i].Y, actual[i].Y, 2, $"{name} point {i} Y");
            }
        }

        // ---- BestRotationScore ----

        [Test]
        public void BestRotationScore_TemplateAgainstItself_IsPerfect()
        {
            foreach (ShapeTemplate template in ShapeTemplates.All)
                Assert.AreEqual(ShapeRecognizer.MaxScore, ShapeRecognizer.BestRotationScore(template.NormalizedPoints, template.NormalizedPoints), template.Id);
        }

        [TestCase(10)]
        [TestCase(90)]
        [TestCase(200)]
        [TestCase(280)]
        public void BestRotationScore_RotatedShapes_StillScoreHigh(int degrees)
        {
            int triangleScore = Score(Triangle(rotationDegrees: degrees), ShapeTemplates.Triangle);
            int flowerScore = Score(Flower(rotationDegrees: degrees), ShapeTemplates.Flower);

            Assert.GreaterOrEqual(triangleScore, 9900, "triangle");
            Assert.GreaterOrEqual(flowerScore, 9900, "flower");
        }

        [Test]
        public void BestRotationScore_RotationBetweenSteps_StaysAboveThreshold()
        {
            // 45° is 5° from the nearest searched rotation, the worst case for the 10° step.
            Assert.GreaterOrEqual(Score(Triangle(rotationDegrees: 45), ShapeTemplates.Triangle), Threshold);
            Assert.GreaterOrEqual(Score(Flower(rotationDegrees: 41), ShapeTemplates.Flower), Threshold);
        }

        [Test]
        public void BestRotationScore_MismatchedLengths_Throw()
        {
            Assert.Throws<ArgumentException>(() => ShapeRecognizer.BestRotationScore(new ShapePoint[3], new ShapePoint[4]));
            Assert.Throws<ArgumentNullException>(() => ShapeRecognizer.BestRotationScore(null, new ShapePoint[4]));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        [TestCase(7)]
        [TestCase(8)]
        [TestCase(9)]
        public void RandomZigzag_ScoresLowOnBothTemplates(int seed)
        {
            List<ShapePoint> zigzag = Zigzag(seed);

            Assert.Less(Score(zigzag, ShapeTemplates.Triangle), 8500, "triangle");
            Assert.Less(Score(zigzag, ShapeTemplates.Flower), 8500, "flower");
            Assert.IsNull(ShapeRecognizer.Recognize(zigzag, ShapeTemplates.All, Threshold));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void JitteredTriangle_PassesTriangleButNotFlower(int seed)
        {
            // ±150 on a 1500 circumradius: corners and edges visibly wobble, like a quick hand drawing.
            List<ShapePoint> shaky = Triangle(radius: 1500, rotationDegrees: seed * 23, stepsPerEdge: 20, jitter: 150, seed: seed);

            Assert.GreaterOrEqual(Score(shaky, ShapeTemplates.Triangle), Threshold, "triangle");
            Assert.Less(Score(shaky, ShapeTemplates.Flower), Threshold, "flower");
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void JitteredFlower_PassesFlowerButNotTriangle(int seed)
        {
            List<ShapePoint> shaky = Flower(rotationDegrees: seed * 31, jitter: 100, seed: seed);

            Assert.GreaterOrEqual(Score(shaky, ShapeTemplates.Flower), Threshold, "flower");
            Assert.Less(Score(shaky, ShapeTemplates.Triangle), Threshold, "triangle");
        }

        // ---- Recognize ----

        [Test]
        public void Recognize_PicksTheMatchingTemplate()
        {
            ShapeMatchResult? triangle = ShapeRecognizer.Recognize(Triangle(cx: 300, cy: 200, radius: 1200, rotationDegrees: 10), ShapeTemplates.All, Threshold);
            ShapeMatchResult? flower = ShapeRecognizer.Recognize(Flower(radius: 800, rotationDegrees: 200), ShapeTemplates.All, Threshold);

            Assert.IsTrue(triangle.HasValue);
            Assert.AreEqual(ShapeTemplates.TriangleId, triangle.Value.TemplateId);
            Assert.GreaterOrEqual(triangle.Value.Score, Threshold);
            Assert.IsTrue(flower.HasValue);
            Assert.AreEqual(ShapeTemplates.FlowerId, flower.Value.TemplateId);
        }

        [Test]
        public void Recognize_ShapeDrawnClockwise_IsStillRecognized()
        {
            List<ShapePoint> clockwise = Triangle(rotationDegrees: 30);
            clockwise.Reverse();

            ShapeMatchResult? result = ShapeRecognizer.Recognize(clockwise, ShapeTemplates.All, Threshold);

            Assert.IsTrue(result.HasValue);
            Assert.AreEqual(ShapeTemplates.TriangleId, result.Value.TemplateId);
        }

        [TestCase(18)]
        [TestCase(36)]
        [TestCase(50)]
        public void Recognize_FlowerStartedAwayFromAPetalTip_IsStillRecognized(int startDegrees)
        {
            ShapeMatchResult? result = ShapeRecognizer.Recognize(Flower(startDegrees: startDegrees), ShapeTemplates.All, Threshold);

            Assert.IsTrue(result.HasValue);
            Assert.AreEqual(ShapeTemplates.FlowerId, result.Value.TemplateId);
        }

        [Test]
        public void Recognize_TriangleStartedMidEdge_IsStillRecognized()
        {
            List<ShapePoint> triangle = Triangle(stepsPerEdge: 20);
            var midEdge = new List<ShapePoint>();
            midEdge.AddRange(triangle.GetRange(30, triangle.Count - 30));
            midEdge.AddRange(triangle.GetRange(1, 30));

            ShapeMatchResult? result = ShapeRecognizer.Recognize(midEdge, ShapeTemplates.All, Threshold);

            Assert.IsTrue(result.HasValue);
            Assert.AreEqual(ShapeTemplates.TriangleId, result.Value.TemplateId);
        }

        [Test]
        public void Recognize_FlowerMissingAPart_IsNotRecognized()
        {
            Assert.IsNull(ShapeRecognizer.Recognize(Flower(coverage: 0.9), ShapeTemplates.All, Threshold));
        }

        // Blade length / width, guard width and grip length spread around both sword templates.
        [TestCase(5, 0.8, 2.5, 1.5)]
        [TestCase(5, 1.3, 3.5, 2.5)]
        [TestCase(7, 1.0, 3.0, 2.0)]
        [TestCase(9, 0.8, 2.5, 2.5)]
        [TestCase(9, 1.3, 3.5, 1.5)]
        public void Recognize_SwordsOfOtherProportions_AreSwords(double blade, double bladeWidth, double guard, double gripLength)
        {
            ShapeMatchResult? result = ShapeRecognizer.Recognize(Sword(blade, bladeWidth, guard, gripLength, rotationDegrees: 20),
                ShapeTemplates.All, Threshold);

            Assert.IsTrue(result.HasValue);
            Assert.AreEqual(ShapeTemplates.SwordId, result.Value.TemplateId);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void Recognize_ShakySword_IsASword(int seed)
        {
            ShapeMatchResult? result = ShapeRecognizer.Recognize(Sword(7, 1, rotationDegrees: seed * 70, jitter: 40, seed: seed),
                ShapeTemplates.All, Threshold);

            Assert.IsTrue(result.HasValue);
            Assert.AreEqual(ShapeTemplates.SwordId, result.Value.TemplateId);
        }

        // Inner-arc depth and opening spread from a fat to a thin crescent.
        [TestCase(-0.7, 40)]
        [TestCase(-0.5, 55)]
        [TestCase(-0.3, 70)]
        [TestCase(-0.1, 40)]
        [TestCase(0.1, 55)]
        public void Recognize_CrescentsOfOtherProportions_AreCrescents(double inner, double openDegrees)
        {
            ShapeMatchResult? result = ShapeRecognizer.Recognize(Crescent(inner, openDegrees, rotationDegrees: 120),
                ShapeTemplates.All, Threshold);

            Assert.IsTrue(result.HasValue);
            Assert.AreEqual(ShapeTemplates.CrescentId, result.Value.TemplateId);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void Recognize_ShakyCrescent_IsACrescent(int seed)
        {
            ShapeMatchResult? result = ShapeRecognizer.Recognize(Crescent(-0.4, 50, rotationDegrees: seed * 100, jitter: 50, seed: seed),
                ShapeTemplates.All, Threshold);

            Assert.IsTrue(result.HasValue);
            Assert.AreEqual(ShapeTemplates.CrescentId, result.Value.TemplateId);
        }

        [Test]
        public void Recognize_TriangleAndFlower_KeepTheirOwnTechniques()
        {
            Assert.AreEqual(ShapeTemplates.TriangleId, ShapeRecognizer.Recognize(Triangle(), ShapeTemplates.All, Threshold)?.TemplateId);
            Assert.AreEqual(ShapeTemplates.FlowerId, ShapeRecognizer.Recognize(Flower(), ShapeTemplates.All, Threshold)?.TemplateId);
        }

        [Test]
        public void Recognize_ArrowIsNotASword()
        {
            Assert.IsNull(ShapeRecognizer.Recognize(Arrow(), ShapeTemplates.All, Threshold));
        }

        [Test]
        public void Recognize_CircleMatchesNeither()
        {
            Assert.IsNull(ShapeRecognizer.Recognize(Circle(), ShapeTemplates.All, Threshold));
        }

        [Test]
        public void Recognize_DegenerateInput_ReturnsNull()
        {
            Assert.IsNull(ShapeRecognizer.Recognize(null, ShapeTemplates.All, Threshold));
            Assert.IsNull(ShapeRecognizer.Recognize(new[] { new ShapePoint(1, 1) }, ShapeTemplates.All, Threshold));
            Assert.IsNull(ShapeRecognizer.Recognize(new[] { new ShapePoint(1, 1), new ShapePoint(1, 1) }, ShapeTemplates.All, Threshold));
        }

        [Test]
        public void Recognize_ThresholdAboveScore_ReturnsNull()
        {
            List<ShapePoint> shaky = Triangle(radius: 1500, stepsPerEdge: 20, jitter: 150, seed: 1);
            int score = ShapeRecognizer.Recognize(shaky, ShapeTemplates.All, 0).Value.Score;

            Assert.IsNull(ShapeRecognizer.Recognize(shaky, ShapeTemplates.All, score + 1));
        }

        [Test]
        public void Recognize_SameInput_GivesTheSameResult()
        {
            List<ShapePoint> shaky = Flower(jitter: 80, seed: 9);

            ShapeMatchResult? a = ShapeRecognizer.Recognize(shaky, ShapeTemplates.All, 0);
            ShapeMatchResult? b = ShapeRecognizer.Recognize(shaky, ShapeTemplates.All, 0);

            Assert.AreEqual(a.Value.TemplateId, b.Value.TemplateId);
            Assert.AreEqual(a.Value.Score, b.Value.Score);
        }
    }
}

using Game.Sim.Stroke;
using NUnit.Framework;

namespace Game.Tests
{
    public class CrossStrokeContactTests
    {
        private static readonly StrokePoint[] Horizontal = { StrokePoint.FromInt(0, 0), StrokePoint.FromInt(200, 0) };

        [Test]
        public void CrossingStrokes_MakeContact()
        {
            StrokePoint[] vertical = { StrokePoint.FromInt(100, -100), StrokePoint.FromInt(100, 100) };
            Assert.IsTrue(StrokeAnalyzer.CrossStrokeContact(Horizontal, vertical));
            Assert.IsTrue(StrokeAnalyzer.CrossStrokeContact(vertical, Horizontal));
        }

        [Test]
        public void NearMissWithinThreshold_MakesContact()
        {
            // Combined length 200 + 195 = 395, threshold = 3% = 11.85; gap is 5.
            StrokePoint[] near = { StrokePoint.FromInt(100, 5), StrokePoint.FromInt(100, 200) };
            Assert.IsTrue(StrokeAnalyzer.CrossStrokeContact(Horizontal, near));
        }

        [Test]
        public void GapBeyondThreshold_NoContact()
        {
            // Combined length 200 + 180 = 380, threshold = 3% = 11.4; gap is 20.
            StrokePoint[] apart = { StrokePoint.FromInt(100, 20), StrokePoint.FromInt(100, 200) };
            Assert.IsFalse(StrokeAnalyzer.CrossStrokeContact(Horizontal, apart));
        }

        [Test]
        public void StrokeWithFewerThanTwoPoints_NoContact()
        {
            Assert.IsFalse(StrokeAnalyzer.CrossStrokeContact(Horizontal, new StrokePoint[0]));
            Assert.IsFalse(StrokeAnalyzer.CrossStrokeContact(new[] { StrokePoint.FromInt(100, 0) }, Horizontal));
            Assert.IsFalse(StrokeAnalyzer.CrossStrokeContact(null, Horizontal));
        }
    }
}

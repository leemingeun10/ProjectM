using Game.Client.Stroke;
using Game.Sim.Cultivation;
using Game.Sim.Stroke;
using NUnit.Framework;

namespace Game.Tests
{
    public class CultivationBattleRulesTests
    {
        // ---- stroke length limit: one value for every character, tied to the frame ----

        [Test]
        public void MaxStrokeLength_IsTwelveCentimetresForTheCurrentFrame()
        {
            Assert.AreEqual(4500, StrokeUnits.FrameWidthCmRaw);
            Assert.AreEqual(12000, CultivationBattleRules.MaxStrokeLengthCmRaw);
        }

        [Test]
        public void MaxStrokeLength_IsFrameWidthTimesEightThirds()
        {
            Assert.AreEqual(StrokeUnits.FrameWidthCmRaw * 8 / 3, CultivationBattleRules.MaxStrokeLengthCmRaw);
            Assert.AreEqual(StrokeUnits.MaxStrokeLengthCmRaw, CultivationBattleRules.MaxStrokeLengthCmRaw);
        }

        [Test]
        public void MaxStrokeLength_IsWhatTheDrawingInputStopsAt()
        {
            Assert.AreEqual(CultivationBattleRules.MaxStrokeLengthCmRaw / (float)Fixed.Scale, StrokeInputController.MaxStrokeLengthCm, 1e-4f);
        }
    }
}

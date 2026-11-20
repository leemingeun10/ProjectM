using System.Collections.Generic;
using Game.Client.Stage;
using Game.Client.Stroke;
using Game.Sim.Ai;
using Game.Sim.Battle;
using Game.Sim.Progress;
using Game.Sim.Stroke;
using Game.Sim.Technique;
using NUnit.Framework;

namespace Game.Tests
{
    // Player strokes are drawn and saved in frame centimetres; battles run in canvas units.
    // These tests pit player-sized strokes against real AI strokes, the pairing that went
    // untested while the two sides silently used different units (Thrust damage 0, no contact).
    public class StrokeUnitsTests
    {
        private static readonly int[] Cos24 =
        {
            1000, 966, 866, 707, 500, 259, 0, -259, -500, -707, -866, -966,
            -1000, -966, -866, -707, -500, -259, 0, 259, 500, 707, 866, 966
        };

        // Closed 24-gon of the given radius around (cx, cy), all in cm * 1000, like a drawn loop.
        private static StrokePoint[] LoopCm(long cx, long cy, long radius)
        {
            var points = new StrokePoint[25];
            for (int k = 0; k < 24; k++)
                points[k] = new StrokePoint(cx + Cos24[k] * radius / 1000, cy + Cos24[(k + 18) % 24] * radius / 1000);
            points[24] = points[0];
            return points;
        }

        // Horizontal 24-point line centred on (cx, cy), in cm * 1000.
        private static StrokePoint[] LineCm(long cx, long cy, long length)
        {
            var points = new StrokePoint[24];
            for (int i = 0; i < 24; i++)
                points[i] = new StrokePoint(cx - length / 2 + length * i / 23, cy);
            return points;
        }

        private static TechniqueStep StepOf(int turn, StrokePoint[] points)
            => new TechniqueStep(turn, false, StrokeAnalyzer.Analyze(points), points);

        // Canvas raw -> cm raw, for placing a player stroke relative to an AI one (test-side only).
        private static long CanvasToCmRaw(long canvasRaw)
            => canvasRaw * StrokeUnits.CanvasPerCmDenominator / StrokeUnits.CanvasPerCmNumerator;

        private static List<TechniqueStep> StageAi(int stageIndex)
        {
            StageDefinition stage = StageCatalog.Get(stageIndex);
            return AiTechniqueGenerator.Generate(stage.Difficulty, stage.Seed);
        }

        // 1. Thrust: a loop a player can really draw deals damage once converted.
        [Test]
        public void OneCentimetreLoop_DealsThrustDamageAfterConversion()
        {
            TechniqueStep cm = StepOf(0, LoopCm(0, 0, 1000));
            Assert.AreEqual(StrokeType.Thrust, cm.Stroke.Type);
            Assert.AreEqual(0, BattleSimulator.ThrustDamage(cm), "In centimetres the area is ~3 and the damage rounds to 0: the bug this guards against.");

            TechniqueStep canvas = StrokeUnits.CmToCanvas(cm);
            Assert.AreEqual(StrokeType.Thrust, canvas.Stroke.Type);
            // Radius 213.3 units: area 12 * r^2 * sin 15 deg ~ 141,350 -> sqrt 375 -> 375 / 26 = 14.
            Assert.AreEqual(14, BattleSimulator.ThrustDamage(canvas));
        }

        // 2. Contact: a player-sized line through the middle of a real AI slash touches it.
        [Test]
        public void PlayerSizedLineThroughAnAiSlash_MakesContactAfterConversion()
        {
            int checkedSlashes = 0;
            foreach (TechniqueStep ai in StageAi(1))
            {
                if (ai.Stroke.Type != StrokeType.Slash)
                    continue;
                StrokePoint middle = ai.RawPoints[ai.RawPoints.Count / 2];
                StrokePoint[] line = LineCm(CanvasToCmRaw(middle.X.Raw), CanvasToCmRaw(middle.Y.Raw), 3000);
                Assert.IsTrue(StrokeAnalyzer.CrossStrokeContact(StrokeUnits.CmToCanvas(line), ai.RawPoints), $"turn {ai.TurnIndex}");
                checkedSlashes++;
            }
            Assert.Greater(checkedSlashes, 0, "Stage 1's AI has no Slash to test against.");
        }

        // 3. Whole battle through the load path: saved as centimetres, read back, converted the way
        // StageSelectController does it, then fought against a StageCatalog AI.
        [Test]
        public void CentimetreTechniqueThroughTheLoadPath_TouchesAndThrustsAgainstStageAi()
        {
            List<TechniqueStep> ai = StageAi(1);
            var builder = new TechniqueBuilder();
            for (int turn = 0; turn < TechniqueBuilder.MaxTurns; turn++)
            {
                // Even turns: a 3 cm line through the AI stroke's middle. Odd turns: a 1 cm loop.
                StrokePoint middle = ai[turn].RawPoints[ai[turn].RawPoints.Count / 2];
                StrokePoint[] points = turn % 2 == 0
                    ? LineCm(CanvasToCmRaw(middle.X.Raw), CanvasToCmRaw(middle.Y.Raw), 3000)
                    : LoopCm(0, 0, 1000);
                Assert.AreEqual(TechniqueAddResult.Success, builder.TryAddStep(false, StrokeAnalyzer.Analyze(points), points));
            }

            (_, List<TechniqueStep> saved) = TechniqueJsonSerializer.Deserialize(TechniqueJsonSerializer.Serialize(builder));
            List<TechniqueStep> player = StageSelectController.PlayerStepsForBattle(saved);

            int thrustDamage = 0;
            foreach (TechniqueStep step in player)
            {
                if (BattleSimulator.GetStepType(step) == BattleStepType.Thrust)
                    thrustDamage += BattleSimulator.ThrustDamage(step);
            }
            Assert.Greater(thrustDamage, 0);

            // Contact is checked turn by turn so a battle ending early cannot hide it.
            int contactTurns = 0;
            for (int turn = 0; turn < TechniqueBuilder.MaxTurns; turn++)
            {
                if (StrokeAnalyzer.CrossStrokeContact(player[turn].RawPoints, ai[turn].RawPoints))
                    contactTurns++;
            }
            Assert.GreaterOrEqual(contactTurns, 1);

            BattleResult result = BattleSimulator.Simulate(player, ai);
            Assert.Greater(result.Events.Count, 0);
        }

        // 4. The client's frame and the simulation's constants describe the same frame.
        [Test]
        public void ClientFrame_MatchesTheSimulationConstants()
        {
            Assert.AreEqual(StrokeUnits.FrameWidthCmRaw, (long)System.Math.Round(StrokeInputController.FrameWidthCm * Fixed.Scale));
            Assert.AreEqual(StrokeUnits.FrameWidthCmRaw, (long)System.Math.Round(StrokeInputController.FrameHeightCm * Fixed.Scale));
            Assert.AreEqual(480000, StrokeUnits.CmToCanvasRaw(2250));
            Assert.AreEqual(StrokeUnits.FrameHalfCanvasRaw, StrokeUnits.CmToCanvasRaw(StrokeUnits.FrameWidthCmRaw / 2));
            Assert.AreEqual(StrokeUnits.MaxStrokeLengthCanvasRaw, StrokeUnits.CmToCanvasRaw(StrokeUnits.MaxStrokeLengthCmRaw));
        }

        // 5. Determinism: same input, same bits; negative coordinates mirror positive ones.
        [Test]
        public void Conversion_IsRepeatableAndSymmetricAroundZero()
        {
            for (long cm = 0; cm <= 20000; cm++)
            {
                long canvas = StrokeUnits.CmToCanvasRaw(cm);
                Assert.AreEqual(canvas, StrokeUnits.CmToCanvasRaw(cm), $"repeat {cm}");
                Assert.AreEqual(-canvas, StrokeUnits.CmToCanvasRaw(-cm), $"mirror {cm}");
                // Nearest integer to cm * 640 / 3 (a third is never a tie).
                Assert.LessOrEqual(System.Math.Abs(canvas * 3 - cm * 640), 1, $"nearest {cm}");
            }

            StrokePoint[] loop = LoopCm(-700, 350, 1200);
            TechniqueStep a = StrokeUnits.CmToCanvas(StepOf(3, loop));
            TechniqueStep b = StrokeUnits.CmToCanvas(StepOf(3, loop));
            Assert.AreEqual(a.Stroke.Area.Raw, b.Stroke.Area.Raw);
            Assert.AreEqual(a.RawPoints.Count, b.RawPoints.Count);
            for (int i = 0; i < a.RawPoints.Count; i++)
            {
                Assert.AreEqual(a.RawPoints[i].X.Raw, b.RawPoints[i].X.Raw);
                Assert.AreEqual(a.RawPoints[i].Y.Raw, b.RawPoints[i].Y.Raw);
            }
        }

        [Test]
        public void StepConversion_KeepsEverythingButTheSize()
        {
            StrokePoint[] line = LineCm(100, -200, 3000);
            var slash = new TechniqueStep(4, true, StrokeAnalyzer.Analyze(line), line);
            TechniqueStep converted = StrokeUnits.CmToCanvas(slash);
            Assert.AreEqual(4, converted.TurnIndex);
            Assert.IsTrue(converted.IsCounter);
            Assert.AreEqual(StrokeType.Slash, converted.Stroke.Type);
            Assert.AreEqual(StrokeUnits.CmToCanvasRaw(slash.Stroke.Length.Raw), converted.Stroke.Length.Raw);
            Assert.AreEqual(slash.Stroke.StartDirectionX.Raw, converted.Stroke.StartDirectionX.Raw);
            Assert.AreEqual(slash.Stroke.EndDirectionY.Raw, converted.Stroke.EndDirectionY.Raw);

            // A hidden turn keeps its technique and move; an open shape saved as a Slash stays one.
            var hidden = new TechniqueStep(0, false, StrokeAnalyzer.Analyze(line), line, Game.Sim.Shape.ShapeTemplates.TriangleId, HiddenTechniqueMove.VerticalSlash);
            TechniqueStep hiddenConverted = StrokeUnits.CmToCanvas(hidden);
            Assert.AreEqual(hidden.HiddenTechniqueId, hiddenConverted.HiddenTechniqueId);
            Assert.AreEqual(HiddenTechniqueMove.VerticalSlash, hiddenConverted.HiddenMove);

            // The saved steps are not modified.
            Assert.AreEqual(line[0].X.Raw, slash.RawPoints[0].X.Raw);
        }
    }
}

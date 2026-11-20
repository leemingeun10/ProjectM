using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Game.Client.Stage;
using Game.Client.Technique;
using Game.Sim.Ai;
using Game.Sim.Battle;
using Game.Sim.Progress;
using Game.Sim.Stroke;
using Game.Sim.Technique;
using NUnit.Framework;

namespace Game.Tests
{
    // Measurements for the stroke-unit alignment work (not a regular test): AI stroke sizes, how
    // many AI strokes stay inside the player's frame, and how 2,000 random player techniques
    // (CombatPowerSimulation.RandomSequence, converted like a saved slot) fare against each stage.
    // [Explicit]: never part of "Run All"; run it by name. Writes Temp/StrokeAlignmentSimulation.txt.
    [Explicit("Measurement; run by name.")]
    [Category("Simulation")]
    public class StrokeAlignmentSimulation
    {
        private const int Games = 2000;
        private const int AiSeeds = 2000;
        private const ulong PlayerSeedBase = 0x5EEDC0DE00000000UL;

        private static double Cm(double canvasUnits)
            => canvasUnits * StrokeUnits.CanvasPerCmDenominator / StrokeUnits.CanvasPerCmNumerator;

        private static double PathLength(IReadOnlyList<StrokePoint> points)
        {
            double length = 0;
            for (int i = 1; i < points.Count; i++)
            {
                double dx = (points[i].X.Raw - points[i - 1].X.Raw) / (double)Fixed.Scale;
                double dy = (points[i].Y.Raw - points[i - 1].Y.Raw) / (double)Fixed.Scale;
                length += Math.Sqrt(dx * dx + dy * dy);
            }
            return length;
        }

        private static bool InsideFrame(TechniqueStep step)
        {
            foreach (StrokePoint p in step.RawPoints)
            {
                if (Math.Abs(p.X.Raw) > StrokeUnits.FrameHalfCanvasRaw || Math.Abs(p.Y.Raw) > StrokeUnits.FrameHalfCanvasRaw)
                    return false;
            }
            return true;
        }

        private struct Spread
        {
            public double Min, Max, Sum;
            public int Count;
            public void Add(double v)
            {
                if (Count == 0 || v < Min) Min = v;
                if (Count == 0 || v > Max) Max = v;
                Sum += v;
                Count++;
            }
            public override string ToString()
                => Count == 0 ? "-" : $"min {Min,6:F2}  avg {Sum / Count,6:F2}  max {Max,6:F2}  (n={Count})";
        }

        [Test]
        public void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Frame half {StrokeUnits.FrameHalfCanvasRaw / 1000.0} canvas units, length limit {StrokeUnits.MaxStrokeLengthCmRaw / 1000.0} cm = {StrokeUnits.MaxStrokeLengthCanvasRaw / 1000.0} canvas units.");

            // ── AI stroke sizes and frame fit ──
            sb.AppendLine();
            sb.AppendLine($"AI STROKES  {AiSeeds} seeds x 10 turns per difficulty (lengths in cm; a Thrust's length is its loop's path)");
            foreach (AiDifficulty difficulty in new[] { AiDifficulty.Easy, AiDifficulty.Normal, AiDifficulty.Hard })
            {
                var slash = new Spread();
                var loop = new Spread();
                var damage = new Spread();
                int strokes = 0, inside = 0;
                for (ulong seed = 0; seed < AiSeeds; seed++)
                {
                    foreach (TechniqueStep step in AiTechniqueGenerator.Generate(difficulty, seed))
                    {
                        strokes++;
                        if (InsideFrame(step)) inside++;
                        if (step.Stroke.Type == StrokeType.Thrust)
                        {
                            loop.Add(Cm(PathLength(step.RawPoints)));
                            damage.Add(BattleSimulator.ThrustDamage(step));
                        }
                        else
                            slash.Add(Cm(step.Stroke.Length.Raw / (double)Fixed.Scale));
                    }
                }
                sb.AppendLine($"{difficulty,-6} slash length : {slash}");
                sb.AppendLine($"{difficulty,-6} thrust loop  : {loop}");
                sb.AppendLine($"{difficulty,-6} thrust damage: {damage}");
                sb.AppendLine($"{difficulty,-6} strokes fully inside the player frame: {inside} / {strokes} = {100.0 * inside / strokes:F1}%");
            }
            {
                int strokes = 0, inside = 0;
                for (int stage = 1; stage <= StageCatalog.StageCount; stage++)
                {
                    foreach (TechniqueStep step in StageAi(stage))
                    {
                        strokes++;
                        if (InsideFrame(step)) inside++;
                    }
                }
                sb.AppendLine($"15 StageCatalog AIs: strokes inside the frame {inside} / {strokes} = {100.0 * inside / strokes:F1}%");
            }

            // ── random player techniques vs every stage ──
            var players = new List<TechniqueStep>[Games];
            var playerThrust = new Spread();
            for (int i = 0; i < Games; i++)
            {
                players[i] = StageSelectController.PlayerStepsForBattle(CombatPowerSimulation.RandomSequence(PlayerSeedBase + (ulong)i, 100).Steps);
                foreach (TechniqueStep step in players[i])
                {
                    if (BattleSimulator.GetStepType(step) == BattleStepType.Thrust)
                        playerThrust.Add(BattleSimulator.ThrustDamage(step));
                }
            }

            sb.AppendLine();
            sb.AppendLine($"RANDOM PLAYER x STAGE  {Games} random centimetre techniques, converted like a saved slot (player = A)");
            sb.AppendLine($"player base Thrust damage per thrust turn: {playerThrust}");
            sb.AppendLine("stage | diff   | win%  | draw% | loss% | contact turns % | AI avg thrust dmg | player avg thrust dmg landed | AI avg thrust dmg landed");
            double winSum = 0;
            for (int stage = 1; stage <= StageCatalog.StageCount; stage++)
            {
                List<TechniqueStep> ai = StageAi(stage);
                var aiThrust = new Spread();
                foreach (TechniqueStep step in ai)
                {
                    if (BattleSimulator.GetStepType(step) == BattleStepType.Thrust)
                        aiThrust.Add(BattleSimulator.ThrustDamage(step));
                }

                int win = 0, draw = 0, loss = 0;
                long turns = 0, contact = 0;
                var landedPlayer = new Spread();
                var landedAi = new Spread();
                for (int i = 0; i < Games; i++)
                {
                    BattleResult r = BattleSimulator.Simulate(players[i], ai);
                    if (r.Outcome == BattleOutcome.WinA) win++; else if (r.Outcome == BattleOutcome.Draw) draw++; else loss++;
                    foreach (TurnEvent e in r.Events)
                    {
                        turns++;
                        if (e.Reason == TurnReason.CrossContact) contact++;
                        if (e.TypeA == BattleStepType.Thrust && e.DamageToB > 0) landedPlayer.Add(e.DamageToB);
                        if (e.TypeB == BattleStepType.Thrust && e.DamageToA > 0) landedAi.Add(e.DamageToA);
                    }
                }
                winSum += 100.0 * win / Games;
                sb.AppendLine($"{stage,5} | {StageCatalog.Get(stage).Difficulty,-6} | {100.0 * win / Games,5:F1} | {100.0 * draw / Games,5:F1} | {100.0 * loss / Games,5:F1} | {100.0 * contact / turns,15:F1} | {Avg(aiThrust),17} | {Avg(landedPlayer),28} | {Avg(landedAi),24}");
            }
            sb.AppendLine($"average win% over the 15 stages: {winSum / StageCatalog.StageCount:F1}");

            // ── the editor's own slot 1 vs every stage ──
            sb.AppendLine();
            sb.AppendLine("SLOT 1 (this machine's technique_slot_1.json) vs each stage");
            if (TechniqueSlotStore.TryLoad(1, out List<TechniqueStep> saved) && saved.Count == BattleSimulator.TurnsPerBattle)
            {
                List<TechniqueStep> slot = StageSelectController.PlayerStepsForBattle(saved);
                sb.AppendLine("stage | diff   | outcome | HP player:AI | turns | contact turns");
                for (int stage = 1; stage <= StageCatalog.StageCount; stage++)
                {
                    BattleResult r = BattleSimulator.Simulate(slot, StageAi(stage));
                    int contact = 0;
                    foreach (TurnEvent e in r.Events)
                        if (e.Reason == TurnReason.CrossContact) contact++;
                    sb.AppendLine($"{stage,5} | {StageCatalog.Get(stage).Difficulty,-6} | {Describe(r.Outcome),-7} | {r.FinalHpA,4}:{r.FinalHpB,-4}    | {r.Events.Count,5} | {contact}");
                }
            }
            else
                sb.AppendLine("no complete slot 1 on this machine");

            string path = Path.GetFullPath("Temp/StrokeAlignmentSimulation.txt");
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            TestContext.WriteLine(sb.ToString());
        }

        private static string Avg(Spread s) => s.Count == 0 ? "-" : (s.Sum / s.Count).ToString("F1");

        private static string Describe(BattleOutcome outcome)
            => outcome == BattleOutcome.WinA ? "win" : outcome == BattleOutcome.WinB ? "loss" : "draw";

        private static List<TechniqueStep> StageAi(int stageIndex)
        {
            StageDefinition s = StageCatalog.Get(stageIndex);
            return AiTechniqueGenerator.Generate(s.Difficulty, s.Seed);
        }
    }
}

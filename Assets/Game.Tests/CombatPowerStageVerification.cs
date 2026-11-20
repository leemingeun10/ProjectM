using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Game.Client.Stage;
using Game.Client.Story;
using Game.Sim.Ai;
using Game.Sim.Battle;
using Game.Sim.Progress;
using Game.Sim.Technique;
using NUnit.Framework;

namespace Game.Tests
{
    // Checks the shipped combat-power numbers with the real BattleSimulator: 2,000 random player
    // techniques (the generator and seeds of CombatPowerFormulaSimulation) against every 수련 stage
    // and story battle, with the player at the recommended power, 1.5x and 0.5x of it.
    // [Explicit]: never part of "Run All"; run it by name. Writes Temp/CombatPowerStageVerification.txt.
    [TestFixture]
    [Explicit("Balance verification; run by name.")]
    public class CombatPowerStageVerification
    {
        private const int Games = 2000;
        private const ulong PlayerSeedBase = 0xC0FFEE0100000000UL;
        private const ulong RivalSeedBase = 0xC0FFEE0200000000UL;
        private const int MinSlashCm = 1500;

        private struct Tally
        {
            public int Win, Draw, Loss, Early;
            public long Turns;
            public int N => Win + Draw + Loss;
            public double WinPct => 100.0 * Win / N;
            public double DrawPct => 100.0 * Draw / N;
            public double LossPct => 100.0 * Loss / N;
            public double AvgTurns => (double)Turns / N;
            public double EarlyPct => 100.0 * Early / N;

            public void Add(BattleResult r)
            {
                if (r.Outcome == BattleOutcome.WinA) Win++;
                else if (r.Outcome == BattleOutcome.WinB) Loss++;
                else Draw++;
                Turns += r.Events.Count;
                // Ended by a knockout before the last turn.
                if (r.Events.Count < BattleSimulator.TurnsPerBattle)
                    Early++;
            }

            public void Add(Tally t)
            {
                Win += t.Win; Draw += t.Draw; Loss += t.Loss; Early += t.Early; Turns += t.Turns;
            }
        }

        private static List<TechniqueStep>[] Pool(ulong seedBase)
        {
            int fallbacks = 0;
            var pool = new List<TechniqueStep>[Games];
            for (int i = 0; i < Games; i++)
            {
                List<TechniqueStep> cm = CombatPowerFormulaSimulation.RandomTechniqueCm(seedBase + (ulong)i, MinSlashCm,
                    (int)Game.Sim.Stroke.StrokeUnits.MaxStrokeLengthCmRaw, ref fallbacks);
                pool[i] = StageSelectController.PlayerStepsForBattle(cm);
            }
            Assert.AreEqual(0, fallbacks, "random techniques that fell back to the spiral");
            return pool;
        }

        private static Tally Run(List<TechniqueStep>[] players, CombatStats playerStats, IReadOnlyList<TechniqueStep> enemy, CombatStats enemyStats)
        {
            var tally = new Tally();
            foreach (List<TechniqueStep> player in players)
                tally.Add(BattleSimulator.Simulate(player, enemy, playerStats, enemyStats));
            return tally;
        }

        private static string F(double v) => v.ToString("F1").PadLeft(5);
        private static string P(int permille) => (permille / 1000.0).ToString("F2");

        // One row: the player at 1.0x / 1.5x / 0.5x of the recommended power.
        private static string StageRow(string label, AiDifficulty difficulty, int recommended, CombatStats enemy,
            List<TechniqueStep>[] players, IReadOnlyList<TechniqueStep> ai, ref Tally total)
        {
            Tally at = Run(players, CombatPowerRules.StatsForPower(recommended), ai, enemy);
            Tally high = Run(players, CombatPowerRules.StatsForPower(recommended * 3 / 2), ai, enemy);
            Tally low = Run(players, CombatPowerRules.StatsForPower(recommended / 2), ai, enemy);
            // A player who never trained: 100 / 100, combat power 1.
            Tally untrained = Run(players, CombatStats.Default, ai, enemy);
            total.Add(at);
            string flag = at.WinPct < 70 ? "  LOW" : at.WinPct > 88 ? "  HIGH" : "";
            return $"{label,-6} {difficulty,-6} rec {P(recommended)}  enemy {enemy.InternalPower,3}/{enemy.ExternalPower,-3} (power {P(CombatPowerRules.PowerPermille(enemy))})  "
                + $"player {CombatPowerRules.StatsForPower(recommended).InternalPower,3}  "
                + $"win@rec {F(at.WinPct)} (draw {F(at.DrawPct)})  @1.5x {F(high.WinPct)}  @0.5x {F(low.WinPct)}  "
                + $"untrained {F(untrained.WinPct)}  turns {at.AvgTurns:F2}  early {F(at.EarlyPct)}%{flag}";
        }

        [Test]
        [Timeout(1800000)]
        public void VerifyShippedStagePowers()
        {
            var clock = Stopwatch.StartNew();
            var sb = new StringBuilder();
            List<TechniqueStep>[] players = Pool(PlayerSeedBase);
            List<TechniqueStep>[] rivals = Pool(RivalSeedBase);
            sb.AppendLine($"Random player techniques: {Games} per condition, seeds {PlayerSeedBase:X}+i (rivals {RivalSeedBase:X}+i). Real BattleSimulator.");
            sb.AppendLine($"x = {CombatPowerRules.DamageScalePermille} permille; enemy divisor {CombatPowerRules.EnemyPowerDivisorTenths / 10.0}.");
            sb.AppendLine("Player stats: 내공 = 외공 = round(100 * sqrt(power)). Flagged at the recommended power: below 70% (LOW) or above 88% (HIGH). untrained = a 100/100 player.");
            sb.AppendLine();

            sb.AppendLine("== 수련 stages ==");
            var trainingTotal = new Tally();
            foreach (StageDefinition stage in StageCatalog.Stages)
            {
                List<TechniqueStep> ai = AiTechniqueGenerator.Generate(stage.Difficulty, stage.Seed);
                sb.AppendLine(StageRow($"st{stage.StageIndex}", stage.Difficulty, stage.RecommendedPowerPermille, stage.EnemyStats, players, ai, ref trainingTotal));
            }
            sb.AppendLine($"all 15 at the recommended power: win {F(trainingTotal.WinPct)}  turns {trainingTotal.AvgTurns:F2}  ends before turn 10 {F(trainingTotal.EarlyPct)}%");
            sb.AppendLine();

            sb.AppendLine("== Story battles ==");
            StoryBook book = StoryDataTests.LoadBook();
            var storyTotal = new Tally();
            StoryStage forcedLoss = null;
            foreach (StoryStage s in book.stages)
            {
                if (!s.HasBattle)
                    continue;
                if (s.Kind == StoryStageKind.Loss)
                {
                    forcedLoss = s;
                    continue;
                }
                StageDefinition aiStage = StageCatalog.Get(s.aiStage);
                List<TechniqueStep> ai = AiTechniqueGenerator.Generate(aiStage.Difficulty, aiStage.Seed);
                sb.AppendLine(StageRow($"{s.id}(ai{s.aiStage})", aiStage.Difficulty, s.recommendedPowerPermille, s.EnemyStats, players, ai, ref storyTotal));
            }
            sb.AppendLine($"all 8 at the recommended power: win {F(storyTotal.WinPct)}  turns {storyTotal.AvgTurns:F2}  ends before turn 10 {F(storyTotal.EarlyPct)}%");
            sb.AppendLine();

            if (forcedLoss != null)
            {
                StageDefinition aiStage = StageCatalog.Get(forcedLoss.aiStage);
                List<TechniqueStep> ai = AiTechniqueGenerator.Generate(aiStage.Difficulty, aiStage.Seed);
                CombatStats enemy = forcedLoss.EnemyStats;
                sb.AppendLine($"== {forcedLoss.id} forced loss (ai{forcedLoss.aiStage} {aiStage.Difficulty}, enemy {enemy.InternalPower}/{enemy.ExternalPower}, power {P(CombatPowerRules.PowerPermille(enemy))}) ==");
                foreach (int power in new[] { 1000, 2000, 3000, 4000, 4410 })
                {
                    Tally t = Run(players, CombatPowerRules.StatsForPower(power), ai, enemy);
                    sb.AppendLine($"player power {P(power)}: win {F(t.WinPct)}  draw {F(t.DrawPct)}  loss {F(t.LossPct)}  turns {t.AvgTurns:F2}");
                }
                // The starter technique a new player would bring if they still had no slot.
                List<TechniqueStep> starter = StarterTechnique.Create();
                sb.AppendLine($"starter technique at power 1.00: {BattleSimulator.Simulate(starter, ai, CombatStats.Default, enemy).Outcome}");
                sb.AppendLine();
            }

            sb.AppendLine("== 1-1 with the starter technique (new player, 100/100) ==");
            {
                StoryStage first = book.stages[0];
                StageDefinition aiStage = StageCatalog.Get(first.aiStage);
                BattleResult r = BattleSimulator.Simulate(StarterTechnique.Create(), AiTechniqueGenerator.Generate(aiStage.Difficulty, aiStage.Seed),
                    CombatStats.Default, first.EnemyStats);
                sb.AppendLine($"{r.Outcome}: player {r.FinalHpA}/{r.MaxHpA}, enemy {r.FinalHpB}/{r.MaxHpB}, {r.Events.Count} turns");
                sb.AppendLine();
            }

            sb.AppendLine("== Same combat power 1.0, different splits (row = A, column = B; A's win / draw / loss %) ==");
            var splits = new[] { new CombatStats(100, 100), new CombatStats(50, 200), new CombatStats(200, 50) };
            var fair = new Tally();
            foreach (CombatStats a in splits)
            {
                var line = new StringBuilder($"A {a.InternalPower,3}/{a.ExternalPower,-3} ");
                foreach (CombatStats b in splits)
                {
                    var t = new Tally();
                    for (int i = 0; i < Games; i++)
                        t.Add(BattleSimulator.Simulate(players[i], rivals[i], a, b));
                    line.Append($" | vs {b.InternalPower,3}/{b.ExternalPower,-3}: {F(t.WinPct)} / {F(t.DrawPct)} / {F(t.LossPct)}  turns {t.AvgTurns:F2} early {F(t.EarlyPct)}%");
                    if (a.InternalPower == 100 && b.InternalPower == 100)
                        fair = t;
                }
                sb.AppendLine(line.ToString());
            }
            sb.AppendLine($"비무 (100/100 both): turns {fair.AvgTurns:F2}, ends before turn 10 {F(fair.EarlyPct)}%");
            sb.AppendLine();
            sb.AppendLine($"Run time: {clock.Elapsed.TotalSeconds:F1} s");

            string path = Path.Combine("Temp", "CombatPowerStageVerification.txt");
            File.WriteAllText(path, sb.ToString());
            TestContext.WriteLine(sb.ToString());
        }
    }
}

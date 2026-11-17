using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Game.Sim.Ai;
using Game.Sim.Battle;
using Game.Sim.Common;
using Game.Sim.Progress;
using Game.Sim.Shape;
using Game.Sim.Stroke;
using Game.Sim.Technique;
using NUnit.Framework;

namespace Game.Tests
{
    // Balance simulation for the proposed 전투력 / damage-scaling rules (not a regular test).
    // [Explicit]: never part of "Run All"; run it by name. Writes its tables to
    // Temp/CombatPowerSimulation.txt (Temp/ is not tracked by git).
    //
    // Nothing in Game.sim is changed. The turn table of BattleSimulator is re-implemented here
    // (Resolve) so damage can be swapped; the re-implementation is first checked to reproduce
    // BattleSimulator.Simulate exactly on every baseline battle.
    [Explicit("Balance simulation; run by name.")]
    [Category("Simulation")]
    public class CombatPowerSimulation
    {
        private const int Games = 2000;
        private const ulong PlayerSeedBase = 0x5EEDC0DE00000000UL;
        private const int StartingHp = BattleConstants.StartingHP;

        // ── random 검로 (player side) ──────────────────────────────────────────────────────
        // Drawn the way the client records them: centimetres from the 4.5 x 4.5 cm frame centre,
        // * Fixed.Scale, 24 points. Each sequence: 0-2 counters (count uniform, turns uniform);
        // every other turn is a Slash or a Thrust (50/50); 20% of the non-counter turns are saved
        // as 삼재검법 (triangle id; move follows the stroke type) while the 내공 budget allows.
        // A Slash is a 1-3 segment polyline (directions from 24, total length 1.5-10 cm, the 외공 0-49
        // cap) starting anywhere in the frame; a Thrust is a closed ellipse (radii 0.3-1.5 cm)
        // centred in the frame. Shapes the analyzer classifies otherwise are redrawn.
        private const int FrameHalf = 2250;
        private const int PointsPerStroke = 24;

        public sealed class Sequence
        {
            public List<TechniqueStep> Steps;
            public int SkillTurns;
        }

        public static Sequence RandomSequence(ulong seed, int internalPower, int unitScale = 1)
        {
            var rng = new DeterministicRng(seed);
            int counters = rng.Range(0, 3);
            var isCounter = new bool[TechniqueBuilder.MaxTurns];
            for (int placed = 0; placed < counters;)
            {
                int t = rng.Range(0, TechniqueBuilder.MaxTurns);
                if (!isCounter[t]) { isCounter[t] = true; placed++; }
            }

            var builder = new TechniqueBuilder { InternalPower = internalPower };
            int skills = 0;
            for (int turn = 0; turn < TechniqueBuilder.MaxTurns; turn++)
            {
                bool thrust = !isCounter[turn] && rng.Chance(50);
                StrokePoint[] pts;
                StrokeResult stroke;
                do
                {
                    pts = thrust ? Ellipse(rng) : Polyline(rng);
                    if (unitScale != 1)
                        for (int i = 0; i < pts.Length; i++)
                            pts[i] = new StrokePoint(pts[i].X.Raw * unitScale, pts[i].Y.Raw * unitScale);
                    stroke = StrokeAnalyzer.Analyze(pts);
                } while (stroke.Type != (thrust ? StrokeType.Thrust : StrokeType.Slash));

                string hiddenId = null;
                var move = HiddenTechniqueMove.None;
                if (!isCounter[turn] && rng.Chance(20) && builder.RemainingInternalPower >= HiddenTechniques.SamjaeCost)
                {
                    hiddenId = ShapeTemplates.TriangleId;
                    move = thrust ? HiddenTechniqueMove.Thrust : HiddenTechniqueMove.VerticalSlash;
                    skills++;
                }
                TechniqueAddResult r = builder.TryAddStep(isCounter[turn], stroke, pts, hiddenId, move);
                if (r != TechniqueAddResult.Success)
                    throw new InvalidOperationException($"random step rejected: {r}");
            }
            return new Sequence { Steps = new List<TechniqueStep>(builder.Steps), SkillTurns = skills };
        }

        private static long Cos(int k) => (long)Math.Round(Math.Cos(k * Math.PI / 12) * 1000);
        private static long Sin(int k) => (long)Math.Round(Math.Sin(k * Math.PI / 12) * 1000);

        private static StrokePoint[] Polyline(DeterministicRng rng)
        {
            int segments = rng.Range(1, 4);
            int total = rng.Range(1500, 10001);
            var corners = new List<(double x, double y)> { (rng.Range(-FrameHalf, FrameHalf + 1), rng.Range(-FrameHalf, FrameHalf + 1)) };
            int dir = rng.Range(0, 24);
            for (int s = 0; s < segments; s++)
            {
                if (s > 0) dir = (dir + rng.Range(-6, 7) + 24) % 24; // turn up to 90 degrees
                double len = total / (double)segments;
                (double x, double y) last = corners[corners.Count - 1];
                corners.Add((last.x + Cos(dir) * len / 1000.0, last.y + Sin(dir) * len / 1000.0));
            }
            return Resample(corners, PointsPerStroke);
        }

        private static StrokePoint[] Ellipse(DeterministicRng rng)
        {
            int rx = rng.Range(300, 1501), ry = rng.Range(300, 1501);
            int cx = rng.Range(-FrameHalf, FrameHalf + 1), cy = rng.Range(-FrameHalf, FrameHalf + 1);
            int start = rng.Range(0, 24);
            var pts = new StrokePoint[PointsPerStroke];
            for (int i = 0; i < PointsPerStroke - 1; i++)
            {
                double a = (start + i * 24.0 / (PointsPerStroke - 1)) * Math.PI / 12;
                pts[i] = new StrokePoint((long)Math.Round(cx + Math.Cos(a) * rx), (long)Math.Round(cy + Math.Sin(a) * ry));
            }
            pts[PointsPerStroke - 1] = pts[0];
            return pts;
        }

        private static StrokePoint[] Resample(List<(double x, double y)> path, int count)
        {
            var cum = new double[path.Count];
            for (int i = 1; i < path.Count; i++)
                cum[i] = cum[i - 1] + Math.Sqrt(Math.Pow(path[i].x - path[i - 1].x, 2) + Math.Pow(path[i].y - path[i - 1].y, 2));
            var pts = new StrokePoint[count];
            int seg = 1;
            for (int k = 0; k < count; k++)
            {
                double d = cum[cum.Length - 1] * k / (count - 1);
                while (seg < path.Count - 1 && cum[seg] < d) seg++;
                double t = cum[seg] > cum[seg - 1] ? (d - cum[seg - 1]) / (cum[seg] - cum[seg - 1]) : 0;
                pts[k] = new StrokePoint((long)Math.Round(path[seg - 1].x + (path[seg].x - path[seg - 1].x) * t),
                    (long)Math.Round(path[seg - 1].y + (path[seg].y - path[seg - 1].y) * t));
            }
            return pts;
        }

        // ── battle engine with swappable damage ────────────────────────────────────────────
        public enum Hit { None, Slash, Thrust, Counter }

        public struct Stats
        {
            public int Internal;
            public int External;
            public Stats(int internalPower, int externalPower) { Internal = internalPower; External = externalPower; }
            public static Stats ForPower(double power) { int v = (int)Math.Round(100 * Math.Sqrt(power)); return new Stats(v, v); }
            public override string ToString() => $"내공 {Internal}·외공 {External}";
        }

        public interface IDamageModel
        {
            int Damage(Hit hit, TechniqueStep attacker, Stats attackerStats, int attackerSkillTurns);
        }

        // Today's rules: Slash 10, Counter 20, Thrust isqrt(area)/26, then the hidden bonus.
        public sealed class CurrentModel : IDamageModel
        {
            public int Damage(Hit hit, TechniqueStep attacker, Stats s, int skills)
            {
                int baseDamage = hit == Hit.Slash ? BattleConstants.SlashDamage
                    : hit == Hit.Counter ? BattleConstants.CounterBonusDamage
                    : BattleSimulator.ThrustDamage(attacker);
                return HiddenTechniques.ScaleDamage(baseDamage, attacker);
            }
        }

        // The proposal, in integers (x as permille, half-up rounding):
        //   ordinary hit = 외공 * x (Slash and Thrust alike; a Counter keeps today's 2:1 ratio);
        //   skill (hidden) hit = 기본 배율 1 * (1 + 사용치/100) * 외공 * x.
        // 사용치: SkillSpend.Cost = today's per-turn cost (5); SkillSpend.WholeBudget = the side's
        // 내공 split evenly over its skill turns (the whole budget is spent).
        public enum SkillSpend { Cost, WholeBudget }

        public sealed class ProposedModel : IDamageModel
        {
            private readonly int xPermille;
            private readonly SkillSpend spend;
            public ProposedModel(int xPermille, SkillSpend spend = SkillSpend.Cost) { this.xPermille = xPermille; this.spend = spend; }

            public int Damage(Hit hit, TechniqueStep attacker, Stats s, int skills)
            {
                long ordinary = (long)s.External * xPermille;                 // * 1/1000
                if (attacker.IsHidden && hit != Hit.Counter)
                {
                    long use = spend == SkillSpend.Cost ? HiddenTechniques.SamjaeCost : skills > 0 ? s.Internal / skills : 0;
                    return (int)((ordinary * (100 + use) + 50000) / 100000);  // / (1000 * 100), half up
                }
                long d = (ordinary + 500) / 1000;
                return (int)(hit == Hit.Counter ? 2 * d : d);
            }
        }

        public struct Result
        {
            public BattleOutcome Outcome;
            public int Turns;
            public int HpA, HpB;
        }

        // Which hit each side takes on each turn. Depends only on the two sequences, not on stats,
        // so it is worked out once per pairing and reused for every damage model.
        public static (Hit onA, Hit onB)[] Hits(IReadOnlyList<TechniqueStep> a, IReadOnlyList<TechniqueStep> b)
        {
            var hits = new (Hit, Hit)[BattleSimulator.TurnsPerBattle];
            for (int t = 0; t < hits.Length; t++)
                hits[t] = Resolve(a[t], b[t]);
            return hits;
        }

        public static Result Fight(IReadOnlyList<TechniqueStep> a, Stats sa, int skillsA,
            IReadOnlyList<TechniqueStep> b, Stats sb, int skillsB, IDamageModel model)
            => Fight(Hits(a, b), a, sa, skillsA, b, sb, skillsB, model);

        // BattleSimulator's turn table, with damage taken from the model.
        public static Result Fight((Hit onA, Hit onB)[] hits, IReadOnlyList<TechniqueStep> a, Stats sa, int skillsA,
            IReadOnlyList<TechniqueStep> b, Stats sb, int skillsB, IDamageModel model)
        {
            int hpA = StartingHp, hpB = StartingHp, turns = 0;
            for (int t = 0; t < BattleSimulator.TurnsPerBattle; t++)
            {
                (Hit onA, Hit onB) = hits[t];
                int dA = onA == Hit.None ? 0 : model.Damage(onA, b[t], sb, skillsB);
                int dB = onB == Hit.None ? 0 : model.Damage(onB, a[t], sa, skillsA);
                hpA -= dA;
                hpB -= dB;
                turns = t + 1;
                if (hpA <= 0 || hpB <= 0) break;
            }
            BattleOutcome o = hpA <= 0 && hpB <= 0 ? BattleOutcome.Draw : hpB <= 0 ? BattleOutcome.WinA : hpA <= 0 ? BattleOutcome.WinB
                : hpA > hpB ? BattleOutcome.WinA : hpB > hpA ? BattleOutcome.WinB : BattleOutcome.Draw;
            return new Result { Outcome = o, Turns = turns, HpA = hpA, HpB = hpB };
        }

        // Which hit each side takes this turn (the attacker is always the other side).
        private static (Hit onA, Hit onB) Resolve(TechniqueStep a, TechniqueStep b)
        {
            BattleStepType ta = BattleSimulator.GetStepType(a), tb = BattleSimulator.GetStepType(b);
            switch (ta, tb)
            {
                case (BattleStepType.Slash, BattleStepType.Slash):
                    return StrokeAnalyzer.CrossStrokeContact(a.RawPoints, b.RawPoints) ? (Hit.None, Hit.None) : (Hit.Slash, Hit.Slash);
                case (BattleStepType.Counter, BattleStepType.Slash):
                    return StrokeAnalyzer.CrossStrokeContact(a.RawPoints, b.RawPoints) ? (Hit.None, Hit.Counter) : (Hit.Slash, Hit.None);
                case (BattleStepType.Thrust, BattleStepType.Slash):
                    return StrokeAnalyzer.CrossStrokeContact(b.RawPoints, a.RawPoints) ? (Hit.Slash, Hit.None) : (Hit.None, Hit.Thrust);
                case (BattleStepType.Thrust, BattleStepType.Thrust):
                    return (Hit.Thrust, Hit.Thrust);
                case (BattleStepType.Thrust, BattleStepType.Counter):
                    return (Hit.None, Hit.Thrust);
                case (BattleStepType.Counter, BattleStepType.Counter):
                    return (Hit.None, Hit.None);
                default:
                    (Hit onB, Hit onA) = Resolve(b, a);
                    return (onA, onB);
            }
        }

        // ── report helpers ─────────────────────────────────────────────────────────────────
        private struct Tally
        {
            public int Win, Draw, Loss;
            public long Turns, HpLeft, Games;
            public void Add(Result r)
            {
                Games++;
                if (r.Outcome == BattleOutcome.WinA) Win++; else if (r.Outcome == BattleOutcome.Draw) Draw++; else Loss++;
                Turns += r.Turns;
                HpLeft += Math.Max(0, r.HpA);
            }
            public double WinPct => 100.0 * Win / Games;
            public double DrawPct => 100.0 * Draw / Games;
            public double AvgTurns => (double)Turns / Games;
            public double AvgHp => (double)HpLeft / Games;
        }

        private static readonly int[] StoryStages = { 1, 2, 6, 8, 10, 11, 13, 15 };

        private static List<TechniqueStep> StageAi(int stageIndex)
        {
            StageDefinition s = StageCatalog.Get(stageIndex);
            return AiTechniqueGenerator.Generate(s.Difficulty, s.Seed);
        }

        [Test]
        public void Run()
        {
            var sb = new StringBuilder();
            var player = new Sequence[Games];
            var playerCanvas = new Sequence[Games];
            var rival = new Sequence[Games];
            for (int i = 0; i < Games; i++)
            {
                player[i] = RandomSequence(PlayerSeedBase + (ulong)i, 100);
                playerCanvas[i] = RandomSequence(PlayerSeedBase + (ulong)i, 100, 213); // 4.5 cm frame ~ 960 canvas units
                rival[i] = RandomSequence(PlayerSeedBase + 0x1000000UL + (ulong)i, 100);
            }

            // Random sequence make-up.
            int thrusts = 0, counters = 0, skills = 0, zeroThrust = 0, all = 0;
            foreach (Sequence s in player)
                foreach (TechniqueStep st in s.Steps)
                {
                    all++;
                    if (st.IsCounter) counters++;
                    if (st.IsHidden) skills++;
                    if (BattleSimulator.GetStepType(st) == BattleStepType.Thrust) { thrusts++; if (BattleSimulator.ThrustDamage(st) == 0) zeroThrust++; }
                }
            sb.AppendLine($"Random sequences: {Games}, turns {all}: thrust {thrusts}, counter {counters}, 삼재 {skills}; thrust turns dealing 0 base damage {zeroThrust}.");

            // ── part 2: baseline (and a check that Fight == BattleSimulator) ──
            var current = new CurrentModel();
            var stats = new Stats(100, 100);
            int mismatches = 0;
            sb.AppendLine();
            sb.AppendLine("PART 2  current rules, 2000 random player sequences vs each StageCatalog AI (player = A)");
            sb.AppendLine("stage | diff   | story | win%  | draw% | avg end turn | avg player HP left | win% (player strokes in canvas units)");
            for (int stage = 1; stage <= StageCatalog.StageCount; stage++)
            {
                List<TechniqueStep> ai = StageAi(stage);
                var tally = new Tally();
                var tallyCanvas = new Tally();
                for (int i = 0; i < Games; i++)
                {
                    Result r = Fight(player[i].Steps, stats, player[i].SkillTurns, ai, stats, 0, current);
                    BattleResult real = BattleSimulator.Simulate(player[i].Steps, ai);
                    if (real.Outcome != r.Outcome || real.Events.Count != r.Turns || real.FinalHpA != r.HpA || real.FinalHpB != r.HpB)
                        mismatches++;
                    tally.Add(r);
                    tallyCanvas.Add(Fight(playerCanvas[i].Steps, stats, playerCanvas[i].SkillTurns, ai, stats, 0, current));
                }
                sb.AppendLine($"{stage,5} | {StageCatalog.Get(stage).Difficulty,-6} | {(Array.IndexOf(StoryStages, stage) >= 0 ? "  *  " : "     ")} | {tally.WinPct,5:F1} | {tally.DrawPct,5:F1} | {tally.AvgTurns,12:F2} | {tally.AvgHp,18:F1} | {tallyCanvas.WinPct,5:F1}");
            }
            sb.AppendLine($"(* = stage used by a story battle.) Re-implementation vs BattleSimulator mismatches: {mismatches} of {Games * StageCatalog.StageCount}.");
            Assert.AreEqual(0, mismatches, "Fight() must reproduce BattleSimulator exactly.");

            List<TechniqueStep> ai1 = StageAi(1);
            var hitsAi = new (Hit, Hit)[Games][];
            var hitsRival = new (Hit, Hit)[Games][];
            for (int i = 0; i < Games; i++)
            {
                hitsAi[i] = Hits(player[i].Steps, ai1);
                hitsRival[i] = Hits(player[i].Steps, rival[i].Steps);
            }
            Result VsAi(int i, Stats a, Stats e, IDamageModel m) => Fight(hitsAi[i], player[i].Steps, a, player[i].SkillTurns, ai1, e, 0, m);
            Result VsRival(int i, Stats a, Stats b, IDamageModel m) => Fight(hitsRival[i], player[i].Steps, a, player[i].SkillTurns, rival[i].Steps, b, rival[i].SkillTurns, m);
            double WinVsAi(Stats e, IDamageModel m) { var t = new Tally(); for (int i = 0; i < Games; i++) t.Add(VsAi(i, stats, e, m)); return t.WinPct; }

            // ── part 3a: x sweep, power 1 vs power 1 ──
            sb.AppendLine();
            sb.AppendLine("PART 3a  x sweep, power 1 (내공 100·외공 100) vs power 1; HP stays 100");
            sb.AppendLine("x    | hit dmg | vs stage-1 AI: win% draw% end turn | random vs random: win% draw% end turn");
            foreach (int x in new[] { 250, 500, 1000, 2000, 4000 })
            {
                var m = new ProposedModel(x);
                var t1 = new Tally();
                var t2 = new Tally();
                for (int i = 0; i < Games; i++)
                {
                    t1.Add(VsAi(i, stats, stats, m));
                    t2.Add(VsRival(i, stats, stats, m));
                }
                sb.AppendLine($"{x / 1000.0,-4} | {m.Damage(Hit.Slash, ai1[0], stats, 0),7} | {t1.WinPct,5:F1} {t1.DrawPct,5:F1} {t1.AvgTurns,8:F2} | {t2.WinPct,5:F1} {t2.DrawPct,5:F1} {t2.AvgTurns,8:F2}");
            }

            // ── part 3b: enemy power sweep vs the 1-1 opponent, and the 80% point ──
            sb.AppendLine();
            sb.AppendLine("PART 3b  player power 1 vs the stage-1 AI technique at enemy power p (내공 = 외공 = 100*sqrt(p))");
            foreach (int x in new[] { 1000, 250 })
            {
                var m = new ProposedModel(x);
                sb.AppendLine($"x = {x / 1000.0}");
                foreach (double p in new[] { 0.25, 0.5, 0.75, 1.0 })
                {
                    var t = new Tally();
                    Stats e = Stats.ForPower(p);
                    for (int i = 0; i < Games; i++)
                        t.Add(VsAi(i, stats, e, m));
                    sb.AppendLine($"  enemy power {p,4} ({e}) : win {t.WinPct,5:F1}%  draw {t.DrawPct,5:F1}%  end turn {t.AvgTurns:F2}");
                }
                // Win rate by enemy stat (equal split) for every 외공 step of 5, and the highest one
                // still giving >= 80%.
                string found = "none in 1..100";
                var curve = new StringBuilder("  curve (enemy 외공: win%):");
                for (int ext = 100; ext >= 5; ext -= 5)
                {
                    double w = WinVsAi(new Stats(ext, ext), m);
                    curve.Append($" {ext}:{w:F1}");
                    if (w >= 80.0 && found.StartsWith("none")) found = $"enemy 외공 {ext} (power {ext * ext / 10000.0:F2}) gives {w:F1}%";
                }
                sb.AppendLine(curve.ToString());
                sb.AppendLine($"  highest enemy stat (step 5) with win >= 80%: {found}");
            }

            // ── part 3c: same power, different split ──
            sb.AppendLine();
            sb.AppendLine("PART 3c  power 1 split three ways, random vs random (A = row, B = column), A win% / draw%, x = 1");
            var splits = new[] { new Stats(100, 100), new Stats(50, 200), new Stats(200, 50) };
            foreach (SkillSpend spend in new[] { SkillSpend.Cost, SkillSpend.WholeBudget })
            {
                var m = new ProposedModel(1000, spend);
                sb.AppendLine(spend == SkillSpend.Cost ? "사용치 = today's cost (5 per 삼재 turn)" : "사용치 = whole 내공 split over the side's 삼재 turns");
                foreach (Stats sa in splits)
                {
                    var line = new StringBuilder($"  A {sa}:");
                    foreach (Stats sbs in splits)
                    {
                        var t = new Tally();
                        for (int i = 0; i < Games; i++)
                            t.Add(VsRival(i, sa, sbs, m));
                        line.Append($"  vs B {sbs}: {t.WinPct,5:F1}/{t.DrawPct,4:F1}");
                    }
                    sb.AppendLine(line.ToString());
                }
            }

            // ── part 3d: battle length as power grows ──
            sb.AppendLine();
            sb.AppendLine("PART 3d  power p vs power p (random vs random), average end turn and share of games over by turn 1");
            foreach (int x in new[] { 1000, 250 })
            {
                var m = new ProposedModel(x);
                var line = new StringBuilder($"x = {x / 1000.0}:");
                foreach (double p in new[] { 1.0, 4.0, 9.0 })
                {
                    Stats s = Stats.ForPower(p);
                    var t = new Tally();
                    int turn1 = 0;
                    for (int i = 0; i < Games; i++)
                    {
                        Result r = VsRival(i, s, s, m);
                        t.Add(r);
                        if (r.Turns == 1) turn1++;
                    }
                    line.Append($"  power {p} (외공 {s.External}, hit {m.Damage(Hit.Slash, ai1[0], s, 0)}): end turn {t.AvgTurns:F2}, over in turn 1 {100.0 * turn1 / Games:F1}%");
                }
                sb.AppendLine(line.ToString());
            }
            // Reference: today's rules, random vs random.
            {
                var t = new Tally();
                for (int i = 0; i < Games; i++)
                    t.Add(VsRival(i, stats, stats, current));
                sb.AppendLine($"current rules, random vs random: win {t.WinPct:F1}%  draw {t.DrawPct:F1}%  end turn {t.AvgTurns:F2}");
            }

            string path = Path.GetFullPath("Temp/CombatPowerSimulation.txt");
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            TestContext.WriteLine(sb.ToString());
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Game.Client.Stage;
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
    // Simulation ahead of the 전투력 system (not a regular test): compares two damage formulas,
    // 안 A (today's damage * 외공, HP from 내공) and 안 B (flat 외공 damage, HP fixed), on random
    // player techniques drawn under the current rules (centimetre strokes inside the frame, 12 cm
    // limit, converted like a saved slot) against each other and against StageCatalog AIs.
    // [Explicit]: never part of "Run All"; run it by name. Writes Temp/CombatPowerFormulaSimulation.txt.
    //
    // Nothing in Game.sim or Game.Client is changed. The turn table is re-implemented here so damage
    // and HP can be swapped; it is first checked to reproduce BattleSimulator.Simulate exactly.
    [Explicit("Balance simulation; run by name.")]
    [Category("Simulation")]
    public class CombatPowerFormulaSimulation
    {
        private const int Games = 2000;
        private const ulong PlayerSeedBase = 0xC0FFEE0100000000UL;
        private const ulong RivalSeedBase = 0xC0FFEE0200000000UL;
        private const ulong LengthPoolSeedBase = 0xC0FFEE0300000000UL;

        private const int FrameHalfCm = (int)(StrokeUnits.FrameWidthCmRaw / 2);
        private const int LimitCm = (int)StrokeUnits.MaxStrokeLengthCmRaw;
        private const int MinSlashCm = 1500;
        private const int Points = 24;
        private static readonly int[] Stages = { 1, 6, 11 };

        // ── random player techniques ───────────────────────────────────────────────────────
        // Drawn in frame centimetres * 1000 like the client records them, 24 points a stroke.
        // 0-2 counters; every other turn is a Slash or a Thrust (50/50); 20% of the non-counter
        // turns are saved as 삼재검법 while the 내공 budget (100) allows. A Slash is a 1-3 segment
        // polyline or a curve (AiTechniqueGenerator.BuildCurve), path length slashMin..slashMax;
        // a Thrust is a closed ellipse (radii 0.3-1.91 cm) whose path is at most the 12 cm limit.
        // Every point is inside the frame, every stroke is classified by StrokeAnalyzer as what
        // it was meant to be, and every step goes through TechniqueBuilder.
        public static List<TechniqueStep> RandomTechniqueCm(ulong seed, int slashMin, int slashMax, ref int fallbacks)
        {
            var rng = new DeterministicRng(seed);
            int counters = rng.Range(0, 3);
            var isCounter = new bool[TechniqueBuilder.MaxTurns];
            for (int placed = 0; placed < counters;)
            {
                int t = rng.Range(0, TechniqueBuilder.MaxTurns);
                if (!isCounter[t]) { isCounter[t] = true; placed++; }
            }

            var builder = new TechniqueBuilder { InternalPower = 100 };
            for (int turn = 0; turn < TechniqueBuilder.MaxTurns; turn++)
            {
                bool thrust = !isCounter[turn] && rng.Chance(50);
                StrokePoint[] pts = thrust ? Loop(rng) : Slash(rng, slashMin, slashMax, ref fallbacks);
                StrokeResult stroke = StrokeAnalyzer.Analyze(pts);
                if (stroke.Type != (thrust ? StrokeType.Thrust : StrokeType.Slash))
                    throw new InvalidOperationException("random stroke classified as the other type");
                foreach (StrokePoint p in pts)
                {
                    if (Math.Abs(p.X.Raw) > FrameHalfCm || Math.Abs(p.Y.Raw) > FrameHalfCm)
                        throw new InvalidOperationException("random stroke left the frame");
                }
                if (PathLength(pts) > LimitCm)
                    throw new InvalidOperationException("random stroke over the length limit");

                string hiddenId = null;
                var move = HiddenTechniqueMove.None;
                if (!isCounter[turn] && rng.Chance(20) && builder.RemainingInternalPower >= HiddenTechniques.SamjaeCost)
                {
                    hiddenId = ShapeTemplates.TriangleId;
                    move = thrust ? HiddenTechniqueMove.Thrust : HiddenTechniqueMove.VerticalSlash;
                }
                TechniqueAddResult r = builder.TryAddStep(isCounter[turn], stroke, pts, hiddenId, move);
                if (r != TechniqueAddResult.Success)
                    throw new InvalidOperationException($"random step rejected: {r}");
            }
            return new List<TechniqueStep>(builder.Steps);
        }

        private static StrokePoint[] Slash(DeterministicRng rng, int min, int max, ref int fallbacks)
        {
            int length = rng.Range(min + 50, Math.Min(max, LimitCm) - 10 + 1);
            for (int attempt = 0; attempt < 200; attempt++)
            {
                StrokePoint[] shape = rng.Range(0, 2) == 0
                    ? Polyline(rng, length)
                    : AiTechniqueGenerator.BuildCurve(length, rng.Range(0, 360),
                        rng.Range(-AiTechniqueGenerator.MaxTurnTenths, AiTechniqueGenerator.MaxTurnTenths + 1),
                        rng.Range(-AiTechniqueGenerator.MaxTurnTenths, AiTechniqueGenerator.MaxTurnTenths + 1));
                StrokePoint[] placed = TryPlace(rng, shape, min, Math.Min(max, LimitCm));
                if (placed != null)
                    return placed;
            }

            fallbacks++;
            int sign = rng.Chance(50) ? 1 : -1;
            for (;;)
            {
                StrokePoint[] spiral = AiTechniqueGenerator.BuildCurve(length, rng.Range(0, 360),
                    sign * AiTechniqueGenerator.FallbackFirstTurnTenths, sign * AiTechniqueGenerator.FallbackLastTurnTenths);
                StrokePoint[] placed = TryPlace(rng, spiral, 0, LimitCm);
                if (placed != null)
                    return placed;
            }
        }

        // 1-3 straight segments of equal length, each turning up to 90 degrees, resampled to 24 points.
        private static StrokePoint[] Polyline(DeterministicRng rng, int length)
        {
            int segments = rng.Range(1, 4);
            int dir = rng.Range(0, 360);
            var corners = new List<(double x, double y)> { (0, 0) };
            for (int s = 0; s < segments; s++)
            {
                if (s > 0) dir += rng.Range(-90, 91);
                double len = length / (double)segments;
                (double x, double y) last = corners[corners.Count - 1];
                corners.Add((last.x + Math.Cos(dir * Math.PI / 180) * len, last.y + Math.Sin(dir * Math.PI / 180) * len));
            }

            var cum = new double[corners.Count];
            for (int i = 1; i < corners.Count; i++)
                cum[i] = cum[i - 1] + Math.Sqrt(Math.Pow(corners[i].x - corners[i - 1].x, 2) + Math.Pow(corners[i].y - corners[i - 1].y, 2));
            var pts = new StrokePoint[Points];
            int seg = 1;
            for (int k = 0; k < Points; k++)
            {
                double d = cum[cum.Length - 1] * k / (Points - 1);
                while (seg < corners.Count - 1 && cum[seg] < d) seg++;
                double t = cum[seg] > cum[seg - 1] ? (d - cum[seg - 1]) / (cum[seg] - cum[seg - 1]) : 0;
                pts[k] = new StrokePoint((long)Math.Round(corners[seg - 1].x + (corners[seg].x - corners[seg - 1].x) * t),
                    (long)Math.Round(corners[seg - 1].y + (corners[seg].y - corners[seg - 1].y) * t));
            }
            return pts;
        }

        // The shape moved to a random place inside the frame, or null when it does not fit, is
        // not a Slash, or its path length is outside minLength..maxLength. The length is the
        // geometric one, like the client's drawn length: StrokeResult.Length is too coarse at
        // centimetre scale (Fixed keeps 3 decimals) to hold a 1.5 cm lower bound.
        private static StrokePoint[] TryPlace(DeterministicRng rng, StrokePoint[] shape, int minLength, int maxLength)
        {
            long minX = long.MaxValue, maxX = long.MinValue, minY = long.MaxValue, maxY = long.MinValue;
            foreach (StrokePoint p in shape)
            {
                minX = Math.Min(minX, p.X.Raw); maxX = Math.Max(maxX, p.X.Raw);
                minY = Math.Min(minY, p.Y.Raw); maxY = Math.Max(maxY, p.Y.Raw);
            }
            if (maxX - minX > 2 * FrameHalfCm || maxY - minY > 2 * FrameHalfCm)
                return null;
            double length = PathLength(shape);
            if (length < minLength || length > maxLength || StrokeAnalyzer.Analyze(shape).Type != StrokeType.Slash)
                return null;

            long ox = rng.Range((int)(-FrameHalfCm - minX), (int)(FrameHalfCm - maxX) + 1);
            long oy = rng.Range((int)(-FrameHalfCm - minY), (int)(FrameHalfCm - maxY) + 1);
            var placed = new StrokePoint[shape.Length];
            for (int i = 0; i < shape.Length; i++)
                placed[i] = new StrokePoint(shape[i].X.Raw + ox, shape[i].Y.Raw + oy);
            return placed;
        }

        private static StrokePoint[] Loop(DeterministicRng rng)
        {
            for (;;)
            {
                int rx = rng.Range(300, 1911), ry = rng.Range(300, 1911);
                int start = rng.Range(0, 360);
                int cx = rng.Range(-(FrameHalfCm - rx), FrameHalfCm - rx + 1);
                int cy = rng.Range(-(FrameHalfCm - ry), FrameHalfCm - ry + 1);
                var pts = new StrokePoint[Points];
                for (int i = 0; i < Points - 1; i++)
                {
                    double a = (start + i * 360.0 / (Points - 1)) * Math.PI / 180;
                    pts[i] = new StrokePoint((long)Math.Round(cx + Math.Cos(a) * rx), (long)Math.Round(cy + Math.Sin(a) * ry));
                }
                pts[Points - 1] = pts[0];
                if (PathLength(pts) > LimitCm - 10)
                    continue;
                if (StrokeAnalyzer.Analyze(pts).Type == StrokeType.Thrust)
                    return pts;
            }
        }

        private static double PathLength(IReadOnlyList<StrokePoint> pts)
        {
            double length = 0;
            for (int i = 1; i < pts.Count; i++)
            {
                double dx = pts[i].X.Raw - pts[i - 1].X.Raw, dy = pts[i].Y.Raw - pts[i - 1].Y.Raw;
                length += Math.Sqrt(dx * dx + dy * dy);
            }
            return length;
        }

        // ── a technique ready to fight (canvas units) ──────────────────────────────────────
        public sealed class Tech
        {
            public readonly List<TechniqueStep> Steps;
            public readonly BattleStepType[] Types;
            public readonly int[] ThrustBase;
            public readonly int SkillTurns;
            // Average path length of the Slash-shaped strokes, in cm (0 when there is none).
            public readonly double AvgSlashCm;

            public Tech(List<TechniqueStep> steps)
            {
                Steps = steps;
                Types = new BattleStepType[steps.Count];
                ThrustBase = new int[steps.Count];
                double sum = 0;
                int slashes = 0;
                for (int i = 0; i < steps.Count; i++)
                {
                    Types[i] = BattleSimulator.GetStepType(steps[i]);
                    if (Types[i] == BattleStepType.Thrust)
                        ThrustBase[i] = BattleSimulator.ThrustDamage(steps[i]);
                    if (steps[i].IsHidden)
                        SkillTurns++;
                    if (steps[i].Stroke.Type == StrokeType.Slash)
                    {
                        sum += ToCm(PathLength(steps[i].RawPoints) / Fixed.Scale);
                        slashes++;
                    }
                }
                AvgSlashCm = slashes > 0 ? sum / slashes : 0;
            }
        }

        private static double ToCm(double canvasUnits)
            => canvasUnits * StrokeUnits.CanvasPerCmDenominator / StrokeUnits.CanvasPerCmNumerator;

        private static Tech RandomTech(ulong seed, int slashMin, int slashMax, ref int fallbacks)
            => new Tech(StageSelectController.PlayerStepsForBattle(RandomTechniqueCm(seed, slashMin, slashMax, ref fallbacks)));

        private static Tech StageAi(int stageIndex)
        {
            StageDefinition s = StageCatalog.Get(stageIndex);
            return new Tech(AiTechniqueGenerator.Generate(s.Difficulty, s.Seed));
        }

        // Every stroke shrunk about the centre of its own bounding box by num / den: the same
        // shapes in the same places, as if the length limit were that much lower.
        private static Tech Shrink(Tech tech, int num, int den)
        {
            if (num == den)
                return tech;
            var steps = new List<TechniqueStep>(tech.Steps.Count);
            foreach (TechniqueStep step in tech.Steps)
            {
                long minX = long.MaxValue, maxX = long.MinValue, minY = long.MaxValue, maxY = long.MinValue;
                foreach (StrokePoint p in step.RawPoints)
                {
                    minX = Math.Min(minX, p.X.Raw); maxX = Math.Max(maxX, p.X.Raw);
                    minY = Math.Min(minY, p.Y.Raw); maxY = Math.Max(maxY, p.Y.Raw);
                }
                long cx = (minX + maxX) / 2, cy = (minY + maxY) / 2;
                var pts = new StrokePoint[step.RawPoints.Count];
                for (int i = 0; i < pts.Length; i++)
                    pts[i] = new StrokePoint(cx + (step.RawPoints[i].X.Raw - cx) * num / den, cy + (step.RawPoints[i].Y.Raw - cy) * num / den);
                StrokeResult old = step.Stroke;
                StrokeResult stroke = old.Type == StrokeType.Thrust
                    ? StrokeResult.Thrust(StrokeAnalyzer.ClosedArea(pts))
                    : StrokeResult.Slash(Fixed.FromRaw(old.Length.Raw * num / den), old.StartDirectionX, old.StartDirectionY, old.EndDirectionX, old.EndDirectionY);
                steps.Add(new TechniqueStep(step.TurnIndex, step.IsCounter, stroke, pts, step.HiddenTechniqueId, step.HiddenMove));
            }
            return new Tech(steps);
        }

        // ── battle engine with swappable damage and HP ─────────────────────────────────────
        public enum Hit { None, Slash, Thrust, Counter }

        public struct TurnHit
        {
            public Hit OnA, OnB;
            public bool Contact;
            public TurnHit(Hit onA, Hit onB, bool contact) { OnA = onA; OnB = onB; Contact = contact; }
        }

        public struct Stats
        {
            public int Internal, External;
            public Stats(int internalPower, int externalPower) { Internal = internalPower; External = externalPower; }
            public static Stats ForPower(double power) { int v = (int)Math.Round(100 * Math.Sqrt(power)); return new Stats(v, v); }
            public double Power => Internal * (double)External / 10000;
            public override string ToString() => $"내공 {Internal}·외공 {External}";
        }

        public abstract class Model
        {
            // False: a battle that reaches turn 10 goes to the higher remaining HP, as today.
            // True: to the higher remaining share of one's own starting HP.
            public bool JudgeByHpShare;
            public abstract int Hp(Stats s);
            public abstract int Damage(Hit hit, Tech attacker, int turn, Stats s);
        }

        // 안 A: today's damage (Slash 10, Counter 20, Thrust by area, hidden bonus) * 외공 / 100 * x,
        // rounded half up; HP = 100 * 내공 / 100. thrustCap limits the Thrust base damage (item 5).
        public sealed class ModelA : Model
        {
            private readonly int xPermille, thrustCap;
            public ModelA(int xPermille, int thrustCap = int.MaxValue) { this.xPermille = xPermille; this.thrustCap = thrustCap; }
            public override int Hp(Stats s) => BattleConstants.StartingHP * s.Internal / 100;
            public override int Damage(Hit hit, Tech attacker, int turn, Stats s)
            {
                int baseDamage = hit == Hit.Slash ? BattleConstants.SlashDamage
                    : hit == Hit.Counter ? BattleConstants.CounterBonusDamage
                    : Math.Min(attacker.ThrustBase[turn], thrustCap);
                int scaled = HiddenTechniques.ScaleDamage(baseDamage, attacker.Steps[turn]);
                return (int)(((long)scaled * s.External * xPermille + 50000) / 100000);
            }
        }

        // 안 B: an ordinary hit = 외공 * x (a Counter twice that); a skill (hidden) hit = base
        // multiplier 1 * (1 + 사용치 / 100) * 외공 * x; rounded half up; HP fixed at 100.
        // 사용치: Cost = the turn's 내공 cost (5 for 삼재검법); WholeBudget = the side's 내공 split
        // evenly over its skill turns.
        public enum SkillSpend { Cost, WholeBudget }

        public sealed class ModelB : Model
        {
            private readonly int xPermille;
            private readonly SkillSpend spend;
            public ModelB(int xPermille, SkillSpend spend = SkillSpend.Cost) { this.xPermille = xPermille; this.spend = spend; }
            public override int Hp(Stats s) => BattleConstants.StartingHP;
            public override int Damage(Hit hit, Tech attacker, int turn, Stats s)
            {
                long ordinary = (long)s.External * xPermille;   // * 1/1000
                TechniqueStep step = attacker.Steps[turn];
                if (hit != Hit.Counter && step.IsHidden)
                {
                    long use = spend == SkillSpend.Cost ? HiddenTechniques.CostOf(step) : attacker.SkillTurns > 0 ? s.Internal / attacker.SkillTurns : 0;
                    return (int)((ordinary * (100 + use) + 50000) / 100000);
                }
                long d = (ordinary + 500) / 1000;
                return (int)(hit == Hit.Counter ? 2 * d : d);
            }
        }

        public struct Result
        {
            public BattleOutcome Outcome;
            public int Turns, HpA, HpB, ContactTurns;
            public long DamageByA, DamageByB, ThrustByA, ThrustByB;
        }

        public static TurnHit[] Hits(Tech a, Tech b)
        {
            var hits = new TurnHit[BattleSimulator.TurnsPerBattle];
            for (int t = 0; t < hits.Length; t++)
                hits[t] = Resolve(a.Types[t], a.Steps[t], b.Types[t], b.Steps[t]);
            return hits;
        }

        // BattleSimulator's turn table: which hit each side takes (dealt by the other side).
        private static TurnHit Resolve(BattleStepType ta, TechniqueStep a, BattleStepType tb, TechniqueStep b)
        {
            switch (ta, tb)
            {
                case (BattleStepType.Slash, BattleStepType.Slash):
                    return StrokeAnalyzer.CrossStrokeContact(a.RawPoints, b.RawPoints) ? new TurnHit(Hit.None, Hit.None, true) : new TurnHit(Hit.Slash, Hit.Slash, false);
                case (BattleStepType.Counter, BattleStepType.Slash):
                    return StrokeAnalyzer.CrossStrokeContact(a.RawPoints, b.RawPoints) ? new TurnHit(Hit.None, Hit.Counter, true) : new TurnHit(Hit.Slash, Hit.None, false);
                case (BattleStepType.Thrust, BattleStepType.Slash):
                    return StrokeAnalyzer.CrossStrokeContact(b.RawPoints, a.RawPoints) ? new TurnHit(Hit.Slash, Hit.None, true) : new TurnHit(Hit.None, Hit.Thrust, false);
                case (BattleStepType.Thrust, BattleStepType.Thrust):
                    return new TurnHit(Hit.Thrust, Hit.Thrust, false);
                case (BattleStepType.Thrust, BattleStepType.Counter):
                    return new TurnHit(Hit.None, Hit.Thrust, false);
                case (BattleStepType.Counter, BattleStepType.Counter):
                    return new TurnHit(Hit.None, Hit.None, false);
                default:
                    TurnHit r = Resolve(tb, b, ta, a);
                    return new TurnHit(r.OnB, r.OnA, r.Contact);
            }
        }

        public static Result Fight(TurnHit[] hits, Tech a, Stats sa, Tech b, Stats sb, Model model)
        {
            var r = new Result { HpA = model.Hp(sa), HpB = model.Hp(sb) };
            for (int t = 0; t < hits.Length; t++)
            {
                TurnHit h = hits[t];
                int dA = h.OnA == Hit.None ? 0 : model.Damage(h.OnA, b, t, sb);
                int dB = h.OnB == Hit.None ? 0 : model.Damage(h.OnB, a, t, sa);
                r.HpA -= dA;
                r.HpB -= dB;
                r.DamageByB += dA;
                r.DamageByA += dB;
                if (h.OnA == Hit.Thrust) r.ThrustByB += dA;
                if (h.OnB == Hit.Thrust) r.ThrustByA += dB;
                if (h.Contact) r.ContactTurns++;
                r.Turns = t + 1;
                if (r.HpA <= 0 || r.HpB <= 0) break;
            }
            long leftA = r.HpA, leftB = r.HpB;
            if (model.JudgeByHpShare)
            {
                leftA = (long)r.HpA * model.Hp(sb);
                leftB = (long)r.HpB * model.Hp(sa);
            }
            r.Outcome = r.HpA <= 0 && r.HpB <= 0 ? BattleOutcome.Draw : r.HpB <= 0 ? BattleOutcome.WinA : r.HpA <= 0 ? BattleOutcome.WinB
                : leftA > leftB ? BattleOutcome.WinA : leftB > leftA ? BattleOutcome.WinB : BattleOutcome.Draw;
            return r;
        }

        private struct Tally
        {
            public int Win, Draw, Loss, Early;
            public long Turns, Contact, DamageByA, DamageByB, ThrustByA, ThrustByB;
            public void Add(Result r)
            {
                if (r.Outcome == BattleOutcome.WinA) Win++; else if (r.Outcome == BattleOutcome.Draw) Draw++; else Loss++;
                if (r.Turns < BattleSimulator.TurnsPerBattle) Early++;
                Turns += r.Turns;
                Contact += r.ContactTurns;
                DamageByA += r.DamageByA; DamageByB += r.DamageByB;
                ThrustByA += r.ThrustByA; ThrustByB += r.ThrustByB;
            }
            public int N => Win + Draw + Loss;
            public double WinPct => 100.0 * Win / N;
            public double DrawPct => 100.0 * Draw / N;
            public double LossPct => 100.0 * Loss / N;
            public double AvgTurns => (double)Turns / N;
            public double EarlyPct => 100.0 * Early / N;
            public double ContactPct => 100.0 * Contact / Turns;
            public double ThrustShare => DamageByA + DamageByB == 0 ? 0 : 100.0 * (ThrustByA + ThrustByB) / (DamageByA + DamageByB);
            public double ThrustShareA => DamageByA == 0 ? 0 : 100.0 * ThrustByA / DamageByA;
            public double ThrustShareB => DamageByB == 0 ? 0 : 100.0 * ThrustByB / DamageByB;
        }

        private static Tally Run(TurnHit[][] hits, Tech[] a, Stats sa, Func<int, Tech> b, Stats sb, Model model)
        {
            var t = new Tally();
            for (int i = 0; i < hits.Length; i++)
                t.Add(Fight(hits[i], a[i], sa, b(i), sb, model));
            return t;
        }

        private static TurnHit[][] AllHits(Tech[] a, Func<int, Tech> b)
        {
            var hits = new TurnHit[a.Length][];
            for (int i = 0; i < a.Length; i++)
                hits[i] = Hits(a[i], b(i));
            return hits;
        }

        private static Tech[] Pool(ulong seedBase, int slashMin, int slashMax, ref int fallbacks)
        {
            var pool = new Tech[Games];
            for (int i = 0; i < Games; i++)
                pool[i] = RandomTech(seedBase + (ulong)i, slashMin, slashMax, ref fallbacks);
            return pool;
        }

        private static string F(double v) => v.ToString("F1").PadLeft(5);

        [Test]
        [Timeout(1800000)] // about 4 minutes on the dev machine; the default limit is 3
        public void Run()
        {
            var total = Stopwatch.StartNew();
            var clock = Stopwatch.StartNew();
            var sb = new StringBuilder();
            var timing = new StringBuilder();
            void Lap(string what) { timing.AppendLine($"  {what}: {clock.Elapsed.TotalSeconds:F1} s"); clock.Restart(); }

            // ── pools ──
            int fallbacks = 0;
            Tech[] player = Pool(PlayerSeedBase, MinSlashCm, LimitCm, ref fallbacks);
            Tech[] rival = Pool(RivalSeedBase, MinSlashCm, LimitCm, ref fallbacks);
            var ai = new Dictionary<int, Tech>();
            foreach (int stage in Stages)
                ai[stage] = StageAi(stage);
            Lap("generate 2 x 2000 random techniques");

            {
                int turns = 0, thrust = 0, counter = 0, skill = 0, slashCount = 0;
                long thrustBase = 0;
                double slashSum = 0, slashMin = double.MaxValue, slashMax = 0;
                foreach (Tech t in player)
                {
                    for (int i = 0; i < t.Steps.Count; i++)
                    {
                        turns++;
                        if (t.Steps[i].IsCounter) counter++;
                        if (t.Steps[i].IsHidden) skill++;
                        if (t.Types[i] == BattleStepType.Thrust) { thrust++; thrustBase += t.ThrustBase[i]; }
                        if (t.Steps[i].Stroke.Type == StrokeType.Slash)
                        {
                            double cm = ToCm(PathLength(t.Steps[i].RawPoints) / Fixed.Scale);
                            slashSum += cm; slashCount++;
                            slashMin = Math.Min(slashMin, cm); slashMax = Math.Max(slashMax, cm);
                        }
                    }
                }
                sb.AppendLine($"RANDOM TECHNIQUES  {Games} per pool, seeds fixed. Player pool: {turns} turns, thrust {thrust} (avg base damage {(double)thrustBase / thrust:F1}), counter {counter}, 삼재 {skill}.");
                sb.AppendLine($"Slash-shaped strokes {slashCount}: length min {slashMin:F2} avg {slashSum / slashCount:F2} max {slashMax:F2} cm. Fallback spirals used (both pools): {fallbacks}.");
            }

            var hitsRival = AllHits(player, i => rival[i]);
            var hitsAi = new Dictionary<int, TurnHit[][]>();
            foreach (int stage in Stages)
            {
                Tech enemy = ai[stage];
                hitsAi[stage] = AllHits(player, i => enemy);
            }
            Lap("turn tables, player x rival and player x stages 1/6/11");

            var one = new Stats(100, 100);
            var modelA = new ModelA(1000);

            // ── check against BattleSimulator ──
            int mismatches = 0, compared = 0;
            for (int i = 0; i < Games; i++)
            {
                BattleResult real = BattleSimulator.Simulate(player[i].Steps, rival[i].Steps);
                Result mine = Fight(hitsRival[i], player[i], one, rival[i], one, modelA);
                compared++;
                if (real.Outcome != mine.Outcome || real.Events.Count != mine.Turns || real.FinalHpA != mine.HpA || real.FinalHpB != mine.HpB) mismatches++;
                foreach (int stage in Stages)
                {
                    real = BattleSimulator.Simulate(player[i].Steps, ai[stage].Steps);
                    mine = Fight(hitsAi[stage][i], player[i], one, ai[stage], one, modelA);
                    compared++;
                    if (real.Outcome != mine.Outcome || real.Events.Count != mine.Turns || real.FinalHpA != mine.HpA || real.FinalHpB != mine.HpB) mismatches++;
                }
            }
            sb.AppendLine();
            sb.AppendLine($"CHECK  안 A at 내공 100·외공 100, x = 1 vs BattleSimulator.Simulate: {mismatches} mismatches in {compared} battles (outcome, end turn, both final HP).");
            Assert.AreEqual(0, mismatches, "The simulation engine must reproduce BattleSimulator exactly.");
            Lap("check against BattleSimulator");

            // ── baseline and 안 B's x ──
            Tally now = Run(hitsRival, player, one, i => rival[i], one, modelA);
            sb.AppendLine();
            sb.AppendLine("BASELINE  current rules (= 안 A, power 1, x = 1)");
            sb.AppendLine($"random vs random: win {F(now.WinPct)} draw {F(now.DrawPct)} loss {F(now.LossPct)} | avg end turn {now.AvgTurns:F2}, ends before turn 10 {F(now.EarlyPct)}% | contact turns {F(now.ContactPct)}% | thrust share of damage {F(now.ThrustShare)}%");
            foreach (int stage in Stages)
            {
                Tech enemy = ai[stage];
                Tally t = Run(hitsAi[stage], player, one, i => enemy, one, modelA);
                sb.AppendLine($"vs stage {stage,2} ({StageCatalog.Get(stage).Difficulty}): win {F(t.WinPct)} draw {F(t.DrawPct)} loss {F(t.LossPct)} | avg end turn {t.AvgTurns:F2}, before turn 10 {F(t.EarlyPct)}% | contact {F(t.ContactPct)}%");
            }

            sb.AppendLine();
            sb.AppendLine("안 B  x search (power 1 vs power 1, random vs random): the x whose average end turn is closest to the baseline's");
            sb.AppendLine("x     | hit dmg | avg end turn | before turn 10 % | win% draw%");
            int xB = 0;
            double best = double.MaxValue;
            for (int x = 50; x <= 250; x += 10)
            {
                var m = new ModelB(x);
                Tally t = Run(hitsRival, player, one, i => rival[i], one, m);
                sb.AppendLine($"{x / 1000.0,-5:F2} | {m.Damage(Hit.Slash, ai[1], 0, one),7} | {t.AvgTurns,12:F2} | {F(t.EarlyPct),16} | {F(t.WinPct)} {F(t.DrawPct)}");
                double gap = Math.Abs(t.AvgTurns - now.AvgTurns);
                if (gap < best) { best = gap; xB = x; }
            }
            var modelB = new ModelB(xB);
            var modelAShare = new ModelA(1000) { JudgeByHpShare = true };
            sb.AppendLine($"chosen x for 안 B: {xB / 1000.0:F2} (baseline avg end turn {now.AvgTurns:F2})");

            // ── 1. fairness at equal power ──
            var splits = new[] { new Stats(100, 100), new Stats(50, 200), new Stats(200, 50) };
            sb.AppendLine();
            sb.AppendLine("1. SAME POWER, DIFFERENT SPLIT  random vs random, A = row, B = column, A win% / draw% / loss%");
            foreach ((string name, Model m) in new (string, Model)[]
            {
                ("안 A (x = 1)", modelA),
                ("안 A (x = 1), turn-10 winner by remaining HP share instead of HP", modelAShare),
                ($"안 B (x = {xB / 1000.0:F2}, 사용치 = cost 5)", modelB),
                ($"안 B (x = {xB / 1000.0:F2}, 사용치 = whole 내공 over the skill turns)", new ModelB(xB, SkillSpend.WholeBudget)),
            })
            {
                sb.AppendLine(name);
                foreach (Stats sa in splits)
                {
                    var line = new StringBuilder($"  A {sa,-16}");
                    foreach (Stats sbs in splits)
                    {
                        Tally t = Run(hitsRival, player, sa, i => rival[i], sbs, m);
                        line.Append($" | vs {sbs}: {F(t.WinPct)}/{F(t.DrawPct)}/{F(t.LossPct)}");
                    }
                    sb.AppendLine(line.ToString());
                }
            }

            // ── 2. power ratio ──
            sb.AppendLine();
            sb.AppendLine("2. POWER RATIO (me / opponent)  random vs random, opponent power 1, 내공 = 외공");
            sb.AppendLine("ratio | my stats          | 안 A win% draw% loss% end turn | 안 B win% draw% loss% end turn | 안 A by HP share win% draw% loss%");
            foreach (double ratio in new[] { 0.5, 0.75, 1.0, 1.5, 2.0, 3.0 })
            {
                Stats me = Stats.ForPower(ratio);
                Tally ta = Run(hitsRival, player, me, i => rival[i], one, modelA);
                Tally tb = Run(hitsRival, player, me, i => rival[i], one, modelB);
                Tally ts = Run(hitsRival, player, me, i => rival[i], one, modelAShare);
                sb.AppendLine($"{ratio,5:F2} | {me,-17} | {F(ta.WinPct)} {F(ta.DrawPct)} {F(ta.LossPct)} {ta.AvgTurns,8:F2} | {F(tb.WinPct)} {F(tb.DrawPct)} {F(tb.LossPct)} {tb.AvgTurns,8:F2} | {F(ts.WinPct)} {F(ts.DrawPct)} {F(ts.LossPct)}");
            }

            // ── 3. enemy power per stage ──
            sb.AppendLine();
            sb.AppendLine("3. ENEMY POWER PER STAGE  player power 1 (random), enemy 내공 = 외공 = 100 * sqrt(power); player win% / draw%");
            foreach ((string name, Model m) in new (string, Model)[] { ("안 A (x = 1)", modelA), ("안 A (x = 1), turn-10 winner by remaining HP share", modelAShare), ($"안 B (x = {xB / 1000.0:F2})", modelB) })
            {
                sb.AppendLine(name);
                sb.AppendLine("  stage      | p 0.25      | p 0.5       | p 0.75      | p 1         | p 1.5       | p 2         | highest enemy stat with win >= 80%");
                foreach (int stage in Stages)
                {
                    Tech enemy = ai[stage];
                    var line = new StringBuilder($"  {stage,2} {StageCatalog.Get(stage).Difficulty,-6}");
                    foreach (double p in new[] { 0.25, 0.5, 0.75, 1.0, 1.5, 2.0 })
                    {
                        Tally t = Run(hitsAi[stage], player, one, i => enemy, Stats.ForPower(p), m);
                        line.Append($" | {F(t.WinPct)}/{F(t.DrawPct)}");
                    }
                    line.Append(" | " + EightyPercentPoint(hitsAi[stage], player, one, enemy, m));
                    sb.AppendLine(line.ToString());
                }
            }
            Lap("items 1-3");

            // 3b. Easy AI stroke length share, stage 1's seed.
            sb.AppendLine();
            sb.AppendLine("3b. STAGE 1 WITH A SHORTER EASY AI  same seed, AiProfile length share changed (thrust 20%, no counters); 안 A x = 1");
            sb.AppendLine("  length share | win%/draw% at enemy power 1 | contact % | AI avg thrust base | highest enemy stat with win >= 80%");
            foreach ((int lo, int hi) in new[] { (30, 50), (20, 40), (15, 30), (10, 25), (5, 15) })
            {
                var enemy = new Tech(AiTechniqueGenerator.Generate(new AiProfile(20, lo, hi, 0), StageCatalog.Get(1).Seed));
                TurnHit[][] hits = lo == 30 ? hitsAi[1] : AllHits(player, i => enemy);
                Tally t = Run(hits, player, one, i => enemy, one, modelA);
                long thrustSum = 0; int thrusts = 0;
                for (int k = 0; k < enemy.Steps.Count; k++)
                    if (enemy.Types[k] == BattleStepType.Thrust) { thrustSum += enemy.ThrustBase[k]; thrusts++; }
                sb.AppendLine($"  {lo,2}-{hi,2}% ({lo * LimitCm / 100000.0:F1}-{hi * LimitCm / 100000.0:F1} cm) | {F(t.WinPct)}/{F(t.DrawPct)} | {F(t.ContactPct)} | {(thrusts > 0 ? ((double)thrustSum / thrusts).ToString("F1") : "-"),5} ({thrusts} thrusts) | {EightyPercentPoint(hits, player, one, enemy, modelA)}");
            }
            Lap("item 3b");

            // ── 4. battle length ──
            sb.AppendLine();
            sb.AppendLine("4. BATTLE LENGTH  power p vs power p, random vs random: avg end turn / ends before turn 10 %");
            sb.AppendLine("model            | power 1        | power 4        | power 9");
            var lengthModels = new List<(string, Model)>
            {
                ($"안 B x = {xB / 1000.0:F2}", modelB),
                ("안 A x = 0.5", new ModelA(500)),
                ("안 A x = 1", modelA),
                ("안 A x = 1.5", new ModelA(1500)),
                ("안 A x = 2", new ModelA(2000)),
            };
            foreach ((string name, Model m) in lengthModels)
            {
                var line = new StringBuilder($"{name,-16}");
                foreach (double p in new[] { 1.0, 4.0, 9.0 })
                {
                    Stats s = Stats.ForPower(p);
                    Tally t = Run(hitsRival, player, s, i => rival[i], s, m);
                    line.Append($" | {t.AvgTurns,5:F2} / {F(t.EarlyPct)}%");
                }
                sb.AppendLine(line.ToString());
            }
            sb.AppendLine("the same against the stage AIs at power 1 (player win% | avg end turn / before turn 10 %):");
            foreach ((string name, Model m) in lengthModels)
            {
                var line = new StringBuilder($"{name,-16}");
                foreach (int stage in Stages)
                {
                    Tech enemy = ai[stage];
                    Tally t = Run(hitsAi[stage], player, one, i => enemy, one, m);
                    line.Append($" | st {stage,2}: {F(t.WinPct)} | {t.AvgTurns,5:F2} / {F(t.EarlyPct)}%");
                }
                sb.AppendLine(line.ToString());
            }

            // ── 5. thrust cap ──
            sb.AppendLine();
            sb.AppendLine("5. THRUST BASE DAMAGE CAP  current rules otherwise (power 1, x = 1): player win%/draw% | thrust share of all damage (player's, AI's)");
            sb.AppendLine("cap   | stage 1                          | stage 6                          | stage 11                         | random vs random");
            foreach (int cap in new[] { int.MaxValue, 20, 15 })
            {
                var m = new ModelA(1000, cap);
                var line = new StringBuilder($"{(cap == int.MaxValue ? "none" : cap.ToString()),-5}");
                foreach (int stage in Stages)
                {
                    Tech enemy = ai[stage];
                    Tally t = Run(hitsAi[stage], player, one, i => enemy, one, m);
                    line.Append($" | {F(t.WinPct)}/{F(t.DrawPct)} | {F(t.ThrustShare)}% ({F(t.ThrustShareA)}, {F(t.ThrustShareB)})");
                }
                Tally r = Run(hitsRival, player, one, i => rival[i], one, m);
                line.Append($" | {F(r.WinPct)}/{F(r.DrawPct)} | {F(r.ThrustShare)}%, end turn {r.AvgTurns:F2}");
                sb.AppendLine(line.ToString());
            }
            Lap("items 4-5");

            // ── 6. limit / frame ratio ──
            sb.AppendLine();
            sb.AppendLine("6. LENGTH LIMIT / FRAME WIDTH  every stroke of both sides shrunk about its own centre; current rules, power 1");
            sb.AppendLine("limit            | stage 1 win%/draw% contact% | stage 6 win%/draw% contact% | stage 11 win%/draw% contact% | random vs random win%/draw% contact% end turn");
            foreach ((string name, int num, int den) in new[] { ("8/3 (12 cm, now)", 1, 1), ("2 (9 cm)", 3, 4), ("1.5 (6.75 cm)", 9, 16) })
            {
                Tech[] p = player, r = rival;
                if (num != den)
                {
                    p = new Tech[Games];
                    r = new Tech[Games];
                    for (int i = 0; i < Games; i++) { p[i] = Shrink(player[i], num, den); r[i] = Shrink(rival[i], num, den); }
                }
                var line = new StringBuilder($"{name,-16}");
                foreach (int stage in Stages)
                {
                    Tech enemy = Shrink(ai[stage], num, den);
                    TurnHit[][] hits = num == den ? hitsAi[stage] : AllHits(p, i => enemy);
                    Tally t = Run(hits, p, one, i => enemy, one, modelA);
                    line.Append($" | {F(t.WinPct)}/{F(t.DrawPct)} {F(t.ContactPct)}");
                }
                Tech[] rr = r;
                TurnHit[][] hr = num == den ? hitsRival : AllHits(p, i => rr[i]);
                Tally tr = Run(hr, p, one, i => rr[i], one, modelA);
                line.Append($" | {F(tr.WinPct)}/{F(tr.DrawPct)} {F(tr.ContactPct)} {tr.AvgTurns:F2}");
                sb.AppendLine(line.ToString());
            }
            Lap("item 6");

            // ── 7. stroke length advantage ──
            sb.AppendLine();
            sb.AppendLine("7. SLASH LENGTH  equal stats (power 1), current rules");
            sb.AppendLine("7a. the player pool split into thirds by its average slash length: win%/draw%/loss%");
            var order = new List<int>();
            for (int i = 0; i < Games; i++)
                if (player[i].AvgSlashCm > 0) order.Add(i);
            order.Sort((x, y) => player[x].AvgSlashCm.CompareTo(player[y].AvgSlashCm));
            string[] thirds = { "short", "middle", "long" };
            for (int g = 0; g < 3; g++)
            {
                int from = order.Count * g / 3, to = order.Count * (g + 1) / 3;
                var vsRival = new Tally();
                var vsStage = new Tally[Stages.Length];
                double lenSum = 0;
                for (int k = from; k < to; k++)
                {
                    int i = order[k];
                    lenSum += player[i].AvgSlashCm;
                    vsRival.Add(Fight(hitsRival[i], player[i], one, rival[i], one, modelA));
                    for (int s = 0; s < Stages.Length; s++)
                        vsStage[s].Add(Fight(hitsAi[Stages[s]][i], player[i], one, ai[Stages[s]], one, modelA));
                }
                var line = new StringBuilder($"  {thirds[g],-6} (n={to - from}, avg {lenSum / (to - from):F2} cm, {player[order[from]].AvgSlashCm:F2}-{player[order[to - 1]].AvgSlashCm:F2}) | vs random {F(vsRival.WinPct)}/{F(vsRival.DrawPct)}/{F(vsRival.LossPct)}");
                for (int s = 0; s < Stages.Length; s++)
                    line.Append($" | st {Stages[s],2} {F(vsStage[s].WinPct)}/{F(vsStage[s].DrawPct)}/{F(vsStage[s].LossPct)}");
                sb.AppendLine(line.ToString());
            }

            sb.AppendLine("7b. separate pools whose every slash is drawn in one length band (thrusts unchanged): win%/draw%/loss%, contact %");
            int bandFallbacks = 0;
            var bands = new[] { ("1.5-5 cm", 1500, 5000), ("5-8.5 cm", 5000, 8500), ("8.5-12 cm", 8500, 12000) };
            for (int g = 0; g < bands.Length; g++)
            {
                (string name, int lo, int hi) = bands[g];
                Tech[] pool = Pool(LengthPoolSeedBase + (ulong)g * 0x10000000UL, lo, hi, ref bandFallbacks);
                Tally vsRival = Run(AllHits(pool, i => rival[i]), pool, one, i => rival[i], one, modelA);
                var line = new StringBuilder($"  {name,-10} | vs random {F(vsRival.WinPct)}/{F(vsRival.DrawPct)}/{F(vsRival.LossPct)} contact {F(vsRival.ContactPct)}");
                foreach (int stage in Stages)
                {
                    Tech enemy = ai[stage];
                    Tally t = Run(AllHits(pool, i => enemy), pool, one, i => enemy, one, modelA);
                    line.Append($" | st {stage,2} {F(t.WinPct)}/{F(t.DrawPct)}/{F(t.LossPct)} contact {F(t.ContactPct)}");
                }
                sb.AppendLine(line.ToString());
            }
            sb.AppendLine($"  (fallback spirals in the band pools: {bandFallbacks})");
            Lap("item 7");

            sb.AppendLine();
            sb.AppendLine($"RUN TIME  total {total.Elapsed.TotalSeconds:F1} s");
            sb.Append(timing);

            string path = Path.GetFullPath("Temp/CombatPowerFormulaSimulation.txt");
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            TestContext.WriteLine(sb.ToString());
        }

        // The highest enemy stat (내공 = 외공, 1..400) at which the player still wins >= 80%.
        private static string EightyPercentPoint(TurnHit[][] hits, Tech[] players, Stats playerStats, Tech enemy, Model model)
        {
            int found = 0;
            double foundWin = 0;
            for (int stat = 1; stat <= 400; stat++)
            {
                Tally t = Run(hits, players, playerStats, i => enemy, new Stats(stat, stat), model);
                if (t.WinPct >= 80.0) { found = stat; foundWin = t.WinPct; }
            }
            return found == 0 ? "none (below 80% even at stat 1)" : $"stat {found} = power {found * found / 10000.0:F2} (win {foundWin:F1}%)";
        }
    }
}

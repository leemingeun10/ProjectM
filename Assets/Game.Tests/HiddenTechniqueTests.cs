using System;
using System.Collections.Generic;
using Game.Sim.Battle;
using Game.Sim.Shape;
using Game.Sim.Stroke;
using Game.Sim.Technique;
using NUnit.Framework;

namespace Game.Tests
{
    public class HiddenTechniqueTests
    {
        private const string Samjae = ShapeTemplates.TriangleId;

        // ---- Fixtures (whole sim units) ----

        // Closed right triangle, area 400 * 360 / 2 = 72000 -> sqrt 268 -> base thrust damage 10.
        private static readonly StrokePoint[] Triangle =
        {
            StrokePoint.FromInt(0, 0), StrokePoint.FromInt(400, 0), StrokePoint.FromInt(0, 360), StrokePoint.FromInt(0, 0)
        };

        // Crosses the triangle's bottom edge.
        private static readonly StrokePoint[] CrossesEdge = { StrokePoint.FromInt(100, -100), StrokePoint.FromInt(100, 50) };

        // Lies inside the triangle, touching none of its edges.
        private static readonly StrokePoint[] InsideTriangle = { StrokePoint.FromInt(80, 100), StrokePoint.FromInt(120, 100) };

        private static readonly StrokePoint[] FarSlash = { StrokePoint.FromInt(0, 5000), StrokePoint.FromInt(200, 5000) };

        private static StrokeResult SlashStroke(StrokePoint[] points) => StrokeAnalyzer.Analyze(points);

        private static TechniqueStep Hidden(int turn, HiddenTechniqueMove move, StrokePoint[] points = null)
        {
            points = points ?? Triangle;
            return new TechniqueStep(turn, false, StrokeAnalyzer.Analyze(points), points, Samjae, move);
        }

        private static TechniqueStep Ordinary(int turn, StrokePoint[] points) => new TechniqueStep(turn, false, StrokeAnalyzer.Analyze(points), points);

        private static TechniqueStep Counter(int turn, StrokePoint[] points) => new TechniqueStep(turn, true, StrokeAnalyzer.Analyze(points), points);

        // Counter vs Counter deals nothing, so it fills the turns a test doesn't use.
        private static List<TechniqueStep> Sequence(params TechniqueStep[] first)
        {
            var steps = new List<TechniqueStep>();
            for (int i = 0; i < BattleSimulator.TurnsPerBattle; i++)
                steps.Add(i < first.Length ? first[i].WithTurnIndex(i) : Counter(i, FarSlash));
            return steps;
        }

        private static TurnEvent FirstTurn(TechniqueStep a, TechniqueStep b)
            => BattleSimulator.Simulate(Sequence(a), Sequence(b)).Events[0];

        // ---- Table ----

        [Test]
        public void Table_OnlySamjaeIsActive_CostFiveDamage105Percent()
        {
            Assert.IsTrue(HiddenTechniques.TryGetActive(Samjae, out HiddenTechniqueDefinition samjae));
            Assert.AreEqual(5, samjae.InternalPowerCost);
            Assert.AreEqual(105, samjae.DamagePercent);
            Assert.AreEqual(HiddenTechniques.SamjaeCost + 100, samjae.DamagePercent);

            foreach (string inactive in new[] { ShapeTemplates.FlowerId, ShapeTemplates.SwordId, ShapeTemplates.CrescentId })
                Assert.IsFalse(HiddenTechniques.TryGetActive(inactive, out _), inactive);
            Assert.IsFalse(HiddenTechniques.TryGetActive("square", out _));
            Assert.IsFalse(HiddenTechniques.TryGetActive(null, out _));
        }

        [Test]
        public void Table_CoversEveryBuiltInShape()
        {
            foreach (ShapeTemplate template in ShapeTemplates.All)
            {
                bool listed = false;
                foreach (HiddenTechniqueDefinition d in HiddenTechniques.All)
                    listed |= d.Id == template.Id;
                Assert.IsTrue(listed, template.Id);
            }
        }

        [Test]
        public void ActiveTemplates_AreOnlyTheActiveTechniques_InactiveShapesAreNotRecognised()
        {
            foreach (ShapeTemplate template in ShapeTemplates.Active)
                Assert.IsTrue(HiddenTechniques.TryGetActive(template.Id, out _), template.Id);
            CollectionAssert.AreEqual(new[] { ShapeTemplates.Triangle }, ShapeTemplates.Active);

            // The flower still matches itself among All, but the game's list can't return it.
            Assert.AreEqual(ShapeTemplates.FlowerId, ShapeRecognizer.Recognize(ShapeTemplates.Flower.NormalizedPoints, ShapeTemplates.All, ShapeRecognizer.DefaultThreshold)?.TemplateId);
            Assert.AreNotEqual(ShapeTemplates.FlowerId, ShapeRecognizer.Recognize(ShapeTemplates.Flower.NormalizedPoints, ShapeTemplates.Active, ShapeRecognizer.DefaultThreshold)?.TemplateId);
        }

        // ---- Rounding ----

        [TestCase(10, 11)] // 10.5 -> 11 (half up): the 삼재 slash
        [TestCase(20, 21)]
        [TestCase(30, 32)] // 31.5 -> 32
        [TestCase(9, 9)]   // 9.45 -> 9
        [TestCase(4, 4)]   // 4.2 -> 4
        [TestCase(1, 1)]   // 1.05 -> 1
        [TestCase(0, 0)]
        public void ScaleDamage_105Percent_RoundsHalfUp(int baseDamage, int expected)
        {
            Assert.AreEqual(expected, HiddenTechniques.ScaleDamage(baseDamage, 105));
            Assert.AreEqual(expected, HiddenTechniques.ScaleDamage(baseDamage, Hidden(0, HiddenTechniqueMove.HorizontalSlash)));
        }

        [Test]
        public void ScaleDamage_OrdinaryTurnIsUnchanged()
        {
            Assert.AreEqual(10, HiddenTechniques.ScaleDamage(10, Ordinary(0, FarSlash)));
            Assert.AreEqual(9, HiddenTechniques.ScaleDamage(9, Ordinary(0, FarSlash)));
        }

        // ---- Battle type and contact ----

        [Test]
        public void TriangleStroke_AnalysesAsThrust_ButTheChosenMoveDecidesItsType()
        {
            Assert.AreEqual(StrokeType.Thrust, StrokeAnalyzer.Analyze(Triangle).Type);
            Assert.AreEqual(BattleStepType.Thrust, BattleSimulator.GetStepType(Ordinary(0, Triangle)));
            Assert.AreEqual(BattleStepType.Slash, BattleSimulator.GetStepType(Hidden(0, HiddenTechniqueMove.HorizontalSlash)));
            Assert.AreEqual(BattleStepType.Slash, BattleSimulator.GetStepType(Hidden(0, HiddenTechniqueMove.VerticalSlash)));
            Assert.AreEqual(BattleStepType.Thrust, BattleSimulator.GetStepType(Hidden(0, HiddenTechniqueMove.Thrust)));
        }

        [Test]
        public void SlashChoice_ContactIsJudgedOnTheTriangleLines()
        {
            // A slash crossing a triangle edge makes contact: both slashes are nullified.
            TurnEvent crossed = FirstTurn(Hidden(0, HiddenTechniqueMove.HorizontalSlash), Ordinary(0, CrossesEdge));
            Assert.AreEqual(BattleStepType.Slash, crossed.TypeA);
            Assert.AreEqual(TurnReason.CrossContact, crossed.Reason);
            Assert.AreEqual((0, 0), (crossed.DamageToA, crossed.DamageToB));

            // A slash inside the triangle touches none of its lines: a miss, both land, 삼재 with its bonus.
            TurnEvent inside = FirstTurn(Hidden(0, HiddenTechniqueMove.VerticalSlash), Ordinary(0, InsideTriangle));
            Assert.AreEqual(TurnReason.Miss, inside.Reason);
            Assert.AreEqual(BattleConstants.SlashDamage, inside.DamageToA);
            Assert.AreEqual(11, inside.DamageToB);
        }

        [Test]
        public void ThrustChoice_DealsTheTriangleAreaDamageTimes105()
        {
            TechniqueStep samjae = Hidden(0, HiddenTechniqueMove.Thrust);
            int baseDamage = BattleSimulator.ThrustDamage(samjae);
            Assert.AreEqual(10, baseDamage); // area 72000

            // Thrust beats a counter unconditionally.
            TurnEvent e = FirstTurn(samjae, Counter(0, FarSlash));
            Assert.AreEqual(TurnReason.Unconditional, e.Reason);
            Assert.AreEqual(11, e.DamageToB);
            Assert.AreEqual(0, e.DamageToA);
        }

        [Test]
        public void ThrustChoice_OpenShape_UsesTheClosedPolygonArea()
        {
            // The same triangle without its closing point analyses as a Slash (no stored area).
            StrokePoint[] open = { Triangle[0], Triangle[1], Triangle[2] };
            Assert.AreEqual(StrokeType.Slash, StrokeAnalyzer.Analyze(open).Type);
            Assert.AreEqual(10, BattleSimulator.ThrustDamage(Hidden(0, HiddenTechniqueMove.Thrust, open)));
        }

        [Test]
        public void OrdinaryTurns_DealTheSameDamageAsBefore()
        {
            // The same triangle drawn as an ordinary turn is a plain Thrust: 10, no bonus.
            Assert.AreEqual(10, FirstTurn(Ordinary(0, Triangle), Counter(0, FarSlash)).DamageToB);
            // Missed ordinary slashes: 10 each.
            TurnEvent miss = FirstTurn(Ordinary(0, FarSlash), Ordinary(0, InsideTriangle));
            Assert.AreEqual((10, 10), (miss.DamageToA, miss.DamageToB));
            // Counter landing on an ordinary slash: 20.
            TurnEvent counter = FirstTurn(Counter(0, CrossesEdge), Ordinary(0, new[] { StrokePoint.FromInt(0, 0), StrokePoint.FromInt(200, 0) }));
            Assert.AreEqual(TurnReason.CrossContact, counter.Reason);
            Assert.AreEqual(BattleConstants.CounterBonusDamage, counter.DamageToB);
        }

        [Test]
        public void Bonus_OnlyAppliesToDamageTheHiddenTurnDeals()
        {
            // A counter landing on a 삼재 slash still deals exactly the counter bonus, and the slash deals 0.
            TurnEvent countered = FirstTurn(Counter(0, CrossesEdge), Hidden(0, HiddenTechniqueMove.HorizontalSlash));
            Assert.AreEqual(TurnReason.CrossContact, countered.Reason);
            Assert.AreEqual(BattleConstants.CounterBonusDamage, countered.DamageToB);
            Assert.AreEqual(0, countered.DamageToA);

            // A missed counter takes the 삼재 slash with its bonus.
            TurnEvent missed = FirstTurn(Counter(0, FarSlash), Hidden(0, HiddenTechniqueMove.HorizontalSlash));
            Assert.AreEqual(TurnReason.Miss, missed.Reason);
            Assert.AreEqual(11, missed.DamageToA);

            // A thrust whose loop is cut by the 삼재 slash takes the slash with its bonus; the slash takes nothing.
            TurnEvent cut = FirstTurn(Ordinary(0, Triangle), Hidden(0, HiddenTechniqueMove.VerticalSlash, CrossesEdge));
            Assert.AreEqual(TurnReason.CrossContact, cut.Reason);
            Assert.AreEqual(11, cut.DamageToA);
            Assert.AreEqual(0, cut.DamageToB);

            // Two 삼재 thrusts: each takes the other's bonus damage.
            TurnEvent both = FirstTurn(Hidden(0, HiddenTechniqueMove.Thrust), Hidden(0, HiddenTechniqueMove.Thrust));
            Assert.AreEqual((11, 11), (both.DamageToA, both.DamageToB));
        }

        [Test]
        public void Simulate_RejectsInvalidHiddenSteps()
        {
            var flower = new TechniqueStep(0, false, StrokeAnalyzer.Analyze(Triangle), Triangle, ShapeTemplates.FlowerId, HiddenTechniqueMove.Thrust);
            var noMove = new TechniqueStep(0, false, StrokeAnalyzer.Analyze(Triangle), Triangle, Samjae, HiddenTechniqueMove.None);
            var counter = new TechniqueStep(0, true, StrokeAnalyzer.Analyze(Triangle), Triangle, Samjae, HiddenTechniqueMove.Thrust);
            foreach (TechniqueStep bad in new[] { flower, noMove, counter })
                Assert.Throws<ArgumentException>(() => BattleSimulator.Simulate(Sequence(bad), Sequence()));
        }

        [Test]
        public void Simulate_IsDeterministicWithHiddenTurns()
        {
            List<TechniqueStep> a = Sequence(Hidden(0, HiddenTechniqueMove.Thrust), Hidden(1, HiddenTechniqueMove.HorizontalSlash),
                Ordinary(2, FarSlash), Hidden(3, HiddenTechniqueMove.VerticalSlash, CrossesEdge));
            List<TechniqueStep> b = Sequence(Ordinary(0, InsideTriangle), Counter(1, CrossesEdge), Hidden(2, HiddenTechniqueMove.Thrust), Ordinary(3, Triangle));

            BattleResult first = BattleSimulator.Simulate(a, b);
            BattleResult second = BattleSimulator.Simulate(a, b);
            Assert.AreEqual(first.Outcome, second.Outcome);
            Assert.AreEqual(first.Events.Count, second.Events.Count);
            for (int i = 0; i < first.Events.Count; i++)
                Assert.AreEqual(first.Events[i].ToString(), second.Events[i].ToString());
        }

        // ---- Builder: validity and 내공 budget ----

        private static TechniqueAddResult AddSamjae(TechniqueBuilder builder, HiddenTechniqueMove move = HiddenTechniqueMove.Thrust)
            => builder.TryAddStep(false, StrokeAnalyzer.Analyze(Triangle), Triangle, Samjae, move);

        [TestCase(0, 0)]
        [TestCase(4, 0)]
        [TestCase(5, 1)]
        [TestCase(9, 1)]
        [TestCase(10, 2)]
        public void Budget_SamjaeTurnsAllowedByInternalPower(int internalPower, int allowed)
        {
            var builder = new TechniqueBuilder { InternalPower = internalPower };
            int added = 0;
            while (AddSamjae(builder) == TechniqueAddResult.Success)
                added++;

            Assert.AreEqual(allowed, added);
            Assert.AreEqual(TechniqueAddResult.NotEnoughInternalPower, AddSamjae(builder));
            Assert.AreEqual(allowed, builder.TurnCount, "a refused hidden turn adds nothing");
            Assert.AreEqual(internalPower - 5 * allowed, builder.RemainingInternalPower);
            // An ordinary turn still fits.
            Assert.AreEqual(TechniqueAddResult.Success, builder.TryAddStep(false, SlashStroke(FarSlash), FarSlash));
        }

        [Test]
        public void Budget_RemovingAHiddenTurnGivesItsCostBack()
        {
            var builder = new TechniqueBuilder { InternalPower = 5 };
            Assert.AreEqual(TechniqueAddResult.Success, builder.TryAddStep(false, SlashStroke(FarSlash), FarSlash));
            Assert.AreEqual(TechniqueAddResult.Success, AddSamjae(builder, HiddenTechniqueMove.VerticalSlash));
            Assert.AreEqual(0, builder.RemainingInternalPower);
            Assert.AreEqual(TechniqueAddResult.NotEnoughInternalPower, AddSamjae(builder));

            Assert.IsTrue(builder.RemoveAt(1));
            Assert.AreEqual(5, builder.RemainingInternalPower);
            Assert.AreEqual(TechniqueAddResult.Success, AddSamjae(builder));
            Assert.AreEqual(0, builder.RemainingInternalPower);
        }

        [Test]
        public void RemoveAt_KeepsHiddenFieldsWhileRenumbering()
        {
            var builder = new TechniqueBuilder { InternalPower = 10 };
            builder.TryAddStep(false, SlashStroke(FarSlash), FarSlash);
            AddSamjae(builder, HiddenTechniqueMove.HorizontalSlash);
            builder.RemoveAt(0);

            Assert.AreEqual(0, builder.Steps[0].TurnIndex);
            Assert.AreEqual(Samjae, builder.Steps[0].HiddenTechniqueId);
            Assert.AreEqual(HiddenTechniqueMove.HorizontalSlash, builder.Steps[0].HiddenMove);
            Assert.AreEqual(5, builder.SpentInternalPower);
        }

        [Test]
        public void Builder_RejectsInvalidHiddenCombinations()
        {
            var builder = new TechniqueBuilder { InternalPower = 100 };
            StrokeResult stroke = StrokeAnalyzer.Analyze(Triangle);

            Assert.AreEqual(TechniqueAddResult.UnknownHiddenTechnique, builder.TryAddStep(false, stroke, Triangle, "square", HiddenTechniqueMove.Thrust));
            Assert.AreEqual(TechniqueAddResult.UnknownHiddenTechnique, builder.TryAddStep(false, stroke, Triangle, ShapeTemplates.FlowerId, HiddenTechniqueMove.Thrust));
            Assert.AreEqual(TechniqueAddResult.HiddenTechniqueMoveMismatch, builder.TryAddStep(false, stroke, Triangle, Samjae, HiddenTechniqueMove.None));
            Assert.AreEqual(TechniqueAddResult.HiddenTechniqueMoveMismatch, builder.TryAddStep(false, stroke, Triangle, null, HiddenTechniqueMove.Thrust));
            Assert.AreEqual(TechniqueAddResult.HiddenTechniqueOnCounter, builder.TryAddStep(true, stroke, Triangle, Samjae, HiddenTechniqueMove.Thrust));
            Assert.AreEqual(0, builder.TurnCount);
            Assert.AreEqual(2, builder.RemainingCounters, "a refused hidden counter doesn't use a counter");
        }

        [Test]
        public void TotalCost_IsWorkedOutFromTheSteps()
        {
            var steps = new List<TechniqueStep> { Hidden(0, HiddenTechniqueMove.Thrust), Ordinary(1, FarSlash), Hidden(2, HiddenTechniqueMove.VerticalSlash) };
            Assert.AreEqual(10, HiddenTechniques.TotalCost(steps));
            Assert.AreEqual(0, HiddenTechniques.TotalCost(new List<TechniqueStep>()));
        }

        // ---- JSON ----

        [Test]
        public void Json_V3RoundTripKeepsHiddenTurns()
        {
            var builder = new TechniqueBuilder { InternalPower = 15 };
            AddSamjae(builder, HiddenTechniqueMove.HorizontalSlash);
            builder.TryAddStep(false, SlashStroke(FarSlash), FarSlash);
            AddSamjae(builder, HiddenTechniqueMove.VerticalSlash);
            AddSamjae(builder, HiddenTechniqueMove.Thrust);

            string json = TechniqueJsonSerializer.Serialize(builder);
            StringAssert.Contains("\"version\": 3", json);
            StringAssert.Contains("\"hiddenTechnique\": \"triangle\"", json);

            (int version, List<TechniqueStep> steps) = TechniqueJsonSerializer.Deserialize(json);
            Assert.AreEqual(3, version);
            Assert.AreEqual(4, steps.Count);
            HiddenTechniqueMove[] moves = { HiddenTechniqueMove.HorizontalSlash, HiddenTechniqueMove.None, HiddenTechniqueMove.VerticalSlash, HiddenTechniqueMove.Thrust };
            for (int i = 0; i < 4; i++)
            {
                Assert.AreEqual(moves[i], steps[i].HiddenMove, $"step {i}");
                Assert.AreEqual(moves[i] == HiddenTechniqueMove.None ? null : Samjae, steps[i].HiddenTechniqueId, $"step {i}");
                Assert.AreEqual(builder.Steps[i].RawPoints.Count, steps[i].RawPoints.Count);
            }
        }

        [Test]
        public void Json_OrdinaryStepsWriteNoHiddenFields()
        {
            var builder = new TechniqueBuilder();
            builder.TryAddStep(false, SlashStroke(FarSlash), FarSlash);
            string json = TechniqueJsonSerializer.Serialize(builder);
            StringAssert.DoesNotContain("hidden", json);
        }

        private const string StepBody = "\"strokeType\": \"Thrust\", \"area\": 5000, \"rawPoints\": [{\"x\": 1, \"y\": 2}]";

        private static string Doc(int version, string stepFields, bool isCounter = false)
            => $"{{\"version\": {version}, \"steps\": [{{\"turnIndex\": 0, \"isCounter\": {(isCounter ? "true" : "false")}, {stepFields}{StepBody}}}]}}";

        [Test]
        public void Json_V2AndV1StepsLoadAsOrdinaryTurns()
        {
            (int v2, List<TechniqueStep> s2) = TechniqueJsonSerializer.Deserialize(Doc(2, ""));
            Assert.AreEqual(2, v2);
            Assert.IsFalse(s2[0].IsHidden);
            Assert.AreEqual(HiddenTechniqueMove.None, s2[0].HiddenMove);

            // Hidden fields in an older version aren't part of its schema and are ignored.
            (_, List<TechniqueStep> s2Extra) = TechniqueJsonSerializer.Deserialize(Doc(2, "\"hiddenTechnique\": \"triangle\", \"hiddenMove\": \"Thrust\", "));
            Assert.IsFalse(s2Extra[0].IsHidden);

            (int v1, List<TechniqueStep> s1) = TechniqueJsonSerializer.Deserialize(Doc(1, "\"stance\": \"Sit\", "));
            Assert.AreEqual(1, v1);
            Assert.IsFalse(s1[0].IsHidden);
        }

        [Test]
        public void Json_V3HiddenStepLoads()
        {
            (_, List<TechniqueStep> steps) = TechniqueJsonSerializer.Deserialize(Doc(3, "\"hiddenTechnique\": \"triangle\", \"hiddenMove\": \"VerticalSlash\", "));
            Assert.AreEqual(Samjae, steps[0].HiddenTechniqueId);
            Assert.AreEqual(HiddenTechniqueMove.VerticalSlash, steps[0].HiddenMove);
        }

        [TestCase("\"hiddenTechnique\": \"square\", \"hiddenMove\": \"Thrust\", ", false)]   // unknown id
        [TestCase("\"hiddenTechnique\": \"flower\", \"hiddenMove\": \"Thrust\", ", false)]   // inactive id
        [TestCase("\"hiddenTechnique\": \"\", \"hiddenMove\": \"Thrust\", ", false)]         // empty id
        [TestCase("\"hiddenTechnique\": \"triangle\", ", false)]                             // id without a move
        [TestCase("\"hiddenMove\": \"Thrust\", ", false)]                                    // move without an id
        [TestCase("\"hiddenTechnique\": \"triangle\", \"hiddenMove\": \"Spin\", ", false)]   // unknown move
        [TestCase("\"hiddenTechnique\": \"triangle\", \"hiddenMove\": 3, ", false)]          // move not a string
        [TestCase("\"hiddenTechnique\": \"triangle\", \"hiddenMove\": \"Thrust\", ", true)]  // hidden counter
        public void Json_InvalidHiddenCombinationsAreRejected(string fields, bool isCounter)
        {
            Assert.Throws<TechniqueJsonException>(() => TechniqueJsonSerializer.Deserialize(Doc(3, fields, isCounter)));
        }
    }
}

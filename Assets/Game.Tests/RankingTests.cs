using Game.Sim.Pvp;
using NUnit.Framework;

namespace Game.Tests
{
    public class RankingTests
    {
        // ---- CalculateScore ----

        [TestCase(3, 1, 2, 10)]   // 9 + 2 - 1
        [TestCase(1, 0, 0, 3)]
        [TestCase(0, 1, 0, -1)]
        [TestCase(0, 0, 1, 1)]
        [TestCase(10, 5, 3, 28)]  // 30 + 3 - 5
        [TestCase(20, 0, 0, 60)]
        [TestCase(0, 7, 2, -5)]   // 2 - 7
        public void CalculateScore_MatchesFormula(int wins, int losses, int draws, int expected)
        {
            Assert.AreEqual(expected, RankingRules.CalculateScore(new PvpRecord(wins, losses, draws)));
        }

        [Test]
        public void FreshRecord_IsZeroScoreAndBronze()
        {
            PvpRecord fresh = default;

            int score = RankingRules.CalculateScore(fresh);

            Assert.AreEqual(0, score);
            Assert.AreEqual(RankingTier.Bronze, RankingRules.GetTier(score));
        }

        // ---- GetTier ----

        [TestCase(-1)]
        [TestCase(-100)]
        [TestCase(int.MinValue)]
        public void GetTier_NegativeScore_IsBronze(int score)
        {
            Assert.AreEqual(RankingTier.Bronze, RankingRules.GetTier(score));
        }

        [Test]
        public void NegativeRecord_IsBronze()
        {
            int score = RankingRules.CalculateScore(new PvpRecord(0, 5, 0));

            Assert.Less(score, 0);
            Assert.AreEqual(RankingTier.Bronze, RankingRules.GetTier(score));
        }

        [TestCase(0, RankingTier.Bronze)]
        [TestCase(9, RankingTier.Bronze)]
        [TestCase(10, RankingTier.Silver)]
        [TestCase(29, RankingTier.Silver)]
        [TestCase(30, RankingTier.Gold)]
        [TestCase(59, RankingTier.Gold)]
        [TestCase(60, RankingTier.Platinum)]
        [TestCase(int.MaxValue, RankingTier.Platinum)]
        public void GetTier_Boundaries(int score, RankingTier expected)
        {
            Assert.AreEqual(expected, RankingRules.GetTier(score));
        }

        [Test]
        public void GetTier_ChangesExactlyAtNamedThresholds()
        {
            Assert.AreEqual(RankingTier.Bronze, RankingRules.GetTier(RankingRules.SilverMinScore - 1));
            Assert.AreEqual(RankingTier.Silver, RankingRules.GetTier(RankingRules.SilverMinScore));
            Assert.AreEqual(RankingTier.Silver, RankingRules.GetTier(RankingRules.GoldMinScore - 1));
            Assert.AreEqual(RankingTier.Gold, RankingRules.GetTier(RankingRules.GoldMinScore));
            Assert.AreEqual(RankingTier.Gold, RankingRules.GetTier(RankingRules.PlatinumMinScore - 1));
            Assert.AreEqual(RankingTier.Platinum, RankingRules.GetTier(RankingRules.PlatinumMinScore));
        }
    }
}

namespace Game.Sim.Pvp
{
    // Pure functions mapping a PvpRecord to a ranking score and tier.
    public static class RankingRules
    {
        public const int WinPoints = 3;
        public const int DrawPoints = 1;
        public const int LossPenalty = 1;

        // Minimum score (inclusive) for each tier. Anything below SilverMinScore,
        // including negative scores, is Bronze.
        public const int SilverMinScore = 10;
        public const int GoldMinScore = 30;
        public const int PlatinumMinScore = 60;

        // checked: a huge record overflowing into a wrong-signed score would silently mis-rank.
        public static int CalculateScore(PvpRecord record)
            => checked(record.Wins * WinPoints + record.Draws * DrawPoints - record.Losses * LossPenalty);

        public static RankingTier GetTier(int score)
        {
            if (score >= PlatinumMinScore) return RankingTier.Platinum;
            if (score >= GoldMinScore) return RankingTier.Gold;
            if (score >= SilverMinScore) return RankingTier.Silver;
            return RankingTier.Bronze;
        }
    }
}

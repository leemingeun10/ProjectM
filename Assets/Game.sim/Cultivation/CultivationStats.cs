namespace Game.Sim.Cultivation
{
    // Player's cultivation (단련) stats. default(CultivationStats) is a fresh save: both powers 0
    // and never trained (LastTrainingDate 0).
    //
    // What the two powers do in battle is not decided yet; other systems should read them from
    // here (and tiers from CultivationRules.GetTier) rather than duplicating the numbers.
    public readonly struct CultivationStats
    {
        // 외공: outer strength, trained by physical drills.
        public readonly int ExternalPower;
        // 내공: inner energy, trained by seated meditation.
        public readonly int InternalPower;
        // Calendar date of the last training as a YYYYMMDD integer (e.g. 20261019); 0 = never.
        // An int rather than DateTime keeps Game.sim free of the clock: the client works out
        // "today" and Game.sim only compares.
        public readonly int LastTrainingDate;

        public CultivationStats(int externalPower, int internalPower, int lastTrainingDate)
        {
            ExternalPower = externalPower;
            InternalPower = internalPower;
            LastTrainingDate = lastTrainingDate;
        }
    }
}

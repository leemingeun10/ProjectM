namespace Game.Sim.Progress
{
    // Player's PvE progress. default(ProgressData) is a fresh save: nothing cleared.
    public readonly struct ProgressData
    {
        // 1-based index of the furthest stage cleared; 0 = none.
        public readonly int HighestClearedStage;

        public ProgressData(int highestClearedStage)
        {
            HighestClearedStage = highestClearedStage;
        }
    }
}

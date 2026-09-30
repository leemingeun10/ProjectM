using System;

namespace Game.Sim.Progress
{
    // Pure functions over ProgressData. Stages unlock strictly in order.
    public static class ProgressRules
    {
        // Stage 1 is always open; otherwise the next stage after the highest clear is open.
        // Indices outside the catalog are never unlocked.
        public static bool IsStageUnlocked(ProgressData progress, int stageIndex)
        {
            if (!StageCatalog.IsValidStageIndex(stageIndex))
                return false;
            return stageIndex == 1 || stageIndex <= progress.HighestClearedStage + 1;
        }

        public static bool IsPvpUnlocked(ProgressData progress)
            => progress.HighestClearedStage >= StageCatalog.PvpUnlockStageIndex;

        // Returns the progress after clearing stageIndex. Replaying an earlier stage never lowers
        // the record. Clearing a stage that is not in the catalog or not yet unlocked is a caller
        // bug (the stage could not have been played), so it throws instead of being ignored.
        public static ProgressData RecordStageCleared(ProgressData current, int stageIndex)
        {
            if (!StageCatalog.IsValidStageIndex(stageIndex))
                throw new ArgumentOutOfRangeException(nameof(stageIndex), stageIndex, $"Stage index must be 1..{StageCatalog.StageCount}.");
            if (!IsStageUnlocked(current, stageIndex))
                throw new InvalidOperationException($"Stage {stageIndex} is locked (highest cleared: {current.HighestClearedStage}).");

            return stageIndex > current.HighestClearedStage ? new ProgressData(stageIndex) : current;
        }
    }
}

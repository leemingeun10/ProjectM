using Game.Sim.Progress;

namespace Game.Client.Stage
{
    // How the 수련 screen groups StageCatalog into chapters: every StagesPerChapter consecutive
    // stages form a chapter and the last of them is its boss fight. Display only: the catalog,
    // its order and the unlock rules stay in Game.sim, so a chapter opens exactly when its first
    // stage does (i.e. when the previous chapter's boss is cleared).
    public static class StageChapters
    {
        public const int StagesPerChapter = 5;

        public static int ChapterCount => (StageCatalog.StageCount + StagesPerChapter - 1) / StagesPerChapter;

        public static int ChapterOf(int stageIndex) => (stageIndex - 1) / StagesPerChapter + 1;

        public static bool IsBoss(int stageIndex) => stageIndex == LastStage(ChapterOf(stageIndex));

        public static int FirstStage(int chapter) => (chapter - 1) * StagesPerChapter + 1;

        // Clamped so a short final chapter still ends on the catalog's last stage.
        public static int LastStage(int chapter)
        {
            int last = chapter * StagesPerChapter;
            return last < StageCatalog.StageCount ? last : StageCatalog.StageCount;
        }

        public static bool IsUnlocked(ProgressData progress, int chapter)
            => ProgressRules.IsStageUnlocked(progress, FirstStage(chapter));

        // Stages of the chapter cleared so far (0..stage count of the chapter).
        public static int ClearedCount(ProgressData progress, int chapter)
        {
            int cleared = progress.HighestClearedStage - FirstStage(chapter) + 1;
            int total = LastStage(chapter) - FirstStage(chapter) + 1;
            return cleared < 0 ? 0 : cleared > total ? total : cleared;
        }
    }
}

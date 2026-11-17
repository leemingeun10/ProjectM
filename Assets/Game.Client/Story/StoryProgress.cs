using System;
using UnityEngine;

namespace Game.Client.Story
{
    // The side chosen at the end of part 1 (prototype FLAG: jeong = 정파, sa = 사파).
    public enum StoryFlag
    {
        None,
        Jeong,
        Sa,
    }

    public enum StoryStageState
    {
        Cleared,
        // The next stage to play.
        Current,
        Locked,
    }

    // Story progress, kept apart from the 수련 save (progress.json): Cleared is how many stages,
    // counted from the first, have been finished. default = fresh save.
    public readonly struct StoryProgress
    {
        public readonly int Cleared;
        public readonly StoryFlag Flag;

        public StoryProgress(int cleared, StoryFlag flag)
        {
            Cleared = cleared;
            Flag = flag;
        }
    }

    // The prototype's stateOf / finish rules. Stage indices are 0-based positions in StoryBook.stages.
    public static class StoryProgressRules
    {
        public static StoryStageState StateOf(StoryProgress progress, int stageIndex)
            => stageIndex < progress.Cleared ? StoryStageState.Cleared
                : stageIndex == progress.Cleared ? StoryStageState.Current
                : StoryStageState.Locked;

        // Cleared stages can be replayed; the current one can be played; later ones are locked.
        public static bool CanPlay(StoryProgress progress, int stageIndex, int stageCount)
            => stageIndex >= 0 && stageIndex < stageCount && stageIndex <= progress.Cleared;

        public static bool IsFirstClear(StoryProgress progress, int stageIndex) => stageIndex == progress.Cleared;

        // Finishing the current stage moves progress on by one; replaying a cleared stage changes
        // nothing. Finishing a stage that could not have been played is a caller bug.
        public static StoryProgress RecordFinished(StoryProgress progress, int stageIndex, int stageCount)
        {
            if (!CanPlay(progress, stageIndex, stageCount))
                throw new InvalidOperationException($"Story stage {stageIndex} cannot be finished (cleared: {progress.Cleared} of {stageCount}).");
            return IsFirstClear(progress, stageIndex) ? new StoryProgress(progress.Cleared + 1, progress.Flag) : progress;
        }

        public static StoryProgress WithFlag(StoryProgress progress, StoryFlag flag) => new StoryProgress(progress.Cleared, flag);

        // The chapter of the next stage to play (the last stage's chapter once all are cleared).
        public static int CurrentChapter(StoryBook book, StoryProgress progress)
            => book.stages[Math.Min(Math.Max(progress.Cleared, 0), book.StageCount - 1)].ch;
    }

    public class StoryProgressException : Exception
    {
        public StoryProgressException(string message) : base(message)
        {
        }
    }

    // storymap.json schema v1: { "version": 1, "cleared": 0, "flag": "" | "jeong" | "sa" }
    public static class StoryProgressJson
    {
        public const int CurrentVersion = 1;

        [Serializable]
        private class Dto
        {
            public int version;
            public int cleared;
            public string flag;
        }

        public static string Serialize(StoryProgress progress)
            => JsonUtility.ToJson(new Dto { version = CurrentVersion, cleared = progress.Cleared, flag = FlagToString(progress.Flag) }, true);

        // A value outside 0..stageCount or an unknown flag means a corrupt or tampered file.
        public static StoryProgress Deserialize(string json, int stageCount)
        {
            if (string.IsNullOrEmpty(json))
                throw new StoryProgressException("Story progress JSON is empty.");

            Dto dto;
            try
            {
                dto = JsonUtility.FromJson<Dto>(json);
            }
            catch (ArgumentException e)
            {
                throw new StoryProgressException("Invalid story progress JSON: " + e.Message);
            }

            if (dto == null)
                throw new StoryProgressException("Invalid story progress JSON: no root object.");
            if (dto.version != CurrentVersion)
                throw new StoryProgressException($"Unsupported story progress version {dto.version} (supported: {CurrentVersion}).");
            if (dto.cleared < 0 || dto.cleared > stageCount)
                throw new StoryProgressException($"cleared out of range: {dto.cleared} (expected 0..{stageCount}).");
            if (!TryParseFlag(dto.flag, out StoryFlag flag))
                throw new StoryProgressException($"Unknown flag \"{dto.flag}\".");
            return new StoryProgress(dto.cleared, flag);
        }

        public static string FlagToString(StoryFlag flag)
            => flag == StoryFlag.Jeong ? "jeong" : flag == StoryFlag.Sa ? "sa" : string.Empty;

        public static bool TryParseFlag(string value, out StoryFlag flag)
        {
            switch (value ?? string.Empty)
            {
                case "": flag = StoryFlag.None; return true;
                case "jeong": flag = StoryFlag.Jeong; return true;
                case "sa": flag = StoryFlag.Sa; return true;
                default: flag = StoryFlag.None; return false;
            }
        }
    }
}

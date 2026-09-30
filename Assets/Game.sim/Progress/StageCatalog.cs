using System;
using System.Collections.Generic;
using Game.Sim.Ai;

namespace Game.Sim.Progress
{
    // Fixed PvE stage list. Seeds are arbitrary constants: changing one changes that stage's
    // AI technique for every player, so treat them as content, not tuning.
    public static class StageCatalog
    {
        public const int StageCount = 15;

        // Clearing this stage unlocks async PvP.
        public const int PvpUnlockStageIndex = 10;

        private static readonly StageDefinition[] stages =
        {
            new StageDefinition(1, AiDifficulty.Easy, 0x99635F3E1FC4BE40UL),
            new StageDefinition(2, AiDifficulty.Easy, 0xD75DF7EE5C1FAA9FUL),
            new StageDefinition(3, AiDifficulty.Easy, 0xE408726B64551FDFUL),
            new StageDefinition(4, AiDifficulty.Easy, 0xAD000EAC13F329C0UL),
            new StageDefinition(5, AiDifficulty.Easy, 0x2FCB5832E466E705UL),
            new StageDefinition(6, AiDifficulty.Normal, 0xA1CC719DB7052664UL),
            new StageDefinition(7, AiDifficulty.Normal, 0x90913A1F31064483UL),
            new StageDefinition(8, AiDifficulty.Normal, 0x610F65E67678CC69UL),
            new StageDefinition(9, AiDifficulty.Normal, 0x572D2D7E36BF7FECUL),
            new StageDefinition(10, AiDifficulty.Normal, 0x098A4DF022E3F570UL),
            new StageDefinition(11, AiDifficulty.Hard, 0xBBB0D683C286C20EUL),
            new StageDefinition(12, AiDifficulty.Hard, 0xD2EDD018CBF9952FUL),
            new StageDefinition(13, AiDifficulty.Hard, 0x52135CB13CCC38B7UL),
            new StageDefinition(14, AiDifficulty.Hard, 0x941707DC8770F0B6UL),
            new StageDefinition(15, AiDifficulty.Hard, 0x302F0AE02661DDFEUL),
        };

        public static IReadOnlyList<StageDefinition> Stages => stages;

        public static bool IsValidStageIndex(int stageIndex) => stageIndex >= 1 && stageIndex <= StageCount;

        public static StageDefinition Get(int stageIndex)
        {
            if (!IsValidStageIndex(stageIndex))
                throw new ArgumentOutOfRangeException(nameof(stageIndex), stageIndex, $"Stage index must be 1..{StageCount}.");
            return stages[stageIndex - 1];
        }
    }
}

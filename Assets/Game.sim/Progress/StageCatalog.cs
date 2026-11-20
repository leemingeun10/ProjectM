using System;
using System.Collections.Generic;
using Game.Sim.Ai;

namespace Game.Sim.Progress
{
    // Fixed PvE stage list. Seeds are arbitrary constants: changing one changes that stage's
    // AI technique for every player, so treat them as content, not tuning.
    // The last column is the recommended combat power in permille (1000 = 1.0): tuning. It starts
    // at 1.0 and rises 5% a stage (rounded to the permille). The enemy's stats are derived from it
    // (CombatPowerRules.EnemyStats, StageDefinition.EnemyStats).
    public static class StageCatalog
    {
        public const int StageCount = 15;

        // Clearing this stage unlocks async PvP.
        public const int PvpUnlockStageIndex = 10;

        private static readonly StageDefinition[] stages =
        {
            new StageDefinition(1, AiDifficulty.Easy, 0x99635F3E1FC4BE40UL, 1000),
            new StageDefinition(2, AiDifficulty.Easy, 0xD75DF7EE5C1FAA9FUL, 1050),
            new StageDefinition(3, AiDifficulty.Easy, 0xE408726B64551FDFUL, 1103),
            new StageDefinition(4, AiDifficulty.Easy, 0xAD000EAC13F329C0UL, 1158),
            new StageDefinition(5, AiDifficulty.Easy, 0x2FCB5832E466E705UL, 1216),
            new StageDefinition(6, AiDifficulty.Normal, 0xA1CC719DB7052664UL, 1276),
            new StageDefinition(7, AiDifficulty.Normal, 0x90913A1F31064483UL, 1340),
            new StageDefinition(8, AiDifficulty.Normal, 0x610F65E67678CC69UL, 1407),
            new StageDefinition(9, AiDifficulty.Normal, 0x572D2D7E36BF7FECUL, 1477),
            new StageDefinition(10, AiDifficulty.Normal, 0x098A4DF022E3F570UL, 1551),
            new StageDefinition(11, AiDifficulty.Hard, 0xBBB0D683C286C20EUL, 1629),
            new StageDefinition(12, AiDifficulty.Hard, 0xD2EDD018CBF9952FUL, 1710),
            new StageDefinition(13, AiDifficulty.Hard, 0x52135CB13CCC38B7UL, 1796),
            new StageDefinition(14, AiDifficulty.Hard, 0x941707DC8770F0B6UL, 1886),
            new StageDefinition(15, AiDifficulty.Hard, 0x302F0AE02661DDFEUL, 1980),
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

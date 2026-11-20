using Game.Sim.Ai;
using Game.Sim.Battle;

namespace Game.Sim.Progress
{
    // One PvE stage. StageIndex is 1-based. The AI opponent is AiTechniqueGenerator.Generate(
    // Difficulty, Seed), so a stage always fields the exact same technique.
    // RecommendedPowerPermille is the combat power (1000 = 1.0) the stage is tuned for; the
    // enemy's own stats follow from it (CombatPowerRules.EnemyStats).
    public readonly struct StageDefinition
    {
        public readonly int StageIndex;
        public readonly AiDifficulty Difficulty;
        public readonly ulong Seed;
        public readonly int RecommendedPowerPermille;

        public StageDefinition(int stageIndex, AiDifficulty difficulty, ulong seed, int recommendedPowerPermille)
        {
            StageIndex = stageIndex;
            Difficulty = difficulty;
            Seed = seed;
            RecommendedPowerPermille = recommendedPowerPermille;
        }

        // 내공 / 외공 the stage's enemy fights with.
        public CombatStats EnemyStats => CombatPowerRules.EnemyStats(RecommendedPowerPermille);
    }
}

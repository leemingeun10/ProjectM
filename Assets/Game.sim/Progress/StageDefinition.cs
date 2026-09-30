using Game.Sim.Ai;

namespace Game.Sim.Progress
{
    // One PvE stage. StageIndex is 1-based. The AI opponent is AiTechniqueGenerator.Generate(
    // Difficulty, Seed), so a stage always fields the exact same technique.
    public readonly struct StageDefinition
    {
        public readonly int StageIndex;
        public readonly AiDifficulty Difficulty;
        public readonly ulong Seed;

        public StageDefinition(int stageIndex, AiDifficulty difficulty, ulong seed)
        {
            StageIndex = stageIndex;
            Difficulty = difficulty;
            Seed = seed;
        }
    }
}

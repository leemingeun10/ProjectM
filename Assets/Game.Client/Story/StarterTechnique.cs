using System.Collections.Generic;
using Game.Sim.Ai;
using Game.Sim.Technique;

namespace Game.Client.Story
{
    // The technique the protagonist fights 1-1 with when the player has no complete slot yet
    // ("the body remembers"). Built by the AI generator from a fixed seed, so it is the same for
    // everyone and is never written to a slot. The seed was picked so that it beats the 1-1
    // opponent (StageCatalog stage 1); StoryMappingTests keeps that true.
    public static class StarterTechnique
    {
        public const AiDifficulty Difficulty = AiDifficulty.Easy;
        // Re-picked when the AI generator moved into the player's frame (0x5EED000B lost 64:80
        // to the regenerated stage 1); this one wins 64:43.
        public const ulong Seed = 0x5EED0000UL;

        public static List<TechniqueStep> Create() => AiTechniqueGenerator.Generate(Difficulty, Seed);
    }
}

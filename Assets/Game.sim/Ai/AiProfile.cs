using System;

namespace Game.Sim.Ai
{
    // Tuning knobs for one AI difficulty. Lengths/radii are whole canvas units (CanvasScaler
    // 1080x1920 reference, origin at the canvas centre). Values are first-pass defaults and
    // should be retuned from playtest data together with the damage constants.
    public readonly struct AiProfile
    {
        // Chance (percent) that a non-counter turn is a Thrust loop instead of a Slash.
        public readonly int ThrustPercent;
        public readonly int SlashLengthMin;
        public readonly int SlashLengthMax;      // exclusive
        public readonly int ThrustRadiusMin;
        public readonly int ThrustRadiusMax;     // exclusive
        public readonly int CounterCount;        // 0..TechniqueBuilder.MaxCounters

        public AiProfile(int thrustPercent, int slashLengthMin, int slashLengthMax,
            int thrustRadiusMin, int thrustRadiusMax, int counterCount)
        {
            ThrustPercent = thrustPercent;
            SlashLengthMin = slashLengthMin;
            SlashLengthMax = slashLengthMax;
            ThrustRadiusMin = thrustRadiusMin;
            ThrustRadiusMax = thrustRadiusMax;
            CounterCount = counterCount;
        }

        // Harder AIs draw bigger thrust loops (more damage, but larger targets to intercept),
        // longer slashes (more likely to parry / intercept) and spend their counters.
        public static AiProfile For(AiDifficulty difficulty)
        {
            switch (difficulty)
            {
                case AiDifficulty.Easy: return new AiProfile(20, 150, 300, 50, 80, 0);
                case AiDifficulty.Normal: return new AiProfile(30, 250, 450, 70, 110, 1);
                case AiDifficulty.Hard: return new AiProfile(40, 350, 600, 90, 140, 2);
                default: throw new ArgumentOutOfRangeException(nameof(difficulty), difficulty, null);
            }
        }
    }
}

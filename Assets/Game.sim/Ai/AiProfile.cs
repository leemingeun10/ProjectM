using System;

namespace Game.Sim.Ai
{
    // Tuning knobs for one AI difficulty. Stroke sizes are a share (percent) of the stroke length
    // limit every character draws under (StrokeUnits.MaxStrokeLength*, itself a ratio of the
    // drawing frame's width), so they follow the frame if it is resized. The percentages are
    // provisional and should be retuned from playtest data together with the damage constants.
    public readonly struct AiProfile
    {
        // Chance (percent) that a non-counter turn is a Thrust loop instead of a Slash.
        public readonly int ThrustPercent;
        // Path length of every stroke, Slash or Thrust loop alike, as a percent of the limit.
        public readonly int LengthMinPercent;
        public readonly int LengthMaxPercent;
        public readonly int CounterCount;        // 0..TechniqueBuilder.MaxCounters

        public AiProfile(int thrustPercent, int lengthMinPercent, int lengthMaxPercent, int counterCount)
        {
            ThrustPercent = thrustPercent;
            LengthMinPercent = lengthMinPercent;
            LengthMaxPercent = lengthMaxPercent;
            CounterCount = counterCount;
        }

        // Harder AIs draw longer strokes (bigger thrust loops deal more damage, longer slashes are
        // more likely to parry / intercept) and spend their counters.
        public static AiProfile For(AiDifficulty difficulty)
        {
            switch (difficulty)
            {
                case AiDifficulty.Easy: return new AiProfile(20, 30, 50, 0);
                case AiDifficulty.Normal: return new AiProfile(30, 50, 75, 1);
                case AiDifficulty.Hard: return new AiProfile(40, 75, 100, 2);
                default: throw new ArgumentOutOfRangeException(nameof(difficulty), difficulty, null);
            }
        }
    }
}

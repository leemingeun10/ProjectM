namespace Game.Sim.Stroke
{
    // A single sample of a drawn stroke, in fixed-point simulation space.
    // Game.Client is responsible for converting its float/pixel coordinates into this type
    // at the input/simulation boundary (see CLAUDE.md architecture rules).
    public readonly struct StrokePoint
    {
        public readonly Fixed X;
        public readonly Fixed Y;

        public StrokePoint(Fixed x, Fixed y)
        {
            X = x;
            Y = y;
        }

        public StrokePoint(long rawX, long rawY) : this(Fixed.FromRaw(rawX), Fixed.FromRaw(rawY))
        {
        }

        public static StrokePoint FromInt(long x, long y) => new StrokePoint(Fixed.FromInt(x), Fixed.FromInt(y));
    }
}

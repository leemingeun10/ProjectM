namespace Game.Sim.Stroke
{
    // A single sample of a stroke, in fixed-point simulation space. The type carries no unit:
    // a drawn or saved player stroke is in frame centimetres (Game.Client converts its float
    // centimetres into this type at the input/simulation boundary, see CLAUDE.md), while AI
    // strokes and every stroke inside a battle are in canvas units. StrokeUnits converts.
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

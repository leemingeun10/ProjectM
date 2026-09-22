namespace Game.Sim.Stroke
{
    // Slash: Length + StartDirection/EndDirection are populated, Area is zero.
    // Thrust: Area is populated, the rest are zero.
    // Direction is returned as a unit vector (not an angle) because computing atan2
    // deterministically in fixed-point is not worth the complexity here; Game.Client
    // (which is free to use float) converts it to a display angle at the boundary.
    public readonly struct StrokeResult
    {
        public readonly StrokeType Type;
        public readonly Fixed Length;
        public readonly Fixed StartDirectionX;
        public readonly Fixed StartDirectionY;
        public readonly Fixed EndDirectionX;
        public readonly Fixed EndDirectionY;
        public readonly Fixed Area;

        private StrokeResult(StrokeType type, Fixed length, Fixed startDirX, Fixed startDirY, Fixed endDirX, Fixed endDirY, Fixed area)
        {
            Type = type;
            Length = length;
            StartDirectionX = startDirX;
            StartDirectionY = startDirY;
            EndDirectionX = endDirX;
            EndDirectionY = endDirY;
            Area = area;
        }

        public static StrokeResult Slash(Fixed length, Fixed startDirX, Fixed startDirY, Fixed endDirX, Fixed endDirY)
            => new StrokeResult(StrokeType.Slash, length, startDirX, startDirY, endDirX, endDirY, Fixed.Zero);

        public static StrokeResult Thrust(Fixed area)
            => new StrokeResult(StrokeType.Thrust, Fixed.Zero, Fixed.Zero, Fixed.Zero, Fixed.Zero, Fixed.Zero, area);
    }
}

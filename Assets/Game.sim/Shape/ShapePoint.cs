namespace Game.Sim.Shape
{
    // One point of a shape, in thousandths (X = 1000 is 1.000 of whatever unit the caller uses;
    // the client passes centimetres from the drawing-frame centre, like StrokePoint.Raw).
    public readonly struct ShapePoint
    {
        public readonly int X;
        public readonly int Y;

        public ShapePoint(int x, int y)
        {
            X = x;
            Y = y;
        }
    }
}

namespace Game.Sim.Shape
{
    // A shape to recognise. NormalizedPoints must already be in ShapeRecognizer's normalized form
    // (ShapeRecognizer.PointCount points, Resample then Normalize), so matching never redoes it.
    public readonly struct ShapeTemplate
    {
        public readonly string Id;
        public readonly ShapePoint[] NormalizedPoints;

        public ShapeTemplate(string id, ShapePoint[] normalizedPoints)
        {
            Id = id;
            NormalizedPoints = normalizedPoints;
        }
    }
}

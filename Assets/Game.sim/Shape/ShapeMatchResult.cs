namespace Game.Sim.Shape
{
    // The template a drawn shape matched best and how closely: 10000 is a perfect match, 0 none.
    public readonly struct ShapeMatchResult
    {
        public readonly string TemplateId;
        public readonly int Score;

        public ShapeMatchResult(string templateId, int score)
        {
            TemplateId = templateId;
            Score = score;
        }
    }
}

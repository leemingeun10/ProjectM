namespace Game.Sim.Technique
{
    // The move a hidden-technique turn is saved with. It decides how the turn fights: both cuts
    // fight as a Slash, Thrust as a Thrust, whatever shape was drawn. None = not a hidden turn.
    // The two cuts fight identically; they differ only in how the replay shows them.
    public enum HiddenTechniqueMove
    {
        None,
        HorizontalSlash,
        VerticalSlash,
        Thrust
    }
}

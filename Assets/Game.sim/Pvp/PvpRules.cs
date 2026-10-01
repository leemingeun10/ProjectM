namespace Game.Sim.Pvp
{
    // Pure functions over PvpRecord. Each returns a new record with exactly one field bumped.
    // checked: a counter wrapping to a negative value would produce a save the serializer rejects.
    public static class PvpRules
    {
        public static PvpRecord RecordWin(PvpRecord current)
            => new PvpRecord(checked(current.Wins + 1), current.Losses, current.Draws);

        public static PvpRecord RecordLoss(PvpRecord current)
            => new PvpRecord(current.Wins, checked(current.Losses + 1), current.Draws);

        public static PvpRecord RecordDraw(PvpRecord current)
            => new PvpRecord(current.Wins, current.Losses, checked(current.Draws + 1));
    }
}

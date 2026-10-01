namespace Game.Sim.Pvp
{
    // Player's PvP win/loss/draw tally. default(PvpRecord) is a fresh save: 0/0/0.
    public readonly struct PvpRecord
    {
        public readonly int Wins;
        public readonly int Losses;
        public readonly int Draws;

        public PvpRecord(int wins, int losses, int draws)
        {
            Wins = wins;
            Losses = losses;
            Draws = draws;
        }
    }
}

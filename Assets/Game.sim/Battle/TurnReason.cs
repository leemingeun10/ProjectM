namespace Game.Sim.Battle
{
    // Why a turn resolved the way it did. Combined with the two step types this fully
    // identifies which rule of the turn table was applied.
    public enum TurnReason
    {
        CrossContact,  // the contact check passed (parry / counter success / interception)
        Miss,          // the contact check failed
        Unconditional  // the matchup has no contact check (Thrust-Thrust, Thrust-Counter, Counter-Counter)
    }
}

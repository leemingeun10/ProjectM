using System.Collections.Generic;
using Game.Sim.Battle;
using Game.Sim.Pvp;
using Game.Sim.Technique;

namespace Game.Client.Pvp
{
    // Boundary between the PvP screen and wherever opponents and records come from. Today that
    // is LocalFakePvpBackend; a real server-backed implementation replaces it without touching
    // the UI. Outcomes are always from the local player's side (player = A).
    public interface IPvpBackend
    {
        // A full TurnsPerBattle-step technique belonging to some other player.
        IReadOnlyList<TechniqueStep> FetchRandomOpponent();

        // Applies one finished battle to the player's record and persists it.
        void ReportResult(BattleOutcome outcome);

        PvpRecord GetRecord();
    }
}

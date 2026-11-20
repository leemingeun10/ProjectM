using System.Collections.Generic;
using Game.Sim.Battle;
using Game.Sim.Cultivation;
using Game.Sim.Progress;
using Game.Sim.Technique;

namespace Game.Client.Stage
{
    // Which stats each kind of battle is fought with. The player is always side A.
    //   수련 / 스토리: the player's trained stats against the stage's enemy stats.
    //   비무: 100 / 100 on both sides, so only the techniques decide it.
    public static class BattleMatchups
    {
        public static CombatStats PlayerStats(CultivationStats cultivation) => CombatPowerRules.FromCultivation(cultivation);

        public static BattleResult Training(IReadOnlyList<TechniqueStep> playerSteps, IReadOnlyList<TechniqueStep> aiSteps,
            StageDefinition stage, CultivationStats cultivation)
            => BattleSimulator.Simulate(playerSteps, aiSteps, PlayerStats(cultivation), stage.EnemyStats);

        // enemyStats: StoryStage.EnemyStats. playerSteps may be the starter technique (1-1): it
        // fights with the player's trained stats like any other.
        public static BattleResult Story(IReadOnlyList<TechniqueStep> playerSteps, IReadOnlyList<TechniqueStep> aiSteps,
            CombatStats enemyStats, CultivationStats cultivation)
            => BattleSimulator.Simulate(playerSteps, aiSteps, PlayerStats(cultivation), enemyStats);

        public static BattleResult Sparring(IReadOnlyList<TechniqueStep> playerSteps, IReadOnlyList<TechniqueStep> opponentSteps)
            => BattleSimulator.Simulate(playerSteps, opponentSteps, CombatStats.Default, CombatStats.Default);
    }
}

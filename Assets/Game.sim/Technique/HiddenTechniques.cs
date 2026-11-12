using System;
using System.Collections.Generic;
using Game.Sim.Shape;

namespace Game.Sim.Technique
{
    // One hidden technique (히든 무공): the shape it is drawn as and the 내공 a turn of it costs.
    // Its damage bonus follows from the cost: a turn deals (100 + cost)% of the move's base damage.
    public readonly struct HiddenTechniqueDefinition
    {
        // ShapeTemplate.Id the technique is drawn as.
        public readonly string Id;
        public readonly int InternalPowerCost;
        // Inactive techniques keep their shapes and entries but are neither recognised nor
        // accepted in a sequence.
        public readonly bool IsActive;

        public HiddenTechniqueDefinition(string id, int internalPowerCost, bool isActive)
        {
            Id = id;
            InternalPowerCost = internalPowerCost;
            IsActive = isActive;
        }

        public int DamagePercent => HiddenTechniques.DamagePercentBase + InternalPowerCost;
    }

    // The hidden-technique table and the rules every hidden turn follows: which ids may be used,
    // what a sequence's turns cost from its 내공 budget, and how their damage is scaled.
    public static class HiddenTechniques
    {
        // 내공 one 삼재검법 turn costs. The only number to change to retune it: the damage bonus
        // (DamagePercent) is derived from it.
        public const int SamjaeCost = 5;
        // 매화/제왕/월영 are not in the game yet; their cost is a placeholder until they are.
        public const int InactivePlaceholderCost = 5;

        public const int DamagePercentBase = 100;

        public static readonly IReadOnlyList<HiddenTechniqueDefinition> All = new[]
        {
            new HiddenTechniqueDefinition(ShapeTemplates.TriangleId, SamjaeCost, true),                // 삼재검법
            new HiddenTechniqueDefinition(ShapeTemplates.FlowerId, InactivePlaceholderCost, false),    // 매화검법
            new HiddenTechniqueDefinition(ShapeTemplates.SwordId, InactivePlaceholderCost, false),     // 제왕검법
            new HiddenTechniqueDefinition(ShapeTemplates.CrescentId, InactivePlaceholderCost, false),  // 월영검법
        };

        // False for unknown and inactive ids alike: neither may be used in a sequence.
        public static bool TryGetActive(string id, out HiddenTechniqueDefinition definition)
        {
            foreach (HiddenTechniqueDefinition d in All)
            {
                if (d.Id == id && d.IsActive)
                {
                    definition = d;
                    return true;
                }
            }
            definition = default;
            return false;
        }

        // Whether a turn's hidden-technique fields form a valid combination: both empty (an
        // ordinary turn), or an active id with a chosen move on a non-counter turn. Budget is
        // checked separately (TechniqueBuilder), since it depends on the rest of the sequence.
        public static TechniqueAddResult Check(bool isCounter, string id, HiddenTechniqueMove move)
        {
            if (id == null && move == HiddenTechniqueMove.None)
                return TechniqueAddResult.Success;
            if (id == null || move == HiddenTechniqueMove.None)
                return TechniqueAddResult.HiddenTechniqueMoveMismatch;
            if (!TryGetActive(id, out _))
                return TechniqueAddResult.UnknownHiddenTechnique;
            if (isCounter)
                return TechniqueAddResult.HiddenTechniqueOnCounter;
            return TechniqueAddResult.Success;
        }

        public static TechniqueAddResult Check(TechniqueStep step) => Check(step.IsCounter, step.HiddenTechniqueId, step.HiddenMove);

        // 0 for an ordinary turn. Callers only see valid steps (Check), so an unknown id never gets here.
        public static int CostOf(TechniqueStep step)
            => step.IsHidden && TryGetActive(step.HiddenTechniqueId, out HiddenTechniqueDefinition d) ? d.InternalPowerCost : 0;

        // 내공 a sequence's hidden turns use, worked out from the steps themselves so removing a
        // turn gives its cost back.
        public static int TotalCost(IReadOnlyList<TechniqueStep> steps)
        {
            if (steps == null) throw new ArgumentNullException(nameof(steps));
            int total = 0;
            foreach (TechniqueStep step in steps)
                total += CostOf(step);
            return total;
        }

        // Damage a turn deals after its hidden-technique bonus: baseDamage * DamagePercent / 100,
        // rounded half up (10 * 105% = 10.5 -> 11, 9 * 105% = 9.45 -> 9). Ordinary turns and
        // zero damage are unchanged, so the bonus only applies when damage actually lands.
        public static int ScaleDamage(int baseDamage, TechniqueStep attacker)
        {
            if (baseDamage <= 0 || !attacker.IsHidden || !TryGetActive(attacker.HiddenTechniqueId, out HiddenTechniqueDefinition d))
                return baseDamage;
            return ScaleDamage(baseDamage, d.DamagePercent);
        }

        public static int ScaleDamage(int baseDamage, int damagePercent)
        {
            long scaled = ((long)baseDamage * damagePercent + DamagePercentBase / 2) / DamagePercentBase;
            return scaled > int.MaxValue ? int.MaxValue : (int)scaled;
        }
    }
}

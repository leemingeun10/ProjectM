namespace Game.Sim.Technique
{
    public enum TechniqueAddResult
    {
        Success,
        SequenceFull,
        CounterLimitReached,
        // Hidden-technique turns (see HiddenTechniques.Check). Any of these means the shape can
        // still be saved as an ordinary turn instead.
        UnknownHiddenTechnique,
        HiddenTechniqueMoveMismatch,
        HiddenTechniqueOnCounter,
        NotEnoughInternalPower
    }
}

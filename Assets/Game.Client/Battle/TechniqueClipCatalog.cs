namespace Game.Client.Battle
{
    // A technique's Animator state with its clip length and main strike, both in seconds of clip time.
    // The strike is when the main blow lands (the last or the strongest one): the replay shows the
    // impact flash and damage there.
    public readonly struct TechniqueClip
    {
        public readonly string State;
        public readonly float Length;
        public readonly float Strike;

        public TechniqueClip(string state, float length, float strike)
        {
            State = state;
            Length = length;
            Strike = strike;
        }
    }

    // Technique id + stage -> long technique clip, for techniques whose turns play their own clip
    // and stretch the turn (ReplayTurnTiming). Empty for now: 삼재검법 plays the ordinary combat
    // clips on the normal turn timing, and the first cuts of the four technique clips are parked in
    // CharacterArt/Combat/_Unused until they are remade. A new entry needs its Animator state (named
    // like its clip), length and main strike in clip seconds, and must keep a technique turn
    // (clip + tail) within MaxTurnSeconds even with techniques on both sides.
    public static class TechniqueClipCatalog
    {
        public const float MaxTurnSeconds = 3f;

        public static bool TryGet(string techniqueId, int stage, out TechniqueClip clip)
        {
            clip = default;
            return false;
        }

        public static TechniqueClip? For(TechniqueCast? cast)
            => cast.HasValue && TryGet(cast.Value.TechniqueId, cast.Value.Stage, out TechniqueClip clip) ? clip : (TechniqueClip?)null;
    }
}

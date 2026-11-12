using Game.Sim.Shape;

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

    // Technique id + stage -> clip. Lengths and strikes are measured from the cut clips in
    // CharacterArt/Combat (an EditMode test checks the lengths against the assets); recutting a clip
    // means updating its entry here.
    public static class TechniqueClipCatalog
    {
        public const string SamjaeStage1State = "Skill_Samjae1";
        public const string MaehwaStage1State = "Skill_Maehwa1";
        public const string JewangStage1State = "Skill_Jewang1";
        public const string WolyeongStage1State = "Skill_Wolyeong1";

        // Stage 2 clips go in a matching branch per technique when they exist.
        public static bool TryGet(string techniqueId, int stage, out TechniqueClip clip)
        {
            clip = default;
            if (stage != 1)
                return false;

            switch (techniqueId)
            {
                // Downward cut (the first and fastest of its three strikes).
                case ShapeTemplates.TriangleId: clip = new TechniqueClip(SamjaeStage1State, 2.2f, 0.617f); return true;
                // First, longest lunge of the thrust flurry.
                case ShapeTemplates.FlowerId: clip = new TechniqueClip(MaehwaStage1State, 2.05f, 0.4f); return true;
                // The single downward strike after the held raise.
                case ShapeTemplates.SwordId: clip = new TechniqueClip(JewangStage1State, 2.2f, 1.6f); return true;
                // Closing low slash.
                case ShapeTemplates.CrescentId: clip = new TechniqueClip(WolyeongStage1State, 2.15f, 1.833f); return true;
                default: return false;
            }
        }

        public static TechniqueClip? For(TechniqueCast? cast)
            => cast.HasValue && TryGet(cast.Value.TechniqueId, cast.Value.Stage, out TechniqueClip clip) ? clip : (TechniqueClip?)null;
    }
}

using UnityEngine;

namespace Game.Client.Scenery
{
    // The 3D set behind the fighters, shared by 무공 제작, 수련 and 비무. Each 수련 chapter fights
    // in its own place; the workshop and 비무 use the 연무장.
    public enum ArenaTheme
    {
        Street = 1,       // 1장: a market street (저잣거리)
        TrainingHall = 2, // 2장: a paved 연무장 in front of a hall
        MountHua = 3,     // 3장: a granite cliff-top training ground of the 화산파 Taoists
    }

    public static class ArenaThemes
    {
        public const ArenaTheme Workshop = ArenaTheme.TrainingHall;
        public const ArenaTheme Sparring = ArenaTheme.TrainingHall;

        // Chapters past the third keep the last theme.
        public static ArenaTheme ForChapter(int chapter)
            => chapter <= 1 ? ArenaTheme.Street : chapter == 2 ? ArenaTheme.TrainingHall : ArenaTheme.MountHua;
    }

    // Sky, fog and painted backdrop that go with a set. A null Backdrop keeps the backdrop
    // image assigned on InkWashBackground.
    public struct ArenaLook
    {
        public Color SkyTop;
        public Color SkyHorizon;
        public Color Fog;
        public float FogStart;
        public float FogEnd;
        public Texture2D Backdrop;
    }
}

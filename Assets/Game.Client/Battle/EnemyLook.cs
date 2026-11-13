namespace Game.Client.Battle
{
    // Which model the opponent (CharacterB) wears. Default is the model built into the scene;
    // the others are prefabs mapped on CharacterModelSwitcher.
    public enum EnemyLook
    {
        Default = 0,
        Ronin = 1,          // 낭인 (Ronin_V2_Fist.prefab; the old Ronin_Fist.prefab is no longer used)
        BlackSwordsman = 2, // 흑의검객 (BlackSwordsman_Fist.prefab)
    }

    // The one place that decides who the player faces. The 무공 제작 screen (and every other
    // non-battle screen, which share the same set) shows Workshop; 비무 keeps the scene's model.
    public static class EnemyLooks
    {
        public const EnemyLook Workshop = EnemyLook.Ronin;
        public const EnemyLook Sparring = EnemyLook.Default;

        // Index 0 = 1장. Chapters past the end keep the last entry; add one line per new chapter.
        private static readonly EnemyLook[] ByChapter =
        {
            EnemyLook.Ronin,          // 1장
            EnemyLook.BlackSwordsman, // 2장
        };

        public static int ChaptersDefined => ByChapter.Length;

        public static EnemyLook ForChapter(int chapter)
        {
            int index = chapter < 1 ? 0 : chapter > ByChapter.Length ? ByChapter.Length - 1 : chapter - 1;
            return ByChapter[index];
        }
    }
}

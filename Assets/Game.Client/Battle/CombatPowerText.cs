using Game.Sim.Battle;

namespace Game.Client.Battle
{
    // How combat power (전투력) is written on screen: permille shown to two decimals, rounded half
    // up, with a trailing zero dropped down to one decimal (1000 -> "1.0", 1210 -> "1.21",
    // 563 -> "0.56").
    public static class CombatPowerText
    {
        public static string Format(int powerPermille)
        {
            int hundredths = (powerPermille + 5) / 10;
            int whole = hundredths / 100;
            int fraction = hundredths % 100;
            return fraction % 10 == 0 ? $"{whole}.{fraction / 10}" : $"{whole}.{fraction:00}";
        }

        public static string Format(CombatStats stats) => Format(CombatPowerRules.PowerPermille(stats));
    }
}

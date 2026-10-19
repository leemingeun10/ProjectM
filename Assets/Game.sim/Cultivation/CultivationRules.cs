using System;

namespace Game.Sim.Cultivation
{
    // Pure functions over CultivationStats: one training a day, picking either power.
    public static class CultivationRules
    {
        // Points gained per training.
        public const int DailyGain = 10;

        // Minimum value (inclusive) for tiers 2 and 3; anything below Tier2Threshold is tier 1.
        // With DailyGain 10 that is 5 trainings of one power for tier 2 and 15 for tier 3.
        public const int Tier2Threshold = 50;
        public const int Tier3Threshold = 150;

        public const int MinTier = 1;
        public const int MaxTier = 3;

        // Returns stats with ExternalPower raised by DailyGain and the training date set to today.
        // checked: a power wrapping to a negative value would produce a save the serializer rejects.
        public static CultivationStats AddExternalPower(CultivationStats current, int today)
        {
            RequireValidDate(today);
            return new CultivationStats(checked(current.ExternalPower + DailyGain), current.InternalPower, today);
        }

        public static CultivationStats AddInternalPower(CultivationStats current, int today)
        {
            RequireValidDate(today);
            return new CultivationStats(current.ExternalPower, checked(current.InternalPower + DailyGain), today);
        }

        public static int GetTier(int value)
        {
            if (value >= Tier3Threshold) return 3;
            if (value >= Tier2Threshold) return 2;
            return 1;
        }

        // Only compares; working out today's date is the client's job.
        public static bool HasTrainedToday(CultivationStats current, int today)
            => current.LastTrainingDate == today;

        // True for a real calendar date written as YYYYMMDD with a four-digit year
        // (10000101..99991231). 0 ("never trained") is not a date.
        public static bool IsValidDate(int yyyymmdd)
        {
            if (yyyymmdd < 10000101 || yyyymmdd > 99991231)
                return false;
            int year = yyyymmdd / 10000;
            int month = yyyymmdd / 100 % 100;
            int day = yyyymmdd % 100;
            return month >= 1 && month <= 12 && day >= 1 && day <= DaysInMonth(year, month);
        }

        private static int DaysInMonth(int year, int month)
        {
            switch (month)
            {
                case 2:
                    bool leap = (year % 4 == 0 && year % 100 != 0) || year % 400 == 0;
                    return leap ? 29 : 28;
                case 4:
                case 6:
                case 9:
                case 11:
                    return 30;
                default:
                    return 31;
            }
        }

        // A malformed date would be saved and then rejected on the next load, losing the save.
        private static void RequireValidDate(int today)
        {
            if (!IsValidDate(today))
                throw new ArgumentOutOfRangeException(nameof(today), today, "today must be a calendar date written as YYYYMMDD.");
        }
    }
}

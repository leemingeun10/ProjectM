using System;

namespace Game.Client.Battle
{
    // When things happen within one replayed turn, in seconds at 1x (the replay divides by its speed).
    // Presentation only: the turn's outcome is already decided; this just sets how long it is shown.
    public readonly struct ReplayTurnTiming
    {
        // A normal turn: long enough to read the turn's one-line explanation at 1x.
        public const float NormalInterval = 1.6f;
        // Fraction of a normal turn at which the blades are treated as meeting; the combat clips
        // are cut so their strike lands here (0.48 s).
        public const float NormalImpactFraction = 0.3f;
        public const float NormalImpact = NormalInterval * NormalImpactFraction;
        // Fraction of a normal turn after which the camera eases back out of its turn focus.
        public const float NormalFocusEndFraction = 0.65f;
        // A technique turn runs this long past the end of its clip.
        public const float TechniqueTail = 0.4f;
        // A technique turn keeps the camera focused at least this long past the impact.
        public const float FocusAfterImpact = 0.3f;

        public readonly float Interval;
        // Impact flash, hit reactions and the start of the HP change.
        public readonly float Impact;
        public readonly float FocusEnd;
        // Each side's clip starts this long into the turn, so its strike lands on Impact.
        public readonly float StartDelayA;
        public readonly float StartDelayB;
        public readonly bool IsTechniqueTurn;

        private ReplayTurnTiming(float interval, float impact, float focusEnd, float startDelayA, float startDelayB, bool isTechniqueTurn)
        {
            Interval = interval;
            Impact = impact;
            FocusEnd = focusEnd;
            StartDelayA = startDelayA;
            StartDelayB = startDelayB;
            IsTechniqueTurn = isTechniqueTurn;
        }

        public static readonly ReplayTurnTiming Normal = new ReplayTurnTiming(
            NormalInterval, NormalImpact, NormalInterval * NormalFocusEndFraction, 0f, 0f, false);

        // Without a technique on either side this is exactly Normal. Otherwise both sides' strikes
        // (a technique's main strike, or 0.48 s for a normal clip) are lined up on the latest of them:
        // each clip starts late by the difference. The turn lasts until the last technique clip ends,
        // plus TechniqueTail.
        public static ReplayTurnTiming For(TechniqueClip? clipA, TechniqueClip? clipB)
        {
            if (!clipA.HasValue && !clipB.HasValue)
                return Normal;

            float strikeA = clipA?.Strike ?? NormalImpact;
            float strikeB = clipB?.Strike ?? NormalImpact;
            float impact = Math.Max(strikeA, strikeB);
            float delayA = impact - strikeA;
            float delayB = impact - strikeB;
            float end = Math.Max(clipA.HasValue ? delayA + clipA.Value.Length : 0f,
                                 clipB.HasValue ? delayB + clipB.Value.Length : 0f);
            float interval = end + TechniqueTail;
            float focusEnd = Math.Max(interval * NormalFocusEndFraction, impact + FocusAfterImpact);
            return new ReplayTurnTiming(interval, impact, focusEnd, delayA, delayB, true);
        }
    }
}

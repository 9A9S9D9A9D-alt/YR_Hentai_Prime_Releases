using System;

namespace YR_Hentai_Prime_AnimationBed
{
    // Reconstructed balance values, not recovered original source.
    public static class TreeRules
    {
        public const int PulseCost = 10;
        public const int PulseCooldown = 0;
        public static int Required(int configured) => Math.Clamp(configured, 1, 100);
        public static float Growth(int consumed, int required)
            => Math.Clamp((float)consumed / Required(required), 0f, 1f);
        public static bool CanPulse(float fuel, int now, int lastPulse, bool mature)
            => mature && fuel >= PulseCost && (long)now - lastPulse >= PulseCooldown;
    }
}

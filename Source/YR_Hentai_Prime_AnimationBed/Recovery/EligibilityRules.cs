using System;

namespace YR_Hentai_Prime_AnimationBed
{
    public static class EligibilityRules
    {
        public static bool Allows(double biologicalAge, bool adultLifeStage, bool humanlike, int minimumAge)
            => humanlike && adultLifeStage && biologicalAge >= Math.Max(18, minimumAge);
    }
}

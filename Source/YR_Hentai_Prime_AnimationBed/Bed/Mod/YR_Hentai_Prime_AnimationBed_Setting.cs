using Verse;


namespace YR_Hentai_Prime_AnimationBed
{
    [StaticConstructorOnStartup]
    public class YR_Hentai_Prime_AnimationBed_Setting : ModSettings
    {
        public int minimumAge = 18;
        public bool enableAnimations = true;
        public bool enableSounds = true;
        public bool enablePortraits = true;
        public bool preventRjwJobConflicts = true;
        public bool enableSamok = true;
        public int altarCorpses = 20;
        public int growthCorpses = 12;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref minimumAge, "recoveryMinimumAge", 18);
            Scribe_Values.Look(ref enableAnimations, "recoveryAnimations", true);
            Scribe_Values.Look(ref enableSounds, "recoverySounds", true);
            Scribe_Values.Look(ref enablePortraits, "recoveryPortraits", true);
            Scribe_Values.Look(ref preventRjwJobConflicts, "recoveryRjwConflictGuard", true);
            Scribe_Values.Look(ref enableSamok, "recoverySamok", true);
            Scribe_Values.Look(ref altarCorpses, "recoveryAltarCorpses", 20);
            Scribe_Values.Look(ref growthCorpses, "recoveryGrowthCorpses", 12);
            minimumAge = System.Math.Clamp(minimumAge, 18, 100);
            altarCorpses = System.Math.Clamp(altarCorpses, 1, 100);
            growthCorpses = System.Math.Clamp(growthCorpses, 1, 100);
        }
    }
}

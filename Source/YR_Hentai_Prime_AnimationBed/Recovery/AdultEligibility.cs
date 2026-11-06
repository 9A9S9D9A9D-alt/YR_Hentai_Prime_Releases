using RimWorld;
using Verse;
using Verse.AI;

namespace YR_Hentai_Prime_AnimationBed
{
    public static class AdultEligibility
    {
        public static bool IsAdult(Pawn pawn)
            => pawn != null && !pawn.Dead && pawn.ageTracker != null &&
               EligibilityRules.Allows(pawn.ageTracker.AgeBiologicalYearsFloat,
                   pawn.DevelopmentalStage == DevelopmentalStage.Adult,
                   pawn.RaceProps.Humanlike,
                   YR_Hentai_Prime_AnimationBed_Mod.Settings?.minimumAge ?? 18);

        public static bool IsAllowed(Pawn pawn)
            => IsAdult(pawn) && UnsupportedRacesV8.Allows(pawn.def) &&
               (YR_Hentai_Prime_AnimationBed_Mod.Settings?.enableAnimations ?? true);
    }

    public class AdultJoyGiver : JoyGiver_InteractBuildingInteractionCell
    {
        public override Job TryGiveJob(Pawn pawn)
        {
            if (!AdultEligibility.IsAllowed(pawn)) return null;
            Job job = base.TryGiveJob(pawn);
            Pawn participant = job?.targetA.Thing?.TryGetComp<CompDummyForJoy>()?.building_AnimationBed?.HeldPawn;
            return AdultEligibility.IsAllowed(participant) ? job : null;
        }
    }
}

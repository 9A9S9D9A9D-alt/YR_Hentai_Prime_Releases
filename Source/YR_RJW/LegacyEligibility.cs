using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using YR_Hentai_Prime_AnimationBed;
namespace YR_RJW
{
    public static class LegacyEligibility
    {
        public static bool Allows(Pawn pawn) => AdultEligibility.IsAdult(pawn) && UnsupportedRacesV8.Allows(pawn.def);
        public static bool Restricted(HediffDef def) => def != null && States.Contains(def.defName);
        private static readonly HashSet<string> States = new HashSet<string> {
            "YR_MilkingMachineAddon_Hediff", "YR_Building_MilkingMachine_Hediff", "YR_Building_Tentacle_Hediff",
            "YR_Disciplined_Hediff", "YR_EggVibrator_Hediff", "YR_Tentacle_Egg_Hediff", "YR_TentacleWiggle_Hediff",
            "YR_Pregnant_Tentacle", "YR_Pregnant_Tentacle_Birth", "YR_Tentacle_Hediff", "YR_Milking_Tentacle_Hediff",
            "YR_Tentacle_Combat_Aphrodisiac_Gas", "YR_Tentacle_Combat_Aphrodisiac_Gas_Aftereffect",
            "YR_BellyPunch_Hediff", "YR_PanPanPlay_Hediff"
        };
    }
    // Check the object overload: the Def overload can still return its uninserted object safely.
    [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.AddHediff),
        new Type[] { typeof(Hediff), typeof(BodyPartRecord), typeof(DamageInfo?), typeof(DamageWorker.DamageResult) })]
    internal static class LegacyStateEligibility
    {
        private static bool Prefix(Pawn ___pawn, Hediff hediff)
            => !LegacyEligibility.Restricted(hediff?.def) || LegacyEligibility.Allows(___pawn);
    }
    [HarmonyPatch(typeof(RecipeWorker), nameof(RecipeWorker.AvailableOnNow))]
    internal static class LegacyRecipeEligibility
    {
        private static void Postfix(RecipeWorker __instance, Thing thing, ref bool __result)
        {
            if (__instance.recipe?.defName == "YR_Tentacle_Egg_Install" || __instance.recipe?.defName == "YR_InsertEggVibrator")
                __result &= LegacyEligibility.Allows(thing as Pawn);
        }
    }
}

using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace YR_Hentai_Prime_AnimationBed
{
    // Optional eligibility filter only. Does not start, duplicate or report RJW interactions.
    [StaticConstructorOnStartup]
    public static class RjwCompatibility
    {
        static RjwCompatibility()
        {
            Type type = AccessTools.TypeByName("rjw.xxx");
            if (type == null) return;
            Harmony harmony = new Harmony("SutSutMan.YRHentaiPrime.Recovery.RJW");
            MethodInfo method = AccessTools.Method(type, "can_do_loving", new[] { typeof(Pawn) });
            if (method == null || method.ReturnType != typeof(bool))
            {
                Log.Warning("[YRP Recovery] RJW eligibility API not found; optional conflict filter skipped.");
                return;
            }
            harmony.Patch(method, postfix: new HarmonyMethod(typeof(RjwCompatibility), nameof(FilterExternalJob)));
        }

        public static void FilterExternalJob(Pawn __0, ref bool __result)
        {
            // Original 1.4 patch was a postfix on can_do_loving for CurrentBed().
            // Preserve that behavior and include the 1.7 drawn-pawn container representation.
            if (FacilityCompatibility.IsUsingFacility(__0) &&
                (YR_Hentai_Prime_AnimationBed_Mod.Settings?.preventRjwJobConflicts ?? true))
            {
                __result = false;
            }
        }
    }
}

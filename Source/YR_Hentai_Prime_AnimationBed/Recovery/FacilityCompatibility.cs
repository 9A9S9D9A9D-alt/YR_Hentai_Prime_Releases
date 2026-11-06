using System;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace YR_Hentai_Prime_AnimationBed
{
    [StaticConstructorOnStartup]
    internal static class FacilityCompatibility
    {
        static FacilityCompatibility()
        {
            var harmony = new Harmony("SutSutMan.YRHentaiPrime.Recovery.Facilities");
            var commonSense = AccessTools.TypeByName("CommonSense.OpportunisticTasks");
            if (commonSense != null)
            {
                var startPatch=AccessTools.TypeByName("CommonSense.OpportunisticTasks+Pawn_JobTracker_StartJob_CommonSensePatch");
                var prefix=AccessTools.Method(startPatch,"Prefix");
                if(prefix!=null && prefix.ReturnType==typeof(bool))
                    harmony.Patch(prefix,prefix:new HarmonyMethod(typeof(FacilityCompatibility),nameof(SkipCleaningForHeldTarget)));
                // CommonSense 1.4 had an IntVec3 parameter; 1.5+ removed it.
                var method = AccessTools.Method(commonSense, "Cleaning_Opportunity");
                if (method != null && method.ReturnType == typeof(Job) &&
                    method.GetParameters().Length > 0 && method.GetParameters()[0].ParameterType == typeof(Job))
                    harmony.Patch(method, postfix: new HarmonyMethod(typeof(FacilityCompatibility), nameof(FilterCleaning)));
                else Log.Warning("[YRP Recovery] CommonSense cleaning API differs; compatibility filter not installed.");
            }
            var medPod = AccessTools.TypeByName("MedPod.MedPodHealthAIUtility");
            if (medPod != null)
            {
                var method = AccessTools.Method(medPod, "ShouldSeekMedPodRest");
                if (method != null && method.ReturnType == typeof(bool) &&
                    method.GetParameters().Length > 0 && method.GetParameters()[0].ParameterType == typeof(Pawn))
                    harmony.Patch(method, postfix: new HarmonyMethod(typeof(FacilityCompatibility), nameof(FilterMedPod)));
                else Log.Warning("[YRP Recovery] MedPod rest API differs; compatibility filter not installed.");
            }
        }

        private static bool SkipCleaningForHeldTarget(Job newJob, ref bool __result)
        {
            if(newJob!=null && newJob.def?.driverClass?.Namespace==typeof(Building_AnimationBed).Namespace &&
                (IsFacility(newJob.targetA.Thing) || IsFacility(newJob.targetB.Thing)))
            {
                // CommonSense assumes the target pawn has a map. A pawn inside
                // our holder is intentionally despawned until release/transfer.
                __result=true;
                return false;
            }
            return true;
        }

        internal static bool IsFacility(Thing thing)
        {
            if (thing is Building_AnimationBed) return true;
            for (Type type = thing?.GetType(); type != null; type = type.BaseType)
                if (type.FullName == "YR_RJW.Building_BaseBondageBed") return true;
            return false;
        }

        internal static bool IsUsingFacility(Pawn pawn) => pawn != null &&
            (pawn.holdingOwner?.Owner is Building_AnimationBed || IsFacility(pawn.CurrentBed()));

        private static void FilterCleaning(Job __0, object[] __args, ref Job __result)
        {
            if (__0?.def == JobDefOf.LayDown &&
                (IsFacility(__0.GetTarget(TargetIndex.A).Thing) || IsUsingFacility(__args.OfType<Pawn>().FirstOrDefault())))
                __result = null;
        }

        private static void FilterMedPod(Pawn __0, ref bool __result)
        {
            if (IsUsingFacility(__0)) __result = false;
        }
    }
}

using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace YR_RJW
{
    // A spawned spa patient remains reachable by ordinary doctors and feeders.
    public static class SamokSpaCare
    {
        public static Building_SamokSwimmingPool PoolFor(Pawn patient)
        {
            if (patient?.Spawned != true || !patient.Downed) return null;
            var def = DefDatabase<ThingDef>.GetNamedSilentFail("YR_SamokSwimmingPool");
            return def == null ? null : patient.Map.listerThings.ThingsOfDef(def)
                .OfType<Building_SamokSwimmingPool>().FirstOrDefault(p => p.Patients.Contains(patient) && p.IsUsing(patient));
        }
        public static bool ShouldFeed(Pawn p)
        {
            if (PoolFor(p) == null || p.GetPosture() == PawnPosture.Standing ||
                (p.Faction != Faction.OfPlayer && p.HostFaction != Faction.OfPlayer) ||
                !p.RaceProps.EatsFood || p.needs?.food == null || p.ShouldBeSlaughtered() ||
                !HealthAIUtility.ShouldSeekMedicalRest(p)) return false;
            if (p.HostFaction != null && (p.HostFaction != Faction.OfPlayer ||
                (p.guest != null && !p.guest.CanBeBroughtFood))) return false;
            return true;
        }
    }
    [HarmonyPatch(typeof(FeedPatientUtility), nameof(FeedPatientUtility.ShouldBeFed))]
    internal static class SamokSpaPatientFeeding
    {
        private static void Postfix(Pawn p, ref bool __result)
        {
            if (!__result && SamokSpaCare.ShouldFeed(p)) __result = true;
        }
    }
    [HarmonyPatch(typeof(WorkGiver_RescueDowned), nameof(WorkGiver_RescueDowned.HasJobOnThing))]
    internal static class SamokSpaAutomaticRescue
    {
        private static void Postfix(Thing t, bool forced, ref bool __result)
        {
            // Explicit rescue always remains available; automatic hauling must not undo assigned care.
            if (__result && !forced && t is Pawn patient && SamokSpaCare.PoolFor(patient) != null) __result = false;
        }
    }
}
using RimWorld;
using Verse.AI;
using System.Collections.Generic;
using System.Linq;
using Verse;
using YR_Hentai_Prime_AnimationBed;
namespace YR_RJW
{
    public partial class Building_BaseBondageBed : IFacilityAssignmentTarget
    {
        public bool AvailableForAssignment => Spawned && occupant == null && CoffinCorpse == null;
        public IEnumerable<FloatMenuOption> AssignmentOptions(Pawn target, Pawn carrier = null)
        {
            if (!Spawned || occupant != null || CoffinCorpse != null || target?.Spawned != true || target.Map != Map || !LegacyEligibility.Allows(target)) yield break;
            var usable = GetComp<CompUsableAdvancedBondageBed>();
            if (usable == null) yield break;
            IEnumerable<Pawn> actors = carrier == null ? Map.mapPawns.FreeColonistsSpawned : new[] { carrier };
            foreach (var actor in actors.Where(p => p != target && LegacyEligibility.Allows(p) && !p.Downed && !p.InMentalState))
                foreach (var option in usable.OptionsForTarget(actor, target))
                    yield return new FloatMenuOption(actor.LabelShort + ": " + option.Label, option.action);
        }
    }
}
namespace YR_RJW {
 // Explicit facility assignments are not ordinary prisoner sleeping-bed searches.
 [HarmonyLib.HarmonyPatch(typeof(RimWorld.RestUtility),nameof(RimWorld.RestUtility.CanUseBedNow))]
 internal static class AssignedFacilityBedAcceptance {
  private static void Postfix(Verse.Thing bedThing,Verse.Pawn sleeper,ref bool __result){
   var bed=bedThing as Building_BaseBondageBed;
   if(__result || bed==null || bed.occupant!=sleeper || !LegacyEligibility.Allows(sleeper) || !bed.Spawned || bed.Map!=sleeper.MapHeld || bed.CoffinCorpse!=null || bed.Medical)return;
   if(bed.IsBurning() || !RimWorld.RestUtility.CanUseBedEver(sleeper,bed.def) || bed.CompAssignableToPawn.IdeoligionForbids(sleeper))return;
   if(sleeper.HarmedByVacuum && bed.Position.GetVacuum(bed.Map)>=.5f)return;
   if(!bed.AnyUnoccupiedSleepingSlot && !bed.IsOwner(sleeper,out _) && sleeper.CurrentBed()!=bed)return;
   if(sleeper.IsColonist && (sleeper.CurJob==null || !sleeper.CurJob.ignoreForbidden) && !sleeper.Downed && bed.IsForbidden(sleeper))return;
   __result=true;
  }
 }
}

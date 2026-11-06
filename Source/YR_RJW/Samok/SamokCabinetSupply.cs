using System;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace YR_RJW {
 [HarmonyPatch(typeof(Pawn_PathFollower),nameof(Pawn_PathFollower.StartPath))]
 static class SamokCabinetSupply {
  static void Prefix(Pawn ___pawn,LocalTargetInfo dest){
   if(!dest.HasThing)return;
   try{TryShareForPickup(___pawn,dest.Thing);}
   catch(Exception e){Log.ErrorOnce("[Samok cabinet supply] "+e,19050611);}
  }
  static bool IsCabinet(Building_Storage cabinet)=>cabinet?.def.defName=="YR_SamokOfferingCabinetPreview";
  public static bool TryShareForPickup(Pawn pawn,Thing item){
   if(pawn?.Spawned!=true||pawn.Faction!=Faction.OfPlayer||pawn.CurJob==null||item?.Spawned!=true||item.Map!=pawn.Map||item.def.category!=ThingCategory.Item||item.def.AffectsRegions||item.IsForbidden(pawn))return false;
   var source=item.GetSlotGroup()?.parent as Building_Storage;
   if(!IsCabinet(source))return false;
   var map=pawn.Map;
   if(map.reservationManager.ReservationsReadOnly.Any(r=>r.Target.Thing==item&&(r.Claimant!=pawn||r.Job!=pawn.CurJob)))return false;
   var treeDef=DefDatabase<ThingDef>.GetNamedSilentFail("Building_Tentacle_Tree");if(treeDef==null)return false;
   Building_Storage nearest=null;Thing merge=null;IntVec3 cell=IntVec3.Invalid;
   float distance=item.Position.DistanceToSquared(pawn.Position);
   // Inspect only this pickup's linked cabinets; there is no background inventory scan.
   foreach(var tree in map.listerThings.ThingsOfDef(treeDef).OfType<ThingWithComps>()){
    if(tree.Faction!=Faction.OfPlayer)continue;
    var network=tree.TryGetComp<CompSamokGrowth>();if(network==null||!network.LinkedCabinets.Contains(source))continue;
    foreach(var cabinet in network.LinkedCabinets){
     if(cabinet==source||!cabinet.Accepts(item))continue;
     foreach(var candidate in cabinet.AllSlotCells()){
      float next=candidate.DistanceToSquared(pawn.Position);
      if(next>=distance||!StoreUtility.IsGoodStoreCell(candidate,map,item,pawn,Faction.OfPlayer)||!pawn.CanReach(candidate,PathEndMode.ClosestTouch,Danger.Some)||!pawn.CanReserve(candidate))continue;
      var stored=candidate.GetThingList(map).Where(t=>t.def.category==ThingCategory.Item).ToList();Thing combine=null;
      if(stored.Count>=cabinet.def.building.maxItemsInCell){
       combine=stored.FirstOrDefault(t=>!map.reservationManager.IsReserved(t)&&item.CanStackWith(t)&&item.stackCount+t.stackCount<=item.def.stackLimit);
       if(combine==null)continue;
      }
      nearest=cabinet;cell=candidate;merge=combine;distance=next;
     }
    }
   }
   if(nearest==null)return false;
   var previous=item.Position;
   try{
    // Keep the original Thing reference: active job targets and reservations stay valid.
    item.Position=cell;
    if(merge!=null&&(!item.TryAbsorbStack(merge,true)||!merge.Destroyed)){item.Position=previous;return false;}
    source.Notify_LostThing(item);nearest.Notify_ReceivedThing(item);
    return true;
   }catch{if(item.Spawned&&item.Position!=previous)item.Position=previous;throw;}
  }
 }
}

using System;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
namespace YR_RJW {
 internal static class SwimmingPoolPlacement {
  internal static bool Overlaps(BuildableDef def,IntVec3 center,Rot4 rot,Map map,Thing ignored=null){
   if(map==null || !(def is ThingDef t) || t.category!=ThingCategory.Building)return false;
   foreach(var cell in GenAdj.OccupiedRect(center,rot,def.Size)){
    if(!cell.InBounds(map))continue;
    foreach(var thing in cell.GetThingList(map))if(thing!=ignored && thing is Building_SamokSwimmingPool)return true;
   }
   return false;
  }
 }
 [HarmonyPatch(typeof(GenConstruct),"CanPlaceBlueprintAt_NewTemp")]
 internal static class SwimmingPoolBlueprintRestriction {
  static bool Prefix(BuildableDef entDef,IntVec3 center,Rot4 rot,Map map,Thing thingToIgnore,ref AcceptanceReport __result){
   if(!SwimmingPoolPlacement.Overlaps(entDef,center,rot,map,thingToIgnore))return true;
   __result="영양액 웅덩이 위에는 건물을 지을 수 없습니다.";return false;
  }
 }
 [HarmonyPatch(typeof(GenConstruct),nameof(GenConstruct.CanConstruct),new Type[]{typeof(Thing),typeof(Pawn),typeof(bool),typeof(bool),typeof(JobDef)})]
 internal static class SwimmingPoolExistingBlueprintRestriction {
  static bool Prefix(Thing t,ref bool __result){
   if(t?.Spawned!=true || !SwimmingPoolPlacement.Overlaps(t.def.entityDefToBuild,t.Position,t.Rotation,t.Map))return true;
   JobFailReason.Is("영양액 웅덩이 위에는 건물을 지을 수 없습니다.");__result=false;return false;
  }
 }
}

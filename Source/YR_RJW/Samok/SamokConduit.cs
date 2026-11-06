using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using HarmonyLib;

namespace YR_RJW {
 [HarmonyPatch(typeof(ShortCircuitUtility),"DoShortCircuit")]
 static class SamokConduitNoShortCircuit {static bool Prefix(Building __0)=>__0?.def.defName!="YR_SamokConduit";}
 [HarmonyPatch(typeof(ShortCircuitUtility),"TryShortCircuitInRain")]
 static class SamokConduitNoRainShortCircuit {
  static bool Prefix(Thing __0,ref bool __result){if(__0?.def.defName!="YR_SamokConduit")return true;__result=false;return false;}
 }
 public class SamokConduitMap : MapComponent {
  public bool visible=true;
  public SamokConduitMap(Map map):base(map){}
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref visible,"samokConduitsVisible",true);}
  public Command ToggleCommand()=>new Command_Toggle {
   defaultLabel="전도근 표시",defaultDesc="이 맵의 전도근 전체를 숨기거나 표시합니다. 전력 연결은 유지됩니다.",
   icon=ContentFinder<Texture2D>.Get("Yuran/UI/Commands/ConduitVisibility"),
   isActive=()=>visible,toggleAction=()=>{
    visible=!visible;
    var def=DefDatabase<ThingDef>.GetNamedSilentFail("YR_SamokConduit");if(def==null)return;
    foreach(var conduit in map.listerThings.ThingsOfDef(def))map.mapDrawer.MapMeshDirty(conduit.Position,MapMeshFlagDefOf.Things);
   }
  };
 }
 public class Building_SamokConduit : Building {
  public override void Print(SectionLayer layer){
   if(!Map.GetComponent<SamokConduitMap>().visible)return;
   base.Print(layer);
  }
  public override IEnumerable<Gizmo> GetGizmos(){
   foreach(var gizmo in base.GetGizmos())yield return gizmo;
   if(Spawned&&Faction==RimWorld.Faction.OfPlayer)yield return Map.GetComponent<SamokConduitMap>().ToggleCommand();
  }
 }
}

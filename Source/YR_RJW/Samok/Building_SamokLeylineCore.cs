using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
namespace YR_RJW {
 public class Building_SamokLeylineCore : Building {
  public override void SpawnSetup(Map map,bool respawningAfterLoad){
   base.SpawnSetup(map,respawningAfterLoad);
   var creep=SamokCreepMap.Creep;
   if(creep!=null && Position.GetTerrain(map)!=creep){
    var old=Position.GetTerrain(map);map.terrainGrid.SetTerrain(Position,creep);map.terrainGrid.SetUnderTerrain(Position,old);
   }
   map.GetComponent<SamokCreepMap>().AnchorCell(Position);
  }
  public override IEnumerable<Gizmo> GetGizmos(){
   foreach(var g in base.GetGizmos())yield return g;
   yield return new Command_Action {defaultLabel="사목뿌리 생성",defaultDesc="사목지반을 클릭해 무작위 크기의 뿌리 하나를 생성합니다.",icon=ThingDef.Named("YR_Tentacle_Rug_A").uiIcon,action=()=>Find.DesignatorManager.Select(new Designator_SamokRoot(this))};
  }
 }
}

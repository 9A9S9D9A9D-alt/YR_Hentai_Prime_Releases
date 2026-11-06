using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
namespace YR_RJW {
 public class CompPowerPlant_SamokElectric : CompPowerPlant {
  protected override float DesiredPowerOutput => parent.Spawned && parent.Position.GetTerrain(parent.Map)==SamokCreepMap.Creep ? 250f : 0f;
  public override string CompInspectStringExtra(){
   return parent.Spawned && parent.Position.GetTerrain(parent.Map)!=SamokCreepMap.Creep ? "사목 지반과의 연결이 끊겨 발전이 멈췄습니다." : base.CompInspectStringExtra();
  }
 }
 public class CompProperties_SamokGrowth : CompProperties {
  public CompProperties_SamokGrowth(){compClass=typeof(CompSamokGrowth);}
 }
 public partial class CompSamokGrowth : ThingComp,IThingHolder {
  private List<Building_Storage> shelves=new List<Building_Storage>();
  public override void PostExposeData(){base.PostExposeData();Scribe_Collections.Look(ref shelves,"samokLinkedShelves",LookMode.Reference);if(Scribe.mode==LoadSaveMode.PostLoadInit){shelves??=new List<Building_Storage>();CleanLinks();}ExposeDaughter();}
  private static bool IsCabinet(Building_Storage shelf)=>shelf?.def.defName=="YR_SamokOfferingCabinetPreview";
  private void CleanLinks()=>shelves.RemoveAll(s=>!IsCabinet(s)||s.Destroyed);
  public bool CanLinkCabinet(Building_Storage shelf)=>parent.Spawned&&IsCabinet(shelf)&&shelf.Spawned&&shelf.Map==parent.Map&&shelf.Faction==Faction.OfPlayer;
  public IEnumerable<Building_Storage> LinkedCabinets=>shelves.Where(CanLinkCabinet);
  public void UnlinkShelf(Building_Storage shelf)=>shelves.Remove(shelf);
  public bool LinkShelf(Building_Storage shelf){
   if(!CanLinkCabinet(shelf))return false;
   CleanLinks();if(!shelves.Contains(shelf))shelves.Add(shelf);return true;
  }
  public int LinkedCount=>LinkedCabinets.Count();
  public override string CompInspectStringExtra()=>"연결 공물함: "+LinkedCount+"개 · 선택 시 연결선 표시"+DaughterStatus;
  public override void PostDrawExtraSelectionOverlays(){
   base.PostDrawExtraSelectionOverlays();
   foreach(var cabinet in LinkedCabinets){GenDraw.DrawLineBetween(parent.TrueCenter(),cabinet.TrueCenter(),SimpleColor.Cyan);GenDraw.DrawFieldEdges(cabinet.OccupiedRect().ToList(),Color.cyan);}
  }
  public override IEnumerable<Gizmo> CompGetGizmosExtra(){
   if(!parent.Spawned || parent.Faction!=Faction.OfPlayer)yield break;
   yield return DaughterCommand();
   yield return new Command_Action{defaultLabel="공물함 연결",defaultDesc="이 맵의 아군 사목 공물함만 재료 공급원으로 연결합니다. 나무를 선택하면 연결선과 공물함 외곽이 표시됩니다. 연결 공물함 사이에서는 폰이 재료를 가지러 갈 때 더 가까운 공물함으로 한 스택씩 공유합니다.",icon=ContentFinder<Texture2D>.Get("Yuran/UI/Commands/CabinetLink"),action=()=>Find.Targeter.BeginTargeting(new TargetingParameters{canTargetBuildings=true,canTargetPawns=false,canTargetItems=false,validator=t=>t.Thing is Building_Storage s&&CanLinkCabinet(s)},t=>{if(LinkShelf(t.Thing as Building_Storage))Messages.Message("공물함을 연결했습니다.",parent,MessageTypeDefOf.TaskCompletion,false);})};
   yield return new Command_Action{defaultLabel="공물함 연결 해제",defaultDesc="연결된 공물함을 선택해 이 나무와의 연결만 해제합니다. 보관 물품은 유지됩니다.",icon=ContentFinder<Texture2D>.Get("Yuran/UI/Commands/CabinetUnlink"),action=()=>{
    var options=LinkedCabinets.Select(s=>new FloatMenuOption(s.LabelCap+" ("+s.Position+")",()=>UnlinkShelf(s))).ToList();
    if(options.Count==0)options.Add(new FloatMenuOption("연결된 공물함이 없습니다.",null));Find.WindowStack.Add(new FloatMenu(options));
   }};
   var grow=new Command_Action{defaultLabel="전기낭 생성",defaultDesc="연결 공물함의 사목 촉수 75개와 백윤액 30개를 사용해 사목 지반에 전기낭을 생성합니다. 전력 생산 250W, 지속 연료 없음.",icon=ContentFinder<Texture2D>.Get("Yuran/Things/Building/ElectricSac/Texture"),action=()=>Find.Targeter.BeginTargeting(new TargetingParameters{canTargetLocations=true,canTargetBuildings=false,canTargetPawns=false,canTargetItems=false},t=>{
    if(!TryGrowElectric(t.Cell,out string reason))Messages.Message(reason,parent,MessageTypeDefOf.RejectInput,false);
   })};
   if(!ResearchFinished)grow.Disable("사목 생체공학 I 연구가 필요합니다.");else if(LinkedCount==0)grow.Disable("공물함을 먼저 연결하세요.");yield return grow;
  }
  private static bool ResearchFinished=>DefDatabase<ResearchProjectDef>.GetNamedSilentFail("YR_SamokBioengineeringI")?.IsFinished==true;
  private List<Thing> AvailableStock(){
   return LinkedCabinets.SelectMany(s=>s.AllSlotCells()).Distinct()
    .SelectMany(c=>c.GetThingList(parent.Map)).Where(t=>t.def.category==ThingCategory.Item&&!parent.Map.reservationManager.IsReserved(t)).Distinct().ToList();
  }
  public bool TryGrowElectric(IntVec3 cell,out string reason){
   reason="";
   if(!parent.Spawned||!ResearchFinished){reason="나무와 사목 생체공학 I 연구가 필요합니다.";return false;}
   var map=parent.Map;var def=ThingDef.Named("YR_SamokElectricSac");
   if(!cell.InBounds(map)||cell.GetTerrain(map)!=SamokCreepMap.Creep){reason="사목 뿌리 지반 위에서만 생성할 수 있습니다.";return false;}
   if(cell.GetThingList(map).Any(t=>!RootRugOverlapV12.IsRoot(t.def)&&(t is Pawn||t.def.category==ThingCategory.Building||t.def.category==ThingCategory.Item||GenSpawn.SpawningWipes(def,t.def,false)))){reason="다른 물체가 없는 사목 지반을 선택하세요.";return false;}
   var stock=AvailableStock();var plan=new List<KeyValuePair<Thing,int>>();
   foreach(var cost in def.costList){
    int needed=cost.count;
    foreach(var t in stock.Where(t=>t.def==cost.thingDef)){int take=Math.Min(needed,t.stackCount);if(take>0)plan.Add(new KeyValuePair<Thing,int>(t,take));needed-=take;if(needed==0)break;}
    if(needed>0){reason="연결 공물함의 예약되지 않은 "+cost.thingDef.label+"이(가) "+needed+"개 부족합니다. 재료는 소비하지 않았습니다.";return false;}
   }
   // The complete stock plan is validated before any split. UI actions execute serially on the game thread.
   var spent=new List<Thing>();Thing grown=null;
   try{
    foreach(var entry in plan)spent.Add(entry.Key.SplitOff(entry.Value));
    grown=ThingMaker.MakeThing(def);grown.SetFaction(Faction.OfPlayer);GenSpawn.Spawn(grown,cell,map,Rot4.North);
   }catch(Exception e){
    if(grown?.Spawned==true)grown.Destroy(DestroyMode.Vanish);
    foreach(var t in spent)if(t!=null&&!t.Destroyed)GenPlace.TryPlaceThing(t,parent.Position,map,ThingPlaceMode.Near);
    Log.Error("[Samok electric growth] "+e);reason="생성에 실패했습니다. 분리한 재료는 나무 주변에 반환했습니다.";return false;
   }
   foreach(var t in spent)t.Destroy(DestroyMode.Vanish);
   Messages.Message("사목의 전기낭이 뿌리내렸습니다.",grown,MessageTypeDefOf.TaskCompletion,false);return true;
  }
 }
}

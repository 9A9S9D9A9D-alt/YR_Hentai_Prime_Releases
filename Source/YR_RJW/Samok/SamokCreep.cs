using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace YR_RJW
{
    [HarmonyPatch(typeof(GhostUtility),"GhostGraphicFor")]
    internal static class SamokDecorationGhost
    {
        static void Prefix(ref Graphic baseGraphic,ThingDef thingDef){
            if(thingDef?.modContentPack?.PackageIdPlayerFacing.Equals("SutSutMan.YRHentaiPrime",System.StringComparison.OrdinalIgnoreCase)==true&&baseGraphic is Graphic_RandomRotated rotated)
                baseGraphic=rotated.SubGraphic;
        }
    }
    public class Designator_SamokGround : Designator_Cells
    {
        private readonly CompSamokCreep source;
        public Designator_SamokGround(CompSamokCreep source){
            this.source=source;defaultLabel="지반 확장";
            defaultDesc="드래그한 영역에서 기존 사목뿌리지반과 연결되는 칸을 확장합니다. 한 칸당 공물 1을 사용합니다.";
            icon=SamokCreepMap.Creep.uiIcon;
            soundDragSustain=SoundDefOf.Designate_DragAreaAdd;soundDragChanged=SoundDefOf.Designate_DragZone_Changed;
        }
        public override AcceptanceReport CanDesignateCell(IntVec3 cell){
            if(!source.parent.Spawned||source.parent.Map!=Map||!cell.InBounds(Map))return false;
            var terrain=cell.GetTerrain(Map);
            return terrain!=null&&terrain!=SamokCreepMap.Creep&&!terrain.IsWater&&terrain.passability!=Traversability.Impassable&&(source.parent.TryGetComp<CompRefuelable>()?.Fuel??0f)>=1f;
        }
        public override void DesignateSingleCell(IntVec3 cell)=>DesignateMultiCell(new[]{cell});
        public override void SelectedUpdate(){base.SelectedUpdate();var cell=UI.MouseCell();if(cell.InBounds(Map))GenDraw.DrawFieldEdges(new List<IntVec3>{cell},CanDesignateCell(cell)?Color.cyan:Color.red);}
        public override void DrawMouseAttachments(){base.DrawMouseAttachments();var mouse=Event.current.mousePosition;var rect=new Rect(Mathf.Min(mouse.x+24,UI.screenWidth-280),Mathf.Min(mouse.y+24,UI.screenHeight-54),270,50);Widgets.DrawMenuSection(rect);Widgets.Label(rect.ContractedBy(5),"사목지반 지정 중 · 드래그로 영역 선택\n공물 1 / 칸 · 우클릭 또는 Esc로 종료");}
        public override void DesignateMultiCell(IEnumerable<IntVec3> cells){
            var pending=new HashSet<IntVec3>(cells.Where(c=>CanDesignateCell(c).Accepted));
            var queue=new Queue<IntVec3>(pending.Where(c=>GenAdj.AdjacentCells.Any(o=>(c+o).InBounds(Map)&&(c+o).GetTerrain(Map)==SamokCreepMap.Creep)));
            int made=0;
            while(queue.Count>0){
                var cell=queue.Dequeue();if(!pending.Remove(cell))continue;
                if(!source.TryExpandGround(cell,out _))continue;
                made++;
                foreach(var offset in GenAdj.AdjacentCells){var next=cell+offset;if(pending.Contains(next))queue.Enqueue(next);}
            }
            if(made==0)Messages.Message("기존 지반과 연결된 영역과 공물 잔량을 확인하세요.",source.parent,MessageTypeDefOf.RejectInput,false);
        }
    }
    public class Designator_SamokRoot : Designator
    {
        private readonly Thing source;
        public Designator_SamokRoot(Thing source){this.source=source;defaultLabel="사목뿌리 생성";defaultDesc="사목지반 한 칸을 클릭해 소형·중형·대형 중 무작위 뿌리 하나를 생성합니다.";icon=ThingDef.Named("YR_Tentacle_Rug_A").uiIcon;}
        public override AcceptanceReport CanDesignateCell(IntVec3 cell)=>source.Spawned&&source.Map==Map&&cell.InBounds(Map)&&cell.GetTerrain(Map)==SamokCreepMap.Creep;
        public override void DesignateSingleCell(IntVec3 cell){if(CanDesignateCell(cell)&&!Map.GetComponent<SamokCreepMap>().PlaceRootAt(cell))Messages.Message("뿌리를 생성할 공간이 부족하거나 기존 뿌리와 겹칩니다.",source,MessageTypeDefOf.RejectInput,false);}
        public override void SelectedUpdate(){base.SelectedUpdate();var cell=UI.MouseCell();if(cell.InBounds(Map))GenDraw.DrawFieldEdges(new List<IntVec3>{cell},CanDesignateCell(cell)?Color.green:Color.red);}
    }
    public class CompProperties_SamokCreep : CompProperties
    {
        public CompProperties_SamokCreep(){compClass=typeof(CompSamokCreep);}
    }
    public class CompSamokCreep : ThingComp
    {
        public CellRect Bounds=>GenAdj.OccupiedRect(parent.Position,parent.Rotation,new IntVec2(10,10));
        public void DebugFill(){if(parent.Spawned)parent.Map.GetComponent<SamokCreepMap>().Fill(this);}
        public void DebugClear(){if(parent.Spawned)parent.Map.GetComponent<SamokCreepMap>().Clear(this);}
        public bool growEnabled=true;
        private bool initialGroundGranted;
        public override void PostExposeData(){base.PostExposeData();Scribe_Values.Look(ref growEnabled,"samokCreepGrowthEnabled",true);Scribe_Values.Look(ref initialGroundGranted,"samokInitialGroundGranted");}
        public override void PostSpawnSetup(bool respawningAfterLoad){
            base.PostSpawnSetup(respawningAfterLoad);
            if(initialGroundGranted||!parent.Spawned||SamokCreepMap.Creep==null)return;
            var map=parent.Map;
            foreach(var cell in Bounds){
                if(!cell.InBounds(map))continue;
                var old=cell.GetTerrain(map);
                if(old==null||old==SamokCreepMap.Creep||old.IsWater||old.passability==Traversability.Impassable)continue;
                map.terrainGrid.SetTerrain(cell,SamokCreepMap.Creep);
                map.terrainGrid.SetUnderTerrain(cell,old);
                map.GetComponent<SamokCreepMap>().AnchorCell(cell);
            }
            initialGroundGranted=true;
        }
        public bool TryExpandGround(IntVec3 cell,out string reason) {
            reason="";
            if(!parent.Spawned||!cell.InBounds(parent.Map)){reason="맵 안의 칸을 선택하세요.";return false;}
            var map=parent.Map;var creep=SamokCreepMap.Creep;var old=cell.GetTerrain(map);
            if(creep==null||old==null||old.IsWater||old.passability==Traversability.Impassable){reason="이 지형에는 지반을 확장할 수 없습니다.";return false;}
            if(old==creep){reason="이미 사목 뿌리 지반입니다.";return false;}
            if(!GenAdj.AdjacentCells.Any(o=>(cell+o).InBounds(map)&&(cell+o).GetTerrain(map)==creep)){reason="주변 8칸 중 한 칸에 사목 뿌리 지반이 필요합니다.";return false;}
            var gauge=parent.TryGetComp<CompRefuelable>();
            if(gauge==null||gauge.Fuel<1f){reason="공물이 1 필요합니다.";return false;}
            map.terrainGrid.SetTerrain(cell,creep);map.terrainGrid.SetUnderTerrain(cell,old);
            map.GetComponent<SamokCreepMap>().AnchorCell(cell);
            gauge.ConsumeFuel(1f);return true;
        }
        public override IEnumerable<Gizmo> CompGetGizmosExtra(){
            if(parent.Spawned&&parent.Faction==Faction.OfPlayer) {
                yield return parent.Map.GetComponent<SamokConduitMap>().ToggleCommand();
                var expand=new Command_Action{defaultLabel="지반 확장",defaultDesc="드래그로 영역을 지정합니다. 기존 지반과 연결되는 칸만 확장하며, 한 칸당 공물 1을 사용합니다.",icon=SamokCreepMap.Creep.uiIcon,action=()=>Find.DesignatorManager.Select(new Designator_SamokGround(this))};
                if((parent.TryGetComp<CompRefuelable>()?.Fuel??0f)<1f)expand.Disable("공물이 1 필요합니다.");
                yield return expand;
            }
            yield return new Command_Action {defaultLabel="사목뿌리 생성",defaultDesc="사목지반의 한 칸을 클릭해 무작위 크기의 뿌리 하나를 생성합니다.",icon=ThingDef.Named("YR_Tentacle_Rug_A").uiIcon,action=()=>Find.DesignatorManager.Select(new Designator_SamokRoot(parent))};
            yield return new Command_Toggle{defaultLabel="뿌리 장식 성장",defaultDesc="기존 지반의 장식 성장을 켜거나 끕니다. 지반 확장은 나무의 지반 확장 버튼에서 공물을 사용합니다.",icon=ThingDef.Named("YR_Tentacle_Deco").uiIcon,
                isActive=()=>growEnabled,toggleAction=()=>{growEnabled=!growEnabled;if(growEnabled&&parent.Spawned)parent.Map.GetComponent<SamokCreepMap>().Resume(this);}};
        }
    }

    public class SamokCreepMap : MapComponent
    {
        private static readonly AccessTools.FieldRef<TerrainGrid,TerrainDef[]> UnderGrid = AccessTools.FieldRefAccess<TerrainGrid,TerrainDef[]>("underGrid");
        private Dictionary<IntVec3,TerrainDef> previous=new Dictionary<IntVec3,TerrainDef>();
        private Dictionary<IntVec3,TerrainDef> previousUnder=new Dictionary<IntVec3,TerrainDef>();
        private bool rootInstancesInitialized;
        private HashSet<IntVec3> suppressed=new HashSet<IntVec3>();
        public SamokCreepMap(Map map):base(map){}
        public static TerrainDef Creep=>DefDatabase<TerrainDef>.GetNamedSilentFail("YR_SamokCreep");
        public IEnumerable<IntVec3> Cells=>previous.Keys;
        private List<Thing> grownRugs=new List<Thing>();
        private List<Thing> grownStanding=new List<Thing>();
        public void AnchorCell(IntVec3 at){previous.Remove(at);previousUnder.Remove(at);suppressed.Remove(at);}
        public bool PlaceRootAt(IntVec3 at)
        {
            if (!at.InBounds(map) || at.GetTerrain(map) != Creep) return false;
            if (GenAdj.AdjacentCellsAndInside.Select(o=>at+o).Where(c=>c.InBounds(map)).SelectMany(c=>c.GetThingList(map)).Any(t=>RootRugOverlapV12.IsRoot(t.def) && System.Math.Abs(t.Position.x-at.x)<=1 && System.Math.Abs(t.Position.z-at.z)<=1)) return false;
            var candidates=new[]{"YR_Tentacle_Rug_A","YR_Tentacle_Rug_B","YR_Tentacle_Rug_C"}.Select(ThingDef.Named).Where(d=>{
                var cells=GenAdj.OccupiedRect(at,Rot4.North,d.size);
                return cells.All(c=>c.InBounds(map))&&!cells.SelectMany(c=>c.GetThingList(map)).Any(t=>t is Building_SamokSwimmingPool||GenSpawn.SpawningWipes(d,t.def,false));
            }).ToList();
            if(candidates.Count==0)return false;
            var def=candidates.RandomElement();
            var root=ThingMaker.MakeThing(def);root.SetFaction(Faction.OfPlayer);
            grownRugs.RemoveAll(t=>t==null || t.Destroyed);
            grownRugs.Add(GenSpawn.Spawn(root,at,map,Rot4.North));
            return true;
        }
        // Explicit command only; no additional tick scanner or automatic regrowth.
        public int ExpandRoots(){
            int placed=0;
            var candidates=CellRect.WholeMap(map).Cells.Where(c=>c.GetTerrain(map)==Creep).OrderBy(c=>c.z).ThenBy(c=>c.x).ToList();
            foreach(var at in candidates)if(PlaceRootAt(at))placed++;
            return placed;
        }
        private void GrowStandingDecor(CompSamokCreep source){
            grownStanding.RemoveAll(t=>t==null||t.Destroyed||!t.Spawned);
            if(previous.Keys.Count(c=>source.Bounds.Contains(c))<12)return;
            var reachable=Reachable(source);
            string[] ids={"YR_Tentacle_Deco","YR_Tentacle_Light"};
            for(int i=0;i<ids.Length;i++){
                string id=ids[i];
                if(grownStanding.Any(t=>t.def.defName==id&&source.Bounds.Contains(t.Position)))continue;
                var def=DefDatabase<ThingDef>.GetNamedSilentFail(id);if(def==null)continue;
                var desired=source.parent.Position+new IntVec3(i==0?-3:3,0,1);
                foreach(var at in previous.Keys.Where(c=>source.Bounds.Contains(c)).OrderBy(c=>c.DistanceToSquared(desired))){
                    var visualCenter=at+new IntVec3(Mathf.RoundToInt(def.graphicData.drawOffset.x),0,Mathf.RoundToInt(def.graphicData.drawOffset.z));
                    var visual=GenAdj.OccupiedRect(visualCenter,Rot4.North,new IntVec2(Mathf.CeilToInt(def.graphicData.drawSize.x),Mathf.CeilToInt(def.graphicData.drawSize.y)));
                    if(visual.Cells.Any(c=>!c.InBounds(map)||!reachable.Contains(c)))continue;
                    if(at.GetThingList(map).Any(t=>t is Pawn||t.def.category==ThingCategory.Item||GenSpawn.SpawningWipes(def,t.def,false)))continue;
                    var plant=ThingMaker.MakeThing(def);plant.SetFaction(Faction.OfPlayer);
                    grownStanding.Add(GenSpawn.Spawn(plant,at,map,Rot4.North));
                    return; // One standing growth per normal growth step.
                }
            }
        }
        public override void ExposeData()
        {
            // Absence represents no under-layer. Def lists cannot round-trip all-null values.
            if(Scribe.mode==LoadSaveMode.Saving){
                grownRugs.RemoveAll(t=>t==null || t.Destroyed || !t.Spawned);
                foreach(var cell in previousUnder.Where(pair=>pair.Value==null).Select(pair=>pair.Key).ToList())previousUnder.Remove(cell);
            }
            Scribe_Values.Look(ref rootInstancesInitialized,"samokRootInstancesV8");
            Scribe_Collections.Look(ref grownRugs,"samokGrownRootRugs",LookMode.Reference);
            Scribe_Collections.Look(ref grownStanding,"samokGrownStanding",LookMode.Reference);
            Scribe_Collections.Look(ref previous,"samokCreepPrevious",LookMode.Value,LookMode.Def);
            Scribe_Collections.Look(ref previousUnder,"samokCreepUnder",LookMode.Value,LookMode.Def);
            Scribe_Collections.Look(ref suppressed,"samokCreepRemoved",LookMode.Value);
            if(Scribe.mode==LoadSaveMode.PostLoadInit)
            {grownStanding??=new List<Thing>();grownRugs??=new List<Thing>();previous??=new Dictionary<IntVec3,TerrainDef>();previousUnder??=new Dictionary<IntVec3,TerrainDef>();suppressed??=new HashSet<IntVec3>();}
        }
        private List<CompSamokCreep> Sources()
        {
            var def=DefDatabase<ThingDef>.GetNamedSilentFail("Building_Tentacle_Tree");
            if(def==null || !(YR_Hentai_Prime_AnimationBed.YR_Hentai_Prime_AnimationBed_Mod.Settings?.enableSamok??true))return new List<CompSamokCreep>();
            return map.listerThings.ThingsOfDef(def).Select(t=>t.TryGetComp<CompSamokCreep>()).Where(c=>c!=null && c.parent.Spawned).ToList();
        }
        private bool Passable(IntVec3 cell,CompSamokCreep source)
        {
            if(!cell.InBounds(map) || !source.Bounds.Contains(cell) || suppressed.Contains(cell))return false;
            var terrain=cell.GetTerrain(map);
            if(terrain==null || terrain.IsWater || terrain.passability==Traversability.Impassable)return false;
            var building=cell.GetEdifice(map);
            if(building==null || building==source.parent)return true;
            if(building is Building_BaseBondageBed bed && bed.GetComp<CompRespawnBed>()?.parentTree==source.parent)return true;
            if(building is Building_Door door)return door.Open;
            return building.def.passability!=Traversability.Impassable;
        }
        public HashSet<IntVec3> Reachable(CompSamokCreep source)
        {
            var result=new HashSet<IntVec3>();var queue=new Queue<IntVec3>();
            if(!source.parent.Spawned || !Passable(source.parent.Position,source))return result;
            result.Add(source.parent.Position);queue.Enqueue(source.parent.Position);
            while(queue.Count>0)
            {
                var cell=queue.Dequeue();
                foreach(var offset in GenAdj.CardinalDirections)
                {var next=cell+offset;if(!result.Contains(next) && Passable(next,source)){result.Add(next);queue.Enqueue(next);}}
            }
            return result;
        }
        private void Grow(IntVec3 cell)
        {
            if(Creep==null || previous.ContainsKey(cell))return;
            var top=cell.GetTerrain(map);
            if(top==Creep)return;
            previous[cell]=top;
            var under=map.terrainGrid.UnderTerrainAt(cell);
            if(under!=null)previousUnder[cell]=under;
            map.terrainGrid.SetTerrain(cell,Creep);
            // Native floor removal exposes exactly the preceding visible terrain.
            map.terrainGrid.SetUnderTerrain(cell,top);

        }
        private void Restore(IntVec3 cell,bool preventRegrowth)
        {
            if(!previous.TryGetValue(cell,out var terrain))return;
            
            if(cell.InBounds(map) && cell.GetTerrain(map)==Creep)map.terrainGrid.SetTerrain(cell,terrain);
            if(cell.InBounds(map) && cell.GetTerrain(map)==terrain)
            {
                previousUnder.TryGetValue(cell,out var under);
                if(under!=null)map.terrainGrid.SetUnderTerrain(cell,under);
                else if(map.terrainGrid.UnderTerrainAt(cell)!=null)
                {
                    // 1.7 SetUnderTerrain dereferences newTerr even when clearing a layer.
                    if(map.terrainGrid.UnderTerrainAt(cell).glowRadius>0f)map.glowGrid.DeregisterTerrain(cell);
                    UnderGrid(map.terrainGrid)[map.cellIndices.CellToIndex(cell)]=null;
                    map.events.Notify_TerrainChanged(cell);
                }
            }
            previous.Remove(cell);previousUnder.Remove(cell);
            if(preventRegrowth)suppressed.Add(cell);
        }
        public void Resume(CompSamokCreep source){suppressed.RemoveWhere(c=>source.Bounds.Contains(c));}
        public void Fill(CompSamokCreep source)
        {Resume(source);foreach(var cell in Reachable(source).OrderBy(c=>c.DistanceToSquared(source.parent.Position)))Grow(cell);GrowStandingDecor(source);UpdateBlessings();}
        public void Clear(CompSamokCreep source)
        {foreach(var cell in previous.Keys.Where(c=>source.Bounds.Contains(c)).ToList())Restore(cell,true);UpdateBlessings();}
        public bool HasActiveCreep(IntVec3 cell)
            =>cell.InBounds(map) && Creep!=null && cell.GetTerrain(map)==Creep;
        public static bool Protected(Pawn pawn)=>pawn?.Spawned==true && SamokBrideRules.IsBride(pawn) && pawn.Map.GetComponent<SamokCreepMap>().HasActiveCreep(pawn.Position);
        public override void MapComponentTick()
        {
            if(Find.TickManager.TicksGame%250!=0)return;
            if(!rootInstancesInitialized){
                var oldDef=DefDatabase<ThingDef>.GetNamedSilentFail("YR_SamokRoot");
                if(oldDef!=null)foreach(var old in map.listerThings.ThingsOfDef(oldDef).ToList())old.Destroy(DestroyMode.Vanish);
                rootInstancesInitialized=true;
            }
            var sources=Sources();var supported=new HashSet<IntVec3>();
            var regions=sources.ToDictionary(s=>s,s=>Reachable(s));
            foreach(var region in regions.Values)supported.UnionWith(region);
            foreach(var cell in previous.Keys.ToList())
                if(cell.GetTerrain(map)!=Creep)Restore(cell,true);
                else if(!supported.Contains(cell))Restore(cell,false);
            foreach(var source in sources)
            {
                if(!source.growEnabled)continue;
                Resume(source);
                GrowStandingDecor(source);
            }
            UpdateBlessings();
        }
        private void UpdateBlessings()
        {
            var def=DefDatabase<HediffDef>.GetNamedSilentFail("YR_SamokCreepBlessing");if(def==null)return;
            foreach(var pawn in map.mapPawns.AllPawnsSpawned.ToList())
            {
                var effect=pawn.health.hediffSet.GetFirstHediffOfDef(def);
                if(!Protected(pawn)){if(effect!=null)pawn.health.RemoveHediff(effect);continue;}
                if(effect==null)effect=pawn.health.AddHediff(def);
                bool blessed=SamokBlessingV48.IsBlessed(pawn);
                float severity=blessed?2f:1f;if(effect.Severity!=severity)effect.Severity=severity;
                pawn.health.hediffSet.hediffs.OfType<Hediff_Injury>().FirstOrDefault(h=>h.Severity>0f)?.Heal(blessed?8f:4f);
                var missing=pawn.health.hediffSet.GetMissingPartsCommonAncestors().FirstOrDefault();
                if(missing!=null)pawn.health.RestorePart(missing.Part);
            }
        }
    }
    public class Hediff_SamokCreep : HediffWithComps
    {public override bool ShouldRemove=>base.ShouldRemove || !SamokCreepMap.Protected(pawn);}
    [HarmonyPatch(typeof(Pawn_HealthTracker),nameof(Pawn_HealthTracker.ShouldBeDead))]
    internal static class SamokCreepDeathCheck
    {
        private static bool Prefix(Pawn ___pawn,ref bool __result)
        {if(!SamokCreepMap.Protected(___pawn))return true;__result=false;return false;}
    }
    [HarmonyPatch(typeof(Pawn),nameof(Pawn.Kill))]
    internal static class SamokCreepPreventDeath
    {private static bool Prefix(Pawn __instance)=>!SamokCreepMap.Protected(__instance);}
}



namespace YR_RJW {
 [HarmonyLib.HarmonyPatch(typeof(Verse.GenSpawn),nameof(Verse.GenSpawn.SpawningWipes))]
 internal static class RootRugOverlapV12 {
  internal static bool IsRoot(Verse.BuildableDef def)=>def?.defName=="YR_Tentacle_Rug_A"||def?.defName=="YR_Tentacle_Rug_B"||def?.defName=="YR_Tentacle_Rug_C"||def?.defName=="YR_SamokRoot";
  internal static bool BuildingOverRoot(Verse.BuildableDef building,Verse.BuildableDef root){
   var thing=building as Verse.ThingDef;
   thing=thing?.entityDefToBuild as Verse.ThingDef??thing;
   return IsRoot(root)&&thing?.category==Verse.ThingCategory.Building&&!IsRoot(thing);
  }
  private static bool Prefix(Verse.BuildableDef __0,Verse.BuildableDef __1,ref bool __result){
   if(BuildingOverRoot(__0,__1)){__result=false;return false;}
   if(!IsRoot(__0))return true;
   if(!IsRoot(__1)&&__1?.defName!="Building_Tentacle_Tree"&&__1?.defName!="YR_Building_Tentacle"&&__1?.defName!="YR_SamokLeylineCore")return true;
   __result=false;return false;
  }
 }
 [HarmonyLib.HarmonyPatch(typeof(RimWorld.GenConstruct),nameof(RimWorld.GenConstruct.CanPlaceBlueprintOver))]
 internal static class RootRugBlueprintPlacement {
  private static bool Prefix(Verse.BuildableDef newDef,Verse.ThingDef oldDef,ref bool __result){
   if(!RootRugOverlapV12.BuildingOverRoot(newDef,oldDef))return true;
   __result=true;return false;
  }
 }
 [HarmonyLib.HarmonyPatch(typeof(RimWorld.GenConstruct),nameof(RimWorld.GenConstruct.BlocksConstruction))]
 internal static class RootRugConstruction {
  private static bool Prefix(Verse.Thing constructible,Verse.Thing t,ref bool __result){
   if(!RootRugOverlapV12.BuildingOverRoot(constructible?.def,t?.def))return true;
   __result=false;return false;
  }
 }
}

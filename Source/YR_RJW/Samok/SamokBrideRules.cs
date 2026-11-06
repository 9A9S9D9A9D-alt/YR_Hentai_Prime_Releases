using System;
using System.Linq;
using RimWorld;
using Verse;
using YR_Hentai_Prime_AnimationBed;

namespace YR_RJW
{
    public static class SamokBrideRules
    {
        public static bool IsBride(Pawn pawn,bool allowDead=false)
        {
            if(pawn?.RaceProps.Humanlike!=true || !UnsupportedRacesV8.Allows(pawn.def) || pawn.ageTracker==null || (!allowDead && pawn.Dead))return false;
            if(pawn.DevelopmentalStage!=DevelopmentalStage.Adult ||
                pawn.ageTracker.AgeBiologicalYearsFloat<Math.Max(18,YR_Hentai_Prime_AnimationBed_Mod.Settings?.minimumAge??18))return false;
            return pawn.story?.traits?.allTraits.Any(t=>t.def.defName=="YR_Tentalcle" || t.def.defName=="YR_Tentalcle_NonColonist" || t.def.defName=="YR_Tentalcle_Blessed" || t.def.defName=="YR_Tentalcle_NonColonist_Blessed")==true;
        }
    }
}

namespace YR_RJW {
 public sealed class RequestedRepairsV8 : Verse.GameComponent {
  private bool retired;
  private int cleanupFrames;
  public RequestedRepairsV8(Verse.Game game){}
  public override void GameComponentUpdate(){
   if(Verse.Current.ProgramState!=Verse.ProgramState.Playing)return;
   if(Verse.GenCommandLine.CommandLineArgPassed("yrpManualV6Scene")) {
    foreach(var map in Verse.Find.Maps)foreach(var pawn in map.mapPawns.AllPawnsSpawned.ToArray()){
     if(!pawn.RaceProps.Humanlike || pawn.Faction?.HostileTo(RimWorld.Faction.OfPlayer)==true){pawn.Destroy(Verse.DestroyMode.Vanish);continue;}
     if(pawn.needs?.food!=null)pawn.needs.food.CurLevelPercentage=1f;
     if(pawn.needs?.mood!=null)pawn.needs.mood.CurLevelPercentage=1f;
    }
    foreach(var map in Verse.Find.Maps)foreach(var thing in map.listerThings.AllThings.ToArray()){
     string name=thing.def.defName;
     if(name=="Bulbfreak"||name=="Toughspike"||name=="Trispike"||name=="Fingerspike"||name=="Dreadmeld"||name.IndexOf("Fleshmass",StringComparison.OrdinalIgnoreCase)>=0){
      Verse.Log.Message("[V8 REMOVED FLESH ENTITY] "+thing.ThingID);thing.Destroy(Verse.DestroyMode.Vanish);
     }
    }
    if(Verse.GenCommandLine.CommandLineArgPassed("yrpSaveFleshCleanupV8")){
     Verse.Find.TickManager.CurTimeSpeed=Verse.TimeSpeed.Paused;
     if(++cleanupFrames==30){
      Verse.GameDataSaveLoader.SaveGame("Autostart");
      Verse.GameDataSaveLoader.SaveGame("Yuran-V8-Clean-Baseline");
      Verse.GameDataSaveLoader.SaveGame("Yuran-V8-Manual-Test-Start");
      Verse.Log.Message("[V8 FLESH CLEANUP SAVED]");UnityEngine.Application.Quit();
     }
    }
   }
   if(!retired){
    if(Verse.GenCommandLine.CommandLineArgPassed("yrpManualV6Scene")) {
     var type=HarmonyLib.AccessTools.TypeByName("AllRaceRosterV8");
     var roster=type==null?null:HarmonyLib.AccessTools.Field(type,"Pawns")?.GetValue(null) as System.Collections.Generic.List<Verse.Pawn>;
     if(roster!=null)foreach(var pawn in roster.ToArray())if(pawn?.def.defName=="AT_TriJaw") {
      if(pawn.Spawned)pawn.DeSpawn();
      Verse.Find.WorldPawns.PassToWorld(pawn,RimWorld.Planet.PawnDiscardDecideMode.KeepForever);roster.Remove(pawn);
     }
    }
    retired=true;
   }
  }
  public override void GameComponentTick(){
   if(Verse.Find.TickManager.TicksGame%500!=0)return;
   var rooted=Verse.DefDatabase<Verse.ThingDef>.GetNamedSilentFail("YR_AP_Tentacle_Rooted");if(rooted==null)return;
   foreach(var map in Verse.Find.Maps)foreach(var pawn in map.mapPawns.AllPawnsSpawned) {
    if(!pawn.def.defName.StartsWith("Yuran_Race")||!SamokBrideRules.IsBride(pawn)||pawn.apparel==null)continue;
    if(pawn.apparel.WornApparel.Exists(a=>a.def==rooted))continue;
    var item=(RimWorld.Apparel)Verse.ThingMaker.MakeThing(rooted);
    pawn.apparel.Wear(item);pawn.apparel.Lock(item);
   }
  }
 }
}

namespace YR_RJW {
 [HarmonyLib.HarmonyPatch]
 internal static class PrivateTestNoFleshmassV8 {
  private static System.Collections.Generic.IEnumerable<System.Reflection.MethodBase> TargetMethods(){
   yield return HarmonyLib.AccessTools.Method(typeof(RimWorld.IncidentWorker),"CanFireNow");
   yield return HarmonyLib.AccessTools.Method(typeof(RimWorld.IncidentWorker),"TryExecute");
  }
  private static bool Prefix(RimWorld.IncidentWorker __instance,ref bool __result){
   if(!Verse.GenCommandLine.CommandLineArgPassed("yrpManualV6Scene"))return true;
   string name=__instance.def?.defName??"";
   __result=false;return false; // Dedicated presentation/test profiles do not run random incidents.
  }
 }
}

namespace YR_RJW {
 public class CompProperties_SamokService : Verse.CompProperties {
  public CompProperties_SamokService(){compClass=typeof(CompSamokService);}
 }
 public class CompSamokService : Verse.ThingComp {
  private Verse.Pawn servant;
  private int serviceTicks;
  private Building_Tentacle_Altar Tree=>parent as Building_Tentacle_Altar;
  private Building_BaseBondageBed Bed=>parent.TryGetComp<CompSpawnBed>()?.SpawnedBed;
  private RimWorld.CompRefuelable Gauge=>parent.TryGetComp<RimWorld.CompRefuelable>();
  private bool Mature=>parent.Spawned && parent.Faction==RimWorld.Faction.OfPlayer && Tree?.CorpseFulfilled==true &&
   (YR_Hentai_Prime_AnimationBed.YR_Hentai_Prime_AnimationBed_Mod.Settings?.enableSamok??true);
  public bool CanServe(Verse.Pawn pawn)=>Mature && (servant==null||servant==pawn) && SamokBrideRules.IsBride(pawn) && pawn.Spawned && pawn.Map==parent.Map &&
   (!pawn.Downed||Bed?.occupant==pawn) && !pawn.InMentalState && Bed?.Spawned==true && Bed.CoffinCorpse==null && (Bed.occupant==null||Bed.occupant==pawn);
  public bool AssignedTo(Verse.Pawn pawn)=>servant==pawn;
  public bool IsServing(Verse.Pawn pawn)=>servant==pawn && Mature && Bed?.occupant==pawn && pawn.CurrentBed()==Bed && pawn.CurJobDef?.defName=="YR_Job_SamokService";
  public static bool SuppliesEnergy(Verse.Pawn pawn)=>pawn?.CurrentBed()?.TryGetComp<CompRespawnBed>()?.parentTree?.TryGetComp<CompSamokService>()?.IsServing(pawn)==true;
  public void FinishService(Verse.Pawn pawn){
   if(servant==pawn)servant=null;
   if(Bed?.occupant==pawn)Bed.TryGetComp<CompRemoveEffectBondageBed>()?.DoEffect(pawn);
  }
  public override void PostExposeData(){
   base.PostExposeData();Verse.Scribe_References.Look(ref servant,"samokServicePawn");Verse.Scribe_Values.Look(ref serviceTicks,"samokServiceTicks",0);
  }
  public override string CompInspectStringExtra()=>servant==null?null:"봉사: "+servant.LabelShort+" · "+(serviceTicks*100/2500)+"%";
  public override void CompTick(){
   if(servant==null)return;
   if(!CanServe(servant)){Stop();return;}
   if(Bed.occupant!=servant||servant.CurrentBed()!=Bed){
    if(servant.CurJobDef?.defName!="YR_Job_SamokService")Stop();
    return;
   }
   if(servant.CurJobDef?.defName!="YR_Job_SamokService"){Begin(servant);return;}
   var gauge=Gauge;if(gauge==null||gauge.Fuel>=gauge.Props.fuelCapacity)return;
   if(++serviceTicks>=2500){gauge.Refuel(1f);serviceTicks-=2500;}
  }
  private void Begin(Verse.Pawn pawn){
   if(!CanServe(pawn))return;
   servant=pawn;
   if(Bed.occupant==pawn&&pawn.CurrentBed()==Bed&&pawn.CurJobDef?.defName=="YR_Job_SamokService")return;
   var job=Verse.JobMaker.MakeJob(Verse.DefDatabase<Verse.JobDef>.GetNamed("YR_Job_SamokService"),Bed);
   pawn.jobs.TryTakeOrderedJob(job,Verse.AI.JobTag.Misc);
  }
  private void Stop(){
   var old=servant;servant=null;
   if(old!=null&&Bed?.occupant==old)Bed.TryGetComp<CompRemoveEffectBondageBed>()?.DoEffect(old);
  }
  private void Produce(){
   if(!Mature||Gauge==null||Gauge.Fuel<10f)return;
   var def=Verse.DefDatabase<Verse.ThingDef>.GetNamedSilentFail("YR_SamokTentacleRaw");if(def==null)return;
   var item=Verse.ThingMaker.MakeThing(def);item.stackCount=75;
   if(Verse.GenPlace.TryPlaceThing(item,parent.Position,parent.Map,Verse.ThingPlaceMode.Near))Gauge.ConsumeFuel(10f);
   else {if(!item.Destroyed)item.Destroy(Verse.DestroyMode.Vanish);Verse.Messages.Message("촉수를 놓을 공간이 없습니다.",RimWorld.MessageTypeDefOf.RejectInput,false);}
  }
  public override System.Collections.Generic.IEnumerable<Verse.Gizmo> CompGetGizmosExtra(){
   if(!parent.Spawned||parent.Faction!=RimWorld.Faction.OfPlayer)yield break;
   var produce=new Verse.Command_Action{defaultLabel="사목촉수 생성",defaultDesc="봉헌 게이지 10을 소비하여 사목의 촉수 75개를 생성합니다.",icon=Verse.ThingDef.Named("YR_SamokTentacleRaw").uiIcon,action=Produce};
   if(!Mature)produce.Disable("사목의 성장 조건을 먼저 충족해야 합니다.");
   else if(Gauge==null||Gauge.Fuel<10f)produce.Disable("봉헌 게이지가 10 이상 필요합니다.");
   yield return produce;
   if(!Mature)yield break;
   yield return new Verse.Command_Action{defaultLabel="봉사",defaultDesc="심연의 신부가 기존 나무 입장 동작으로 들어갑니다. 봉사 1시간마다 봉헌 게이지 1을 얻습니다. 봉사 종료 전까지 고정되며 식량 게이지는 감소하지 않습니다. 게이지가 가득 차면 적립이 멈춥니다.",icon=Verse.ContentFinder<UnityEngine.Texture2D>.Get("Yuran/Icon/Wiggle"),
    action=()=>{
     var options=new System.Collections.Generic.List<Verse.FloatMenuOption>();
     foreach(var pawn in parent.Map.mapPawns.AllPawnsSpawned.Where(CanServe)){
      var captured=pawn;options.Add(new Verse.FloatMenuOption(captured.LabelShort,()=>Begin(captured)));
     }
     if(options.Count==0)options.Add(new Verse.FloatMenuOption("봉사 가능한 심연의 신부가 없습니다.",null));
     Verse.Find.WindowStack.Add(new Verse.FloatMenu(options));
    }};
   if(servant!=null)yield return new Verse.Command_Action{defaultLabel="봉사 종료",icon=RimWorld.TexCommand.Install,action=Stop};

  }
 }
}

namespace YR_RJW {
 [HarmonyLib.HarmonyPatch(typeof(RimWorld.Need_Food),"IsFrozen",HarmonyLib.MethodType.Getter)]
 internal static class SamokServiceFoodSupply {
  private static void Postfix(Verse.Pawn ___pawn,ref bool __result){if((Verse.GenCommandLine.CommandLineArgPassed("yrpManualV6Scene")&&___pawn?.RaceProps.Humanlike==true)||CompSamokService.SuppliesEnergy(___pawn))__result=true;}
 }
}

namespace YR_RJW {
 [HarmonyLib.HarmonyPatch(typeof(RimWorld.Need),"IsFrozen",HarmonyLib.MethodType.Getter)]
 internal static class PrivateTestMoodFreezeV13 {
  private static void Postfix(RimWorld.Need __instance,ref bool __result){if(__instance is RimWorld.Need_Mood && Verse.GenCommandLine.CommandLineArgPassed("yrpManualV6Scene"))__result=true;}
 }
 [HarmonyLib.HarmonyPatch]
 internal static class PrivateTestNoArrivalsV13 {
  private static System.Collections.Generic.IEnumerable<System.Reflection.MethodBase> TargetMethods(){
   yield return HarmonyLib.AccessTools.Method(typeof(RimWorld.Storyteller),"StorytellerTick");
   yield return HarmonyLib.AccessTools.Method(typeof(RimWorld.WildAnimalSpawner),"WildAnimalSpawnerTick");
  }
  private static bool Prefix()=>!Verse.GenCommandLine.CommandLineArgPassed("yrpManualV6Scene");
 }
}

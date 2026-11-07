using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace YR_RJW {
 public sealed class Hediff_CoccyxEscort:Hediff {
  public Pawn escort;
  public int liftStart;
  public float Lift=>Valid?Mathf.SmoothStep(0,1,(Find.TickManager.TicksGame-liftStart)/12f):0;
  public static Hediff_CoccyxEscort Get(Pawn pawn)=>pawn?.health?.hediffSet.GetFirstHediffOfDef(HediffDef.Named("YR_CoccyxEscort")) as Hediff_CoccyxEscort;
  public static bool IsHeld(Pawn pawn)=>Get(pawn)?.Valid==true;
  bool Valid=>escort?.Spawned==true&&!escort.Dead&&pawn?.Spawned==true&&pawn.Map==escort.Map;
  public override bool ShouldRemove=>base.ShouldRemove||!Valid;
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref liftStart,"coccyxEscortLiftStart");Scribe_References.Look(ref escort,"coccyxEscortOwner");}
 }

 public sealed partial class HediffComp_CoccyxCombat {
  const int EscortWindup=24;
  const int ExtractionTicks=2500,ExtractionYield=10;
  int extractReadyAt=-1,nextExtractDropAttempt;
  Pawn escortTarget;
  public bool splitMouthEscort;
  int escortGrabAt=-1,escortAnimationTicks=EscortWindup;
  IntVec3 lastEscortCell=IntVec3.Invalid,escortOffset=IntVec3.Invalid,lastLandingCell=IntVec3.Invalid;
  public Pawn EscortTarget=>escortTarget;
  public bool HasEscortGrip=>escortGrabAt<0&&escortTarget?.Spawned==true&&!escortTarget.Dead&&
   Hediff_CoccyxEscort.IsHeld(escortTarget)&&Hediff_CoccyxEscort.Get(escortTarget).escort==Pawn;
  public float EscortProgress=>escortGrabAt<0?1f:Mathf.Clamp01(1f-(escortGrabAt-Find.TickManager.TicksGame)/(float)escortAnimationTicks);
  public float ExtractionFill=>extractReadyAt<0?0f:
   Mathf.Clamp01(1f-(extractReadyAt-Find.TickManager.TicksGame)/(float)ExtractionTicks);
  void ExposeEscort(){ExposeSplitAnimation();Scribe_Values.Look(ref extractReadyAt,"coccyxExtractReadyAt",-1);Scribe_Values.Look(ref escortAnimationTicks,"escortAnimationTicks",EscortWindup);Scribe_Values.Look(ref splitMouthEscort,"coccyxSplitMouthEscort");Scribe_Values.Look(ref escortGrabAt,"coccyxEscortGrabAt",-1);Scribe_References.Look(ref escortTarget,"coccyxEscortTarget");Scribe_Values.Look(ref lastEscortCell,"coccyxEscortCell",IntVec3.Invalid);Scribe_Values.Look(ref escortOffset,"coccyxEscortOffset",IntVec3.Invalid);Scribe_Values.Look(ref lastLandingCell,"coccyxEscortLandingCell",IntVec3.Invalid);
   if(Scribe.mode==LoadSaveMode.PostLoadInit&&splitTargetKey==null&&escortTarget!=null)splitTargetKey=SplitTargetKey(escortTarget);
  }
  bool CanEscort(Pawn target)=>Pawn.Spawned&&Pawn.Drafted&&!Pawn.Downed&&!Pawn.Dead&&!Hediff_CoccyxEscort.IsHeld(Pawn)&&target!=null&&target!=Pawn&&target.Spawned&&!target.Dead&&target.Map==Pawn.Map&&!Hediff_CoccyxEscort.IsHeld(target)&&Pawn.Position.DistanceToSquared(target.Position)<=Range*Range&&GenSight.LineOfSight(Pawn.Position,target.Position,Pawn.Map);
  public bool BeginEscort(Pawn target){
   if(escortTarget!=null||!CanEscort(target))return false;
   SelectSplitTargetSettings(target);
   CancelTentacleAttacks();escortTarget=target;escortAnimationTicks=EscortDuration;escortGrabAt=Find.TickManager.TicksGame+escortAnimationTicks;lastEscortCell=Pawn.Position;
   return true;
  }
  void LockEscort(Pawn target){
   var targetComp=target.health.hediffSet.GetFirstHediffOfDef(HediffDef.Named("YR_CoccyxTentacleImplant"))?.TryGetComp<HediffComp_CoccyxCombat>();
   targetComp?.EndEscort();targetComp?.CancelTentacleAttacks();
   var hold=(Hediff_CoccyxEscort)HediffMaker.MakeHediff(HediffDef.Named("YR_CoccyxEscort"),target);hold.escort=Pawn;hold.liftStart=Find.TickManager.TicksGame;
   target.health.AddHediff(hold);lastEscortCell=Pawn.Position;escortOffset=target.Position-Pawn.Position;lastLandingCell=target.Position;escortGrabAt=-1;
   target.pather?.StopDead();target.stances?.CancelBusyStanceHard();
   if(target.CurJob!=null)target.jobs.EndCurrentJob(JobCondition.InterruptForced,false);
  }
  void CancelTentacleAttacks(){
   fluidOn=false;frenzyPending=false;frenzyStart=-1;jumpAim=IntVec3.Invalid;
   foreach(var limb in limbs){limb.started=-1;limb.target=null;limb.shotTick=-1;}
  }
  public void EndEscort(){
   extractReadyAt=-1;
   drawing.ReleaseSplitBubble();
   var target=escortTarget;var landing=lastLandingCell;
   escortTarget=null;escortGrabAt=-1;lastEscortCell=IntVec3.Invalid;escortOffset=IntVec3.Invalid;lastLandingCell=IntVec3.Invalid;
   var hold=Hediff_CoccyxEscort.Get(target);
   if(hold?.escort!=Pawn)return;
   if(target.Spawned&&!target.Position.Walkable(target.Map)){
    var map=target.Map;
    var nearest=GenRadial.RadialCellsAround(target.Position,4,true).Where(c=>c.InBounds(map)&&c.Walkable(map)&&!c.GetThingList(map).Any(t=>t is Pawn)).Cast<IntVec3?>().FirstOrDefault();
    if(nearest.HasValue)landing=nearest.Value;
    if(!landing.InBounds(map)||!landing.Walkable(map))landing=Pawn.Spawned&&Pawn.Map==map?Pawn.Position:IntVec3.Invalid;
    if(landing.InBounds(map)&&landing.Walkable(map)){target.Position=landing;target.Notify_Teleported(false,false);}
   }
   target.health.RemoveHediff(hold);
  }
  public bool TickEscort(int now){
   var target=escortTarget;if(target==null)return false;
   if(escortGrabAt>=0){
    if(!CanEscort(target)){EndEscort();return false;}
    if(now>=escortGrabAt)LockEscort(target);
    return true;
   }
   if(!Pawn.Spawned||Pawn.Dead||Pawn.Downed||!Pawn.Drafted||target.Dead||!target.Spawned||target.Map!=Pawn.Map||Hediff_CoccyxEscort.Get(target)?.escort!=Pawn){EndEscort();return false;}
   TickExtraction(now);
   var cell=Pawn.Position;var map=Pawn.Map;
   if(escortOffset==IntVec3.Invalid)escortOffset=target.Position-cell;
   if(cell==lastEscortCell)return true;
   lastEscortCell=cell;
   var desired=cell+escortOffset;
   desired.x=Mathf.Clamp(desired.x,0,map.Size.x-1);
   desired.z=Mathf.Clamp(desired.z,0,map.Size.z-1);
   if(target.Position!=desired){target.Position=desired;target.Notify_Teleported(false,false);}
   if(desired.Walkable(map))lastLandingCell=desired;
   return true;
  }
  void BeginExtraction(){
   if(splitMouthEscort&&HasEscortGrip&&extractReadyAt<0){
    extractReadyAt=Find.TickManager.TicksGame+ExtractionTicks;
    nextExtractDropAttempt=0;
   }
  }
  void TickExtraction(int now){
   if(!splitMouthEscort||extractReadyAt<0||now<extractReadyAt||now<nextExtractDropAttempt)return;
   var fluid=ThingMaker.MakeThing(ThingDef.Named("YR_BaekyunFluid"));
   fluid.stackCount=ExtractionYield;
   if(!GenPlace.TryPlaceThing(fluid,Pawn.Position,Pawn.Map,ThingPlaceMode.Near,out var placed)){
    fluid.Destroy();
    nextExtractDropAttempt=now+250;
    return;
   }
   extractReadyAt=-1;
   if(Pawn.Faction==Faction.OfPlayer)
    Messages.Message("백윤액 10개를 얻었습니다.",placed??Pawn,MessageTypeDefOf.PositiveEvent,false);
  }
  Gizmo ExtractionCommand(Texture2D icon){
   var command=new Command_Action{
    defaultLabel=extractReadyAt<0?"백윤액 착취":$"백윤액 착취 중 · {ExtractionFill:P0}",
    defaultDesc="스플릿 마우스로 대상을 붙잡은 채 1시간을 유지하면 백윤액 10개가 나옵니다. 도중에 내려놓으면 중단됩니다.",
    icon=icon,action=BeginExtraction
   };
   if(extractReadyAt>=0)command.Disable(ExtractionFill>=1f?"배출 공간 대기 중입니다.":"이미 진행 중입니다.");
   return command;
  }
  IEnumerable<Gizmo> EscortGizmos(){
   var icon=ContentFinder<Texture2D>.Get("Yuran/UI/Commands/TentacleFocus");
   if(HasEscortGrip){
    if(splitMouthEscort)yield return ExtractionCommand(ThingDef.Named("YR_BaekyunFluid").uiIcon);
    yield return SplitAnimationCommand();
   }
   var style=new Command_Action{defaultLabel=splitMouthEscort?"자진입대 · 스플릿 마우스":"자진입대 · 6가닥 감기",defaultDesc="눌러 붙잡는 형태를 전환합니다. 스플릿 마우스는 굵어진 한 가닥의 중앙 기관으로 고정하고, 양쪽 입술로 어깨 아래를 감쌉니다.",icon=icon,action=()=>splitMouthEscort=!splitMouthEscort};
   if(escortTarget!=null)style.Disable("대상을 내려놓은 뒤 형태를 바꿀 수 있습니다.");
   yield return style;
   if(escortTarget!=null){yield return new Command_Action{defaultLabel="자진입대 해제",defaultDesc="촉수에서 대상을 내려놓습니다.",icon=icon,action=EndEscort};yield break;}
   var command=new CoccyxRangeTarget{
    origin=Pawn,radius=Range,defaultLabel="촉수 자진입대",defaultDesc="대상 하나를 지정해 붙잡고 함께 이동합니다. 유지 중 대상은 이동하거나 공격할 수 없으며, 시전자의 촉수 공격도 잠깁니다.",icon=icon,
    targetingParams=new TargetingParameters{canTargetPawns=true,canTargetAnimals=true,canTargetHumans=true,canTargetMechs=true,canTargetBuildings=false,validator=t=>CanEscort(t.Thing as Pawn)},
    action=t=>BeginEscort(t.Pawn)
   };
   if(!Pawn.Drafted||Pawn.Downed||Hediff_CoccyxEscort.IsHeld(Pawn))command.Disable("집결 상태에서 사용할 수 있습니다.");
   yield return command;
  }
 }

 [HarmonyPatch(typeof(PawnRenderer),"GetDrawParms")]
 static class CoccyxEscortLiftDrawing {
  static void Postfix(Pawn ___pawn,ref PawnDrawParms __result){
   if(__result.Portrait)return;
   var hold=Hediff_CoccyxEscort.Get(___pawn);
   float lift=hold?.Lift??0;
   if(lift<=0)return;
   var owner=hold.escort?.health?.hediffSet.GetFirstHediffOfDef(HediffDef.Named("YR_CoccyxTentacleImplant"))?.TryGetComp<HediffComp_CoccyxCombat>();
   if(___pawn.Downed){
    // The grip lifts the displayed pawn upright without changing their health or job.
    var position=new Vector3(__result.matrix.m03,__result.matrix.m13,__result.matrix.m23);
    __result.matrix=Matrix4x4.TRS(position,Quaternion.identity,Vector3.one);
    __result.posture=PawnPosture.Standing;
    __result.crawling=false;
    __result.facing=Rot4.South;
   }
   if(owner?.splitMouthEscort==true){
    __result.facing=Rot4.South;
    var pose=CompServiceSacInterior.PawnPoseTransform((Find.TickManager.TicksGame/6)%6,Vector3.zero,1);
    __result.matrix.m23+=pose.m23;
   }
   __result.matrix.m13=Mathf.Lerp(__result.matrix.m13,AltitudeLayer.MoteOverhead.AltitudeFor()-.012f,lift);
   __result.matrix.m23+=.22f*lift;
  }
 }
 [HarmonyPatch(typeof(Pawn_PathFollower),"StartPath")]
 static class CoccyxEscortNoPath {static bool Prefix(Pawn ___pawn)=>!Hediff_CoccyxEscort.IsHeld(___pawn);}
 [HarmonyPatch(typeof(Pawn_JobTracker),"TryFindAndStartJob")]
 static class CoccyxEscortNoJobSearch {static bool Prefix(Pawn ___pawn)=>!Hediff_CoccyxEscort.IsHeld(___pawn);}
 [HarmonyPatch(typeof(Pawn_JobTracker),"StartJob")]
 static class CoccyxEscortNoJob {static bool Prefix(Pawn ___pawn)=>!Hediff_CoccyxEscort.IsHeld(___pawn);}
 [HarmonyPatch(typeof(Verb),"TryStartCastOn",new[]{typeof(LocalTargetInfo),typeof(LocalTargetInfo),typeof(bool),typeof(bool),typeof(bool),typeof(bool)})]
 static class CoccyxEscortNoCast {static bool Prefix(Verb __instance)=>!Hediff_CoccyxEscort.IsHeld(__instance.CasterPawn);}
 [HarmonyPatch(typeof(Verb),"TryCastNextBurstShot")]
 static class CoccyxEscortNoBurst {
  static bool Prefix(Verb __instance){if(!Hediff_CoccyxEscort.IsHeld(__instance.CasterPawn))return true;__instance.Reset();return false;}
 }
}

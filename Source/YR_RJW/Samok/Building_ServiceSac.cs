using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using YR_Hentai_Prime_AnimationBed;

namespace YR_RJW {
 // A self-contained biological resource processor. No legacy adult-facility jobs/render patches.
 public partial class Building_ServiceSac : Building_Enterable {
  private readonly ServiceSacCycle cycle=new ServiceSacCycle();
  private int pendingOutput, lastTick=-1,spillStartTick=-1,spillUnits;
  private Material spillMat,transitionPawnMat;
  private bool riskAtEntry,completionApplied;
  private Material openMat, dirtyOpenMat, closedMat, puddleMat;
  private const string Art="Yuran/Things/Building/ServiceSac/";
  public override Vector3 PawnDrawOffset => Vector3.zero;
  internal Pawn InteriorPawn => Occupant;
  internal bool InteriorWorking => cycle.Phase==ServiceSacPhase.Working;
  internal bool InteriorVisible => cycle.Phase==ServiceSacPhase.Working || cycle.Phase==ServiceSacPhase.Opening;
  internal float InteriorLiquidLevel => cycle.LiquidLevel;
  internal ServiceSacPhase AudioPhase => cycle.Phase;
  public override bool IsContentsSuspended => true;
  private Pawn Occupant {
   get {for(int i=0;i<innerContainer.Count;i++)if(innerContainer[i] is Pawn p)return p;return null;}
  }
  public override AcceptanceReport CanAcceptPawn(Pawn p){
   if(!Spawned || Destroyed)return "시설을 사용할 수 없습니다.";
   if(Occupant!=null || cycle.Phase!=ServiceSacPhase.Idle || pendingOutput>0)return "현재 사용 중이거나 배출을 기다리고 있습니다.";
   if(p==null || !AdultEligibility.IsAdult(p) || !UnsupportedRacesV8.OrdinaryHumanlike(p) || !UnsupportedRacesV8.Allows(p.def))return "대상에 해당하는 성인 인간형만 이용할 수 있습니다.";
   if(p.Faction!=Faction.OfPlayer && !p.IsPrisonerOfColony)return "정착민 또는 식민지 포로만 지정할 수 있습니다.";
   if(selectedPawn!=null && selectedPawn!=p)return "다른 대상의 입장을 기다리고 있습니다.";
   return true;
  }
  protected override void SelectPawn(Pawn p){
   selectedPawn=p;
   p.jobs.TryTakeOrderedJob(JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("YR_EnterServiceSac"),this),JobTag.Misc);
  }
  private void RequestEntry(Pawn p){
   if(!CanAcceptPawn(p))return;
   if(!p.IsPrisonerOfColony && !p.Downed){SelectPawn(p);return;}
   var carrier=Map.mapPawns.FreeColonistsSpawned.FirstOrDefault(w=>!w.Downed && !w.Drafted && w.jobs!=null &&
    (w.workSettings?.WorkIsActive(WorkTypeDefOf.Warden) ?? false) && w.CanReserveAndReach(p,PathEndMode.OnCell,Danger.Some) &&
    w.CanReserveAndReach(this,PathEndMode.InteractionCell,Danger.Some));
   if(carrier==null){Messages.Message("호송할 수 있는 정착민이 없습니다.",this,MessageTypeDefOf.RejectInput,false);return;}
   selectedPawn=p;
   var transport=JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("YR_CarryToServiceSac"),this,p);
   transport.count=1;
   if(!carrier.jobs.TryTakeOrderedJob(transport,JobTag.Misc)){
    selectedPawn=null;
    Messages.Message("호송 명령을 시작하지 못했습니다. 교도관의 현재 작업을 확인하세요.",this,MessageTypeDefOf.RejectInput,false);
   }
  }
  public override void TryAcceptPawn(Pawn p){
   if(!CanAcceptPawn(p))return;
   Map oldMap=p.Map;IntVec3 oldPosition=p.Position;bool wasSpawned=p.Spawned;
   p.DeSpawnOrDeselect();
   if(!innerContainer.TryAddOrTransfer(p)){
    if(wasSpawned && !p.Spawned && p.ParentHolder==null)GenSpawn.Spawn(p,oldPosition,oldMap);
    return;
   }
   riskAtEntry=!SamokBrideRules.IsBride(p) && ServiceSacEffects.HasExhaustion(p);completionApplied=false;
   selectedPawn=p;cycle.Begin();startTick=lastTick=Find.TickManager.TicksGame;
  }
  protected override void Tick(){
   base.Tick();
   if(!Spawned)return;
   int now=Find.TickManager.TicksGame;
   if(Occupant!=null){
    if(cycle.Phase==ServiceSacPhase.Idle){TryRelease();}
    else{
     int delta=lastTick<0?1:Math.Max(0,now-lastTick);
     pendingOutput+=cycle.Advance(delta);
     lastTick=now;
     if(cycle.Phase==ServiceSacPhase.Idle)TryRelease();
    }
   }else if(cycle.Phase!=ServiceSacPhase.Idle){cycle.Cancel();cycle.Advance(ServiceSacCycle.TransitionDuration);startTick=-1;selectedPawn=null;}
   if(cycle.Phase==ServiceSacPhase.Opening && spillStartTick<startTick && cycle.ProducedUnits>0){spillStartTick=now;spillUnits=cycle.ProducedUnits;floorResidueMade=false;}
   TickResidue();
   if(pendingOutput>0 && this.IsHashIntervalTick(60))FlushOutput();
   if(Occupant==null && cycle.Phase==ServiceSacPhase.Idle && innerContainer.Count>0 && this.IsHashIntervalTick(60))DropProducts();
  }
  private void FlushOutput(){
   if(pendingOutput<=0)return;
   var resource=DefDatabase<ThingDef>.GetNamedSilentFail("YR_BaekyunFluid");
   if(resource==null)return;
   while(pendingOutput>0){
    Thing product=ThingMaker.MakeThing(resource);product.stackCount=Math.Min(pendingOutput,resource.stackLimit);
    int count=product.stackCount;
    if(!innerContainer.TryAdd(product)){product.Destroy();break;}
    pendingOutput-=count;
   }
   DropProducts();
  }
  private void DropProducts(){
   foreach(var t in innerContainer.Where(t=>!(t is Pawn) && !(t is Corpse)).ToArray())
    innerContainer.TryDrop(t,InteractionCell,Map,ThingPlaceMode.Near,out var _);
  }
  private void TryRelease(){
   if(!Spawned)return;
   FlushOutput();
   foreach(var t in innerContainer.Where(t=>t is Pawn || t is Corpse).ToArray()){
    if(innerContainer.TryDrop(t,InteractionCell,Map,ThingPlaceMode.Near,out var _) && t is Pawn p && !completionApplied && cycle.WorkTicks>=ServiceSacCycle.Duration){
     completionApplied=true;ServiceSacEffects.Complete(p,riskAtEntry);
    }
   }
   if(Occupant==null){selectedPawn=null;startTick=lastTick=-1;}
  }
  public override void DeSpawn(DestroyMode mode=DestroyMode.Vanish){
   // Materialize earned credit before native DeSpawn drops the holder contents.
   if(Spawned){EnsureFloorResidue();FlushOutput();innerContainer.TryDropAll(Position,Map,ThingPlaceMode.Near);}
   // If an unusually blocked map refuses a drop, the holder remains attached to this object.
   if(spillMat!=null){UnityEngine.Object.Destroy(spillMat);spillMat=null;}
   if(transitionPawnMat!=null){UnityEngine.Object.Destroy(transitionPawnMat);transitionPawnMat=null;}
   base.DeSpawn(mode);
  }
  public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn p){
   foreach(var o in base.GetFloatMenuOptions(p))yield return o;
   var report=CanAcceptPawn(p);
   if(!report.Accepted){yield return new FloatMenuOption("봉사낭 입장 불가: "+report.Reason,null);yield break;}
   if(p.Downed || p.IsPrisonerOfColony)yield break;
   if(!p.CanReserveAndReach(this,PathEndMode.InteractionCell,Danger.Some)){yield return new FloatMenuOption("봉사낭 입장 불가: 경로 또는 예약 문제",null);yield break;}
   yield return new FloatMenuOption("봉사낭에 들어가기",()=>RequestEntry(p));
  }
  public override IEnumerable<Gizmo> GetGizmos(){
   foreach(var g in base.GetGizmos())yield return g;
   if(!Spawned)yield break;
   var entryCommand=new Command_Action {defaultLabel="이용 대상 지정",defaultDesc="이 봉사낭을 이용할 대상을 선택합니다. 정착민은 직접 입장하고, 식민지 포로는 현재 위치에서 교도관이 호송합니다.",icon=ContentFinder<Texture2D>.Get(Art+"Open"),action=()=>{
    var options=new List<FloatMenuOption>();
    foreach(var p in Map.mapPawns.AllPawnsSpawned){var target=p;if(CanAcceptPawn(p))options.Add(new FloatMenuOption(p.LabelShortCap,()=>RequestEntry(target)));}
    if(options.Count==0)options.Add(new FloatMenuOption("지정할 수 있는 대상이 없습니다.",null));
    Find.WindowStack.Add(new FloatMenu(options));
   }};
   if(Occupant!=null || cycle.Phase!=ServiceSacPhase.Idle || pendingOutput>0)
    entryCommand.Disable("현재 사용 중이거나 배출을 기다리고 있습니다.");
   yield return entryCommand;
   if(Occupant==null && cycle.Phase==ServiceSacPhase.Idle && selectedPawn!=null)
    yield return new Command_Action {defaultLabel="지정 취소",icon=TexCommand.ClearPrioritizedWork,action=()=>{selectedPawn=null;}};
   foreach(var g in ResidueGizmos())yield return g;
   if(Prefs.DevMode && Occupant!=null && cycle.Phase==ServiceSacPhase.Working)
    yield return new Command_Action {defaultLabel="개발자: 1시간 진행",icon=TexCommand.DesirePower,action=()=>{pendingOutput+=cycle.Advance(2500);FlushOutput();}};
  }
  public override string GetInspectString(){
   string text=base.GetInspectString();
   string state=cycle.Phase==ServiceSacPhase.Closing?"입구 닫는 중":cycle.Phase==ServiceSacPhase.Working?"백윤액 생성 중":cycle.Phase==ServiceSacPhase.Opening?"입구 여는 중":Occupant!=null?"퇴장 대기":"대기";
   return (text.NullOrEmpty()?"":text+"\n")+state+"\n진행: "+(cycle.WorkTicks/2500f).ToString("0.0")+" / 6시간\n이번 생산량: "+cycle.ProducedUnits+" / 150"+
    (Occupant!=null?"\n수용: "+Occupant.LabelShortCap:"")+(pendingOutput>0?"\n배출 대기: "+pendingOutput:"");
  }
  public override void ExposeData(){
   base.ExposeData();
   Scribe_Values.Look(ref cycle.Phase,"serviceSacPhase",ServiceSacPhase.Idle);
   Scribe_Values.Look(ref cycle.WorkTicks,"serviceSacWorkTicks");
   Scribe_Values.Look(ref cycle.TransitionTicks,"serviceSacTransitionTicks");
   Scribe_Values.Look(ref cycle.ProducedUnits,"serviceSacProducedUnits");
   Scribe_Values.Look(ref pendingOutput,"serviceSacPendingOutput");
   Scribe_Values.Look(ref lastTick,"serviceSacLastTick",-1);
   Scribe_Values.Look(ref spillStartTick,"serviceSacSpillStartTick",-1);
   Scribe_Values.Look(ref spillUnits,"serviceSacSpillUnits");
   Scribe_Values.Look(ref riskAtEntry,"serviceSacRiskAtEntry");
   Scribe_Values.Look(ref completionApplied,"serviceSacCompletionApplied");
   ExposeResidueData();
  }
  protected override void DrawAt(Vector3 drawLoc,bool flip=false){
   if(!Spawned || Position.Fogged(Map))return;
   // DrawAt runs in the main draw phase. No GraphicDatabase/material creation in ParallelPreDraw.
   if(openMat==null){
    openMat=MaterialPool.MatFrom(Art+"Open",ShaderDatabase.Cutout);
    dirtyOpenMat=MaterialPool.MatFrom(Art+"OpenResidue",ShaderDatabase.Cutout);
    closedMat=MaterialPool.MatFrom(Art+"Closed",ShaderDatabase.Cutout);
    puddleMat=MaterialPool.MatFrom("Yuran/Things/Filth/BaekyunPuddle",ShaderDatabase.Cutout);
   }
   bool active=cycle.Phase==ServiceSacPhase.Working;
   // Sixteen prebaked frames: eight body poses plus staggered painted droplet motion.
   int workingFrame=(Find.TickManager.TicksGame/18+thingIDNumber)%16;
   int fluidStage=cycle.FluidStage;
   float transition=cycle.TransitionTicks/(float)ServiceSacCycle.TransitionDuration;
   Quaternion rot=Rotation.AsQuat;
   Vector3 size=new Vector3(def.graphicData.drawSize.x,1f,def.graphicData.drawSize.y);
   drawLoc.y=AltitudeLayer.Building.AltitudeFor();
   var matrix=Matrix4x4.TRS(drawLoc,rot,size);
   if(cycle.Phase==ServiceSacPhase.Closing || cycle.Phase==ServiceSacPhase.Opening){
    // Back shell, occupant, then foreground closure: stable depth order.
    Graphics.DrawMesh(MeshPool.plane10,matrix,HasInteriorResidue?dirtyOpenMat:openMat,0);
    DrawTransitionPawn(drawLoc,rot,transition);
    int frame=cycle.Phase==ServiceSacPhase.Closing
     ?SamokSpriteFrames.ClosingFrame(cycle.TransitionTicks)
     :SamokSpriteFrames.TransitionFrame(cycle.TransitionTicks);
    var front=matrix;front.m13+=.02f;
    Graphics.DrawMesh(SamokSpriteFrames.CocoonFrames[frame],front,cycle.Phase==ServiceSacPhase.Closing?SamokSpriteFrames.ClosingMaterial:SamokSpriteFrames.OpeningMaterial,0);
   }else if(active){
    // Preserve body size and position while reserving space below for falling droplets.
    var workingLoc=drawLoc+rot*new Vector3(0,0,-24f*size.z/543f);
    var workingSize=new Vector3(size.x,1f,size.z*591f/543f);
    Graphics.DrawMesh(SamokSpriteFrames.WorkingFrames[workingFrame],Matrix4x4.TRS(workingLoc,rot,workingSize),SamokSpriteFrames.WorkingForStage(fluidStage),0);
   }else Graphics.DrawMesh(MeshPool.plane10,matrix,Occupant!=null?closedMat:HasInteriorResidue?dirtyOpenMat:openMat,0);
   // Body and exterior stains are already one image; only the separate ground puddle remains.
   if(fluidStage>=4 && (Occupant!=null || cycle.Phase!=ServiceSacPhase.Idle)){
    Vector3 ground=drawLoc+rot*new Vector3(0.5f,0f,-size.z*0.4f);ground.y=AltitudeLayer.FloorEmplacement.AltitudeFor();
    float pool=0.3f+(fluidStage-4)*0.3f;
    Graphics.DrawMesh(MeshPool.plane10,Matrix4x4.TRS(ground,rot,new Vector3(pool,1f,pool*0.7f)),puddleMat,0);
   }
   DrawSpill(drawLoc,rot,size);
   Comps_PostDraw();
  }
  private void DrawTransitionPawn(Vector3 origin,Quaternion rotation,float progress){
   if(Occupant==null || (cycle.Phase!=ServiceSacPhase.Closing && cycle.Phase!=ServiceSacPhase.Opening))return;
   bool entering=cycle.Phase==ServiceSacPhase.Closing;
   // Complete entry before advancing the closing frames; keep the occupant fixed afterward.
   float travel=entering?Mathf.SmoothStep(0f,1f,Mathf.Clamp01(cycle.TransitionTicks/(float)SamokSpriteFrames.EntryMoveTicks))
    :Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(0.30f,1f,progress));
   float outside=entering?1f-travel:travel;
   var texture=PortraitsCache.Get(Occupant,new Vector2(256,256),Rot4.South,new Vector3(0,0,-0.15f),1f,false,false,false,false);
   if(texture==null)return;
   if(transitionPawnMat==null)transitionPawnMat=new Material(ShaderDatabase.Cutout);
   transitionPawnMat.mainTexture=texture;
   Vector3 exit=InteractionCell.ToVector3Shifted()-DrawPos;exit.y=0;
   Vector3 loc=origin+exit*outside;loc.y=origin.y+0.009f;
   Graphics.DrawMesh(MeshPool.plane10,Matrix4x4.TRS(loc,Quaternion.identity,new Vector3(1.55f,1f,1.9f)),transitionPawnMat,0);
  }
  private void DrawSpill(Vector3 origin,Quaternion rotation,Vector3 size){
   if(spillStartTick<0 || spillUnits<=0)return;
   int age=Find.TickManager.TicksGame-spillStartTick;
   if(age<0 || (age>=120 && floorResidueMade))return;
   if(spillMat==null)spillMat=new Material(MaterialPool.MatFrom("Yuran/Things/Filth/BaekyunPuddle",ShaderDatabase.Transparent));
   float burst=Mathf.Clamp01(age/120f);
   float volume=Mathf.Sqrt(Mathf.Clamp01(spillUnits/150f));
   spillMat.color=new Color(1f,0.98f,0.92f,0.6f);
   Vector3 pool=origin+rotation*new Vector3(0,0,-size.z*0.42f-0.5f-burst*0.45f);pool.y=AltitudeLayer.FloorEmplacement.AltitudeFor()+0.005f;
   float width=(0.5f+1.8f*burst)*volume;
   Graphics.DrawMesh(MeshPool.plane10,Matrix4x4.TRS(pool,rotation,new Vector3(width,1,width*0.75f)),spillMat,0);
   if(age<120){
    Vector3 flow=origin+rotation*new Vector3(0,0,-size.z*0.35f-burst*0.55f);flow.y=pool.y+0.003f;
    Graphics.DrawMesh(MeshPool.plane10,Matrix4x4.TRS(flow,rotation,new Vector3((0.35f+0.4f*Mathf.Sin(burst*Mathf.PI))*volume,1,0.5f+1.4f*burst)),spillMat,0);
   }
  }

 }
 public class JobDriver_EnterServiceSac : JobDriver_EnterBuilding {
  protected override IEnumerable<Toil> MakeNewToils(){
   this.FailOn(()=>!(job.targetA.Thing is Building_ServiceSac b) || b.SelectedPawn!=pawn);
   foreach(var toil in base.MakeNewToils())yield return toil;
  }
 }
 public class JobDriver_CarryToServiceSac : JobDriver_CarryToBuilding {
  protected override IEnumerable<Toil> MakeNewToils(){
   this.FailOn(()=>!(job.targetA.Thing is Building_ServiceSac b) || b.SelectedPawn!=job.targetB.Pawn);
   foreach(var toil in base.MakeNewToils())yield return toil;
  }
  public override bool TryMakePreToilReservations(bool errorOnFailed){
   return pawn.Reserve(job.targetA,job,1,-1,null,errorOnFailed) && base.TryMakePreToilReservations(errorOnFailed);
  }
 }
}

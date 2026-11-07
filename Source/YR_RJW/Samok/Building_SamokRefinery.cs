using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
namespace YR_RJW {
 public static class SamokHumanlikeMeat {
  private static HashSet<ThingDef> defs;
  public static IEnumerable<ThingDef> All=>defs??=new HashSet<ThingDef>(DefDatabase<ThingDef>.AllDefsListForReading.Where(d=>d.race?.Humanlike==true&&d.race.meatDef!=null).Select(d=>d.race.meatDef).Where(d=>d.ingestible!=null&&(d.ingestible.foodType&FoodTypeFlags.Meat)!=0));
  public static bool Allows(ThingDef def){_ = All;return def!=null&&defs.Contains(def);}
 }
 public class Building_SamokRefinery : Building_WorkTable,IThingHolder {
  private ThingOwner<Thing> contents;
  private readonly RefineryProgress progress=new RefineryProgress();
  private int mode,lastProcessTick=-1,pendingOutput;
  public bool animationEnabled=true;
  public bool autoSupplyEnabled=true;
  public bool AllowsSupply(bool forced)=>autoSupplyEnabled || forced;
  private Material idleMat,activeMat;
  public Building_SamokRefinery(){contents=new ThingOwner<Thing>(this);}
  public ThingDef InputDef=>ThingDef.Named(mode==1?"Meat_Human":"YR_BaekyunFluid");
  public bool Accepts(ThingDef input)=>mode==1?SamokHumanlikeMeat.Allows(input):input==InputDef;
  public string InputLabel=>mode==1?"인간형 고기":InputDef.label;
  public ThingDef OutputDef=>ThingDef.Named(mode==0?"YR_Baekgyojil":mode==1?"YR_PulsatingTissue":HediffComp_CoccyxCombat.CartridgeDef);
  public int Ratio=>mode==0?3:mode==1?2:10;
  public int InputCount=>contents.Where(t=>Accepts(t.def)).Sum(t=>t.stackCount);
  public int SpaceLeft=>Math.Max(0,200-InputCount);
  public bool Processing=>InputCount>=Ratio;
  public ThingOwner GetDirectlyHeldThings()=>contents;
  public void GetChildHolders(List<IThingHolder> outChildren)=>ThingOwnerUtility.AppendThingHoldersFromThings(outChildren,contents);
  public override void SpawnSetup(Map map,bool respawningAfterLoad){base.SpawnSetup(map,respawningAfterLoad);if(!respawningAfterLoad || lastProcessTick<0)lastProcessTick=Find.TickManager.TicksGame;}
  public int AcceptInput(Thing t){
   if(t==null || !Accepts(t.def) || t.holdingOwner==contents)return 0;
   SettleProgress();if(SpaceLeft<=0)return 0;
   return contents.TryAddOrTransfer(t,Math.Min(SpaceLeft,t.stackCount));
  }
  public bool SelectRecipe(int choice){
   if(choice<0 || choice>2)return false;if(choice==mode)return true;
   SettleProgress();if(contents.Count>0 || pendingOutput>0)return false;
   mode=choice;progress.Credit=0;progress.BatchElapsed=0;return true;
  }
  public void ProcessElapsed(int delta){
   if(delta<=0)return;
   int made=progress.AdvanceHourly(delta,InputCount,Ratio);
   int remaining=made*Ratio;
   foreach(var t in contents.Where(t=>Accepts(t.def)).ToArray()){
    if(remaining<=0)break;int take=Math.Min(remaining,t.stackCount);contents.Take(t,take).Destroy();remaining-=take;
   }
   pendingOutput+=made;
   FlushOutput();
  }
  private void FlushOutput(){
   while(pendingOutput>0){
    var t=ThingMaker.MakeThing(OutputDef);t.stackCount=Math.Min(pendingOutput,OutputDef.stackLimit);int count=t.stackCount;
    if(!contents.TryAdd(t)){t.Destroy();break;}pendingOutput-=count;
   }
   if(!Spawned)return;
   foreach(var t in contents.Where(t=>!Accepts(t.def)).ToArray())contents.TryDrop(t,InteractionCell,Map,ThingPlaceMode.Near,out _);
  }
  private void SettleProgress(){
   if(!Spawned)return;
   int now=Find.TickManager.TicksGame;int delta=lastProcessTick<0?250:Math.Max(0,now-lastProcessTick);lastProcessTick=now;
   ProcessElapsed(delta);
  }
  public override void TickRare(){base.TickRare();SettleProgress();}
  public void EjectContents(){SettleProgress();FlushOutput();if(Spawned)contents.TryDropAll(InteractionCell,Map,ThingPlaceMode.Near);progress.Credit=0;progress.BatchElapsed=0;}
  public override void DeSpawn(DestroyMode mode=DestroyMode.Vanish){
   if(Spawned)EjectContents();lastProcessTick=-1;base.DeSpawn(mode);
  }
  public override void ExposeData(){
   base.ExposeData();Scribe_Deep.Look(ref contents,"samokRefineryContents",this);
   Scribe_Values.Look(ref lastProcessTick,"samokRefineryLastTick",-1);
   Scribe_Values.Look(ref mode,"samokRefineryRecipe");Scribe_Values.Look(ref progress.Credit,"samokRefineryWork");
   Scribe_Values.Look(ref progress.BatchElapsed,"samokRefineryBatchElapsed");
   Scribe_Values.Look(ref pendingOutput,"samokRefineryPendingOutput");Scribe_Values.Look(ref animationEnabled,"samokRefineryAnimation",true);
   Scribe_Values.Look(ref autoSupplyEnabled,"samokRefineryAutoSupply",true);
   if(Scribe.mode==LoadSaveMode.PostLoadInit){billStack??=new BillStack(this);contents??=new ThingOwner<Thing>(this);mode=Math.Max(0,Math.Min(2,mode));}
  }
  public override string GetInspectString(){
   string prefix=base.GetInspectString();
   return (string.IsNullOrWhiteSpace(prefix)?"":prefix.Trim()+"\n")+"가공: "+InputLabel+" → "+OutputDef.label+"\n원료: "+InputCount+" / 200\n처리 속도: 시간당 원료 35개 · 1시간마다 일괄 배출\n"+(Processing?"가공 중":"원료 대기")+"\n원료 자동 투입: "+(autoSupplyEnabled?"허용":"금지");
  }
  public override IEnumerable<Gizmo> GetGizmos(){
   foreach(var g in base.GetGizmos())yield return g;
   yield return new Command_Action {defaultLabel="가공 항목: "+OutputDef.label,icon=OutputDef.uiIcon,action=()=>{
    var list=new List<FloatMenuOption>();for(int i=0;i<3;i++){int choice=i;string label=i==0?"백윤액 3 → 백교질 1":i==1?"인간형 고기 2 → 맥동조직 1":"백윤액 10 → 카트리지 1";
     list.Add(new FloatMenuOption(label,()=>{if(!SelectRecipe(choice))Messages.Message("원료와 생산물을 먼저 비워야 가공 항목을 변경할 수 있습니다.",this,MessageTypeDefOf.RejectInput,false);}));}
    Find.WindowStack.Add(new FloatMenu(list));
   }};
   yield return new Command_Action {defaultLabel="내용물 꺼내기",icon=ContentFinder<Texture2D>.Get("Yuran/UI/Commands/EjectContents"),action=EjectContents};
   yield return new Command_Toggle {defaultLabel="원료 자동 투입",defaultDesc="끄면 정착민의 자동 원료 운반을 중단합니다. 이미 들어 있는 원료는 계속 가공하며, 직접 지시한 운반은 허용합니다.",icon=ContentFinder<Texture2D>.Get("Yuran/UI/Commands/AutoSupply"),isActive=()=>autoSupplyEnabled,toggleAction=()=>autoSupplyEnabled=!autoSupplyEnabled};
   yield return new Command_Toggle {defaultLabel="가공 애니메이션",defaultDesc="원료를 가공할 때만 내벽 수축과 영양액 움직임을 표시합니다. 꺼도 생산은 계속됩니다.",icon=ContentFinder<Texture2D>.Get("Yuran/UI/Commands/Animation"),isActive=()=>animationEnabled,toggleAction=()=>animationEnabled=!animationEnabled};
  }
  protected override void DrawAt(Vector3 loc,bool flip=false){
   if(!Spawned || Position.Fogged(Map))return;
   if(idleMat==null){idleMat=MaterialPool.MatFrom("Yuran/Things/Building/SamokRefinery/Texture",ShaderDatabase.Cutout);activeMat=MaterialPool.MatFrom("Yuran/Things/Building/SamokRefinery/Active",ShaderDatabase.Cutout);}
   bool active=animationEnabled && Processing;
   loc.y=AltitudeLayer.Building.AltitudeFor();
   var matrix=Matrix4x4.TRS(loc,Rotation.AsQuat,new Vector3(def.graphicData.drawSize.x,1,def.graphicData.drawSize.y));
   Graphics.DrawMesh(MeshPool.plane10,matrix,active?activeMat:idleMat,0);
   if(active){
    int frame=(Find.TickManager.TicksGame/10+thingIDNumber)%8;
    Vector3 opening=loc+new Vector3(0,.004f,def.graphicData.drawSize.y*.065f);
    Graphics.DrawMesh(SamokSpriteFrames.ApertureFrames[frame],Matrix4x4.TRS(opening,Quaternion.identity,new Vector3(def.graphicData.drawSize.x*.48f,1,def.graphicData.drawSize.y*.43f)),SamokSpriteFrames.ApertureMaterial,0);
   }
   Comps_PostDraw();
  }
 }
 public class WorkGiver_SupplySamokRefinery : WorkGiver_Scanner {
  public override ThingRequest PotentialWorkThingRequest=>ThingRequest.ForDef(ThingDef.Named("YR_SamokRefinery"));
  public override PathEndMode PathEndMode=>PathEndMode.Touch;
  public Thing FindInput(Pawn p,Building_SamokRefinery b)=>GenClosest.ClosestThingReachable(p.Position,p.Map,ThingRequest.ForGroup(ThingRequestGroup.HaulableEver),PathEndMode.ClosestTouch,TraverseParms.For(p),9999f,t=>b.Accepts(t.def) && !t.IsForbidden(p) && p.CanReserve(t));
  public override bool HasJobOnThing(Pawn p,Thing t,bool forced=false)=>t is Building_SamokRefinery b && b.AllowsSupply(forced) && b.SpaceLeft>0 && !b.IsForbidden(p) && !b.IsBurning() && p.CanReserveAndReach(b,PathEndMode.Touch,Danger.Some,1,-1,null,forced) && p.Map.designationManager.DesignationOn(b,DesignationDefOf.Deconstruct)==null && FindInput(p,b)!=null;
  public override Job JobOnThing(Pawn p,Thing t,bool forced=false){var b=t as Building_SamokRefinery;if(b==null || !b.AllowsSupply(forced))return null;var input=FindInput(p,b);if(input==null)return null;var job=JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("YR_SupplySamokRefinery"),b,input);job.count=Math.Min(b.SpaceLeft,input.stackCount);return job;}
 }
 public class JobDriver_SupplySamokRefinery : JobDriver {
  private Building_SamokRefinery Refinery=>job.targetA.Thing as Building_SamokRefinery;
  public override bool TryMakePreToilReservations(bool errorOnFailed)=>Refinery!=null && Refinery.AllowsSupply(job.playerForced) && pawn.Reserve(Refinery,job,1,-1,null,errorOnFailed) && pawn.Reserve(job.targetB,job,1,-1,null,errorOnFailed);
  protected override IEnumerable<Toil> MakeNewToils(){
   this.FailOnDespawnedNullOrForbidden(TargetIndex.A);this.FailOnBurningImmobile(TargetIndex.A);
   this.FailOn(()=>Refinery==null || !Refinery.AllowsSupply(job.playerForced) || Refinery.SpaceLeft<=0 || !Refinery.Accepts(job.targetB.Thing?.def));
   AddFinishAction(condition=>{var held=pawn.carryTracker.CarriedThing;if(held!=null)pawn.carryTracker.TryDropCarriedThing(pawn.Position,ThingPlaceMode.Near,out _);});
   yield return Toils_Goto.GotoThing(TargetIndex.B,PathEndMode.ClosestTouch).FailOnDespawnedNullOrForbidden(TargetIndex.B);
   yield return Toils_Haul.StartCarryThing(TargetIndex.B);
   yield return Toils_Goto.GotoThing(TargetIndex.A,PathEndMode.Touch);
   yield return Toils_General.Wait(60).FailOnCannotTouch(TargetIndex.A,PathEndMode.Touch);
   yield return Toils_General.Do(()=>{var held=pawn.carryTracker.CarriedThing;if(held!=null)Refinery.AcceptInput(held);});
  }
 }
}

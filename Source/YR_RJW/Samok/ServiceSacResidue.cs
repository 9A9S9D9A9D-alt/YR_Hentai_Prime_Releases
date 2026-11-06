using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
namespace YR_RJW {
 public partial class Building_ServiceSac {
  private int interiorResidueStage;
  private bool floorResidueMade;
  internal int InteriorResidueStage=>interiorResidueStage;
  public bool HasInteriorResidue=>interiorResidueStage>0;
  public bool CanCleanInterior=>Spawned && !Destroyed && Occupant==null && cycle.Phase==ServiceSacPhase.Idle && HasInteriorResidue;
  private void ExposeResidueData(){
   Scribe_Values.Look(ref interiorResidueStage,"serviceSacInteriorResidue",cycle.FluidStage);
   Scribe_Values.Look(ref floorResidueMade,"serviceSacFloorResidueMade");
  }
  private void TickResidue(){
   if(Occupant!=null && cycle.FluidStage>interiorResidueStage)interiorResidueStage=cycle.FluidStage;
   if(!floorResidueMade && spillStartTick>=0 && Find.TickManager.TicksGame-spillStartTick>=120 && this.IsHashIntervalTick(60))EnsureFloorResidue();
  }
  private void EnsureFloorResidue(){
   if(!Spawned || floorResidueMade || spillStartTick<0 || spillUnits<=0)return;
   var def=DefDatabase<ThingDef>.GetNamedSilentFail("YR_Filth_BaekyunResidue");if(def==null)return;
   int thickness=UnityEngine.Mathf.Clamp(spillUnits/25,1,6);
   foreach(var cell in GenRadial.RadialCellsAround(InteractionCell,2f,true)){
    if(!cell.InBounds(Map) || !cell.Standable(Map))continue;
    if(FilthMaker.TryMakeFilth(cell,Map,def,thickness,FilthSourceFlags.None,false)){floorResidueMade=true;break;}
   }
  }
  public void CleanInterior(){if(CanCleanInterior)interiorResidueStage=0;}
  private IEnumerable<Gizmo> ResidueGizmos(){
   if(!CanCleanInterior)yield break;
   yield return new Command_Action {defaultLabel="내부 청소",defaultDesc="폰이 내부에 남은 점액 흔적을 닦아냅니다. 수용 중에는 청소할 수 없습니다.",icon=TexCommand.ClearPrioritizedWork,action=()=>{
    var worker=new WorkGiver_CleanServiceSac();var options=new List<FloatMenuOption>();
    foreach(var p in Map.mapPawns.FreeColonistsSpawned.Where(p=>!p.Downed && !p.Drafted && !p.WorkTypeIsDisabled(WorkTypeDefOf.Cleaning))){
     var pawn=p;if(worker.HasJobOnThing(pawn,this,true))options.Add(new FloatMenuOption(pawn.LabelShortCap,()=>pawn.jobs.TryTakeOrderedJob(worker.JobOnThing(pawn,this,true),JobTag.Misc)));
    }
    if(options.Count==0)options.Add(new FloatMenuOption("청소할 수 있는 정착민이 없습니다.",null));
    Find.WindowStack.Add(new FloatMenu(options));
   }};
  }
 }
 public class WorkGiver_CleanServiceSac : WorkGiver_Scanner {
  public override ThingRequest PotentialWorkThingRequest=>ThingRequest.ForDef(ThingDef.Named("YR_ServiceSac"));
  public override PathEndMode PathEndMode=>PathEndMode.Touch;
  public override bool HasJobOnThing(Pawn p,Thing t,bool forced=false)=>t is Building_ServiceSac b && b.CanCleanInterior && !b.IsForbidden(p) && !b.IsBurning() && (forced || p.Map.areaManager.Home[b.Position]) && p.CanReserveAndReach(b,PathEndMode.Touch,Danger.Some,1,-1,null,forced);
  public override Job JobOnThing(Pawn p,Thing t,bool forced=false)=>JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("YR_CleanServiceSac"),t);
 }
 public class JobDriver_CleanServiceSac : JobDriver {
  private float work;
  private Building_ServiceSac Sac=>job.targetA.Thing as Building_ServiceSac;
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref work,"serviceSacCleaningWork");}
  public override bool TryMakePreToilReservations(bool errorOnFailed)=>Sac!=null && pawn.Reserve(Sac,job,1,-1,null,errorOnFailed);
  protected override IEnumerable<Toil> MakeNewToils(){
   this.FailOnDespawnedNullOrForbidden(TargetIndex.A);this.FailOn(()=>Sac==null || !Sac.CanCleanInterior);
   yield return Toils_Goto.GotoThing(TargetIndex.A,PathEndMode.Touch);
   var clean=ToilMaker.MakeToil("CleanServiceSacInterior");clean.defaultCompleteMode=ToilCompleteMode.Never;
   clean.tickIntervalAction=delta=>{work+=pawn.GetStatValue(StatDefOf.CleaningSpeed)*delta;if(work>=240f){Sac.CleanInterior();pawn.records?.Increment(RecordDefOf.MessesCleaned);EndJobWith(JobCondition.Succeeded);}};
   clean.FailOnCannotTouch(TargetIndex.A,PathEndMode.Touch);
   clean.WithEffect(EffecterDefOf.Clean,TargetIndex.A);clean.WithProgressBar(TargetIndex.A,()=>work/240f);clean.PlaySustainerOrSound(SoundDefOf.Interact_CleanFilth);
   yield return clean;
  }
 }
}

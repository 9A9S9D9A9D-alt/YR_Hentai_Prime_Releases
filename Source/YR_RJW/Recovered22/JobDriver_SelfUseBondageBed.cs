using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace YR_RJW
{
    public class JobDriver_SelfUseBondageBed : JobDriver
    {
        private Building_BaseBondageBed Bed => job.GetTarget(TargetIndex.A).Thing as Building_BaseBondageBed;
        private bool handingOffToRest;
        private bool Valid => Bed?.Spawned == true && Bed.CoffinCorpse == null && Bed.occupant == null &&
            LegacyEligibility.Allows(pawn) && Bed.GetComp<CompUsableAdvancedBondageBed>()?.AllowsTarget(pawn) == true;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => Valid &&
            pawn.Reserve(Bed, job, 1, -1, null, errorOnFailed);

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => Bed == null || Bed.CoffinCorpse != null || !LegacyEligibility.Allows(pawn) ||
                (Bed.occupant != null && Bed.occupant != pawn) || (Bed.occupant == null && !Valid));
            AddFinishAction(condition =>
            {
                // Native Notify_TuckedIntoBed cleans up this driver before starting LayDown.
                // CurrentBed is still null during that successful handoff, not evidence of departure.
                if (!handingOffToRest && Bed?.occupant == pawn && pawn.CurrentBed() != Bed)
                    Bed.TryGetComp<CompRemoveEffectBondageBed>()?.DoEffect(pawn);
            });
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            var wait = Toils_General.Wait(100);
            wait.FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch);
            wait.WithProgressBarToilDelay(TargetIndex.A);
            yield return wait;
            yield return Toils_Reserve.Release(TargetIndex.A);
            var enter = ToilMaker.MakeToil("EnterRestored22Bed");
            enter.initAction = () =>
            {
                if (!Valid) { EndJobWith(JobCondition.Incompletable); return; }
                Bed.SetOccupant(pawn);
            };
            enter.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return enter;
            var tuck = Toils_Bed.TuckIntoBed(Bed, pawn, pawn);
            var nativeTuck = tuck.initAction;
            tuck.initAction = () =>
            {
                Building_BaseBondageBed bed = Bed;
                if (!RestUtility.CanUseBedNow(bed, pawn, checkSocialProperness: false))
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }
                handingOffToRest = true;
                try { nativeTuck(); }
                finally { handingOffToRest = false; }
                if (pawn.CurJobDef != JobDefOf.LayDown || pawn.CurJob.GetTarget(TargetIndex.A).Thing != bed)
                {
                    if (bed.occupant == pawn) bed.TryGetComp<CompRemoveEffectBondageBed>()?.DoEffect(pawn);
                    if (pawn.jobs.curDriver == this) EndJobWith(JobCondition.Incompletable);
                    return;
                }
                // As with the carried-use driver, effects start after the native bed job exists.
                bed.TryGetComp<CompEffectBondageBed>()?.DoEffect(pawn);
                MoteMaker.ThrowText(pawn.PositionHeld.ToVector3(), pawn.MapHeld, "YR_Bound".Translate(), 4f);
            };
            yield return tuck;
        }
    }
}

namespace YR_RJW {
 public class JobDriver_SamokService : RimWorld.JobDriver_LayDown {
  private Building_BaseBondageBed BoundBed=>Bed as Building_BaseBondageBed;
  private CompSamokService Service=>BoundBed?.TryGetComp<CompRespawnBed>()?.parentTree?.TryGetComp<CompSamokService>();
  public override bool LookForOtherJobs=>false;
  public override bool CanSleep=>false;
  public override bool TryMakePreToilReservations(bool errorOnFailed)=>Service?.CanServe(pawn)==true && base.TryMakePreToilReservations(errorOnFailed);
  protected override System.Collections.Generic.IEnumerable<Verse.AI.Toil> MakeNewToils(){
   this.FailOnDespawnedOrNull(Verse.AI.TargetIndex.A);
   this.FailOn(()=>Service?.AssignedTo(pawn)!=true || Service.CanServe(pawn)!=true);
   AddFinishAction(condition=>Service?.FinishService(pawn));
   yield return RimWorld.Toils_Bed.GotoBed(Verse.AI.TargetIndex.A);
   yield return Verse.AI.Toils_General.Wait(100);
   var bind=Verse.ToilMaker.MakeToil("EnterSamokService");
   bind.initAction=()=>BoundBed.SetOccupant(pawn);bind.defaultCompleteMode=Verse.AI.ToilCompleteMode.Instant;yield return bind;
   var lay=RimWorld.Toils_LayDown.LayDown(Verse.AI.TargetIndex.A,true,false,false,true);
   var initialize=lay.initAction;
   lay.initAction=()=>{initialize();if(BoundBed?.occupant==pawn && pawn.CurrentBed()==BoundBed)BoundBed.TryGetComp<CompEffectBondageBed>()?.DoEffect(pawn);};
   yield return lay;
  }
 }
}

using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace YR_RJW
{
    public class JobDriver_ReleaseBondageBed : JobDriver_UseItem
    {
        protected Thing Thing => job.GetTarget(TargetIndex.A).Thing;

        protected Thing Target => job.GetTarget(TargetIndex.B).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(Target, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.A);
            this.FailOnDestroyedOrNull(TargetIndex.B);
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOnAggroMentalStateAndHostile(TargetIndex.B);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch).FailOnForbidden(TargetIndex.A);
            Pawn prisoner = (Pawn)Target;
            if (prisoner.Dead)
            {
                yield break;
            }

            yield return Toils_General.WaitWith(TargetIndex.A, 60, true, true);
            yield return Toils_Reserve.Release(TargetIndex.B);
            yield return new Toil
            {
                initAction = delegate
                {
                    CompRemoveEffectBondageBed compRemoveEffectBondageBed = Thing?.TryGetComp<CompRemoveEffectBondageBed>();
                    if (compRemoveEffectBondageBed == null)
                    {
                        return;
                    }

                    compRemoveEffectBondageBed.DoEffect(prisoner);
                    MoteMaker.ThrowText(Target.PositionHeld.ToVector3(), Target.MapHeld, "YR_Release".Translate(),
                        4f);
                },
                defaultCompleteMode = ToilCompleteMode.Instant
            };
        }
    }
}

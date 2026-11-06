using RimWorld;
using System;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace YR_RJW
{
    public class JobDriver_UseBondageBed : JobDriver_UseItem
    {
        protected Thing Thing => job.GetTarget(TargetIndex.A).Thing;

        protected Thing Target => job.GetTarget(TargetIndex.B).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return !(Thing is Building_BaseBondageBed bed && bed.CoffinCorpse != null) && LegacyEligibility.Allows(pawn) && LegacyEligibility.Allows(Target as Pawn) && pawn.Reserve(Thing, job, 1, -1, null, errorOnFailed) &&
            pawn.Reserve(Target, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => !LegacyEligibility.Allows(pawn) || !LegacyEligibility.Allows(Target as Pawn));
            this.FailOn(() => Thing is Building_BaseBondageBed bed && bed.CoffinCorpse != null);
            this.FailOnDestroyedOrNull(TargetIndex.A);
            this.FailOnDestroyedOrNull(TargetIndex.B);
            this.FailOnAggroMentalStateAndHostile(TargetIndex.B);
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch);
            yield return Toils_Haul.StartCarryThing(TargetIndex.B);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch).FailOnForbidden(TargetIndex.A);
            Pawn prisoner = (Pawn)Target;
            if (prisoner.Dead)
            {
                yield break;
            }

            yield return Toils_General.WaitWith(TargetIndex.A, 60, true, true);
            yield return Toils_Reserve.Release(TargetIndex.A);
            yield return new Toil
            {
                initAction = delegate
                {
                    if (!Thing.Destroyed)
                    {
                        Building_BaseBondageBed building_BondageBed = (Building_BaseBondageBed)Thing ??
                                                  throw new Exception("cant find Building_BondageBed");
                        building_BondageBed.SetOccupant(prisoner);
                    }
                    else
                    {
                        pawn.jobs.EndCurrentJob(JobCondition.Incompletable);
                    }
                },
                defaultCompleteMode = ToilCompleteMode.Instant
            };
            yield return Toils_Bed.TuckIntoBed((Building_BaseBondageBed)Thing, pawn, prisoner);
            yield return new Toil
            {
                initAction = delegate
                {
                    CompEffectBondageBed compEffectBondageBed = Thing?.TryGetComp<CompEffectBondageBed>();
                    if (compEffectBondageBed == null)
                    {
                        return;
                    }

                    compEffectBondageBed.DoEffect(prisoner);
                    MoteMaker.ThrowText(Target.PositionHeld.ToVector3(), Target.MapHeld, "YR_Bound".Translate(),
                        4f);
                },
                defaultCompleteMode = ToilCompleteMode.Instant
            };
        }
    }
}

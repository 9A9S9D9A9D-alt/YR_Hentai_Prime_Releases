using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace YR_RJW
{
    public class MimicJobDriver_CaptureDownedPawn : JobDriver
    {
        protected Pawn Takee
        {
            get
            {
                return (Pawn)job.GetTarget(TargetIndex.A).Thing;
            }
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(Takee, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.A);
            this.FailOnAggroMentalStateAndHostile(TargetIndex.A);

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch).FailOnDespawnedNullOrForbidden(TargetIndex.A).FailOn(() => !Takee.Downed).FailOnSomeonePhysicallyInteracting(TargetIndex.A);

            Toil toil2 = ToilMaker.MakeToil("MakeNewToils");
            toil2.initAction = delegate ()
            {
                var comp = pawn.TryGetComp<MimicComp_Pawn>();
                if (comp != null && LegacyEligibility.Allows(Takee))
                {
                    comp.StartSpawnBed(Takee);
                }
            };
            toil2.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return toil2;
            yield break;
        }
        private const TargetIndex TakeeIndex = TargetIndex.A;
    }
}

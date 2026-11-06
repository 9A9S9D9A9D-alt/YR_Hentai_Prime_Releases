using RimWorld;
using System;
using System.Linq;
using Verse;
using Verse.AI;

namespace YR_RJW
{
    public class MimicJobGiver_Nearby : ThinkNode_JobGiver
    {
        public override ThinkNode DeepCopy(bool resolve = true)
        {
            MimicJobGiver_Nearby jobGiver_RapeNearby = (MimicJobGiver_Nearby)base.DeepCopy(resolve);
            jobGiver_RapeNearby.radius = radius;
            return jobGiver_RapeNearby;
        }

        protected override Job TryGiveJob(Pawn pawn)
        {

            Predicate<Thing> validator = delegate (Thing t)
            {
                Pawn pawn2 = (Pawn)t;
                return LegacyEligibility.Allows(pawn2) && pawn2.Downed && pawn2.RaceProps.Humanlike && !pawn2.InBed() && pawn.CanReserve(pawn2, 1, -1, null, false);
            };
            Pawn target = (Pawn)GenClosest.ClosestThingReachable(pawn.Position, pawn.Map, ThingRequest.ForGroup(ThingRequestGroup.Pawn), PathEndMode.OnCell, TraverseParms.For(pawn, Danger.Deadly, TraverseMode.ByPawn, false, false, false), radius, validator, null, 0, -1, false, RegionType.Set_Passable, false);
            if (target == null || pawn.Downed || target.Position.GetEdifice(target.Map) != null)
            {
                return null;
            }

            bool becomPrisoner = (target.IsPrisoner || target.Faction?.HostileTo(Faction.OfPlayer) == true);
            Room targetRoom = target.GetRoom();
            bool bedNearby = GenAdj.CellsAdjacent8Way(target).Any(cell => cell.GetEdifice(target.Map) is Building_Bed);

            if ((becomPrisoner && (targetRoom == null || !targetRoom.IsPrisonCell) && bedNearby) ||
                (!becomPrisoner && targetRoom?.IsPrisonCell == true && bedNearby))
            {
                return null;
            }

            Job job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("YR_Mimic_CaptureDownedPawn"), target, pawn);
            job.count = 1;
            return job;
        }

        private float radius = 30f;

        private const float MinDistFromEnemy = 25f;
    }
}

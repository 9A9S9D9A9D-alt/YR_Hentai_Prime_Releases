using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace YR_RJW
{
    public class MimicProperties_Bed : CompProperties
    {
        public int tendTick = 100;
        public MimicProperties_Bed() { compClass = typeof(MimicComp_Bed); }
    }

    // Mimic-only port of the supplied queen holder, without unused warp/death family closure.
    public class MimicComp_Bed : Comp_Building_BaseBondageBed, IThingHolder
    {
        public MimicProperties_Bed MimicProps => (MimicProperties_Bed)props;
        public ThingOwner innerContainer;
        int tendCountdown = -10;
        public bool activated;
        public MimicComp_Bed() { innerContainer = new ThingOwner<Thing>(this); }
        public override void CompTick()
        {
            base.CompTick();
            if (tendCountdown < -1) tendCountdown = MimicProps.tendTick;
            var occupant = BBB.occupant;
            if (innerContainer.Count > 0) activated = true;
            if (!activated) return;
            if (occupant == null || occupant.CurrentBed() != parent)
            {
                parent.Destroy();
                return;
            }
            if (--tendCountdown <= 0)
            {
                tendCountdown = MimicProps.tendTick;
                var injury = occupant.health.hediffSet.hediffs.OfType<Hediff_Injury>().FirstOrDefault(h => h.TendableNow());
                injury?.Tended(.01f, .05f);
            }
        }
        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            base.PostDeSpawn(map, mode);
            var pawns = innerContainer.OfType<Pawn>().ToList();
            innerContainer.TryDropAll(parent.Position, map, ThingPlaceMode.Near, null, null, true);
            foreach (var pawn in pawns)
            {
                if (pawn.Dead) continue;
                var hediff = HediffMaker.MakeHediff(HediffDefOf.Anesthetic, pawn);
                hediff.Severity = 1;
                pawn.health.AddHediff(hediff);
            }
        }
        public override void PostRemoveOccupant(Pawn pawn)
        {
            base.PostRemoveOccupant(pawn);
            if (!pawn.Dead && pawn.GetPosture() != PawnPosture.LayingInBed)
            {
                var hediff = HediffMaker.MakeHediff(HediffDefOf.Anesthetic, pawn);
                hediff.Severity = .75f;
                pawn.health.AddHediff(hediff);
            }
        }
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Deep.Look(ref innerContainer, "innerContainer", this);
            Scribe_Values.Look(ref tendCountdown, "tendCountdown", -10);
            Scribe_Values.Look(ref activated, "activated", false);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && innerContainer.Count > 0) activated = true;
        }
        public void GetChildHolders(List<IThingHolder> outChildren)
            => ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
        public ThingOwner GetDirectlyHeldThings() => innerContainer;
        public override string CompInspectStringExtra()
            => "Contents".Translate() + ": " + innerContainer.ContentsString.CapitalizeFirst();
    }
}



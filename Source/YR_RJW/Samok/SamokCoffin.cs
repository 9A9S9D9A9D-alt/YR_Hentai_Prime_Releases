using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RimWorld;
using Verse;
using Verse.AI;

namespace YR_RJW
{
    // Keep the original bed's saved class name and ordinary living-pawn behavior.
    public partial class Building_BaseBondageBed
    {
        public const int CoffinResurrectionTicks = 300000;
        private ThingOwner<Corpse> coffinContents;
        private int coffinTicks;
        private bool autoHaulBrideCorpses = true;

        public bool IsSamokCoffin => def?.defName == "YR_Building_Tentacle";
        public bool IsContentsSuspended => IsSamokCoffin;
        public Corpse CoffinCorpse => coffinContents != null && coffinContents.Count > 0 ? coffinContents[0] : null;
        // A display source only: never assign a dead Pawn to occupant/CurOccupants.
        public Pawn CoffinDisplayPawn => IsSamokCoffin && CoffinCorpse?.InnerPawn?.Dead==true ? CoffinCorpse.InnerPawn : null;
        public int CoffinTicks => coffinTicks;
        public bool AutoHaulBrideCorpses => autoHaulBrideCorpses;

        public Building_BaseBondageBed()
        {
            coffinContents = new ThingOwner<Corpse>(this, true);
            coffinContents.dontTickContents = true;
        }

        private void ExposeCoffinData()
        {
            Scribe_Deep.Look(ref coffinContents, "samokCoffinContents", this);
            Scribe_Values.Look(ref coffinTicks, "samokCoffinTicks");
            Scribe_Values.Look(ref autoHaulBrideCorpses, "samokAutoHaulBrideCorpses", true);
            if (coffinContents == null) coffinContents = new ThingOwner<Corpse>(this, true);
            // Corpses in the coffin must not rot, deteriorate, or tick their apparel.
            coffinContents.dontTickContents = true;
        }

        public ThingOwner GetDirectlyHeldThings() => coffinContents;

        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, coffinContents);
        }

        public bool CanAcceptCoffinCorpse(Corpse corpse)
        {
            return IsSamokCoffin && Spawned && !Destroyed && CoffinCorpse == null &&
                occupant == null && !CurOccupants.Any() && corpse != null && !corpse.Destroyed &&
                corpse.InnerPawn?.Dead == true && SamokBrideRules.IsBride(corpse.InnerPawn, true) &&
                !(corpse is UnnaturalCorpse);
        }

        public bool TryAcceptCoffinCorpse(Corpse corpse)
        {
            if (!CanAcceptCoffinCorpse(corpse)) return false;
            Map oldMap = corpse.Map;
            IntVec3 oldPosition = corpse.Position;
            bool wasSpawned = corpse.Spawned;
            if (wasSpawned) corpse.DeSpawn();
            if (!coffinContents.TryAddOrTransfer(corpse, false))
            {
                if (wasSpawned) GenSpawn.Spawn(corpse, oldPosition, oldMap);
                return false;
            }
            coffinTicks = 0;
            return true;
        }

        private void TickCoffin()
        {
            Corpse corpse = CoffinCorpse;
            if (!IsSamokCoffin || corpse == null || !Spawned) return;
            if (coffinTicks < CoffinResurrectionTicks) coffinTicks++;
            if (coffinTicks < CoffinResurrectionTicks) return;
            // Native resurrection keeps the very same Pawn, inventory, identity and relations.
            // It resolves the held corpse's map/position and removes the empty corpse itself.
            if (ResurrectionUtility.TryResurrect(corpse.InnerPawn)) coffinTicks = 0;
        }

        public void DebugCompleteCoffinResurrection()
        {
            if (!Prefs.DevMode || CoffinCorpse == null) return;
            coffinTicks = CoffinResurrectionTicks;
            TickCoffin();
        }

        public bool TryEjectCoffinCorpse()
        {
            Corpse corpse = CoffinCorpse;
            if (corpse == null) return true;
            Map map = MapHeld;
            if (map == null) return false;
            if (!coffinContents.TryDrop(corpse, PositionHeld, map, ThingPlaceMode.Near, out Thing dropped))
                return false;
            coffinTicks = 0;
            return true;
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            // No destruction mode is permitted to silently destroy a contained Pawn.
            // A mapless bed retains its contents until it can be placed and safely emptied.
            if (!TryEjectCoffinCorpse()) return;
            base.Destroy(mode);
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos()) yield return gizmo;
            if (!IsSamokCoffin || Faction != Faction.OfPlayer) yield break;
            yield return new Command_Toggle
            {
                defaultLabel = "YR_SamokCoffinAutoHaul".Translate(),
                defaultDesc = "YR_SamokCoffinAutoHaulDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get("Yuran/UI/Commands/AutoHaul"),
                isActive = () => autoHaulBrideCorpses,
                toggleAction = () => autoHaulBrideCorpses = !autoHaulBrideCorpses
            };
            if (CoffinCorpse != null)
            {
                yield return new Command_Action
                {
                    defaultLabel = "YR_SamokCoffinEject".Translate(),
                    defaultDesc = "YR_SamokCoffinEjectDesc".Translate(),
                    icon = ContentFinder<Texture2D>.Get("Yuran/UI/Commands/EjectContents"),
                    action = () => { if (TryEjectCoffinCorpse()) autoHaulBrideCorpses = false; }
                };
            }
        }

        private IEnumerable<FloatMenuOption> CoffinFloatMenuOptions(Pawn hauler)
        {
            if (!IsSamokCoffin || !Spawned || Faction != Faction.OfPlayer ||
                hauler?.Map != Map || !hauler.IsColonistPlayerControlled ||
                !hauler.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation) ||
                hauler.WorkTagIsDisabled(WorkTags.Hauling)) yield break;
            foreach (Corpse corpse in Map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse).OfType<Corpse>())
            {
                if (!CanAcceptCoffinCorpse(corpse)) continue;
                string label = "YR_SamokCoffinHaul".Translate(corpse.InnerPawn.LabelShort);
                if (!hauler.CanReserveAndReach(this, PathEndMode.Touch, Danger.Deadly) ||
                    !hauler.CanReserveAndReach(corpse, PathEndMode.ClosestTouch, Danger.Deadly))
                {
                    yield return new FloatMenuOption(label + " (" + "NoPath".Translate() + ")", null);
                    continue;
                }
                Corpse target = corpse;
                yield return new FloatMenuOption(label, () =>
                {
                    Job job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("YR_SamokHaulBrideCorpse"), target, this);
                    job.count = 1;
                    hauler.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                });
            }
        }
    }

    public class JobDriver_SamokHaulBrideCorpse : JobDriver
    {
        private Building_BaseBondageBed Bed => job.targetB.Thing as Building_BaseBondageBed;
        private Corpse Corpse => job.targetA.Thing as Corpse;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return Bed != null && Bed.CanAcceptCoffinCorpse(Corpse) &&
                pawn.Reserve(Corpse, job, 1, -1, null, errorOnFailed) &&
                pawn.Reserve(Bed, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.A);
            this.FailOnDespawnedNullOrForbidden(TargetIndex.B);
            this.FailOn(() => Bed == null || !Bed.CanAcceptCoffinCorpse(Corpse));
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch);
            yield return Toils_Haul.StartCarryThing(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.Touch);
            yield return Toils_General.Do(() =>
            {
                if (!Bed.TryAcceptCoffinCorpse(pawn.carryTracker.CarriedThing as Corpse))
                    EndJobWith(JobCondition.Incompletable);
            });
        }
    }

    public class WorkGiver_SamokHaulBrideCorpse : WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForGroup(ThingRequestGroup.Corpse);

        public override Job JobOnThing(Pawn pawn, Thing thing, bool forced = false)
        {
            Corpse corpse = thing as Corpse;
            if (corpse == null || corpse.IsForbidden(pawn) || !SamokBrideRules.IsBride(corpse.InnerPawn, true) ||
                !pawn.CanReserveAndReach(corpse, PathEndMode.ClosestTouch, Danger.Some)) return null;
            ThingDef bedDef = DefDatabase<ThingDef>.GetNamed("YR_Building_Tentacle");
            Building_BaseBondageBed bed = GenClosest.ClosestThingReachable(corpse.Position, pawn.Map,
                ThingRequest.ForDef(bedDef), PathEndMode.Touch, TraverseParms.For(pawn), 9999f,
                t => t is Building_BaseBondageBed candidate && candidate.Faction == Faction.OfPlayer &&
                    candidate.AutoHaulBrideCorpses && !candidate.IsForbidden(pawn) &&
                    candidate.CanAcceptCoffinCorpse(corpse) && pawn.CanReserve(candidate)) as Building_BaseBondageBed;
            if (bed == null) return null;
            Job job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("YR_SamokHaulBrideCorpse"), corpse, bed);
            job.count = 1;
            return job;
        }
    }
}

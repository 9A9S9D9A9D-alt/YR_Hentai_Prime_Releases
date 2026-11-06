using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace YR_RJW
{
    public partial class Building_SamokSwimmingPool : Building
    {
        public const int Capacity = 4;
        public CellRect WaterCells => this.OccupiedRect().ContractedBy(1);
        public static bool Eligible(Pawn pawn)
            => LegacyEligibility.Allows(pawn) && pawn.ageTracker.AgeBiologicalYearsFloat >= 18f &&
               !pawn.Downed && !pawn.Drafted && pawn.needs?.joy != null &&
               pawn.def.defName.IndexOf("maggot", StringComparison.OrdinalIgnoreCase) < 0;
        public bool CanSwim(Pawn pawn)
            => Spawned && HasNutrition && pawn != null && pawn.Spawned && pawn.Map == Map && Eligible(pawn) &&
               !this.IsForbidden(pawn) && HasPlace(pawn) && pawn.CanReserve(this, Capacity, 0) &&
               WaterCells.Cells.Any(c => SafeCell(pawn, c) && pawn.CanReach(c, PathEndMode.OnCell, Danger.None));
        public bool SafeCell(Pawn pawn, IntVec3 cell)
            => cell.InBounds(Map) && WaterCells.Contains(cell) && cell.Walkable(Map) && cell.GetThingList(Map).All(t => t == this || t.def.passability == Traversability.Standable) &&
               !cell.IsForbidden(pawn) && !ReservedWaterCell(cell, pawn) && pawn.CanReserve(cell) && !cell.GetThingList(Map).OfType<Pawn>().Any(p => p != pawn);
        public IntVec3 NextCell(Pawn pawn)
        {
            var candidates = WaterCells.Cells.Where(c => c != pawn.Position && SafeCell(pawn, c) &&
                (!WaterCells.Contains(pawn.Position) || c.AdjacentTo8Way(pawn.Position)) &&
                pawn.CanReach(c, PathEndMode.OnCell, Danger.None)).ToList();
            return candidates.Count == 0 ? IntVec3.Invalid : candidates.RandomElement();
        }
        public Job SwimJob(Pawn pawn)
        {
            if (!CanSwim(pawn)) return null;
            IntVec3 entry = NextCell(pawn);
            return entry.IsValid ? JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("YR_SamokSwim"), this, entry) : null;
        }
        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn selPawn)
        {
            foreach (FloatMenuOption option in base.GetFloatMenuOptions(selPawn)) yield return option;
            foreach (FloatMenuOption option in SpaOptions(selPawn)) yield return option;
            if (CanSwim(selPawn))
                yield return new FloatMenuOption("수영하기", () => {
                    Job swim = SwimJob(selPawn);
                    if (swim == null) return;
                    swim.ignoreJoyTimeAssignment = true;
                    selPawn.jobs.TryTakeOrderedJob(swim, JobTag.Misc);
                });
        }
    }
    public class JoyGiver_SamokSwimming : JoyGiver_InteractBuilding
    {
        protected override bool CanInteractWith(Pawn pawn, Thing t, bool inBed)
            => !inBed && t is Building_SamokSwimmingPool pool && pool.CanSwim(pawn) && !t.Fogged() && t.IsSociallyProper(pawn) && t.IsPoliticallyProper(pawn) && !t.VacuumConcernTo(pawn);
        protected override Job TryGivePlayJob(Pawn pawn, Thing bestGame)
            => (bestGame as Building_SamokSwimmingPool)?.SwimJob(pawn);
    }
    public class JobDriver_SamokSwimming : JobDriver
    {
        public const int SessionTicks = 2500;
        private bool inWater;
        private int swimTicks;
        public Building_SamokSwimmingPool Pool => job.targetA.Thing as Building_SamokSwimmingPool;
        public bool IsSwimming => inWater && !ended && Pool != null && Pool.Spawned && Pool.HasNutrition && pawn.Spawned &&
            pawn.Map == Pool.Map && Pool.WaterCells.Contains(pawn.Position) && Building_SamokSwimmingPool.Eligible(pawn);
        public int SwimTicks => swimTicks;
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref inWater, "samokInWater");
            Scribe_Values.Look(ref swimTicks, "samokSwimTicks");
        }
        public override bool TryMakePreToilReservations(bool errorOnFailed)
            => Pool != null && Pool.CanSwim(pawn) && pawn.Reserve(Pool, job, Building_SamokSwimmingPool.Capacity, 0, null, errorOnFailed) && pawn.Reserve(job.targetB, job, 1, -1, null, errorOnFailed);
        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOn(() => Pool==null || !Pool.HasNutrition || !Building_SamokSwimmingPool.Eligible(pawn) ||
                (inWater && (Pool == null || pawn.Map != Pool.Map || !Pool.WaterCells.Contains(pawn.Position))));
            AddFinishAction(condition => {
                inWater = false;
                pawn.pather?.StopDead();
                // Native cleanup releases job reservations; no stats or persistent visual state change.
            });
            yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);
            Toil swim = ToilMaker.MakeToil("SamokSwimming");
            swim.defaultCompleteMode = ToilCompleteMode.Never;
            swim.initAction = () => inWater = true;
            swim.tickAction = () => {
                if (!IsSwimming) { EndJobWith(JobCondition.Incompletable); return; }
                swimTicks++;
                if (JoyUtility.JoyTickCheckEnd(pawn, 1, JoyTickFullJoyAction.EndJob, 1f, Pool)) return;
                if (swimTicks >= SessionTicks) { EndJobWith(JobCondition.Succeeded); return; }
                if (!pawn.pather.Moving && swimTicks % 12 == 0)
                {
                    IntVec3 next = Pool.NextCell(pawn);
                    if (next.IsValid && pawn.Reserve(next, job))
                    {
                        pawn.Map.reservationManager.Release(job.targetB, pawn, job);
                        job.targetB = next;
                        pawn.pather.StartPath(next, PathEndMode.OnCell);
                    }
                }
                if (swimTicks % 40 == 0) FleckMaker.WaterRipple(pawn.DrawPos, pawn.Map, 0.65f);
            };
            yield return swim;
        }
    }
    // Only render-call-local values change. Hair, gear, pawn state and portraits remain native.
    public static class SamokSwimmingVisuals
    {
        public static bool Active(Pawn pawn)
            => (pawn?.jobs?.curDriver is JobDriver_SamokSwimming swimming && swimming.IsSwimming) ||
               (pawn?.jobs?.curDriver is JobDriver_SamokBath bath && bath.IsBathing) ||
               SamokSpaCare.PoolFor(pawn) != null;
        public static void Apply(ref PawnDrawParms parms)
        {
            if (parms.Portrait || parms.flags.FlagSet(PawnRenderFlags.Cache) || !Active(parms.pawn)) return;
            if (SamokSpaCare.PoolFor(parms.pawn) != null)
            {
                // Native lying angle belongs to the patient state, not this calm floating presentation.
                Vector3 root = parms.matrix.GetColumn(3);
                parms.matrix = Matrix4x4.TRS(root, Quaternion.identity, parms.matrix.lossyScale);
                parms.facing = Rot4.South;
                parms.posture = PawnPosture.Standing;
                parms.crawling = false;
            }
            parms.skipFlags |= DefDatabase<RenderSkipFlagDef>.GetNamed("Body");
            parms.swimming = true;
            float bob = Mathf.Sin((Find.TickManager.TicksGame + parms.pawn.thingIDNumber * 11) * 0.075f) * 0.045f;
            parms.matrix = Matrix4x4.Translate(new Vector3(0f, 0f, bob)) * parms.matrix;
        }
    }
    [HarmonyPatch(typeof(PawnRenderer), "GetDrawParms")]
    internal static class SamokSwimmingDrawParms
    {
        private static void Postfix(ref PawnDrawParms __result) => SamokSwimmingVisuals.Apply(ref __result);
    }
    [HarmonyPatch(typeof(PawnRenderer), "ParallelGetPreRenderResults")]
    internal static class SamokSwimmingWorldCache
    {
        private static void Prefix(Pawn ___pawn, ref bool disableCache)
        {
            if (SamokSwimmingVisuals.Active(___pawn)) disableCache = true;
        }
    }
}


namespace YR_RJW
{
    // HAR renders addons through a separate worker. Head-aligned addons stay attached;
    // body-aligned addons are below the liquid for this job only.
    [HarmonyPatch(typeof(AlienRace.AlienPawnRenderNodeWorker_BodyAddon), "CanDrawNow")]
    internal static class SamokSwimmingAddonVisibility
    {
        private static bool Prefix(PawnRenderNode node, ref PawnDrawParms parms, ref bool __result)
        {
            if (parms.Portrait || parms.flags.FlagSet(PawnRenderFlags.Cache) || !SamokSwimmingVisuals.Active(parms.pawn)) return true;
            var addon = AlienRace.AlienPawnRenderNodeWorker_BodyAddon.AddonFromNode(node);
            if (!addon.alignWithHead) { __result = false; return false; }
            parms.skipFlags &= ~(ulong)DefDatabase<RenderSkipFlagDef>.GetNamed("Body");
            return true;
        }
    }
    [HarmonyPatch(typeof(AlienRace.AlienPawnRenderNodeWorker_BodyAddon), "OffsetFor")]
    internal static class SamokSwimmingAddonOffset
    {
        private static void Postfix(PawnRenderNode node, PawnDrawParms parms, ref Vector3 __result)
        {
            if (parms.Portrait || !SamokSwimmingVisuals.Active(parms.pawn) ||
                !AlienRace.AlienPawnRenderNodeWorker_BodyAddon.AddonFromNode(node).alignWithHead) return;
            // Avoid applying the native head's immersion offset twice if HAR parents under Head.
            for (PawnRenderNode parent = node.parent; parent != null; parent = parent.parent)
                if (parent is PawnRenderNode_Head) return;
            __result.z -= 0.5f;
        }
    }
}



namespace YR_RJW
{
    [HarmonyPatch(typeof(PawnRenderNodeWorker_Carried), "CanDrawNow")]
    internal static class SamokSwimmingCarriedVisibility
    {
        private static bool Prefix(PawnDrawParms parms, ref bool __result)
        {
            if (parms.Portrait || parms.flags.FlagSet(PawnRenderFlags.Cache) || !SamokSwimmingVisuals.Active(parms.pawn)) return true;
            // The carried node's PostDraw also draws weapons and apparel extras.
            __result = false;
            return false;
        }
    }
}




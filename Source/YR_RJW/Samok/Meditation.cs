using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace YR_RJW
{
    // A separate focus never grants access to Natural or other focus types.
    [HarmonyPatch(typeof(MeditationFocusDef), nameof(MeditationFocusDef.CanPawnUse))]
    public static class MeditationBrideFocus
    {
        public static bool Prefix(MeditationFocusDef __instance, Pawn p, ref bool __result)
        {
            if (__instance.defName != "YR_SamokBrideFocus") return true;
            __result = ModsConfig.RoyaltyActive && SamokBrideRules.IsBride(p);
            return false;
        }
    }

    [HarmonyPatch(typeof(JobDriver_Meditate), "MeditationTick")]
    public static class MeditationSamokGrowth
    {
        public static void Postfix(JobDriver_Meditate __instance)
        {
            Pawn pawn = __instance.pawn;
            // Native MeditationTick can end its own job; Focus then dereferences a null job.
            if (!ModsConfig.RoyaltyActive || !SamokBrideRules.IsBride(pawn) ||
                __instance.job == null || pawn.jobs?.curDriver != __instance) return;
            Thing focus = __instance.Focus.Thing;
            if (focus?.def.defName != "Building_Tentacle_Tree" || !focus.Spawned ||
                !InMeditationRange(pawn, focus)) return;
            focus.TryGetComp<CompSpawnSubplant>()?.AddProgress(JobDriver_Meditate.AnimaTreeSubplantProgressPerTick);
        }

        public static bool InMeditationRange(Pawn pawn, Thing focus)
        {
            if (pawn == null || !pawn.Spawned || focus == null || !focus.Spawned || pawn.Map != focus.Map) return false;
            // Native MeditationSpotForFocus measures from the occupied rectangle. The Samok
            // tree is 3x4: its valid native spots are over 3.9 cells from the center.
            IntVec3 edge = focus.OccupiedRect().ClosestCellTo(pawn.Position);
            return pawn.Position.DistanceTo(edge) <= MeditationUtility.FocusObjectSearchRadius &&
                GenSight.LineOfSight(pawn.Position, edge, pawn.Map);
        }
    }

    // The original tree ticks normally; native anima trees tick Long.
    public class CompSamokMeditationSubplants : CompSpawnSubplant
    {
        public override void CompTick()
        {
            base.CompTick();
            if (parent.IsHashIntervalTick(2000)) base.CompTickLong();
        }
    }

    public class CompProperties_SamokMeditation : CompProperties
    {
        public CompProperties_SamokMeditation() { compClass = typeof(CompSamokMeditation); }
    }

    public class CompSamokMeditation : ThingComp
    {
        public const int TentaclesPerLink = 10;
        public CompSpawnSubplant Growth => parent.TryGetComp<CompSpawnSubplant>();

        public bool CanLink(Pawn pawn)
        {
            return ModsConfig.RoyaltyActive && parent.Spawned && pawn != null && pawn.Spawned &&
                pawn.Map == parent.Map && pawn.Faction == Faction.OfPlayer && SamokBrideRules.IsBride(pawn) &&
                !pawn.Downed && pawn.health.hediffSet.GetBrain() != null &&
                pawn.GetPsylinkLevel() < pawn.GetMaxPsylinkLevel() &&
                Growth != null && Growth.SubplantsForReading.Count >= TentaclesPerLink;
        }

        public bool TryFinishLink(Pawn pawn)
        {
            if (!CanLink(pawn) || !MeditationSamokGrowth.InMeditationRange(pawn, parent)) return false;
            int oldLevel = pawn.GetPsylinkLevel();
            List<Thing> payment = Growth.SubplantsForReading.Take(TentaclesPerLink).ToList();
            // Native gain also grants the corresponding native psycast. Consume only on success.
            pawn.ChangePsylinkLevel(1);
            if (pawn.GetPsylinkLevel() != oldLevel + 1) return false;
            foreach (Thing tentacle in payment) tentacle.Destroy(DestroyMode.Vanish);
            Growth.Cleanup();
            Find.History.Notify_PsylinkAvailable();
            return true;
        }

        public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn pawn)
        {
            if (!ModsConfig.RoyaltyActive || !SamokBrideRules.IsBride(pawn) || pawn.Drafted || pawn.Downed) yield break;
            LocalTargetInfo spot = MeditationUtility.MeditationSpotForFocus(parent, pawn);
            bool reachable = spot.IsValid && pawn.CanReserveAndReach(spot, PathEndMode.OnCell, Danger.Some);
            yield return new FloatMenuOption("사목숭배", reachable ? (System.Action)(() =>
            {
                Job job = JobMaker.MakeJob(JobDefOf.Meditate, spot, LocalTargetInfo.Invalid, parent);
                job.ignoreJoyTimeAssignment = true;
                pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
            }) : null);
            bool canLink = reachable && CanLink(pawn) && pawn.CanReserve(parent);
            string label = "사목 정신 연결 / Psychic link (10)";
            if (!canLink) label += " — 성인 신부, 촉수 10개, 정신 연결 여유 필요";
            yield return new FloatMenuOption(label, canLink ? (System.Action)(() =>
                pawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("YR_SamokPsychicLink"), parent, spot), JobTag.Misc)) : null);
        }
    }

    public class JobDriver_SamokPsychicLink : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed) &&
                pawn.Reserve(job.targetB, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOn(() => TargetThingA.TryGetComp<CompSamokMeditation>()?.CanLink(pawn) != true);
            yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);
            Toil linking = Toils_General.Wait(6000, TargetIndex.A);
            linking.WithProgressBarToilDelay(TargetIndex.A);
            linking.FailOn(() => !MeditationSamokGrowth.InMeditationRange(pawn, TargetThingA));
            linking.tickAction = () =>
            {
                pawn.rotationTracker.FaceTarget(TargetThingA);
                if (pawn.IsHashIntervalTick(100)) FleckMaker.ThrowMetaIcon(pawn.Position, Map, FleckDefOf.Meditating);
            };
            yield return linking;
            Toil finish = ToilMaker.MakeToil("SamokFinishPsychicLink");
            finish.initAction = () =>
            {
                if (!TargetThingA.TryGetComp<CompSamokMeditation>().TryFinishLink(pawn)) EndJobWith(JobCondition.Incompletable);
            };
            finish.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return finish;
        }
    }
}

namespace YR_RJW
{
    // Native job requests only: no extra per-tick pawn or map scan.
    internal static class SamokWorshipV62
    {
        internal static ThingDef TreeDef => DefDatabase<ThingDef>.GetNamedSilentFail("Building_Tentacle_Tree");
        internal static bool CanAuto(Pawn pawn) => ModsConfig.RoyaltyActive && pawn?.IsColonistPlayerControlled==true &&
            pawn.Spawned && !pawn.Drafted && !pawn.InMentalState && pawn.CurrentBed()==null &&
            SamokBrideRules.IsBride(pawn) && MeditationUtility.CanMeditateNow(pawn);
        internal static bool HasTree(Pawn pawn)
        {
            var def=TreeDef;
            return def!=null && pawn.Map.listerThings.ThingsOfDef(def).Any(t=>t.Faction==Faction.OfPlayer && !t.IsForbidden(pawn));
        }
        internal static bool NeedsPsyfocus(Pawn pawn) => pawn.HasPsylink && pawn.psychicEntropy!=null &&
            pawn.psychicEntropy.PsychicSensitivity>float.Epsilon &&
            pawn.psychicEntropy.CurrentPsyfocus<UnityEngine.Mathf.Min(pawn.psychicEntropy.TargetPsyfocus,.95f);
        internal static Job TryMakeJob(Pawn pawn,bool forJoy)
        {
            var def=TreeDef;if(def==null)return null;
            foreach(var tree in pawn.Map.listerThings.ThingsOfDef(def).OrderBy(t=>t.Position.DistanceToSquared(pawn.Position))){
                if(tree.Faction!=Faction.OfPlayer || tree.IsForbidden(pawn) || tree.TryGetComp<CompSamokMeditation>()==null ||
                    tree.TryGetComp<CompMeditationFocus>()?.CanPawnUse(pawn)!=true)continue;
                var spot=MeditationUtility.MeditationSpotForFocus(tree,pawn,cell=>
                    MeditationUtility.SafeEnvironmentalConditions(pawn,cell,pawn.Map) &&
                    (cell.GetRoom(pawn.Map)==null || MeditationUtility.CanUseRoomToMeditate(cell.GetRoom(pawn.Map),pawn)));
                if(!spot.IsValid)continue;
                var job=JobMaker.MakeJob(JobDefOf.Meditate,spot,LocalTargetInfo.Invalid,tree);
                job.ignoreJoyTimeAssignment=!forJoy;
                return job;
            }
            return null;
        }
    }
    [HarmonyPatch(typeof(JobGiver_Meditate),nameof(JobGiver_Meditate.GetPriority))]
    internal static class SamokWorshipIdleV62
    {
        private static void Postfix(Pawn pawn,ref float __result)
        {
            // Lower than ordinary work/needs: allow worship during otherwise idle Anything time,
            // even before a first psylink. Native scheduled/psyfocus priorities remain unchanged.
            if(__result>0f || !SamokWorshipV62.CanAuto(pawn) || pawn.GetTimeAssignment()!=TimeAssignmentDefOf.Anything)return;
            if(SamokWorshipV62.HasTree(pawn))__result=1f;
        }
    }
    [HarmonyPatch(typeof(MeditationUtility),nameof(MeditationUtility.GetMeditationJob))]
    internal static class SamokWorshipFocusV62
    {
        private static bool Prefix(Pawn pawn,bool forJoy,ref Job __result)
        {
            if(!SamokWorshipV62.CanAuto(pawn))return true;
            var job=SamokWorshipV62.TryMakeJob(pawn,forJoy);
            if(job!=null){__result=job;return false;}
            // Do not turn the added idle priority into unrelated floor meditation when blocked.
            if(!forJoy && pawn.GetTimeAssignment()==TimeAssignmentDefOf.Anything && !SamokWorshipV62.NeedsPsyfocus(pawn)){
                __result=null;return false;
            }
            return true;
        }
    }
}

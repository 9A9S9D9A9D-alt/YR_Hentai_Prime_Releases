using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using YR_Hentai_Prime_AnimationBed;

namespace YR_RJW
{
    public class CompProperties_LovePillar : CompProperties
    {
        public CompProperties_LovePillar() { compClass = typeof(CompLovePillar); }
    }

    // A direct order for the pillar's supplied visitor recreation; effects stay in Play_BellyPunch.
    public class CompLovePillar : ThingComp
    {
        public Building_BaseBondageBed Bed => parent as Building_BaseBondageBed;
        public bool CanUse(Pawn actor)
        {
            var dummy = Bed?.dummyForJoy;
            return parent.Spawned && LegacyEligibility.Allows(actor) && LegacyEligibility.Allows(Bed?.occupant) &&
                actor != Bed.occupant && actor.Spawned && actor.Map == parent.Map && !actor.Downed && !actor.InMentalState &&
                actor.Faction == Faction.OfPlayer && !actor.IsPrisoner && dummy?.Spawned == true && dummy.user == null &&
                actor.CanReserve(dummy) && actor.CanReserveAndReach(dummy.InteractionCell, PathEndMode.OnCell, Danger.Some);
        }
        public void StartUse(Pawn actor)
        {
            if (!CanUse(actor)) return;
            var job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("Play_BellyPunch"), Bed.dummyForJoy, Bed.dummyForJoy.InteractionCell);
            actor.jobs.TryTakeOrderedJob(job, JobTag.Misc);
        }
        public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn pawn)
        {
            if (CanUse(pawn)) yield return new FloatMenuOption("Use".Translate() + ": " + parent.LabelCap, () => StartUse(pawn));
        }

        public static bool TryHeadMatrix(Building_BaseBondageBed bed, out Matrix4x4 matrix)
        {
            matrix = Matrix4x4.identity;
            var pawn = bed?.occupant;
            var comp = bed?.GetComp<CompChangeBedTexture>();
            if (bed?.def.defName != "YR_Building_BellyPunch" || !LegacyEligibility.Allows(pawn) || comp == null || pawn.CurrentBed() != bed) return false;
            var head = FacilityRenderNodes.Find(pawn, PawnRenderNodeTagDefOf.Head);
            if (head == null) return false;
            var root = new Vector3(bed.DrawPos.x, bed.DrawPos.y + 0.5f, bed.DrawPos.z + 0.25f) + comp.headOffSet;
            var parms = FacilityRender.MakeDrawParms(pawn, root, comp.bodyFacing, PawnRenderFlags.None);
            var tree = pawn.Drawer.renderer.renderTree;
            tree.ParallelPreDraw(parms);
            return tree.TryGetMatrix(head, parms, out matrix);
        }

        public override void PostDraw()
        {
            if (!TryHeadMatrix(Bed, out var matrix)) return;
            var pawn = Bed.occupant;
            var mesh = HumanlikeMeshPoolUtility.GetHumanlikeHeadSetForPawn(pawn).MeshAt(Bed.GetComp<CompChangeBedTexture>().bodyFacing);
            var graphic = GraphicDatabase.Get<Graphic_Single>("Yuran/Things/Building/Furniture/BellyPunch/Body/EyePatch/1", ShaderDatabase.Cutout, Vector2.one, Color.white);
            // Stay just above this native head, rather than the old global facility depth 7.
            matrix.m13 += 0.01f;
            GenDraw.DrawMeshNowOrLater(mesh, matrix, graphic.MatSingle, false);
        }
    }
}

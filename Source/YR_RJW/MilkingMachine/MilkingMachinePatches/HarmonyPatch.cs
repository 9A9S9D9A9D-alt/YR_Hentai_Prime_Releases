using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;
namespace YR_RJW
{
    // 1.7 draws through render nodes instead of PawnGraphicSet/DrawPawnBody.
    public static class FacilityRender
    {
        [ThreadStatic] public static bool Manual;
        [ThreadStatic] public static PawnRenderFlags ManualFlags;
        [ThreadStatic] private static Pawn headPawn;
        [ThreadStatic] private static Mesh headMesh;
        [ThreadStatic] private static Matrix4x4 headMatrix;
        public static bool TryDrawHeadOverlay(Pawn pawn, Graphic graphic)
        {
            if (headPawn != pawn || headMesh == null || graphic == null) return false;
            var matrix = headMatrix;
            matrix.m13 += .0005f;
            var adjustment=YR_Hentai_Prime_AnimationBed.CompAnimationPosition.For(pawn);
            if(adjustment!=null){var delta=adjustment.Offset(pawn,"face");matrix.m03+=delta.x;matrix.m23+=delta.z;float size=adjustment.DisplayScale(pawn,"face");matrix*=Matrix4x4.Scale(new Vector3(size,1f,size));}
            GenDraw.DrawMeshNowOrLater(headMesh, matrix, graphic.MatSingle, false);
            return true;
        }
        public static CompChangeBedTexture For(Pawn pawn)
        {
            var bed=pawn?.CurrentBed() as Building_BaseBondageBed;
            if(bed==null){
                var coffin=(pawn?.ParentHolder as Corpse)?.ParentHolder as Building_BaseBondageBed;
                if(coffin?.CoffinDisplayPawn==pawn && pawn!=null)bed=coffin;
            }
            return bed?.TryGetComp<CompChangeBedTexture>();
        }
        public static PawnDrawParms MakeDrawParms(Pawn pawn, Vector3 position, Rot4 facing, PawnRenderFlags flags)
            => new PawnDrawParms
            {
                pawn = pawn,
                matrix = Matrix4x4.TRS(position + pawn.ageTracker.CurLifeStage.bodyDrawOffset, Quaternion.identity, Vector3.one),
                facing = facing,
                rotDrawMode = RotDrawMode.Fresh,
                posture = For(pawn)?.parent.def.defName == "YR_Building_LoveRoom" ? PawnPosture.Standing : pawn.GetPosture(),
                flags = flags,
                tint = Color.white,
                // Facility layers already supply the origin and body visibility.
                bed = null
            };
        public static void Draw(Pawn pawn, Vector3 position, Rot4 facing, PawnRenderFlags flags)
        {
            bool previous = Manual;
            PawnRenderFlags previousFlags = ManualFlags;
            try
            {
                Manual = true;
                ManualFlags = flags;
                var renderer = pawn.Drawer.renderer;
                renderer.EnsureGraphicsInitialized();
                var parms = MakeDrawParms(pawn, position, facing, flags);
                // RenderPawnAt reapplies bed position/angle and can reuse a world atlas frame.
                renderer.renderTree.ParallelPreDraw(parms);
                headPawn = null;
                headMesh = null;
                if (renderer.renderTree.TryGetNodeByTag(PawnRenderNodeTagDefOf.Head, out var head) &&
                    renderer.renderTree.TryGetMatrix(head, parms, out headMatrix))
                {
                    headMesh = head.GetMesh(parms);
                    headPawn = pawn;
                }
                renderer.renderTree.Draw(parms);
            }
            finally { Manual = previous; ManualFlags = previousFlags; }
        }
    }
    [HarmonyPatch(typeof(PawnRenderer), nameof(PawnRenderer.DynamicDrawPhaseAt))]
    internal static class FacilityNativeDrawV8
    {
        private static bool Prefix(Pawn ___pawn,ref Vector3 drawLoc)
        {
            var facility=FacilityRender.For(___pawn);
            return FacilityRender.Manual || facility==null || (facility.parent as Building_BaseBondageBed)?.occupant!=___pawn;
        }
    }
    [HarmonyPatch(typeof(PawnRenderer), nameof(PawnRenderer.RenderPawnAt))]
    internal static class FacilityRenderPawn
    {
        private static bool Prefix(Pawn ___pawn, ref Vector3 drawLoc)
        {
            if (!FacilityRender.Manual && (FacilityRender.For(___pawn) != null ||
                (___pawn.jobs?.curDriver is JobDriver_WatchDummy watcher && watcher.startAni))) return false;
            if (___pawn.apparel?.WornApparel.Any(a => a.def.defName == "YR_AP_Golem") == true) drawLoc.z += 0.2f;
            return true;
        }
    }
    [HarmonyPatch]
    internal static class FacilityNodeVisibility
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (Type type in typeof(PawnRenderNodeWorker).Assembly.GetTypes()
                .Concat(typeof(AlienRace.AlienPawnRenderNodeWorker_BodyAddon).Assembly.GetTypes()))
            {
                if (!typeof(PawnRenderNodeWorker).IsAssignableFrom(type)) continue;
                var method = type.GetMethod("CanDrawNow", BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly,
                    null, new[] { typeof(PawnRenderNode), typeof(PawnDrawParms) }, null);
                if (method != null && !method.IsAbstract) yield return method;
            }
        }
        private static void Postfix(PawnRenderNode __0, PawnDrawParms __1, ref bool __result)
        {
            PawnRenderNode node = __0;
            PawnDrawParms parms = __1;
            var comp = FacilityRender.For(parms.pawn);
            if(FacilityRender.Manual && comp?.parent.def.defName=="YR_Building_EggMachine" && node is PawnRenderNode_Hair && node.PrimaryGraphic!=null && !comp.Props.hideHead)__result=true;
            if (!__result || parms.flags.FlagSet(PawnRenderFlags.Portrait)) return;
            if (comp == null) return;
            if (comp.Props.hideHead)
                for (var ancestor = node; ancestor != null; ancestor = ancestor.parent)
                    if (ancestor is PawnRenderNode_Head) { __result = false; return; }
            if (!comp.Props.showBodyAddon && node is AlienRace.AlienPawnRenderNode_BodyAddon) __result = false;
            if (FacilityRender.Manual && node.apparel != null)
            {
                bool headgear = node.apparel.def.apparel.LastLayer == ApparelLayerDefOf.Overhead ||
                    node.apparel.def.apparel.LastLayer == ApparelLayerDefOf.EyeCover;
                if (!FacilityRender.ManualFlags.FlagSet(headgear ? PawnRenderFlags.Headgear : PawnRenderFlags.Clothes)) __result = false;
            }
        }
    }
    [HarmonyPatch(typeof(PawnRenderNodeWorker), nameof(PawnRenderNodeWorker.GetFinalizedMaterial))]
    internal static class FacilityBodyMaterial
    {
        private static void Postfix(PawnRenderNode node, PawnDrawParms parms, ref Material __result)
        {
            if (parms.flags.FlagSet(PawnRenderFlags.Portrait)) return;
            if (node.apparel?.def.defName == "YR_AP_Golem")
            {
                if (GolemMaterialCacheV8.TryGet(node.apparel, parms.facing, out var readyMaterial))
                    __result = readyMaterial;
                return;
            }
            if (!(node is PawnRenderNode_Body)) return;
            var comp = FacilityRender.For(parms.pawn);
            if (comp?.BoundGraphic == null) return;
            Material mat = comp.BoundGraphic.MatAt(parms.facing);
            if (InvisibilityUtility.IsPsychologicallyInvisible(parms.pawn)) mat = InvisibilityMatPool.GetInvisibleMat(mat);
            __result = parms.pawn.Drawer.renderer.flasher.GetDamagedMat(mat);
        }
    }
    [HarmonyPatch(typeof(PawnRenderNodeWorker), nameof(PawnRenderNodeWorker.OffsetFor))]
    internal static class FacilityBodyOffset
    {
        private static void Postfix(PawnRenderNode node, PawnDrawParms parms, ref Vector3 __result)
        {
            if (node.apparel?.def.defName == "YR_AP_Golem") { __result.y -= 0.025f; __result.z -= 0.195f; }
            var comp = FacilityRender.For(parms.pawn);
            if(comp!=null && FacilityRender.Manual && node is AlienRace.AlienPawnRenderNode_BodyAddon){
                string path=node.PrimaryGraphic?.path?.ToLowerInvariant()??"";
                if(path.Contains("tail")||path.Contains("꼬리"))__result.y-=.6f;
            }
            if (node is PawnRenderNode_Body && comp != null && !comp.followHeadOffset)
                __result += comp.bodyOffSet - comp.movedHeadOffSet;
        }
    }
    [HarmonyPatch(typeof(Building_Bed), "GetSleepingSlotPos")]
    internal static class BuildingBed_GetSleepingSlotPos
    {
        private static void Postfix(ref IntVec3 __result, ref Building_Bed __instance)
        {
            if (__instance.def.thingClass == typeof(Building_BaseBondageBed))
            {
                __result = __instance.Position;
            }
        }
    }

    [HarmonyPatch(typeof(Building_Bed), "SleepingSlotsCount", (MethodType)1)]
    internal static class BuildingBed_SleepingSlotsCount
    {
        private static void Postfix(ref int __result, ref Building_Bed __instance)
        {
            if (__instance.def.thingClass == typeof(Building_BaseBondageBed))
            {
                __result = 1;
            }
        }
    }

    [HarmonyPatch(typeof(PawnRenderer), "BodyAngle")]
    internal static class PawnRenderer_BodyAngle_NonHumanoidPawnPreventRandomBodyAngle
    {
        private static void Postfix(ref float __result, Pawn ___pawn)
        {
            if (___pawn.CurrentBed() is Building_BaseBondageBed Building_AdvancedBondageBed && !___pawn.RaceProps.Humanlike && !Building_AdvancedBondageBed.def.building.bed_humanlike)
            {
                Rot4 rotation = Building_AdvancedBondageBed.Rotation;
                if (rotation == Rot4.North)
                {
                    __result = -90f;
                    return;
                }
                if (rotation == Rot4.South)
                {
                    __result = 90f;
                    return;
                }
                __result = 0f;
            }
        }
    }

    [HarmonyPatch(typeof(PawnRenderer), "LayingFacing")]
    internal static class PawnRenderer_LayingFacing_AlwaysLieOnBackForMilkingMachine
    {
        private static void Postfix(ref Rot4 __result, Pawn ___pawn)
        {
            if (___pawn.CurrentBed() is Building_BaseBondageBed Building_AdvancedBondageBed)
            {
                if (___pawn.RaceProps.Humanlike)
                {
                    CompChangeBedTexture comp = Building_AdvancedBondageBed.TryGetComp<CompChangeBedTexture>();
                    if (comp != null)
                    {
                        __result = comp.Props.faceRotation;
                    }
                    return;
                }
                __result = (Building_AdvancedBondageBed.Rotation == Rot4.West) ? Rot4.East : Rot4.West;
            }
        }
    }


    [HarmonyPatch(typeof(RestUtility), "IsValidBedFor")]
    internal static class RestUtility_IsValidBedFor_IgnoreMilkingMachine
    {
        private static bool Prefix(ref bool __result, Thing bedThing)
        {
            if (bedThing is Building_BaseBondageBed)
            {
                __result = false;
                return false;
            }
            return true;
        }
    }

    //[HarmonyPatch(typeof(PawnRenderer), "LayingFacing")]
    //internal static class PawnRenderer_LayingFacing_AlwaysLieOnBackForMilkingMachine
    //{
    //    private static void Postfix(ref Rot4 __result, Pawn ___pawn)
    //    {
    //        Building_BaseBondageBed Building_AdvancedBondageBed = ___pawn.CurrentBed() as Building_BaseBondageBed;
    //        if (Building_AdvancedBondageBed != null)
    //        {
    //            if (___pawn.RaceProps.Humanlike)
    //            {
    //                CompChangeBedTexture comp = Building_AdvancedBondageBed.TryGetComp<CompChangeBedTexture>();
    //                if (comp != null)
    //                {
    //                    __result = comp.Props.faceRotation;
    //                }
    //                return;
    //            }
    //            __result = ((Building_AdvancedBondageBed.Rotation == Rot4.West) ? Rot4.East : Rot4.West);
    //        }
    //    }
    //}
}

using AlienRace;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace YR_Hentai_Prime_AnimationBed
{
    // HAR's old world-space +/-0.3 depth can cross a held pawn's covering meshes.
    [HarmonyPatch(typeof(AlienPawnRenderNodeWorker_BodyAddon), nameof(AlienPawnRenderNodeWorker_BodyAddon.OffsetFor))]
    public static class FacilityAddonRenderV8
    {
        public static float NativeLayerDepth(float legacyDepth)
            // Legacy HAR depth uses 0.01 per layer. Convert its unit without
            // clamping negative addon layers to the native helper's -10 limit.
            => Mathf.Sign(legacyDepth) * PawnRenderUtility.AltitudeForLayer(Mathf.Abs(legacyDepth) * 100f);

        public static bool Applies(PawnRenderNode node, PawnDrawParms parms)
        {
            if (parms.Portrait || !(parms.pawn?.holdingOwner?.Owner is Building_AnimationBed)) return false;
            if (!(node is AlienPawnRenderNode_BodyAddon addonNode) || addonNode.props.addon.alignWithHead) return false;
            for (var ancestor = node.parent; ancestor != null; ancestor = ancestor.parent)
                if (ancestor is PawnRenderNode_Head || ancestor.Props?.tagDef == PawnRenderNodeTagDefOf.Head) return false;
            return true;
        }

        public static float AddonDepth(PawnRenderNode node, PawnDrawParms parms)
        {
            var addon = AlienPawnRenderNodeWorker_BodyAddon.AddonFromNode(node);
            var offsets = (parms.pawn.gender == Gender.Female ? addon.femaleOffsets : addon.offsets) ?? addon.offsets;
            var bodyType = parms.pawn.story?.bodyType ?? BodyTypeDefOf.Male;
            var headType = parms.pawn.story?.headType ?? HeadTypeDefOf.Skull;
            float depth = (addon.defaultOffsets.GetOffset(parms.facing)?.GetOffset(false, bodyType, headType) ?? Vector3.zero).y
                + (offsets.GetOffset(parms.facing)?.GetOffset(false, bodyType, headType) ?? Vector3.zero).y;
            depth = addon.inFrontOfBody ? 0.3f + depth : -0.3f - depth;
            return parms.facing == Rot4.North && addon.layerInvert ? -depth : depth;
        }

        private static void Postfix(PawnRenderNode node, PawnDrawParms parms, ref Vector3 __result)
        {
            if (!Applies(node, parms)) return;
            float depth = AddonDepth(node, parms);
            // Replace only HAR's depth. Preserve native animation/debug offsets,
            // body-parent transform, x/z position, scale and rotation.
            __result.y += NativeLayerDepth(depth) - depth;
        }
    }
}


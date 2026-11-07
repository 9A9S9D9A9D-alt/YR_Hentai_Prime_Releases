using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Verse;

namespace YR_Hentai_Prime_AnimationBed
{
    public static class FacilityRenderNodes
    {
        public static PawnRenderNode Find(Pawn pawn, PawnRenderNodeTagDef tag)
        {
            if (pawn == null || tag == null) return null;
            pawn.Drawer.renderer.EnsureGraphicsInitialized();
            pawn.Drawer.renderer.renderTree.TryGetNodeByTag(tag, out var node);
            return node;
        }
    }

    public static class FacilityPortrait
    {
        [ThreadStatic] private static Pawn headOnlyPawn;
        private static readonly AccessTools.FieldRef<PawnRenderTree, PawnDrawParms> PreparedParms =
            AccessTools.FieldRefAccess<PawnRenderTree, PawnDrawParms>("oldParms");
        public static bool HeadOnlyFor(Pawn pawn) => pawn != null && pawn == headOnlyPawn;

        public readonly struct BodyGraphicLayer
        {
            public readonly Graphic graphic;
            public readonly Vector3 offset;
            public readonly Vector2 size;
            public readonly float depth;
            public BodyGraphicLayer(Graphic graphic,Vector3 offset,Vector2 size,float depth)
            { this.graphic=graphic;this.offset=offset;this.size=size;this.depth=depth; }
        }

        // Prepare an authored sprite once; playback only switches these textures.
        // This path never renders a Pawn or changes its render-tree preparation.
        public static Texture2D FrameTexture(BedAnimationSetting frame,Vector3 offset,float scale,Texture2D mask,Graphic nativeBody=null,Rot4? direction=null,
            Vector3? nativeOffset=null,Vector2? sharedMeshSize=null,IReadOnlyList<BodyGraphicLayer> bodyLayers=null)
        {
            const int size=256;
            var pixels=new Color[size*size];
            var maskPixels=mask.GetPixels32();
            void Layer(Graphic graphic,Vector3 position,float layerScale,Rot4 facing,Vector2? layerSize=null)
            {
                if(graphic==null)return;
                var material=graphic.MatAt(facing);
                var texture=material.mainTexture as Texture2D;
                if(texture==null||texture==BaseContent.BadTex)return;
                var source=texture.GetPixels32();
                var drawSize=layerSize??sharedMeshSize??graphic.drawSize;
                var tintMask=material.GetTexture("_MaskTex") as Texture2D;
                var tintPixels=tintMask?.GetPixels32();
                bool complex=material.shader==ShaderDatabase.CutoutComplex;
                bool skin=material.shader==ShaderDatabase.CutoutSkin;
                bool skinOverride=material.shader==ShaderDatabase.CutoutSkinColorOverride;
                for(int y=0;y<size;y++)for(int x=0;x<size;x++){
                    float u=(((x+.5f)/size-.5f)*2-position.x)/(drawSize.x*layerScale)+.5f;
                    float v=(((y+.5f)/size-.5f)*2-position.z)/(drawSize.y*layerScale)+.5f;
                    if(u<0||u>=1||v<0||v>=1)continue;
                    Color color=source[(int)(v*texture.height)*texture.width+(int)(u*texture.width)];
                    var original=color;
                    if(complex&&tintPixels!=null){
                        Color tint=tintPixels[(int)(v*tintMask.height)*tintMask.width+(int)(u*tintMask.width)];
                        float gate=Mathf.Clamp01(tint.a*255f);tint.r*=gate;tint.g*=gate;
                        color*=Color.Lerp(Color.white,graphic.color,tint.r);
                        color*=Color.Lerp(Color.white,graphic.colorTwo,tint.g);
                    }else if(!complex)color*=graphic.color;
                    if(skin){
                        if(original.a<.1f)continue;
                        color+=material.GetColor("_ShadowColor")*Mathf.Min((1-original.r)*original.r,.11f);
                    }else if(color.a<(skinOverride?.1f:.5f))continue;
                    int i=y*size+x;
                    Color below=pixels[i];
                    float opacity=Mathf.Clamp01(color.a);
                    float covered=below.a*(1-opacity);
                    float alpha=opacity+covered;
                    if(alpha>0)
                        pixels[i]=new Color((color.r*opacity+below.r*covered)/alpha,
                            (color.g*opacity+below.g*covered)/alpha,(color.b*opacity+below.b*covered)/alpha,alpha);
                }
            }
            var facing=direction??Rot4.South;
            void BodyLayers(bool behind)
            {
                if(bodyLayers==null)return;
                foreach(var layer in bodyLayers)
                    if((layer.depth<0)==behind)Layer(layer.graphic,offset+layer.offset*scale,scale,facing,layer.size);
            }
            BodyLayers(true);
            Layer(nativeBody,nativeOffset??offset,scale,facing);
            BodyLayers(false);
            Layer(frame?.graphic,offset+(frame?.offset??Vector3.zero),scale,facing);
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
                pixels[y*size+x].a*=1f-maskPixels[(y*mask.height/size)*mask.width+x*mask.width/size].r/255f;
            var result=new Texture2D(size,size,TextureFormat.RGBA32,false);
            result.name="Facility fixed body frame";result.wrapMode=TextureWrapMode.Clamp;
            result.SetPixels(pixels);result.Apply(false,false);
            return result;
        }

        public static Texture GetTexture(PortraitIngredient ingredient, Vector3 cameraOffset, float cameraZoom, Rot4? direction = null)
        {
            var setting = ingredient.portraitSetting;
            Rot4 rotation = setting.headOnly ? setting.rotation : direction ?? setting.rotation;
            if(!setting.headOnly){
                var bubble=BubblePortraitV57.Capture(ingredient,cameraOffset,cameraZoom,rotation);
                if(bubble!=null)return bubble;
                return PortraitsCache.Get(ingredient.pawn, new Vector2(256, 256), rotation, cameraOffset,
                    cameraZoom, renderClothes: setting.renderClothes, renderHeadgear: setting.renderHeadgear,
                    stylingStation: false, healthStateOverride: PawnHealthState.Mobile);
            }

            // A head-only display is a fixed photograph, not a live expression feed.
            if (ingredient.ownedTexture != null) return ingredient.ownedTexture;
            ingredient.ownedTexture = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32);
            ingredient.ownedTexture.Create();
            var tree = ingredient.pawn.Drawer.renderer.renderTree;
            PawnDrawParms preparedParms = PreparedParms(tree);
            Pawn previous = headOnlyPawn;
            try
            {
                headOnlyPawn = ingredient.pawn;
                // A separate target avoids contaminating the shared full-pawn portrait cache.
                // Center the head itself instead of framing a whole pawn and its body offset.
                Vector3 headCenter = ingredient.pawn.Drawer.renderer.BaseHeadOffsetAt(rotation);
                Find.PawnCacheRenderer.RenderPawn(ingredient.pawn, ingredient.ownedTexture, Vector3.zero,
                    cameraZoom, 0f, rotation, renderHead: ingredient.pawn.health.hediffSet.HasHead, renderHeadgear: setting.renderHeadgear,
                    renderClothes: setting.renderClothes, portrait: true, positionOffset: -headCenter);
            }
            finally
            {
                headOnlyPawn = previous;
                // RenderCache replaces the tree's request list and prepared matrices. A later
                // RenderPawnAt may already have valid world results, so restore that preparation.
                if (preparedParms.pawn != null) tree.ParallelPreDraw(preparedParms);
            }
            return ingredient.ownedTexture;
        }

        public static void Release(PortraitIngredient ingredient)
        {
            if (ingredient.ownedTexture != null)
            {
                ingredient.ownedTexture.Release();
                UnityEngine.Object.Destroy(ingredient.ownedTexture);
            }
            if (ingredient.iconMat != null) UnityEngine.Object.Destroy(ingredient.iconMat);
        }
    }

    [HarmonyPatch(typeof(PawnRenderer), "GetDrawParms")]
    internal static class FacilityPortraitDrawParms
    {
        private static void Postfix(ref PawnDrawParms __result)
        {
            // Distinguish this request from an ordinary portrait in the render-tree cache.
            if (FacilityPortrait.HeadOnlyFor(__result.pawn)) __result.flags |= PawnRenderFlags.NoBody;
        }
    }

    [HarmonyPatch]
    internal static class FacilityPortraitVisibility
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(PawnRenderNodeWorker), nameof(PawnRenderNodeWorker.CanDrawNow));
            var addonWorker = AccessTools.TypeByName("AlienRace.AlienPawnRenderNodeWorker_BodyAddon");
            if (addonWorker != null) yield return AccessTools.DeclaredMethod(addonWorker, "CanDrawNow");
        }

        private static void Postfix(PawnRenderNode __0, PawnDrawParms __1, ref bool __result)
        {
            if(!__result||!__1.Portrait)return;
            if(BubblePortraitV57.HideHairFor(__1.pawn)){
                // Rear-facing body close-ups must not be occluded by native hair or
                // HAR head-attached details. The marker exists only during this capture.
                for(var ancestor=__0;ancestor!=null;ancestor=ancestor.parent)
                    if(ancestor is PawnRenderNode_Hair){__result=false;return;}
                if(__0 is AlienRace.AlienPawnRenderNode_BodyAddon &&
                    AlienRace.AlienPawnRenderNodeWorker_BodyAddon.AddonFromNode(__0).alignWithHead){__result=false;return;}
            }
            if(!FacilityPortrait.HeadOnlyFor(__1.pawn))return;
            if (__0.parent == null) return; // Keep the structural root traversable.
            for (var node = __0; node != null; node = node.parent)
                if (node is PawnRenderNode_Head || node.Props?.tagDef == PawnRenderNodeTagDefOf.Head) return;
            if (__0 is AlienRace.AlienPawnRenderNode_BodyAddon &&
                AlienRace.AlienPawnRenderNodeWorker_BodyAddon.AddonFromNode(__0).alignWithHead) return;
            __result = false;
        }
    }
}

namespace YR_Hentai_Prime_AnimationBed
{
    [HarmonyPatch(typeof(PawnRenderTree), nameof(PawnRenderTree.ParallelPreDraw))]
    internal static class FacilityHeadBehindBaseV32
    {
        private static readonly AccessTools.FieldRef<PawnRenderTree, List<PawnGraphicDrawRequest>> Requests =
            AccessTools.FieldRefAccess<PawnRenderTree, List<PawnGraphicDrawRequest>>("drawRequests");

        private static bool IsHead(PawnRenderNode node)
        {
            for (var ancestor = node; ancestor != null; ancestor = ancestor.parent)
                if (ancestor is PawnRenderNode_Head || ancestor is PawnRenderNode_Hair ||
                    ancestor.Props?.tagDef == PawnRenderNodeTagDefOf.Head) return true;
            return node is AlienRace.AlienPawnRenderNode_BodyAddon &&
                AlienRace.AlienPawnRenderNodeWorker_BodyAddon.AddonFromNode(node).alignWithHead;
        }

        private static void Postfix(PawnRenderTree __instance, PawnDrawParms parms)
        {
            // Take photographs with the native head/hair order, without facility depth changes.
            if (parms.Portrait || FacilityPortrait.HeadOnlyFor(parms.pawn)) return;
            var holder = parms.pawn?.ParentHolder;
            var facility = holder as Building_AnimationBed ?? holder?.ParentHolder as Building_AnimationBed;
            if (facility?.def.defName != "YR_Building_GloryHole") return;
            var requests = Requests(__instance);
            float frontmost = float.NegativeInfinity;
            foreach (var request in requests)
                if (IsHead(request.node)) frontmost = Mathf.Max(frontmost, request.preDrawnComputedMatrix.m13);
            if (float.IsNegativeInfinity(frontmost)) return;
            float shift = Mathf.Min(0f, facility.DrawPos.y - .01f - frontmost);
            if (shift == 0f) return;
            for (int i = 0; i < requests.Count; i++)
            {
                var request = requests[i];
                if (!IsHead(request.node)) continue;
                var matrix = request.preDrawnComputedMatrix;
                matrix.m13 += shift; // One translation preserves every native head/hair depth difference.
                request.preDrawnComputedMatrix = matrix;
                requests[i] = request;
            }
        }
    }
}

namespace YR_Hentai_Prime_AnimationBed
{
    // Read preloaded frame materials only. No graphic/texture creation on draw workers.
    [HarmonyPatch(typeof(PawnRenderNodeWorker), nameof(PawnRenderNodeWorker.GetFinalizedMaterial))]
    public static class FacilityBodyFramesV37
    {
        public static BedAnimationSetting Frame(Pawn pawn) => FindFrame(pawn,false);
        public static BedAnimationSetting OverlayFrame(Pawn pawn) => FindFrame(pawn,true);
        private static BedAnimationSetting FindFrame(Pawn pawn,bool overlay)
        {
            var holder = pawn?.ParentHolder;
            var facility = holder as Building_AnimationBed ?? holder?.ParentHolder as Building_AnimationBed;
            if (facility?.AnimationSettingComp == null) return null;
            foreach (var layer in facility.AnimationSettingComp.bedAnimationSettingAndTicks)
            {
                if (!layer.parentBedAnimationDef.fixedFacingBody || layer.parentBedAnimationDef.bodyOverlay!=overlay) continue;
                BedAnimationSetting selected = null;
                foreach (var frame in layer.bedAnimationSettings)
                    if (frame.graphic != null && frame.tick <= layer.currentTick &&
                        (selected == null || frame.tick >= selected.tick)) selected = frame;
                if (selected == null && layer.bedAnimationSettings.Count != 0)
                    selected = layer.bedAnimationSettings[0];
                if (selected?.graphic != null) return selected;
            }
            return null;
        }
        private static void Postfix(PawnRenderNode node, PawnDrawParms parms, ref Material __result)
        {
            if(!(node is PawnRenderNode_Body))return;
            var bubble=BubblePortraitV57.Body(parms.pawn,false);
            if(bubble!=null){__result=bubble.MatSingle;return;}
            if(parms.Portrait)return;
            var frame = Frame(parms.pawn);
            if (frame == null) return;
            Material material = frame.graphic.MatSingle;
            if (InvisibilityUtility.IsPsychologicallyInvisible(parms.pawn))
                material = InvisibilityMatPool.GetInvisibleMat(material);
            __result = parms.pawn.Drawer.renderer.flasher.GetDamagedMat(material);
        }
    }
}

namespace YR_Hentai_Prime_AnimationBed
{
    public interface IBodyOverlaySourceV46 { Graphic BodyOverlay { get; } }
    [HarmonyPatch(typeof(PawnRenderTree),nameof(PawnRenderTree.Draw))]
    internal static class HumanBodyOverlayV46
    {
        private static readonly AccessTools.FieldRef<PawnRenderTree,List<PawnGraphicDrawRequest>> Requests=
            AccessTools.FieldRefAccess<PawnRenderTree,List<PawnGraphicDrawRequest>>("drawRequests");
        private static void Postfix(PawnRenderTree __instance,PawnDrawParms parms)
        {
            bool bubble=BubblePortraitV57.Capturing(parms.pawn);
            if((parms.Portrait&&!bubble) || !UnsupportedRacesV8.OrdinaryHumanlike(parms.pawn) || FacilityPortrait.HeadOnlyFor(parms.pawn))return;
            Graphic overlay=bubble?BubblePortraitV57.Body(parms.pawn,true):FacilityBodyFramesV37.OverlayFrame(parms.pawn)?.graphic;
            if(bubble&&overlay==null)return;
            if(overlay==null){
                var bed=parms.pawn.CurrentBed();
                if(bed!=null)foreach(var comp in bed.AllComps)
                    if(comp is IBodyOverlaySourceV46 source){overlay=source.BodyOverlay;if(overlay!=null)break;}
            }
            if(overlay==null)return;
            foreach(var request in Requests(__instance)){
                if(!(request.node is PawnRenderNode_Body))continue;
                var mesh=request.node.GetMesh(parms);if(mesh==null)return;
                var matrix=request.preDrawnComputedMatrix;matrix.m13+=.001f;
                var adjustment=CompAnimationPosition.For(parms.pawn);
                if(adjustment!=null&&!bubble){
                    // Base-body/leg arrows must not also translate the torso overlay.
                    matrix=AnimationPositionNative.WithoutUserScale(__instance,request.node,matrix);
                    matrix*=Matrix4x4.Scale(new Vector3(adjustment.RenderScale(parms.pawn,"bodyOverlay"),1f,adjustment.RenderScale(parms.pawn,"bodyOverlay")));
                    var delta=adjustment.Offset(parms.pawn,"bodyOverlay")-adjustment.Offset(parms.pawn,"body");
                    matrix.m03+=delta.x;matrix.m23+=delta.z;
                }
                Material material=overlay.MatSingle;
                if(InvisibilityUtility.IsPsychologicallyInvisible(parms.pawn))material=InvisibilityMatPool.GetInvisibleMat(material);
                material=parms.pawn.Drawer.renderer.flasher.GetDamagedMat(material);
                GenDraw.DrawMeshNowOrLater(mesh,matrix,material,parms.flags.FlagSet(PawnRenderFlags.DrawNow));
                return;
            }
        }
    }
}

namespace YR_Hentai_Prime_AnimationBed
{
    [HarmonyPatch(typeof(AnimationWorker_Keyframes), nameof(AnimationWorker_Keyframes.OffsetAtTick))]
    internal static class FacilityBodyDepthV56
    {
        private static void Postfix(AnimationDef def, PawnRenderNode node, PawnDrawParms parms, ref Vector3 __result)
        {
            if (parms.Portrait || !(node is PawnRenderNode_Body)) return;
            // These legacy animations hid the original body 180 units below the map,
            // then drew a separate body sprite. A native material replacement inherits
            // that hiding offset unless we remove it; retain the authored x/z motion.
            if (def.defName != "YR_Global_Animation_BodyChange_Sound" &&
                def.defName != "YR_Global_Animation_BodyChange_Sound_NoMove") return;
            bool visible = FacilityBodyFramesV37.Frame(parms.pawn) != null ||
                FacilityBodyFramesV37.OverlayFrame(parms.pawn) != null;
            if (!visible)
            {
                var adjustment = CompAnimationPosition.For(parms.pawn);
                visible = adjustment?.SupportsBodyChoice == true && adjustment.BodyChoice(parms.pawn) == "native";
            }
            if (visible && __result.y == -180f) __result.y = 0f;
        }
    }
}

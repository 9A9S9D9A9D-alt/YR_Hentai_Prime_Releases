using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace YR_RJW
{
    // Optional SA0 coexistence. No SA0 dependency and no changes to SA0 settings/files.
    [StaticConstructorOnStartup]
    internal static class SizedApparelCompatV34
    {
        private sealed class BodyState { public volatile bool dedicated; }
        private static readonly ConditionalWeakTable<Pawn, BodyState> Bodies = new ConditionalWeakTable<Pawn, BodyState>();
        private static readonly HashSet<string> OwnedBodies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        static SizedApparelCompatV34()
        {
            var worker = AccessTools.TypeByName("SizedApparel.SizedApparelRenderNodeWorker");
            if (worker == null) return;
            var content = DefDatabase<ThingDef>.GetNamedSilentFail("YR_AP_Tentacle")?.modContentPack;
            if (content == null) return;
            foreach (string folder in new[] { "Content", "YuranRaceMod", "AntyRaceMod", "MinchoRaceMod", "Furmaru" })
            {
                string textures = Path.Combine(content.RootDir, folder, "Textures");
                if (!Directory.Exists(textures)) continue;
                foreach (string file in Directory.EnumerateFiles(textures, "*.png", SearchOption.AllDirectories))
                {
                    string path = file.Substring(textures.Length + 1).Replace('\\', '/');
                    path = path.Substring(0, path.Length - 4);
                    foreach (string suffix in new[] { "_south", "_north", "_east", "_west" })
                        if (path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) { path = path.Substring(0, path.Length - suffix.Length); break; }
                    if (path.IndexOf("/Bodies/AddonGraphic/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        (path.StartsWith("Yuran/Things/Building/Furniture/", StringComparison.OrdinalIgnoreCase) &&
                         path.IndexOf("/Body/Race/", StringComparison.OrdinalIgnoreCase) >= 0)) OwnedBodies.Add(path);
                }
            }
            var harmony = new Harmony("local.yuran.sa0.coexistence.v34");
            Patch(harmony, "SizedApparel.MatsBodyBastAtPatch", "Postfix", nameof(PreserveDedicatedBody), true);
            Patch(harmony, "SizedApparel.GetApparelGraphicFix", "Postfix", nameof(PreserveRootApparel), true);
            Patch(harmony, "SizedApparel.SizedApparelRenderNodeWorker", "CanDrawNow", nameof(BodyVisibility), false);
            Patch(harmony, "SizedApparel.SizedApparelApparelRenderNodeWorker", "CanDrawNow", nameof(ApparelVisibility), false);
            Log.Message("[YRP SA0] Optional dedicated-body, facility-node and head-photograph compatibility installed.");
        }

        private static void Patch(Harmony harmony, string typeName, string methodName, string handler, bool prefix)
        {
            Type type = AccessTools.TypeByName(typeName);
            MethodInfo method = type == null ? null : AccessTools.DeclaredMethod(type, methodName);
            if (method == null)
            {
                Log.Warning("[YRP SA0] Unsupported SA0 API: " + typeName + "." + methodName);
                return;
            }
            var patch = new HarmonyMethod(typeof(SizedApparelCompatV34), handler);
            if (prefix) harmony.Patch(method, prefix: patch);
            else harmony.Patch(method, postfix: patch);
        }

        // Arguments belong to SA0's postfix method, rather than a native method return value.
        private static bool PreserveDedicatedBody(Graphic __0, Pawn __1)
        {
            if (__1 == null) return true;
            if(CompSA0PillarV45.EnabledFor(__1)){
                Bodies.GetValue(__1,_=>new BodyState()).dedicated=false;
                return true;
            }
            bool dedicated = __0?.path != null && OwnedBodies.Contains(__0.path);
            Bodies.GetValue(__1, _ => new BodyState()).dedicated = dedicated;
            return !dedicated;
        }

        private static bool PreserveRootApparel(Apparel __0)
            => !RootApparelGraphicFallback.RootBody(__0) && !RootApparelGraphicFallback.RootHead(__0) &&
               __0?.def.defName != "YR_AP_Golem";

        private static void BodyVisibility(PawnRenderNode __0, PawnDrawParms __1, ref bool __result)
        {
            if (!__result || __1.pawn == null) return;
            if (YR_Hentai_Prime_AnimationBed.FacilityPortrait.HeadOnlyFor(__1.pawn)) { __result = false; return; }
            if(YR_Hentai_Prime_AnimationBed.BubblePortraitV57.Body(__1.pawn,false)!=null){__result=false;return;}
            if (__1.Portrait) return;
            if(CompSA0PillarV45.EnabledFor(__1.pawn))return;
            bool dedicated = Bodies.TryGetValue(__1.pawn, out var state) && state.dedicated;
            if (dedicated || YR_Hentai_Prime_AnimationBed.FacilityBodyFramesV37.Frame(__1.pawn) != null || FacilityRender.For(__1.pawn)?.BoundGraphic != null) __result = false;
        }

        private static void ApparelVisibility(PawnRenderNode __0, PawnDrawParms __1, ref bool __result)
        {
            if (!__result || __1.pawn == null) return;
            if (YR_Hentai_Prime_AnimationBed.FacilityPortrait.HeadOnlyFor(__1.pawn)) { __result = false; return; }
            if (__1.Portrait || !FacilityRender.Manual || FacilityRender.For(__1.pawn) == null || __0.apparel == null) return;
            bool headgear = __0.apparel.def.apparel.LastLayer == ApparelLayerDefOf.Overhead ||
                __0.apparel.def.apparel.LastLayer == ApparelLayerDefOf.EyeCover;
            if (!FacilityRender.ManualFlags.FlagSet(headgear ? PawnRenderFlags.Headgear : PawnRenderFlags.Clothes)) __result = false;
        }
    }
}

namespace YR_RJW
{
    public class CompProperties_SA0PillarV45 : CompProperties_Building_BaseBondageBed
    {
        public CompProperties_SA0PillarV45(){compClass=typeof(CompSA0PillarV45);}
    }
    public class CompSA0PillarV45 : Comp_Building_BaseBondageBed
    {
        private bool useSA0;
        public static bool Available => AccessTools.TypeByName("SizedApparel.SizedApparelRenderNodeWorker")!=null;
        public static bool EnabledFor(Pawn pawn)
        {
            var facility=FacilityRender.For(pawn);
            return facility?.parent.def.defName=="YR_Building_BellyPunch" && Available &&
                facility.parent.TryGetComp<CompSA0PillarV45>()?.useSA0==true &&
                YR_Hentai_Prime_AnimationBed.UnsupportedRacesV8.OrdinaryHumanlike(pawn) &&
                YR_Hentai_Prime_AnimationBed.AdultEligibility.IsAllowed(pawn);
        }
        private static void Refresh(Pawn pawn){
            pawn?.Drawer.renderer.SetAllGraphicsDirty();
            if(pawn!=null)PortraitsCache.SetDirty(pawn);
        }
        public override void PostExposeData(){base.PostExposeData();Scribe_Values.Look(ref useSA0,"pillarUseSA0BodyV45",false);}
        public override void PostSetOccupantByAnother(Pawn pawn){base.PostSetOccupantByAnother(pawn);if(useSA0)Refresh(pawn);}
        public override void PostRemoveOccupantByAnother(Pawn pawn){base.PostRemoveOccupantByAnother(pawn);if(useSA0)Refresh(pawn);}
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if(!Available)yield break;
            yield return new Command_Toggle {
                defaultLabel="SA0 체형 사용",
                defaultDesc="켜면 사랑의 기둥에서 고정 몸체 대신 SA0 몸체와 추가 부품을 사용합니다. 끄면 기존 몸체 표시로 돌아갑니다. SA0의 종족 지원·설정에 따릅니다.",
                icon=TexCommand.DesirePower,
                isActive=()=>useSA0,
                toggleAction=()=>{useSA0=!useSA0;Refresh(pawn);}
            };
        }
    }
    [HarmonyPatch(typeof(CompChangeBedTexture),nameof(CompChangeBedTexture.BoundGraphic),MethodType.Getter)]
    internal static class PillarSA0BodyV45
    {
        private static bool Prefix(CompChangeBedTexture __instance,ref Graphic __result)
        {
            if(!CompSA0PillarV45.EnabledFor(__instance.pawn))return true;
            __result=null;return false;
        }
    }
}

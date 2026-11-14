using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace YuranLivingCoat
{
    [StaticConstructorOnStartup]
    internal static class FixedHelmet
    {
        internal const string DefName = "YR_AP_FixedModeHelmet";
        internal const int IntroFrames = 8;
        internal const int FrameCount = 24;
        const int IntroFrameTicks = 4;
        const int IdleFrameTicks = 5;
        static readonly FieldInfo Addon = AccessTools.Field("AlienRace.AlienPawnRenderNodeProperties_BodyAddon:addon");
        static readonly FieldInfo AddonPath = AccessTools.Field("AlienRace.ExtendedGraphics.AbstractExtendedGraphic:path")
            ?? AccessTools.Field(AccessTools.TypeByName("AlienRace.AlienPartGenerator+BodyAddon"), "path");

        [ThreadStatic] static Pawn legacyDrawPawn;
        [ThreadStatic] static bool legacyPortrait;

        static FixedHelmet()
        {
            var harmony = new Harmony("local.yuran.fixedHelmet");
            harmony.Patch(AccessTools.Method(typeof(PawnRenderTree), "AdjustParms"),
                postfix: new HarmonyMethod(typeof(FixedHelmet), nameof(HideHair)));
            harmony.Patch(AccessTools.Method(typeof(PawnRenderNodeWorker), nameof(PawnRenderNodeWorker.CanDrawNow)),
                postfix: new HarmonyMethod(typeof(FixedHelmet), nameof(HideHairAndBrows)));
            harmony.Patch(AccessTools.Method(typeof(PawnRenderTree), nameof(PawnRenderTree.ParallelPreDraw)),
                postfix: new HarmonyMethod(typeof(FixedHelmet), nameof(FilterHairRequests)) { priority = Priority.Last });
            var addonWorker = AccessTools.TypeByName("AlienRace.AlienPawnRenderNodeWorker_BodyAddon");
            if (addonWorker == null || Addon == null || AddonPath == null)
            {
                var legacyDraw = AccessTools.Method("AlienRace.HarmonyPatches:DrawAddons");
                var legacyCheck = AccessTools.Method(
                    AccessTools.TypeByName("AlienRace.AlienPartGenerator+BodyAddon"), "CanDrawAddon", new[] { typeof(Pawn) });
                if (legacyDraw != null && legacyCheck != null && AddonPath != null)
                {
                    harmony.Patch(legacyDraw,
                        prefix: new HarmonyMethod(typeof(FixedHelmet), nameof(BeginLegacyDraw)),
                        finalizer: new HarmonyMethod(typeof(FixedHelmet), nameof(EndLegacyDraw)));
                    harmony.Patch(legacyCheck, postfix: new HarmonyMethod(typeof(FixedHelmet), nameof(HideLegacyHair)));
                }
                return;
            }
            harmony.Patch(AccessTools.DeclaredMethod(addonWorker, "OffsetFor"),
                postfix: new HarmonyMethod(typeof(FixedHelmet), nameof(LiftEars)));
            harmony.Patch(AccessTools.DeclaredMethod(addonWorker, "CanDrawNow"),
                postfix: new HarmonyMethod(typeof(FixedHelmet), nameof(HideAddonHair)));
        }

        internal static Apparel Wear(Pawn pawn)
        {
            var def = DefDatabase<ThingDef>.GetNamedSilentFail(DefName);
            if (def == null || pawn.apparel == null || !ApparelUtility.HasPartsToWear(pawn, def)) return null;
            var helmet = (Apparel)ThingMaker.MakeThing(def);
            if (!helmet.PawnCanWear(pawn, ignoreGender: true))
            {
                helmet.Destroy();
                return null;
            }

            var toStow = new List<(Apparel item, bool locked, bool forced)>();
            var tracker = pawn.apparel;
            foreach (Apparel item in tracker.WornApparel)
                if (item.def.apparel.LastLayer == ApparelLayerDefOf.Overhead ||
                    item.def.apparel.LastLayer == ApparelLayerDefOf.EyeCover ||
                    !ApparelUtility.CanWearTogether(def, item.def, pawn.RaceProps.body))
                    toStow.Add((item, tracker.IsLocked(item), pawn.outfits?.forcedHandler.IsForced(item) == true));

            var inventory = pawn.inventory?.innerContainer;
            int moved = 0;
            foreach (var (item, _, _) in toStow)
            {
                if (inventory?.TryAddOrTransfer(item, canMergeWithExistingStacks: false) != true)
                    break;
                moved++;
            }
            if (moved == toStow.Count)
            {
                tracker.Wear(helmet, dropReplacedApparel: false, locked: true);
                if (helmet.Wearer == pawn) return helmet;
            }

            for (int i = moved - 1; i >= 0; i--)
            {
                var (item, locked, forced) = toStow[i];
                inventory.Remove(item);
                tracker.Wear(item, dropReplacedApparel: false, locked: locked);
                if (forced && item.Wearer == pawn) pawn.outfits?.forcedHandler.SetForced(item, true);
            }
            helmet.Destroy();
            return null;
        }

        internal static FixedModeComp ModeFor(Pawn pawn)
        {
            if (pawn?.apparel == null) return null;
            foreach (Apparel item in pawn.apparel.WornApparel)
                if (item.TryGetComp<FixedModeComp>() is { HasFixedHelmet: true } mode)
                    return mode;
            return null;
        }

        internal static int Frame(int elapsed)
        {
            int introTicks = IntroFrameTicks * IntroFrames;
            if (elapsed < introTicks) return elapsed / IntroFrameTicks;
            return IntroFrames + (elapsed - introTicks) / IdleFrameTicks % (FrameCount - IntroFrames);
        }

        static bool Sealed(PawnDrawParms parms)
        {
            if (!PawnRenderNodeWorker_Apparel_Head.HeadgearVisible(parms) ||
                parms.flags.FlagSet(PawnRenderFlags.HeadStump)) return false;
            var mode = ModeFor(parms.pawn);
            return mode != null && (parms.Portrait || Frame(mode.HelmetElapsed) >= IntroFrames - 1);
        }

        static void HideHair(ref PawnDrawParms parms)
        {
            if (!Sealed(parms)) return;
            parms.skipFlags |= RenderSkipFlagDefOf.Hair;
            parms.skipFlags |= RenderSkipFlagDefOf.Beard;
            parms.skipFlags |= RenderSkipFlagDefOf.Eyes;
        }

        static void HideHairAndBrows(PawnRenderNode node, PawnDrawParms parms, ref bool __result)
        {
            if (__result && (node is PawnRenderNode_Hair || node.Props.debugLabel == "BrowControllerComp_Basic") && Sealed(parms))
                __result = false;
        }

        static string AddonTexturePath(PawnRenderNode node)
        {
            var addon = Addon.GetValue(node.Props);
            return addon == null ? null : AddonPath.GetValue(addon) as string;
        }

        static void FilterHairRequests(PawnDrawParms parms, List<PawnGraphicDrawRequest> ___drawRequests)
        {
            if (!Sealed(parms)) return;
            for (int i = ___drawRequests.Count - 1; i >= 0; i--)
            {
                var request = ___drawRequests[i];
                var node = request.node;
                if (node is PawnRenderNode_Hair || node.Props.debugLabel == "BrowControllerComp_Basic" ||
                    (node.apparel == null && (IsHairTexture(node.PrimaryGraphic?.path) ||
                        IsHairTexture(request.material?.mainTexture?.name))))
                {
                    // Draw requests are cached. Rebuild when a later draw needs the original hair.
                    node.requestRecache = true;
                    ___drawRequests.RemoveAt(i);
                }
            }
        }

        static void BeginLegacyDraw(bool portrait, Pawn pawn, out (Pawn, bool) __state)
        {
            __state = (legacyDrawPawn, legacyPortrait);
            legacyDrawPawn = pawn;
            legacyPortrait = portrait;
        }

        static void EndLegacyDraw((Pawn, bool) __state) => (legacyDrawPawn, legacyPortrait) = __state;

        static void HideLegacyHair(object __instance, Pawn pawn, ref bool __result)
        {
            if (!__result || legacyDrawPawn != pawn || !IsHairTexture(AddonPath.GetValue(__instance) as string))
                return;
            if (ModeFor(pawn) is { } mode && (legacyPortrait ? !Prefs.HatsOnlyOnMap : Frame(mode.HelmetElapsed) >= IntroFrames - 1))
                __result = false;
        }

        static bool IsHairTexture(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            // HAR hair can be a separate add-on, such as AddonHair, BackHairs, or Dummy/Large_Hair_Lower.
            foreach (string segment in path.Split('/'))
            {
                if (segment.StartsWith("Hair", StringComparison.OrdinalIgnoreCase) ||
                    segment.StartsWith("AddonHair", StringComparison.OrdinalIgnoreCase) ||
                    segment.StartsWith("BackHair", StringComparison.OrdinalIgnoreCase) ||
                    segment.IndexOf("_Hair", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        static void HideAddonHair(PawnRenderNode node, PawnDrawParms parms, ref bool __result)
        {
            if (!__result || !Sealed(parms)) return;
            string path = AddonTexturePath(node);
            string activePath = node.PrimaryGraphic?.path;
            if (IsHairTexture(path) || (activePath != path && IsHairTexture(activePath)))
                __result = false;
        }
        static void LiftEars(PawnRenderNode node, PawnDrawParms parms, ref Vector3 __result)
        {
            if (AddonTexturePath(node)?.StartsWith("Yuran/Yuranlike/Ear/", StringComparison.Ordinal) == true &&
                ModeFor(parms.pawn) != null &&
                PawnRenderNodeWorker_Apparel_Head.HeadgearVisible(parms))
                __result.y += .30f;
        }
    }

    public sealed class FixedHelmetNode : PawnRenderNode_Apparel
    {
        readonly Graphic[] frames = new Graphic[FixedHelmet.FrameCount];

        public FixedHelmetNode(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree)
            : base(pawn, props, tree) { }

        protected override IEnumerable<Graphic> GraphicsFor(Pawn pawn)
        {
            for (int frame = 0; frame < frames.Length; frame++)
            {
                frames[frame] ??= GraphicDatabase.Get<Graphic_Multi>(
                    "YuranLivingCoat/FixedHelmet/Mask" + frame.ToString("00"),
                    ShaderDatabase.Cutout, Vector2.one, Color.white);
                yield return frames[frame];
            }
        }

        internal Graphic At(int index) => frames[index];
    }

    public sealed class FixedHelmetWorker : PawnRenderNodeWorker_Apparel_Head
    {
        protected override Graphic GetGraphic(PawnRenderNode node, PawnDrawParms parms)
        {
            var mode = FixedHelmet.ModeFor(parms.pawn);
            if (mode == null || parms.flags.FlagSet(PawnRenderFlags.HeadStump)) return null;
            return ((FixedHelmetNode)node).At(parms.Portrait ? FixedHelmet.IntroFrames : FixedHelmet.Frame(mode.HelmetElapsed));
        }
    }
}

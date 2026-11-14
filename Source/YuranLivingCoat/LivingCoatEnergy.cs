using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace YuranLivingCoat
{
    [StaticConstructorOnStartup]
    static class LivingCoatHeartbeat
    {
        const string CoatDef = "YR_AP_LivingCoat";
        const string CoatPath = "YuranLivingCoat/Finished/Coat";

        static LivingCoatHeartbeat()
        {
            var harmony = new Harmony("local.yuran.livingCoatHeartbeat");
            harmony.Patch(
                AccessTools.Method(typeof(DynamicPawnRenderNodeSetup_Apparel), nameof(DynamicPawnRenderNodeSetup_Apparel.GetDynamicNodes)),
                postfix: new HarmonyMethod(typeof(LivingCoatHeartbeat), nameof(ReplaceCoat)));
            harmony.Patch(
                AccessTools.Method(typeof(ApparelGraphicRecordGetter), nameof(ApparelGraphicRecordGetter.TryGetGraphicApparel)),
                prefix: new HarmonyMethod(typeof(LivingCoatHeartbeat), nameof(UseCoatFallback)) { priority = Priority.First });
        }

        static bool UseCoatFallback(Apparel apparel, BodyTypeDef bodyType, bool forStatue,
            ref ApparelGraphicRecord rec, ref bool __result)
        {
            if (apparel?.def.defName != CoatDef || apparel.WornGraphicPath != CoatPath ||
                bodyType == BodyTypeDefOf.Thin)
                return true;

            if (bodyType != null && ContentFinder<Texture2D>.Get(CoatPath + "_" + bodyType.defName + "_south", false) != null)
                return true;

            Shader shader = ShaderDatabase.Cutout;
            if (!forStatue)
            {
                if (apparel.StyleDef?.graphicData.shaderType != null)
                    shader = apparel.StyleDef.graphicData.shaderType.Shader;
                else if (apparel.StyleDef?.UseWornGraphicMask ?? apparel.def.apparel.useWornGraphicMask)
                    shader = ShaderDatabase.CutoutComplex;
            }

            var graphic = GraphicDatabase.Get<Graphic_Multi>(CoatPath + "_Thin", shader,
                apparel.def.graphicData.drawSize, apparel.DrawColor);
            rec = new ApparelGraphicRecord(graphic, apparel);
            __result = true;
            return false;
        }

        static void ReplaceCoat(Pawn pawn, PawnRenderTree tree,
            ref IEnumerable<(PawnRenderNode node, PawnRenderNode parent)> __result)
        {
            __result = WithHeartbeat(pawn, tree, __result);
        }

        static IEnumerable<(PawnRenderNode node, PawnRenderNode parent)> WithHeartbeat(Pawn pawn, PawnRenderTree tree,
            IEnumerable<(PawnRenderNode node, PawnRenderNode parent)> nodes)
        {
            foreach (var (node, parent) in nodes)
            {
                if (node?.apparel?.def.defName == "YR_AP_LivingCoat" && parent == null
                    && node.Props.debugLabel == "Living coat front collar" && pawn.story?.bodyType?.defName == "Thin")
                    continue;
                if (node?.apparel?.def.defName == "YR_AP_LivingCoat" && parent != null
                    && node.Props.workerClass == typeof(PawnRenderNodeWorker_Apparel_Body))
                {
                    node.Props.workerClass = typeof(EnergyFlowWorker);
                    yield return (new EnergyFlowNode(pawn, node.Props, tree, node.apparel), parent);
                    continue;
                }
                yield return (node, parent);
            }
        }
    }

    public sealed class EnergyFlowNode : PawnRenderNode_Apparel
    {
        internal const int FrameCount = 20;
        const string Root = "YuranLivingCoat/Energy/";
        internal static readonly bool Preview = GenCommandLine.CommandLineArgPassed("yrpLivingCoatEnergyPreview");
        readonly Graphic[] frames = new Graphic[FrameCount];
        Graphic rest;

        public EnergyFlowNode(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree, Apparel apparel)
            : base(pawn, props, tree, apparel) { }

        protected override IEnumerable<Graphic> GraphicsFor(Pawn pawn)
        {
            if (!ApparelGraphicRecordGetter.TryGetGraphicApparel(apparel, pawn.story.bodyType,
                pawn.Drawer.renderer.StatueColor.HasValue, out var record))
                yield break;
            rest = record.graphic;
            yield return rest;
            if (pawn.story.bodyType?.defName != "Thin")
                yield break;
            for (int frame = 0; frame < FrameCount; frame++)
            {
                frames[frame] = GraphicDatabase.Get<Graphic_Multi>(
                    Root + "Flow" + frame.ToString("00"), ShaderDatabase.Cutout,
                    apparel.def.graphicData.drawSize, apparel.DrawColor);
                yield return frames[frame];
            }
        }

        internal Graphic GraphicAt(PawnDrawParms parms)
        {
            if (parms.pawn.story?.bodyType?.defName != "Thin" || frames[0] == null)
                return rest;
            if (parms.Portrait || Find.TickManager == null)
                return frames[0];

            const int ticksPerFrame = 5;
            const int cycleTicks = FrameCount * ticksPerFrame;
            int ticks = Preview ? (int)(Time.realtimeSinceStartup * 60f) : Find.TickManager.TicksGame;
            int phase = (ticks + apparel.thingIDNumber % cycleTicks) % cycleTicks;
            return frames[phase / ticksPerFrame];
        }
    }

    public sealed class EnergyFlowWorker : PawnRenderNodeWorker_Apparel_Body
    {
        static readonly HashSet<string> SeenFrames = new();
        protected override Graphic GetGraphic(PawnRenderNode node, PawnDrawParms parms) =>
            ((EnergyFlowNode)node).GraphicAt(parms);

        public override float LayerFor(PawnRenderNode node, PawnDrawParms parms)
        {
            // The south frame includes the collar. Keep its original layer below the hair (62).
            return (parms.facing == Rot4.South && parms.pawn.story?.bodyType?.defName == "Thin" ? 55f : 49f)
                + node.debugLayerOffset;
        }

        public override void PostDraw(PawnRenderNode node, PawnDrawParms parms, Mesh mesh, Matrix4x4 matrix)
        {
            if (!EnergyFlowNode.Preview || parms.Portrait || parms.pawn.Name?.ToString() != "유란")
                return;
            string texture = GetGraphic(node, parms)?.MatAt(parms.facing)?.mainTexture?.name;
            if (texture?.StartsWith("Flow") == true && SeenFrames.Add(texture))
                Log.Message("[LivingCoatEnergy] " + texture);
        }
    }
}

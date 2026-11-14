using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace YR_RJW
{
    public sealed partial class HediffComp_CoccyxCombat
    {
        private Gizmo GasArmorCommand()
        {
            var command = new Command_Toggle
            {
                defaultLabel = "폭주하는 최음가스",
                defaultDesc = "생체 헬멧을 장착해 전투용 가스를 순환시킵니다. 다시 눌러 벗으면 사목의 마스크와 같은 후유증이 생깁니다.",
                icon = ContentFinder<Texture2D>.Get("Yuran/UI/Commands/TentacleFocus"),
                isActive = () => CoccyxGasArmor.WornHelmet(Pawn) != null,
                toggleAction = () =>
                {
                    var helmet = CoccyxGasArmor.WornHelmet(Pawn);
                    if (helmet != null)
                        helmet.TakeOff(Pawn);
                    else if (Pawn.Spawned && !Pawn.Downed && !Pawn.Dead && Type2Active)
                        CoccyxGasArmor.WearHelmet(Pawn);
                }
            };
            if (CoccyxGasArmor.WornHelmet(Pawn) == null && (!Pawn.Spawned || Pawn.Downed))
                command.Disable("움직일 수 있을 때 사용합니다.");
            return command;
        }
    }

    public sealed class CoccyxGasHelmetApparel : Apparel
    {
        internal void TakeOff(Pawn pawn)
        {
            if (Wearer != pawn)
                return;
            pawn.apparel.Remove(this);
            Destroy(DestroyMode.Vanish);
        }

        public override IEnumerable<Gizmo> GetWornGizmos()
        {
            foreach (var gizmo in base.GetWornGizmos())
                yield return gizmo;

            var pawn = Wearer;
            if (pawn?.Faction == Faction.OfPlayer &&
                !pawn.health.hediffSet.HasHediff(HediffDef.Named("YR_CoccyxTentacleImplant")))
                yield return new Command_Action
                {
                    defaultLabel = "생체 가스 헬멧 벗기",
                    defaultDesc = "헬멧을 벗으면 사목의 마스크와 같은 후유증이 생깁니다.",
                    icon = def.uiIcon,
                    action = () => TakeOff(pawn)
                };
        }

        public override void Notify_Equipped(Pawn pawn)
        {
            base.Notify_Equipped(pawn);
            if (CoccyxGasArmor.Active(pawn) == null)
                pawn.health.AddHediff(CoccyxGasArmor.Def);
            PlayMaskSound(pawn);
        }

        public override void Notify_Unequipped(Pawn pawn)
        {
            base.Notify_Unequipped(pawn);
            var gasArmor = CoccyxGasArmor.Active(pawn);
            if (gasArmor != null)
                pawn.health.RemoveHediff(gasArmor);
            PlayMaskSound(pawn);
        }

        protected override void Tick()
        {
            base.Tick();
            var pawn = Wearer;
            if (pawn == null || pawn.Dead || !pawn.IsHashIntervalTick(200))
                return;
            var gasDef = def.GetCompProperties<CompProperties_Tentacle_Mask>()?.hediffDef;
            if (gasDef == null)
                return;
            var gas = pawn.health.hediffSet.GetFirstHediffOfDef(gasDef);
            if (gas == null)
                pawn.health.AddHediff(gasDef);
            else if (gas.Severity < .02f)
                gas.Severity = gasDef.initialSeverity;
        }

        private static void PlayMaskSound(Pawn pawn)
        {
            if (pawn.Spawned)
                DefDatabase<SoundDef>.GetNamed("YR_AP_Tentacle_Headgear_Mask_Sound").PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
        }
    }

    public sealed class CoccyxGasArmor : HediffWithComps
    {
        internal const string ApparelName = "YR_AP_YuranBioArmor";
        internal const string HelmetName = "YR_AP_CoccyxGasHelmet";
        internal const string TextureRoot = "Yuran/Apparel/YuranBioArmor/GasSkill/";
        private const int CloseTicks = 16;
        private const int FrameTicks = 10;
        private const int GasFillTicks = 18;
        private const int FullGasFrame = 3;
        internal const int HelmetFrameCount = 7;
        private static HediffDef definition;
        private int drawFrame = -1;

        internal static HediffDef Def => definition ?? (definition = DefDatabase<HediffDef>.GetNamed("YR_CoccyxGasArmor"));
        internal static CoccyxGasArmor Active(Pawn pawn) =>
            pawn.health?.hediffSet.GetFirstHediffOfDef(Def) as CoccyxGasArmor;

        internal static CoccyxGasHelmetApparel WornHelmet(Pawn pawn)
        {
            if (pawn.apparel != null)
                foreach (var apparel in pawn.apparel.WornApparel)
                    if (apparel is CoccyxGasHelmetApparel helmet)
                        return helmet;
            return null;
        }

        internal static Apparel WornBioArmor(Pawn pawn)
        {
            if (pawn.apparel != null)
                foreach (var apparel in pawn.apparel.WornApparel)
                    if (apparel.def.defName == ApparelName)
                        return apparel;
            return null;
        }

        internal static void WearHelmet(Pawn pawn)
        {
            var helmetDef = ThingDef.Named(HelmetName);
            if (!ApparelUtility.HasPartsToWear(pawn, helmetDef))
                return;
            var helmet = (CoccyxGasHelmetApparel)ThingMaker.MakeThing(helmetDef);
            if (!helmet.PawnCanWear(pawn, ignoreGender: true))
            {
                helmet.Destroy(DestroyMode.Vanish);
                return;
            }

            var inventory = pawn.inventory?.innerContainer;
            var toStow = pawn.apparel.WornApparel.Where(item =>
                item.def.apparel.LastLayer == ApparelLayerDefOf.Overhead ||
                item.def.apparel.LastLayer == ApparelLayerDefOf.EyeCover ||
                !ApparelUtility.CanWearTogether(helmetDef, item.def, pawn.RaceProps.body)).ToList();
            foreach (var item in toStow)
                if (inventory?.TryAddOrTransfer(item, canMergeWithExistingStacks: false) != true)
                {
                    helmet.Destroy(DestroyMode.Vanish);
                    Messages.Message("머리 장비를 소지품으로 옮길 수 없습니다.", pawn, MessageTypeDefOf.RejectInput, false);
                    return;
                }

            pawn.apparel.Wear(helmet, dropReplacedApparel: false);
            if (helmet.Wearer != pawn && !helmet.Destroyed)
                helmet.Destroy(DestroyMode.Vanish);
        }

        private int Elapsed => Mathf.Max(0, Find.TickManager.TicksGame - tickAdded);
        internal int Frame => Mathf.Max(0, Elapsed - CloseTicks) / FrameTicks % 4;
        internal int HelmetFrame
        {
            get
            {
                int fill = Mathf.Max(0, Elapsed - CloseTicks) / GasFillTicks;
                return fill < FullGasFrame ? fill : FullGasFrame + Frame;
            }
        }
        internal bool CoversFace => HelmetFrame >= FullGasFrame;
        internal float Closure => Mathf.Clamp01(Elapsed / (float)CloseTicks);

        public override bool ShouldRemove => base.ShouldRemove || WornHelmet(pawn) == null;
        public override HediffStage CurStage => WornBioArmor(pawn) != null && WornHelmet(pawn) != null
            ? base.CurStage : null;

        internal void RequestFrame(PawnRenderNode root)
        {
            int frame = HelmetFrame;
            if (drawFrame == frame || root == null)
                return;
            root.requestRecache = true;
            drawFrame = frame;
        }
    }

    public sealed class PawnRenderNode_CoccyxGasHelmet : PawnRenderNode
    {
        private readonly Graphic[] helmet = new Graphic[CoccyxGasArmor.HelmetFrameCount];

        public PawnRenderNode_CoccyxGasHelmet(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree)
            : base(pawn, props, tree) { }

        protected override IEnumerable<Graphic> GraphicsFor(Pawn pawn)
        {
            for (int frame = 0; frame < helmet.Length; frame++)
            {
                helmet[frame] = helmet[frame] ?? GraphicDatabase.Get<Graphic_Multi>(
                    CoccyxGasArmor.TextureRoot + "Helmet" + frame, ShaderDatabase.Transparent, Vector2.one, Color.white);
                yield return helmet[frame];
            }
        }

        internal Graphic FrameGraphic(int frame) => helmet[frame];
    }

    public sealed class PawnRenderNodeWorker_CoccyxGasHelmet : PawnRenderNodeWorker_FlipWhenCrawling
    {
        public override bool CanDrawNow(PawnRenderNode node, PawnDrawParms parms) =>
            !parms.Portrait && !parms.flags.FlagSet(PawnRenderFlags.HeadStump) &&
            node.hediff is CoccyxGasArmor gas && !gas.ShouldRemove && base.CanDrawNow(node, parms);

        protected override Graphic GetGraphic(PawnRenderNode node, PawnDrawParms parms) =>
            node.hediff is CoccyxGasArmor gas ? ((PawnRenderNode_CoccyxGasHelmet)node).FrameGraphic(gas.HelmetFrame) : null;

        public override Vector3 ScaleFor(PawnRenderNode node, PawnDrawParms parms)
        {
            var scale = base.ScaleFor(node, parms);
            if (node.hediff is CoccyxGasArmor gas)
            {
                scale.x *= Mathf.Lerp(.72f, 1f, gas.Closure);
                scale.z *= Mathf.Lerp(.72f, 1f, gas.Closure);
            }
            return scale;
        }
    }

    [StaticConstructorOnStartup]
    internal static class CoccyxBioArmorAnimation
    {
        internal static readonly Graphic[] Frames = new Graphic[4];

        static CoccyxBioArmorAnimation()
        {
            for (int frame = 0; frame < Frames.Length; frame++)
                Frames[frame] = GraphicDatabase.Get<Graphic_Multi>(
                    CoccyxGasArmor.TextureRoot + "Armor" + frame, ShaderDatabase.Cutout, Vector2.one, Color.white);
        }

        internal static CompApparelScaleV45 EnabledFor(Pawn pawn)
        {
            var comp = CoccyxGasArmor.WornBioArmor(pawn)?.TryGetComp<CompApparelScaleV45>();
            return comp?.AnimationEnabled == true ? comp : null;
        }
    }

    [HarmonyPatch(typeof(PawnRenderTree), nameof(PawnRenderTree.ParallelPreDraw))]
    internal static class CoccyxGasArmorFramePatch
    {
        private static void Prefix(PawnRenderTree __instance, PawnDrawParms parms)
        {
            if (!parms.Portrait)
            {
                CoccyxGasArmor.Active(__instance.pawn)?.RequestFrame(__instance.rootNode);
                CoccyxBioArmorAnimation.EnabledFor(__instance.pawn)?.RequestAnimationFrame(__instance.rootNode);
            }
        }
    }

    [HarmonyPatch(typeof(PawnRenderNodeWorker), "GetGraphic")]
    internal static class CoccyxGasArmorGraphicPatch
    {
        private static void Postfix(PawnRenderNode node, PawnDrawParms parms, ref Graphic __result)
        {
            if (parms.Portrait || __result == null || node.apparel?.def.defName != CoccyxGasArmor.ApparelName ||
                node.parent?.apparel == node.apparel)
                return;
            var animation = node.apparel.TryGetComp<CompApparelScaleV45>();
            if (animation?.AnimationEnabled == true)
                __result = CoccyxBioArmorAnimation.Frames[animation.AnimationFrame] ?? __result;
        }
    }

    [HarmonyPatch(typeof(PawnRenderNodeWorker), nameof(PawnRenderNodeWorker.CanDrawNow))]
    internal static class CoccyxGasArmorBrowPatch
    {
        private static void Postfix(PawnRenderNode node, PawnDrawParms parms, ref bool __result)
        {
            // Facial Animation can put its brow node above the hat when that option is enabled.
            if (__result && !parms.Portrait && parms.facing != Rot4.North &&
                node.Props.debugLabel == "BrowControllerComp_Basic" &&
                CoccyxGasArmor.Active(parms.pawn)?.CoversFace == true)
                __result = false;
        }
    }

    [HarmonyPatch(typeof(PawnRenderer), "ParallelGetPreRenderResults")]
    internal static class CoccyxGasArmorWorldCachePatch
    {
        private static void Prefix(Pawn ___pawn, ref bool disableCache)
        {
            if (!disableCache && (CoccyxGasArmor.Active(___pawn) != null || CoccyxBioArmorAnimation.EnabledFor(___pawn) != null ||
                CoccyxSecretion.Active(___pawn)))
                disableCache = true;
        }
    }
}

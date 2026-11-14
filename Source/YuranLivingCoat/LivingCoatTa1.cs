using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace YuranLivingCoat
{
    [StaticConstructorOnStartup]
    public sealed partial class FixedModeComp : ThingComp
    {
        const int IntroEnd = 30;
        const int StageTwoStart = 82;
        const int StageThreeStart = 134;
        const int LastFrame = 393;
        const int LoopFrames = 26;
        const float BaseTicksPerFrame = 4.08f;
        const float DefaultVertical = -.27f;
        const float DefaultScale = .25f;
        const float UseRaceDefault = float.MaxValue;
        internal const string MihoRace = "Alien_Miho";
        internal const string MinchoWitchRace = "MinchoWitch";
        static readonly Material[] Frames = new Material[LastFrame + 1];
        int startedAt = -1;
        Apparel fixedHelmet;
        float horizontal;
        float vertical = UseRaceDefault;
        float animationWidth = DefaultScale;
        float animationHeight = DefaultScale;
        Dictionary<string, Vector3> fixedPositions = new();
        Dictionary<string, Vector3> fixedSizes = new();
        float eastAngle = UseRaceDefault;
        float westAngle = UseRaceDefault;
        float eastWidth = UseRaceDefault;
        float eastHeight = 1f;
        float westWidth = UseRaceDefault;
        float westHeight = 1f;
        Dictionary<string, Vector3> eastByRace = new();
        Dictionary<string, Vector3> westByRace = new();
        bool adjusting;
        public Pawn Wearer => (parent as Apparel)?.Wearer;
        float DefaultVerticalForWearer =>
            Wearer?.def.defName switch
            {
                MihoRace => -.20f,
                MinchoWitchRace => -.37f,
                _ => DefaultVertical
            };

        static string FitKey(Pawn pawn) =>
            pawn == null ? null : pawn.def.defName + "/" + (pawn.story?.bodyType?.defName ?? "none") + "/" + pawn.gender;

        void MigrateFixedSettings(string key)
        {
            if (key == null) return;
            if (horizontal != 0f || vertical != UseRaceDefault)
                fixedPositions.TryAdd(key, new Vector3(horizontal, 0f, vertical));
            if (animationWidth != DefaultScale || animationHeight != DefaultScale)
                fixedSizes.TryAdd(key, new Vector3(animationWidth, 1f, animationHeight));
            horizontal = 0f;
            vertical = UseRaceDefault;
            animationWidth = animationHeight = DefaultScale;
        }

        void FixedSettings(string key, out Vector3 position, out Vector3 size)
        {
            MigrateFixedSettings(key);
            if (!fixedPositions.TryGetValue(key, out position))
                position = new Vector3(0f, 0f, DefaultVerticalForWearer);
            else
                position.z = ResolveDefault(position.z, DefaultVerticalForWearer);
            if (!fixedSizes.TryGetValue(key, out size))
                size = new Vector3(DefaultScale, 1f, DefaultScale);
        }
        internal bool Active => startedAt >= 0 && Wearer is { Downed: false, Dead: false };

        internal bool HasFixedHelmet => Active && fixedHelmet != null && fixedHelmet.Wearer == Wearer;
        internal int HelmetElapsed => Mathf.Max(0, (Find.TickManager?.TicksGame ?? startedAt) - startedAt);

        float DefaultSideAngle => Wearer?.def.defName == "Yuran_Race" ? 7f : 0f;
        float DefaultSideWidth => Wearer?.def.defName == "Yuran_Race" ? 1.55f : 1f;

        static float ResolveDefault(float value, float fallback) => value == UseRaceDefault ? fallback : value;

        Vector3 SideSettings(Rot4 facing)
        {
            bool east = facing == Rot4.East;
            var settings = east ? eastByRace : westByRace;
            string race = Wearer?.def.defName;
            if (race != null && settings.TryGetValue(race, out var saved))
                return saved;
            return new Vector3(DefaultSideWidth, DefaultSideAngle, 1f);
        }

        internal float AngleFor(Rot4 facing) => SideSettings(facing).y;
        internal void ApplySideScale(Rot4 facing, ref Vector3 size)
        {
            var settings = SideSettings(facing);
            size.x *= settings.x;
            size.z *= settings.z;
        }

        internal void EndMode()
        {
            ResetRift();
            startedAt = -1;
            adjusting = false;
            var helmet = fixedHelmet;
            fixedHelmet = null;
            if (helmet == null || helmet.Destroyed) return;
            helmet.Wearer?.apparel.Remove(helmet);
            helmet.Destroy();
        }

        void Toggle()
        {
            if (Active)
            {
                EndMode();
                return;
            }

            Pawn pawn = Wearer;
            if (pawn == null || !pawn.Spawned || pawn.Downed || pawn.Dead) return;
            fixedHelmet = FixedHelmet.Wear(pawn);
            if (fixedHelmet == null)
            {
                Messages.Message("생체 헬멧을 착용하거나 기존 머리 장비를 소지품으로 옮길 수 없습니다.",
                    pawn, MessageTypeDefOf.RejectInput, false);
                return;
            }
            startedAt = Find.TickManager.TicksGame;
            rootedFluidSince = startedAt;
            pawn.Rotation = Rot4.South;
            pawn.pather?.StopDead();
            pawn.jobs?.StopAll();
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            ExposeRift();
            Scribe_Values.Look(ref startedAt, "ta1StartedAt", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && startedAt >= 0 && rootedFluidSince < 0)
                rootedFluidSince = Find.TickManager.TicksGame;
            Scribe_References.Look(ref fixedHelmet, "fixedModeHelmet");
            Scribe_Values.Look(ref horizontal, "ta1Horizontal");
            Scribe_Values.Look(ref vertical, "ta1Vertical", UseRaceDefault);
            Scribe_Values.Look(ref animationWidth, "ta1Scale", DefaultScale);
            // Older saves have one uniform scale. Use it for both axes when the height key is absent.
            Scribe_Values.Look(ref animationHeight, "ta1HeightScale", animationWidth, forceSave: true);
            Scribe_Collections.Look(ref fixedPositions, "ta1PositionsByFit", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref fixedSizes, "ta1SizesByFit", LookMode.Value, LookMode.Value);
            Scribe_Values.Look(ref eastAngle, "livingCoatEastAngle", UseRaceDefault);
            Scribe_Values.Look(ref westAngle, "livingCoatWestAngle", UseRaceDefault);
            Scribe_Values.Look(ref eastWidth, "livingCoatEastWidth", UseRaceDefault);
            Scribe_Values.Look(ref eastHeight, "livingCoatEastHeight", 1f);
            Scribe_Values.Look(ref westWidth, "livingCoatWestWidth", UseRaceDefault);
            Scribe_Values.Look(ref westHeight, "livingCoatWestHeight", 1f);
            Scribe_Collections.Look(ref eastByRace, "livingCoatEastByRace", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref westByRace, "livingCoatWestByRace", LookMode.Value, LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                fixedPositions ??= new();
                fixedSizes ??= new();
                MigrateFixedSettings(FitKey(Wearer));
                eastByRace ??= new();
                westByRace ??= new();
                string race = Wearer?.def.defName;
                if (race != null)
                {
                    MigrateSide(eastByRace, race, eastWidth, eastAngle, eastHeight);
                    MigrateSide(westByRace, race, westWidth, westAngle, westHeight);
                }
                eastAngle = westAngle = eastWidth = westWidth = UseRaceDefault;
                eastHeight = westHeight = 1f;
            }
            if (Scribe.mode == LoadSaveMode.PostLoadInit && (Wearer is { Downed: true } || (startedAt >= 0 && fixedHelmet == null)))
                EndMode();
        }

        void MigrateSide(Dictionary<string, Vector3> settings, string race, float width, float angle, float height)
        {
            if (width != UseRaceDefault || angle != UseRaceDefault || height != 1f)
                settings.TryAdd(race, new Vector3(ResolveDefault(width, DefaultSideWidth),
                    ResolveDefault(angle, DefaultSideAngle), height));
        }

        public override void Notify_Unequipped(Pawn pawn)
        {
            MigrateFixedSettings(FitKey(pawn));
            base.Notify_Unequipped(pawn);
            EndMode();
        }

        public override IEnumerable<Gizmo> CompGetWornGizmosExtra()
        {
            if (Wearer == null || Wearer.Faction != Faction.OfPlayer) yield break;
            var icon = ContentFinder<Texture2D>.Get("YuranLivingCoat/Finished/Item");
            var toggle = new Command_Toggle
            {
                defaultLabel = "뿌리내림",
                defaultDesc = "제자리에 뿌리를 내려 차원 능력을 사용합니다. 유지하는 동안 이동과 욕구 변화가 멈추고 30분당 백윤액 10개가 쌓입니다.",
                icon = ContentFinder<Texture2D>.Get("YuranLivingCoat/Rift/RootedIcon"),
                isActive = () => Active,
                toggleAction = Toggle
            };
            if (Wearer.Downed) toggle.Disable("쓰러진 상태에서는 뿌리내림을 사용할 수 없습니다.");
            yield return toggle;
            foreach (var gizmo in RiftGizmos(icon)) yield return gizmo;
            foreach (var gizmo in CartridgeHolderGizmos(icon)) yield return gizmo;
            var adjustmentType = AccessTools.TypeByName("YR_RJW.CompApparelScaleV45");
            if (adjustmentType == null || !parent.AllComps.Exists(adjustmentType.IsInstanceOfType))
                yield return new Command_Action
                {
                    defaultLabel = "의상 조정",
                    defaultDesc = "뿌리내림과 외투 옆면을 조정합니다.",
                    icon = icon,
                    groupable = false,
                    action = () => OpenCoatAdjustments(null)
                };
        }

        internal void OpenCoatAdjustments(System.Action apparelAdjustment)
        {
            var options = new List<FloatMenuOption>();
            if (apparelAdjustment != null)
                options.Add(new FloatMenuOption("의상 크기 및 위치", apparelAdjustment));
            options.Add(new FloatMenuOption("뿌리내림 조정",
                () => Find.WindowStack.Add(new PositionWindow(this))));
            options.Add(new FloatMenuOption("외투 옆면 조정",
                () => Find.WindowStack.Add(new SideAdjustmentWindow(this))));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        internal static int FrameAt(float ticks)
        {
            const float stageTwoTick = StageTwoStart * BaseTicksPerFrame;
            const float stageThreeTick = stageTwoTick + (StageThreeStart - StageTwoStart) * BaseTicksPerFrame / 1.6f;
            if (ticks < stageTwoTick) return Mathf.Clamp(Mathf.FloorToInt(ticks / BaseTicksPerFrame), 0, StageTwoStart - 1);
            if (ticks < stageThreeTick)
                return Mathf.Min(StageThreeStart - 1, StageTwoStart + Mathf.FloorToInt((ticks - stageTwoTick) * 1.6f / BaseTicksPerFrame));
            int frame = StageThreeStart + Mathf.FloorToInt((ticks - stageThreeTick) * 2.4f / BaseTicksPerFrame);
            int repeatFrom = LastFrame + 1 - LoopFrames;
            return frame <= LastFrame ? frame : repeatFrom + (frame - repeatFrom) % LoopFrames;
        }

        internal void Draw(Vector3 drawLoc)
        {
            if (Wearer is not { Downed: false, Dead: false } || (!Active && !adjusting)) return;
            int frame = adjusting ? IntroEnd + LoopFrames : FrameAt(Find.TickManager.TicksGame - startedAt);
            if (frame < 3) return;
            if (frame >= IntroEnd + LoopFrames && frame <= StageTwoStart)
                frame = IntroEnd + (frame - IntroEnd) % LoopFrames;
            var material = Frames[frame] ??= MaterialPool.MatFrom(
                "YuranLivingCoat/Ta1/Frame" + frame.ToString("000"), ShaderDatabase.Transparent);
            FixedSettings(FitKey(Wearer), out var offset, out var size);
            float horizontalUnitsPerPixel = .0075f * size.x;
            float verticalUnitsPerPixel = .0075f * size.z;
            // Register the crop to the contact point (286, 136) in the source animation.
            var position = new Vector3(drawLoc.x + offset.x, AltitudeLayer.MoteOverhead.AltitudeFor(),
                drawLoc.z + offset.z - 78f * verticalUnitsPerPixel);
            var matrix = Matrix4x4.TRS(position, Quaternion.identity,
                new Vector3(232f * horizontalUnitsPerPixel, 1f, 236f * verticalUnitsPerPixel));
            GenDraw.DrawMeshNowOrLater(MeshPool.plane10, matrix, material, false);
        }

        static float ScaleSlider(float width, float y, float scale, string label, float minimum = .25f)
        {
            Widgets.Label(new Rect(0f, y, 95f, 28f), label + " " + scale.ToString("0%"));
            return Widgets.HorizontalSlider(new Rect(98f, y + 6f, width - 102f, 24f),
                scale, minimum, 2f, roundTo: .01f);
        }

        sealed class SideAdjustmentWindow : Window
        {
            readonly FixedModeComp comp;
            public override Vector2 InitialSize => new Vector2(410f, 365f);

            public SideAdjustmentWindow(FixedModeComp comp)
            {
                this.comp = comp;
                doCloseButton = true;
                doCloseX = true;
                draggable = true;
                absorbInputAroundWindow = false;
            }

            public override void DoWindowContents(Rect rect)
            {
                string race = comp.Wearer?.def.defName;
                if (race == null) return;
                AdjustSide(rect.width, 0f, Rot4.East, "동쪽", comp.eastByRace, race);
                AdjustSide(rect.width, 124f, Rot4.West, "서쪽", comp.westByRace, race);
                if (Widgets.ButtonText(new Rect(0f, 249f, 100f, 27f), "기본값"))
                {
                    comp.eastByRace.Remove(race);
                    comp.westByRace.Remove(race);
                }
            }

            void AdjustSide(float width, float y, Rot4 facing, string label,
                Dictionary<string, Vector3> settings, string race)
            {
                var current = comp.SideSettings(facing);
                Widgets.Label(new Rect(0f, y, width, 24f), label);
                Widgets.Label(new Rect(0f, y + 24f, 90f, 28f), "각도 " + current.y.ToString("0") + "°");
                float angle = Widgets.HorizontalSlider(new Rect(95f, y + 30f, width - 99f, 24f),
                    current.y, -45f, 45f, roundTo: 1f);
                float horizontal = ScaleSlider(width, y + 55f, current.x, "가로");
                float vertical = ScaleSlider(width, y + 86f, current.z, "세로");
                var adjusted = new Vector3(horizontal, angle, vertical);
                if (adjusted != current) settings[race] = adjusted;
            }
        }

        sealed class PositionWindow : Window
        {
            readonly FixedModeComp comp;
            public override Vector2 InitialSize => new Vector2(380f, 295f);

            public PositionWindow(FixedModeComp comp)
            {
                this.comp = comp;
                doCloseButton = true;
                doCloseX = true;
                draggable = true;
                absorbInputAroundWindow = false;
            }

            public override void PreOpen()
            {
                base.PreOpen();
                comp.adjusting = true;
            }

            public override void PostClose()
            {
                comp.adjusting = false;
                base.PostClose();
            }

            public override void DoWindowContents(Rect rect)
            {
                string key = FitKey(comp.Wearer);
                if (key == null) return;
                comp.FixedSettings(key, out var position, out var size);
                Widgets.Label(new Rect(0f, 0f, rect.width, 27f), "뿌리내림 위치 및 크기");
                float horizontal = OffsetSlider(rect.width, 32f, position.x, "가로");
                float vertical = OffsetSlider(rect.width, 71f, position.z, "세로");
                float width = ScaleSlider(rect.width, 110f, size.x, "가로 크기", .05f);
                float height = ScaleSlider(rect.width, 145f, size.z, "세로 크기", .05f);
                if (!Mathf.Approximately(horizontal, position.x) || !Mathf.Approximately(vertical, position.z))
                    comp.fixedPositions[key] = new Vector3(horizontal, 0f, vertical);
                if (!Mathf.Approximately(width, size.x) || !Mathf.Approximately(height, size.z))
                    comp.fixedSizes[key] = new Vector3(width, 1f, height);
                if (Widgets.ButtonText(new Rect(0f, 185f, 100f, 27f), "기본값"))
                {
                    comp.fixedPositions.Remove(key);
                    comp.fixedSizes.Remove(key);
                }
            }

            static float OffsetSlider(float width, float y, float value, string label)
            {
                Widgets.Label(new Rect(0f, y, 77f, 28f), label + " " + value.ToString("0.00"));
                return Widgets.HorizontalSlider(new Rect(83f, y + 6f, width - 87f, 24f),
                    value, -2f, 2f, roundTo: .01f);
            }
        }
    }

    [StaticConstructorOnStartup]
    static class FixedModeRestrictions
    {
        static FixedModeRestrictions()
        {
            var harmony = new Harmony("local.yuran.livingcoat.ta1.restrictions");
            var whileFree = new HarmonyMethod(typeof(FixedModeRestrictions), nameof(WhileFree));
            harmony.Patch(AccessTools.Method(typeof(Pawn_PathFollower), nameof(Pawn_PathFollower.StartPath)), prefix: whileFree);
            harmony.Patch(AccessTools.Method(typeof(Pawn_PathFollower), nameof(Pawn_PathFollower.PatherTick)), prefix: whileFree);
            harmony.Patch(AccessTools.Method(typeof(Pawn_JobTracker), "TryFindAndStartJob"), prefix: whileFree);
            harmony.Patch(AccessTools.Method(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.StartJob)),
                prefix: new HarmonyMethod(typeof(FixedModeRestrictions), nameof(BlockFlee)));
            harmony.Patch(AccessTools.Method(typeof(Pawn_RotationTracker), nameof(Pawn_RotationTracker.UpdateRotation)),
                prefix: new HarmonyMethod(typeof(FixedModeRestrictions), nameof(KeepFacingSouth)));
            harmony.Patch(AccessTools.Method(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.TryTakeOrderedJob)), prefix: whileFree);
            harmony.Patch(AccessTools.Method(typeof(Pawn_NeedsTracker), nameof(Pawn_NeedsTracker.NeedsTrackerTickInterval)), prefix: whileFree);
            harmony.Patch(AccessTools.PropertySetter(typeof(Need), nameof(Need.CurLevel)), prefix: whileFree);
            harmony.Patch(AccessTools.Method(typeof(Need_Joy), nameof(Need_Joy.GainJoy)), prefix: whileFree);
            harmony.Patch(AccessTools.Method(typeof(Pawn_HealthTracker), "MakeDowned"),
                postfix: new HarmonyMethod(typeof(FixedModeRestrictions), nameof(EndWhenDowned)));
            harmony.Patch(AccessTools.Method(typeof(PawnRenderNodeWorker), nameof(PawnRenderNodeWorker.RotationFor)),
                postfix: new HarmonyMethod(typeof(FixedModeRestrictions), nameof(CoatRotation)));
            harmony.Patch(AccessTools.Method(typeof(PawnRenderNodeWorker), nameof(PawnRenderNodeWorker.ScaleFor)),
                postfix: new HarmonyMethod(typeof(FixedModeRestrictions), nameof(CoatSideScale)));
            var apparelAdjustment = AccessTools.TypeByName("YR_RJW.CompApparelScaleV45");
            if (apparelAdjustment == null) return;
            harmony.Patch(AccessTools.PropertyGetter(apparelAdjustment, "DefaultScale"),
                postfix: new HarmonyMethod(typeof(FixedModeRestrictions), nameof(CoatScale)));
            harmony.Patch(AccessTools.PropertyGetter(apparelAdjustment, "DefaultPosition"),
                postfix: new HarmonyMethod(typeof(FixedModeRestrictions), nameof(CoatPosition)));
            harmony.Patch(AccessTools.DeclaredMethod(apparelAdjustment, nameof(ThingComp.CompGetWornGizmosExtra)),
                postfix: new HarmonyMethod(typeof(FixedModeRestrictions), nameof(CoatAdjustmentMenu)));
        }

        static void CoatAdjustmentMenu(ThingComp __instance, ref IEnumerable<Gizmo> __result)
        {
            if (__instance.parent.TryGetComp<FixedModeComp>() is { } mode)
                __result = WithCoatAdjustments(__result, mode);
        }

        static IEnumerable<Gizmo> WithCoatAdjustments(IEnumerable<Gizmo> gizmos, FixedModeComp mode)
        {
            foreach (var gizmo in gizmos)
            {
                if (gizmo is Command_Action { defaultLabel: "의상 조정" } adjustment)
                {
                    var open = adjustment.action;
                    adjustment.action = () => mode.OpenCoatAdjustments(open);
                }
                yield return gizmo;
            }
        }

        static void EndWhenDowned(Pawn ___pawn)
        {
            if (!___pawn.Downed || ___pawn.apparel == null) return;
            var worn = ___pawn.apparel.WornApparel;
            for (int i = 0; i < worn.Count; i++)
            {
                if (worn[i].TryGetComp<FixedModeComp>() is not { } mode) continue;
                mode.EndMode();
                break;
            }
        }

        static void CoatScale(ThingComp __instance, ref Vector3 __result)
        {
            if (__instance.parent.def.defName != "YR_AP_LivingCoat") return;
            string race = (__instance.parent as Apparel)?.Wearer?.def.defName;
            __result.x = race switch
            {
                FixedModeComp.MihoRace => .85f,
                FixedModeComp.MinchoWitchRace => .9f,
                "Ratkin" => .7f,
                _ => .8f
            };
            if (race == FixedModeComp.MihoRace)
                __result.z = .65f;
            else if (race == "Ratkin")
                __result.z = .6f;
            else if (race is "Wolfein_Race" or FixedModeComp.MinchoWitchRace)
                __result.z = .8f;
        }

        static void CoatPosition(ThingComp __instance, ref Vector3 __result)
        {
            if (__instance.parent.def.defName != "YR_AP_LivingCoat") return;
            string race = (__instance.parent as Apparel)?.Wearer?.def.defName;
            __result.z = race is FixedModeComp.MihoRace or FixedModeComp.MinchoWitchRace ? -.05f : -.10f;
        }

        static void CoatRotation(PawnRenderNode node, PawnDrawParms parms, ref Quaternion __result)
        {
            if (!parms.facing.IsHorizontal || node.apparel?.def.defName != "YR_AP_LivingCoat" ||
                node.parent?.apparel == node.apparel)
                return;
            float angle = node.apparel.TryGetComp<FixedModeComp>()?.AngleFor(parms.facing) ?? 0f;
            if (angle != 0f) __result *= Quaternion.AngleAxis(angle, Vector3.up);
        }

        static void CoatSideScale(PawnRenderNode node, PawnDrawParms parms, ref Vector3 __result)
        {
            if (!parms.facing.IsHorizontal || node.apparel?.def.defName != "YR_AP_LivingCoat" ||
                node.parent?.apparel == node.apparel)
                return;
            node.apparel.TryGetComp<FixedModeComp>()?.ApplySideScale(parms.facing, ref __result);
        }

        static bool KeepFacingSouth(Pawn ___pawn)
        {
            if (WhileFree(___pawn)) return true;
            ___pawn.Rotation = Rot4.South;
            return false;
        }

        static bool BlockFlee(Pawn ___pawn, Job newJob) =>
            newJob?.def?.defName is not ("Flee" or "FleeAndCower") || WhileFree(___pawn);

        static bool WhileFree(Pawn ___pawn)
        {
            var worn = ___pawn?.apparel?.WornApparel;
            if (worn == null) return true;
            for (int i = 0; i < worn.Count; i++)
            {
                if (worn[i].def.defName == "YR_AP_LivingCoat" && worn[i].TryGetComp<FixedModeComp>()?.Active == true)
                    return false;
            }
            return true;
        }
    }

    [StaticConstructorOnStartup]
    static class FixedModeRenderer
    {
        static FixedModeRenderer()
        {
            new Harmony("local.yuran.livingcoat.ta1").Patch(
                AccessTools.Method(typeof(PawnRenderer), nameof(PawnRenderer.RenderPawnAt)),
                postfix: new HarmonyMethod(typeof(FixedModeRenderer), nameof(Draw)));
        }

        static void Draw(Pawn ___pawn, Vector3 drawLoc)
        {
            if (!___pawn.Spawned || ___pawn.Dead || ___pawn.apparel == null) return;
            foreach (var item in ___pawn.apparel.WornApparel)
                item.TryGetComp<FixedModeComp>()?.Draw(drawLoc);
        }
    }
}

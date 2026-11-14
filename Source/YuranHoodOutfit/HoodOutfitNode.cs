using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace YuranHoodOutfit
{
    public sealed class OutfitLayers : ThingComp
    {
        internal static readonly string[] Parts =
        {
            "Hood", "Collar", "ShoulderLeft", "ShoulderRight", "ChestLeft",
            "ChestRight", "Lower", "LegWrap", "CapeLeft", "CapeRight"
        };
        internal static readonly string[] Labels =
        {
            "후드", "목 장식", "왼쪽 어깨", "오른쪽 어깨", "왼쪽 흉부",
            "오른쪽 흉부", "하복부", "다리 랩", "왼쪽 케이프", "오른쪽 케이프"
        };
        private Dictionary<string, Vector3> positions = new Dictionary<string, Vector3>();
        private Dictionary<string, Vector2> scales = new Dictionary<string, Vector2>();
        private Dictionary<string, float> layerOffsets = new Dictionary<string, float>();
        private Dictionary<string, float> angles = new Dictionary<string, float>();
        private static readonly Dictionary<string, Vector3> DefaultPositions = new Dictionary<string, Vector3>
        {
            ["Hood:1"] = new Vector3(.03f, 0, 0), ["Hood:3"] = new Vector3(-.03f, 0, 0),
            ["Collar:1"] = new Vector3(-.05f, 0, -.04f), ["Collar:3"] = new Vector3(.05f, 0, -.04f),
            ["ChestRight:1"] = new Vector3(-.05f, 0, -.09f), ["ChestLeft:3"] = new Vector3(.05f, 0, -.09f),
            ["ChestRight:2"] = new Vector3(.02f, 0, 0), ["ChestLeft:2"] = new Vector3(-.04f, 0, 0),
            ["Lower:1"] = new Vector3(-.02f, 0, -.23f), ["Lower:3"] = new Vector3(.02f, 0, -.23f),
            ["Lower:2"] = new Vector3(0, 0, -.17f),
            ["LegWrap:1"] = new Vector3(0, 0, -.24f), ["LegWrap:3"] = new Vector3(0, 0, -.24f),
            ["LegWrap:2"] = new Vector3(0, 0, -.19f),
            ["CapeLeft:1"] = new Vector3(.16f, 0, 0),
            ["CapeLeft:3"] = new Vector3(-.16f, 0, 0), ["CapeRight:3"] = new Vector3(-.16f, 0, 0)
        };
        private static readonly Dictionary<string, Vector2> DefaultScales = new Dictionary<string, Vector2>
        {
            ["Collar:2"] = new Vector2(.7f, .7f),
            ["ChestRight:1"] = new Vector2(1.5f, 1), ["ChestLeft:3"] = new Vector2(1.5f, 1),
            ["Lower:1"] = new Vector2(.93f, .76f), ["Lower:3"] = new Vector2(.93f, .76f),
            ["Lower:2"] = new Vector2(.69f, .69f),
            ["LegWrap:1"] = new Vector2(1.12f, .69f), ["LegWrap:3"] = new Vector2(1.12f, .69f),
            ["LegWrap:2"] = new Vector2(.93f, .75f)
        };
        internal bool Animate = true;
        internal Vector3 GrabEast = new Vector3(45, 0, -15), GrabWest = new Vector3(45, 0, -15);
        internal Vector2 GrabAnglesEast = new Vector2(2, -39), GrabAnglesWest = new Vector2(2, -39);
        internal static readonly Vector2 DefaultPinOffset = new Vector2(-27, 64);
        internal Vector2 PinOffsetEast = DefaultPinOffset, PinOffsetWest = DefaultPinOffset;

        private string Race => (parent as Apparel)?.Wearer?.def.defName;
        internal bool IsHelmet => parent.def.defName == OutfitRendering.HelmetDefName;
        internal bool HasYuranGrabPose => HoodGrab.Grab.IsYuran((parent as Apparel)?.Wearer);
        private string OverrideKey(string key) => Race == null ? key : Race + "/" + key;
        private OutfitFit RaceFit(string race, string key)
        {
            if (race == null) return null;
            var saved = Current.Game?.GetComponent<OutfitRaceFits>()?.Get(race + "/" + key);
            if (saved != null) return saved;
            var presets = race switch
            {
                "Ratkin" => OutfitRaceFits.Ratkin,
                "Dragonian_Race" => OutfitRaceFits.Dragonian,
                _ => null
            };
            return presets != null && presets.TryGetValue(key, out var fit) ? fit : null;
        }

        private bool TryOverride<T>(Dictionary<string, T> values, string key, out T value) =>
            values.TryGetValue(OverrideKey(key), out value) || values.TryGetValue(key, out value);

        internal Vector3 Position(string key)
        {
            if (TryOverride(positions, key, out var value)) return value;
            return RaceFit(Race, key)?.Position ??
                (DefaultPositions.TryGetValue(key, out value) ? value : Vector3.zero);
        }

        internal Vector2 Scale(string key)
        {
            if (TryOverride(scales, key, out var value)) return value;
            return RaceFit(Race, key)?.Scale ??
                (DefaultScales.TryGetValue(key, out value) ? value : Vector2.one);
        }

        internal float Layer(string key) =>
            TryOverride(layerOffsets, key, out var value) ? value : RaceFit(Race, key)?.Layer ?? 0f;
        internal float Angle(string key) =>
            TryOverride(angles, key, out var value) ? value : RaceFit(Race, key)?.Angle ?? 0f;
        internal void Reset(string key)
        {
            string raceKey = OverrideKey(key);
            positions = WithoutOverride(positions, key, raceKey);
            scales = WithoutOverride(scales, key, raceKey);
            layerOffsets = WithoutOverride(layerOffsets, key, raceKey);
            angles = WithoutOverride(angles, key, raceKey);
            RefreshWearer();
        }

        private static Dictionary<string, T> WithoutOverride<T>(Dictionary<string, T> values, string key, string raceKey)
        {
            var updated = new Dictionary<string, T>(values);
            updated.Remove(key);
            updated.Remove(raceKey);
            return updated;
        }

        internal void SaveRaceDefault(string key)
        {
            var defaults = Current.Game?.GetComponent<OutfitRaceFits>();
            if (parent is not Apparel { Wearer: Pawn wearer } || defaults == null) return;
            string race = wearer.def.defName;
            defaults.Set(race + "/" + key, new OutfitFit
            {
                Position = Position(key), Scale = Scale(key), Layer = Layer(key), Angle = Angle(key)
            });
            Reset(key);
            foreach (Map map in Find.Maps)
                foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
                    if (pawn != wearer && pawn.def.defName == race &&
                        (IsHelmet ? OutfitRendering.HelmetSettingsFor(pawn) : OutfitRendering.SettingsFor(pawn)) != null)
                        RefreshPawn(pawn);
        }

        internal void Set(string key, Vector3 position, Vector2 scale, float layer, float angle)
        {
            string raceKey = OverrideKey(key);
            positions = new Dictionary<string, Vector3>(positions) { [raceKey] = position };
            scales = new Dictionary<string, Vector2>(scales) { [raceKey] = scale };
            layerOffsets = new Dictionary<string, float>(layerOffsets) { [raceKey] = layer };
            angles = new Dictionary<string, float>(angles) { [raceKey] = angle };
            RefreshWearer();
        }

        private void RefreshWearer()
        {
            if (parent is Apparel { Wearer: Pawn wearer }) RefreshPawn(wearer);
        }

        private static void RefreshPawn(Pawn pawn)
        {
            pawn.Drawer.renderer.SetAllGraphicsDirty();
            PortraitsCache.SetDirty(pawn);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Collections.Look(ref positions, "partPositions", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref scales, "partScales", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref layerOffsets, "partLayers", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref angles, "partAngles", LookMode.Value, LookMode.Value);
            Scribe_Values.Look(ref Animate, "outfitAnimation", true);
            Scribe_Values.Look(ref GrabEast, "grabContactEast", new Vector3(45, 0, -15));
            Scribe_Values.Look(ref GrabWest, "grabContactWest", new Vector3(45, 0, -15));
            Scribe_Values.Look(ref GrabAnglesEast, "grabAnglesEast", new Vector2(2, -39));
            Scribe_Values.Look(ref GrabAnglesWest, "grabAnglesWest", new Vector2(2, -39));
            Scribe_Values.Look(ref PinOffsetEast, "pinOffsetEast", DefaultPinOffset);
            Scribe_Values.Look(ref PinOffsetWest, "pinOffsetWest", DefaultPinOffset);
            if (Scribe.mode != LoadSaveMode.PostLoadInit) return;
            positions ??= new Dictionary<string, Vector3>();
            scales ??= new Dictionary<string, Vector2>();
            layerOffsets ??= new Dictionary<string, float>();
            angles ??= new Dictionary<string, float>();
        }

        public override IEnumerable<Gizmo> CompGetWornGizmosExtra()
        {
            yield return new Command_Action
            {
                defaultLabel = IsHelmet ? "후드 조정" : "의상 파츠 조정",
                defaultDesc = "방향과 파츠를 골라 위치, 크기, 각도, 겹침 순서를 조정합니다.",
                icon = parent.def.uiIcon,
                groupable = false,
                action = () => Find.WindowStack.Add(new OutfitLayerDialog(this))
            };
            if (!IsHelmet)
                foreach (Gizmo command in HoodGrab.Commands(this))
                    yield return command;
        }
    }

    internal sealed class OutfitLayerDialog : Window
    {
        private static readonly string[] Directions = { "북 · 뒤", "동 · 오른쪽", "남 · 정면", "서 · 왼쪽" };
        private readonly OutfitLayers settings;
        private int direction = 2;
        private int part;
        private bool grabTab;
        public override Vector2 InitialSize => new Vector2(430, 466);

        internal OutfitLayerDialog(OutfitLayers settings)
        {
            this.settings = settings;
            part = settings.IsHelmet ? 0 : 1;
            doCloseX = true;
            draggable = true;
            if (settings.parent is Apparel { Wearer: Pawn wearer }) direction = wearer.Rotation.AsInt;
        }

        public override void DoWindowContents(Rect rect)
        {
            if (!settings.IsHelmet && Widgets.ButtonText(new Rect(0, grabTab ? 372 : 344, 205, 30),
                grabTab ? "의상 파츠 조정" : "목 잡기 애니메이션 조정"))
                grabTab = !grabTab;
            if (grabTab)
            {
                DrawGrabSettings(rect);
                return;
            }
            Widgets.Label(new Rect(0, 0, rect.width, 26), settings.IsHelmet ? "후드 조정" : "의상 파츠 조정");
            if (Widgets.ButtonText(new Rect(0, 32, 165, 28), Directions[direction]))
                Choose(Directions, value => direction = value);
            if (!settings.IsHelmet && Widgets.ButtonText(new Rect(173, 32, rect.width - 173, 28), OutfitLayers.Labels[part]))
                Choose(OutfitLayers.Labels, value => part = value, 1);

            string key = OutfitLayers.Parts[part] + ":" + direction;
            Vector3 position = settings.Position(key);
            Vector2 scale = settings.Scale(key);
            float layer = settings.Layer(key);
            float angle = settings.Angle(key);
            Vector3 nextPosition = new Vector3(Slider(75, "좌우", position.x, -.75f, .75f, .01f), 0,
                Slider(111, "상하", position.z, -.75f, .75f, .01f));
            Vector2 nextScale = new Vector2(Slider(147, "가로 배율", scale.x, .2f, 2f, .01f),
                Slider(183, "세로 배율", scale.y, .2f, 2f, .01f));
            float nextAngle = Slider(219, "각도 (°)", angle, -180, 180, 1);
            float nextLayer = Slider(255, "겹침 순서", layer, -50, 50, 1);
            if (Widgets.ButtonText(new Rect(0, 304, 155, 28), "선택 파츠 초기화"))
                settings.Reset(key);
            else if (nextPosition != position || nextScale != scale || nextLayer != layer || nextAngle != angle)
                settings.Set(key, nextPosition, nextScale, nextLayer, nextAngle);
            if (!settings.IsHelmet)
                Widgets.CheckboxLabeled(new Rect(166, 304, 203, 28), "애니메이션 재생", ref settings.Animate);
            if (Widgets.ButtonText(new Rect(0, 384, 205, 30), "이 파츠를 종족 기본값으로 저장"))
                settings.SaveRaceDefault(key);
        }

        private bool grabEast = true;

        private void DrawGrabSettings(Rect rect)
        {
            Widgets.Label(new Rect(0, 0, rect.width, 28), "목 잡기 · 얼굴 도달 자세");
            if (Widgets.ButtonText(new Rect(0, 32, 165, 28), grabEast ? "동 · 오른쪽" : "서 · 왼쪽"))
                grabEast = !grabEast;
            ref Vector3 position = ref (grabEast ? ref settings.GrabEast : ref settings.GrabWest);
            position.x = Slider(75, "몸통 좌우", position.x, -64, 64, 1);
            position.z = Slider(111, "몸통 상하", position.z, -64, 64, 1);
            ref Vector2 angles = ref (grabEast ? ref settings.GrabAnglesEast : ref settings.GrabAnglesWest);
            if (settings.HasYuranGrabPose)
            {
                angles.x = Slider(147, "바깥 다리 각도", angles.x, -180, 180, 1);
                angles.y = Slider(183, "안쪽 다리 각도", angles.y, -180, 180, 1);
            }
            Widgets.Label(new Rect(0, 220, rect.width, 28), "완전제압 착취 · 애니메이션 위치");
            ref Vector2 pinOffset = ref (grabEast ? ref settings.PinOffsetEast : ref settings.PinOffsetWest);
            pinOffset.x = Slider(252, "좌우", pinOffset.x, -64, 64, 1);
            pinOffset.y = Slider(288, "상하", pinOffset.y, -64, 64, 1);
            if (Widgets.ButtonText(new Rect(0, 330, 165, 28), "현재 방향 초기화"))
            {
                position = new Vector3(45, 0, -15);
                angles = new Vector2(2, -39);
                pinOffset = OutfitLayers.DefaultPinOffset;
            }
        }

        private static float Slider(float y, string label, float value, float min, float max, float step)
        {
            Widgets.Label(new Rect(0, y, 116, 24), label + " " + value.ToString("0.##"));
            return Widgets.HorizontalSlider(new Rect(120, y + 5, 245, 18), value, min, max, roundTo: step);
        }

        private static void Choose(string[] labels, Action<int> select, int first = 0)
        {
            var options = new List<FloatMenuOption>();
            for (int i = first; i < labels.Length; i++)
            {
                int index = i;
                options.Add(new FloatMenuOption(labels[i], () => select(index)));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }
    }

    public sealed class InstallLivingSpine : Recipe_InstallImplant
    {
        public override IEnumerable<BodyPartRecord> GetPartsToApplyOn(Pawn pawn, RecipeDef recipe)
        {
            foreach (BodyPartRecord part in base.GetPartsToApplyOn(pawn, recipe))
            {
                // Yuran defines the tail as a second Spine record.
                if (string.IsNullOrEmpty(part.untranslatedCustomLabel))
                    yield return part;
            }
        }
    }

    public sealed class OutfitPartNode : PawnRenderNode_Apparel
    {
        internal string PartName => Props.texPath.Substring(Props.texPath.LastIndexOf('/') + 1);
        internal OutfitLayers Settings => apparel?.TryGetComp<OutfitLayers>();
        internal string SettingsKey(Rot4 facing) => (PartName == "HoodLining" ? "Hood" : PartName) + ":" + facing.AsInt;
        private readonly Graphic[] pulse = new Graphic[14];
        private readonly Graphic[] walk = new Graphic[8];
        private static Material southHoodShade;

        public OutfitPartNode(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree)
            : base(pawn, props, tree)
        {
        }

        public override GraphicMeshSet MeshSetFor(Pawn pawn)
        {
            var meshes = Props.parentTagDef == PawnRenderNodeTagDefOf.ApparelHead
                ? HumanlikeMeshPoolUtility.GetHumanlikeHeadSetForPawn(pawn)
                : HumanlikeMeshPoolUtility.GetHumanlikeBodySetForPawn(pawn);
            // The west textures are already drawn facing west.
            return new GraphicMeshSet(meshes.MeshAt(Rot4.North), meshes.MeshAt(Rot4.East),
                meshes.MeshAt(Rot4.South), meshes.MeshAt(Rot4.East));
        }

        public override Graphic GraphicFor(Pawn pawn)
        {
            if (PartName != "Hood") return base.GraphicFor(pawn);
            return GraphicDatabase.Get<Graphic_Multi>(Props.texPath + "Rim", ShaderFor(pawn),
                Vector2.one, ColorFor(pawn));
        }

        protected override IEnumerable<Graphic> GraphicsFor(Pawn pawn)
        {
            if (HasGraphic(pawn))
            {
                yield return GraphicFor(pawn);
                foreach (Graphic graphic in LoadFrames(pulse, "pulse")) yield return graphic;
                if (PartName.StartsWith("Cape", StringComparison.Ordinal))
                    foreach (Graphic graphic in LoadFrames(walk, "walk")) yield return graphic;
            }
        }

        private IEnumerable<Graphic> LoadFrames(Graphic[] frames, string mode)
        {
            for (int i = 0; i < frames.Length; i++)
            {
                string path = Props.texPath + "_" + mode + i;
                if (!OutfitRendering.HasFrame(path)) continue;
                frames[i] = GraphicDatabase.Get<Graphic_Multi>(path, ShaderDatabase.Cutout, Vector2.one, Color.white);
                yield return frames[i];
            }
        }

        internal Graphic ActiveGraphic(PawnDrawParms parms)
        {
            if (parms.Portrait || Settings?.Animate != true) return PrimaryGraphic;
            int ticks = Find.TickManager.TicksGame;
            if (PartName.StartsWith("Cape", StringComparison.Ordinal) && parms.pawn.pather?.MovingNow == true)
                return FrameForFacing(walk[ticks / 4 % walk.Length], parms.facing);
            return FrameForFacing(pulse[ticks % 190 * pulse.Length / 190], parms.facing);
        }

        private Graphic FrameForFacing(Graphic frame, Rot4 facing) =>
            frame != null && OutfitRendering.HasFrame(frame.path, facing) ? frame : PrimaryGraphic;

        internal Material SouthHoodShade => southHoodShade;

        internal static void LoadSouthHoodShade()
        {
            const string path = "Yuran/Apparel/YuranHoodOutfit/Hood_south";
            var original = ContentFinder<Texture2D>.Get(path);
            if (original == null) return;
            if (!original.isReadable)
            {
                southHoodShade = MaterialPool.MatFrom("Yuran/Apparel/YuranHoodOutfit/HoodRim_south", ShaderDatabase.Transparent);
                return;
            }
            var pixels = original.GetPixels32();
            int width = original.width, height = original.height;
            for (int y = 0; y < height; y++)
            {
                float fromTop = (height - y - .5f) * 256f / height;
                if (fromTop < 102f || fromTop > 159f) continue;
                float depth = Mathf.SmoothStep(.8f, 0f, Mathf.InverseLerp(113f, 150f, fromTop));
                float halfWidth = 6f + 39f * Mathf.Sqrt(Mathf.InverseLerp(102f, 153f, fromTop));
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    var color = pixels[index];
                    if (color.a == 0) continue;
                    float distance = Mathf.Abs((x + .5f) * 256f / width - 128f);
                    float aperture = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(halfWidth - 3f, halfWidth + 1f, distance));
                    float innerFabric = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(43f, 63f, Mathf.Max(color.r, Mathf.Max(color.g, color.b))));
                    float shade = aperture * innerFabric;
                    if (shade <= 0f) continue;
                    pixels[index] = new Color32(
                        (byte)(color.r * (1f - shade)), (byte)(color.g * (1f - shade)),
                        (byte)(color.b * (1f - shade)), (byte)(color.a * Mathf.Lerp(1f, depth, shade)));
                }
            }
            var shaded = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "Samok assassin hood south shade", wrapMode = TextureWrapMode.Clamp,
                filterMode = original.filterMode
            };
            shaded.SetPixels32(pixels);
            shaded.Apply(false, true);
            southHoodShade = MaterialPool.MatFrom(new MaterialRequest(shaded, ShaderDatabase.Transparent));
        }
    }

    public sealed class OutfitPartWorker : PawnRenderNodeWorker_Apparel_Body
    {
        public override bool CanDrawNow(PawnRenderNode node, PawnDrawParms parms)
        {
            if (!base.CanDrawNow(node, parms)) return false;
            string part = ((OutfitPartNode)node).PartName;
            if (!parms.Portrait && !parms.Cache && HoodGrab.Current?.Attacker(parms.pawn) is HoodGrab.Grab grab &&
                ((grab.UseYuranPose && (part == "Lower" || part == "LegWrap")) ||
                 (grab.CapeAction && part.StartsWith("Cape", StringComparison.Ordinal))))
                return false;
            return (part != "Hood" && part != "HoodLining") ||
                (PawnRenderNodeWorker_Apparel_Head.HeadgearVisible(parms) && !parms.flags.FlagSet(PawnRenderFlags.HeadStump));
        }

        protected override Graphic GetGraphic(PawnRenderNode node, PawnDrawParms parms)
            => ((OutfitPartNode)node).ActiveGraphic(parms);

        protected override Material GetMaterial(PawnRenderNode node, PawnDrawParms parms)
        {
            var part = (OutfitPartNode)node;
            if (parms.facing == Rot4.South && part.PartName == "Hood" && part.SouthHoodShade != null)
                return !parms.Portrait && parms.flags.FlagSet(PawnRenderFlags.Invisible)
                    ? InvisibilityMatPool.GetInvisibleMat(part.SouthHoodShade) : part.SouthHoodShade;
            return base.GetMaterial(node, parms);
        }

        public override Vector3 OffsetFor(PawnRenderNode node, PawnDrawParms parms, out Vector3 pivot)
        {
            var result = base.OffsetFor(node, parms, out pivot);
            var part = (OutfitPartNode)node;
            return result + (part.Settings?.Position(part.SettingsKey(parms.facing)) ?? Vector3.zero);
        }

        public override Vector3 ScaleFor(PawnRenderNode node, PawnDrawParms parms)
        {
            var result = base.ScaleFor(node, parms);
            var part = (OutfitPartNode)node;
            var scale = part.Settings?.Scale(part.SettingsKey(parms.facing)) ?? Vector2.one;
            return new Vector3(result.x * scale.x, result.y, result.z * scale.y);
        }

        public override Quaternion RotationFor(PawnRenderNode node, PawnDrawParms parms)
        {
            var rotation = base.RotationFor(node, parms);
            var part = (OutfitPartNode)node;
            float angle = part.Settings?.Angle(part.SettingsKey(parms.facing)) ?? 0f;
            return angle == 0f ? rotation : rotation * Quaternion.AngleAxis(angle, Vector3.up);
        }

        public override float LayerFor(PawnRenderNode node, PawnDrawParms parms)
        {
            var part = (OutfitPartNode)node;
            if (part.PartName == "HoodLining") return base.LayerFor(node, parms);
            return base.LayerFor(node, parms) + (part.Settings?.Layer(part.SettingsKey(parms.facing)) ?? 0);
        }
    }

    [StaticConstructorOnStartup]
    internal static class OutfitRendering
    {
        private const string DefName = "YR_AP_HoodOutfit";
        internal const string HelmetDefName = "YR_AP_HoodHelmet";
        private static readonly HashSet<string> Frames = new HashSet<string>();
        private static readonly string[] FacingSuffixes = { "_north", "_east", "_south", "_west" };

        static OutfitRendering()
        {
            OutfitPartNode.LoadSouthHoodShade();
            foreach (string part in OutfitLayers.Parts)
            {
                CheckFrames(part, "pulse", 14);
                if (part.StartsWith("Cape", StringComparison.Ordinal)) CheckFrames(part, "walk", 8);
            }
            var harmony = new Harmony("local.yuran.hoodoutfit");
            harmony.Patch(AccessTools.Method(typeof(PawnRenderer), "ParallelGetPreRenderResults"),
                prefix: new HarmonyMethod(typeof(OutfitRendering), nameof(UseLiveGraphics)));
            harmony.Patch(AccessTools.Method(typeof(PawnRenderTree), nameof(PawnRenderTree.ParallelPreDraw)),
                prefix: new HarmonyMethod(typeof(OutfitRendering), nameof(ApplyGrabFacing)) { priority = Priority.Last },
                postfix: new HarmonyMethod(typeof(OutfitRendering), nameof(FilterHoodDrawRequests)) { priority = Priority.Last });
            HoodGrabDrawing.Install(harmony);
        }

        internal static bool HasFrame(string path) => Frames.Contains(path);
        internal static bool HasFrame(string path, Rot4 facing) => Frames.Contains(path + FacingSuffixes[facing.AsInt]);

        private static void CheckFrames(string part, string mode, int count)
        {
            for (int i = 0; i < count; i++)
            {
                string path = "Yuran/Apparel/YuranHoodOutfit/" + part + "_" + mode + i;
                foreach (string suffix in FacingSuffixes)
                {
                    if (ContentFinder<Texture2D>.Get(path + suffix, false) == null) continue;
                    Frames.Add(path);
                    Frames.Add(path + suffix);
                }
            }
        }

        internal static OutfitLayers SettingsFor(Pawn pawn) => SettingsFor(pawn, DefName);
        internal static OutfitLayers HelmetSettingsFor(Pawn pawn) => SettingsFor(pawn, HelmetDefName);

        private static OutfitLayers SettingsFor(Pawn pawn, string defName)
        {
            var worn = pawn?.apparel?.WornApparel;
            if (worn == null) return null;
            foreach (Apparel apparel in worn)
                if (apparel.def.defName == defName) return apparel.TryGetComp<OutfitLayers>();
            return null;
        }

        private static void UseLiveGraphics(Pawn ___pawn, ref Rot4? rotOverride, ref bool disableCache)
        {
            if (SettingsFor(___pawn)?.Animate == true) disableCache = true;
            if (GrabFacing(___pawn) is not Rot4 facing) return;
            disableCache = true;
            rotOverride = facing;
        }

        private static void ApplyGrabFacing(ref PawnDrawParms parms)
        {
            if (parms.Portrait || parms.Cache || GrabFacing(parms.pawn) is not Rot4 facing) return;
            parms.facing = facing;
            parms.flipHead = false;
        }

        private static Rot4? GrabFacing(Pawn pawn)
        {
            var current = HoodGrab.Current;
            if (current?.Attacker(pawn) is HoodGrab.Grab grab) return grab.East ? Rot4.East : Rot4.West;
            if (current?.Victim(pawn) is HoodGrab.Grab victim) return victim.East ? Rot4.West : Rot4.East;
            return null;
        }

        private static void FilterHoodDrawRequests(PawnDrawParms parms, List<PawnGraphicDrawRequest> ___drawRequests)
        {
            if (!PawnRenderNodeWorker_Apparel_Head.HeadgearVisible(parms) ||
                parms.flags.FlagSet(PawnRenderFlags.HeadStump) || HelmetSettingsFor(parms.pawn) == null) return;
            bool coverHair = parms.facing == Rot4.North &&
                !parms.pawn.def.defName.StartsWith("Yuran_Race", StringComparison.Ordinal);
            for (int i = ___drawRequests.Count - 1; i >= 0; i--)
            {
                var node = ___drawRequests[i].node;
                if (!(parms.facing == Rot4.North && node.Props.debugLabel == "BrowControllerComp_Basic") &&
                    !(coverHair && (node is PawnRenderNode_Hair ||
                        (node.apparel == null && IsHairGraphic(node.PrimaryGraphic?.path))))) continue;
                node.requestRecache = true;
                ___drawRequests.RemoveAt(i);
            }
        }

        private static bool IsHairGraphic(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            foreach (string segment in path.Split('/'))
                if (segment.StartsWith("Hair", StringComparison.OrdinalIgnoreCase) ||
                    segment.StartsWith("AddonHair", StringComparison.OrdinalIgnoreCase) ||
                    segment.StartsWith("BackHair", StringComparison.OrdinalIgnoreCase) ||
                    segment.IndexOf("_Hair", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            return false;
        }
    }
}

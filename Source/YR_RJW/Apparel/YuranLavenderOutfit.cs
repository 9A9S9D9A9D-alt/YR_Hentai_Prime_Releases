using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

namespace YR_RJW
{
    [StaticConstructorOnStartup]
    internal static class YuranLavenderOutfit
    {
        internal const string DefName = "YR_AP_LavenderRootOutfit";
        private const string Root = "Yuran/Apparel/LavenderRootOutfit/";
        internal static readonly Graphic Outfit = GraphicDatabase.Get<Graphic_Multi>(
            Root + "Worn_Thin", ShaderDatabase.Cutout, Vector2.one, Color.white);
        internal static readonly Graphic SouthStance = GraphicDatabase.Get<Graphic_Single>(
            Root + "Stance_south", ShaderDatabase.Cutout, Vector2.one, Color.white);
        internal static readonly Graphic NorthStance = GraphicDatabase.Get<Graphic_Single>(
            Root + "Stance_north", ShaderDatabase.Cutout, Vector2.one, Color.white);

        private static Graphic LoadSide(string layer, string facing) =>
            GraphicDatabase.Get<Graphic_Single>(Root + layer + "_" + facing, ShaderDatabase.Cutout, Vector2.one, Color.white);

        internal static readonly Graphic NearEast = LoadSide("Near", "east");
        internal static readonly Graphic NearWest = LoadSide("Near", "west");
        internal static readonly Graphic FarEast = LoadSide("Far", "east");
        internal static readonly Graphic FarWest = LoadSide("Far", "west");

        internal static Apparel WornBy(Pawn pawn) =>
            pawn?.def?.defName == "Yuran_Race"
                ? pawn.apparel?.WornApparel.FirstOrDefault(item => item.def.defName == DefName)
                : null;

        internal static bool AppliesTo(Pawn pawn) => WornBy(pawn) != null;

        internal static bool DemoEnabled => GenCommandLine.CommandLineArgPassed("yrpLavenderOutfitDemo");
        internal static readonly ConcurrentDictionary<string, string> DemoDraws = new ConcurrentDictionary<string, string>();

        [HarmonyPatch(typeof(PawnRenderNodeWorker), "GetGraphic")]
        private static class WornGraphic
        {
            private static void Postfix(PawnRenderNode node, PawnDrawParms parms, ref Graphic __result)
            {
                if (!AppliesTo(parms.pawn))
                    return;

                string layer;
                if (node.apparel?.def.defName == DefName && node.parent?.apparel != node.apparel)
                {
                    __result = parms.facing == Rot4.East ? NearEast :
                        parms.facing == Rot4.West ? NearWest : Outfit;
                    layer = "outfit";
                }
                else if (node is PawnRenderNode_Body && !parms.facing.IsHorizontal &&
                    parms.posture == PawnPosture.Standing && !parms.pawn.Downed)
                {
                    __result = parms.facing == Rot4.North ? NorthStance : SouthStance;
                    layer = "stance";
                }
                else
                {
                    return;
                }

                if (DemoEnabled && !parms.Portrait)
                    DemoDraws.TryAdd(layer + ":" + parms.facing, __result.MatAt(parms.facing).mainTexture.name);
            }
        }
    }

    public sealed class YuranLavenderRearSetup : DynamicPawnRenderNodeSetup
    {
        public override bool HumanlikeOnly => true;

        public override IEnumerable<(PawnRenderNode node, PawnRenderNode parent)> GetDynamicNodes(Pawn pawn, PawnRenderTree tree)
        {
            Apparel item = YuranLavenderOutfit.WornBy(pawn);
            if (item == null || !tree.TryGetNodeByTag(PawnRenderNodeTagDefOf.Body, out var body))
                yield break;

            var properties = new PawnRenderNodeProperties
            {
                workerClass = typeof(PawnRenderNodeWorker_YuranLavenderRear),
                debugLabel = "Lavender outfit far side",
                baseLayer = -5f,
                drawSize = Vector2.one,
                useGraphic = true
            };
            yield return (new PawnRenderNode_Apparel(pawn, properties, tree, item), body);
        }
    }

    public sealed class PawnRenderNodeWorker_YuranLavenderRear : PawnRenderNodeWorker
    {
        public override bool CanDrawNow(PawnRenderNode node, PawnDrawParms parms) =>
            parms.facing.IsHorizontal && base.CanDrawNow(node, parms);

        protected override Graphic GetGraphic(PawnRenderNode node, PawnDrawParms parms)
        {
            var graphic = parms.facing == Rot4.West ? YuranLavenderOutfit.FarWest : YuranLavenderOutfit.FarEast;
            if (YuranLavenderOutfit.DemoEnabled && !parms.Portrait)
                YuranLavenderOutfit.DemoDraws.TryAdd("rear:" + parms.facing, graphic.MatAt(parms.facing).mainTexture.name);
            return graphic;
        }
    }
    public sealed class YuranLavenderOutfitDemo : GameComponent
    {
        private readonly List<Pawn> models = new List<Pawn>();
        private readonly Rot4[] directions = { Rot4.South, Rot4.East, Rot4.North, Rot4.West };
        private Map map;
        private IntVec3 demoCenter;
        private bool started;
        private bool reported;

        public YuranLavenderOutfitDemo(Game game) { }

        public override void GameComponentUpdate()
        {
            if (!YuranLavenderOutfit.DemoEnabled || reported)
                return;
            if (!started)
            {
                map = Find.CurrentMap;
                if (map?.Size.x != 50 || !File.Exists(Path.Combine(GenFilePaths.SaveDataFolderPath, "demo-map-ready.json")))
                    return;
                started = true;
                try { SetUp(); }
                catch (Exception error) { Log.Error("Lavender outfit demo setup failed: " + error); reported = true; }
            }

            var draws = YuranLavenderOutfit.DemoDraws;
            if (models.Count == 4 && draws.Count >= 8)
            {
                FrameDemo();
                string[] rows = models.Select(pawn => pawn.LabelShort + " | " + pawn.Rotation + " | " +
                    pawn.apparel.WornApparel.Single().def.defName).Concat(
                    draws.OrderBy(entry => entry.Key).Select(entry => entry.Key + " | " + entry.Value)).ToArray();
                File.WriteAllLines(Path.Combine(GenFilePaths.SaveDataFolderPath, "lavender-outfit-demo.txt"), rows);
                Log.Message("[YuranLavenderOutfit] " + string.Join("; ", rows));
                reported = true;
            }
        }

        private void SetUp()
        {
            var race = DefDatabase<ThingDef>.GetNamed("Yuran_Race");
            var kind = map.mapPawns.AllPawnsSpawned.First(pawn => pawn.def == race).kindDef;
            var apparel = DefDatabase<ThingDef>.GetNamed(YuranLavenderOutfit.DefName);
            var center = new IntVec3(map.Size.x / 2, 0, map.Size.z / 2);
            int[] xOffsets = { -5, -2, 1, 4 };
            demoCenter = GenRadial.RadialCellsAround(center, 18, true).First(candidate => xOffsets.All(x =>
            {
                var cell = candidate + new IntVec3(x, 0, 0);
                return cell.InBounds(map) && cell.Standable(map) && cell.GetEdifice(map) == null &&
                    !cell.GetThingList(map).Any(thing => thing is Pawn);
            }));
            string[] labels = { "유란 · 남", "유란 · 동", "유란 · 북", "유란 · 서" };
            for (int i = 0; i < directions.Length; i++)
            {
                var pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, Faction.OfPlayer,
                    forceGenerateNewPawn: true, canGeneratePawnRelations: false, fixedGender: Gender.Female,
                    fixedBiologicalAge: 25));
                pawn.Name = new NameSingle(labels[i]);
                pawn.apparel.DestroyAll();
                pawn.equipment.DestroyAllEquipment();
                pawn.apparel.Wear((Apparel)ThingMaker.MakeThing(apparel));
                GenSpawn.Spawn(pawn, demoCenter + new IntVec3(xOffsets[i], 0, 0), map, directions[i]);
                pawn.pather.StopDead();
                pawn.Rotation = directions[i];
                pawn.Drawer.renderer.SetAllGraphicsDirty();
                models.Add(pawn);
            }
            Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
            Find.Selector.ClearSelection();
            FrameDemo();
        }

        private void FrameDemo()
        {
            float size = 5.5f;
            var cameraPlus = AccessTools.TypeByName("CameraPlus.Tools");
            if (cameraPlus != null)
            {
                var lerp = AccessTools.Method(cameraPlus, "LerpRootSize");
                if (lerp != null)
                {
                    float low = 11f, high = 60f;
                    for (int i = 0; i < 18; i++)
                    {
                        float middle = (low + high) / 2;
                        if ((float)lerp.Invoke(null, new object[] { middle }) < size) low = middle;
                        else high = middle;
                    }
                    size = (low + high) / 2;
                }
            }
            Find.CameraDriver.SetRootPosAndSize(demoCenter.ToVector3Shifted(), size);
            AccessTools.Method(typeof(CameraDriver), "ApplyPositionToGameObject")?.Invoke(Find.CameraDriver, null);
            Current.SubcameraDriver.UpdatePositions(Find.Camera);
        }
    }
}

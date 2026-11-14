using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace YuranLivingCoat
{
    [StaticConstructorOnStartup]
    sealed class PenanceWindow : Window
    {
        const int AtlasColumns = 7;
        const int AtlasRows = 6;
        const int FramesPerMotion = 21;
        const int TissueBands = 12;
        const int IncapacitationStage = 3;
        const float InteriorCrop = .74f;
        const string TextureRoot = "YuranLivingCoat/Rift/";
        static readonly float[] CycleSeconds = { 8f, 6.9f, 6f, 5.1f, 4.4f };
        static readonly Texture2D[] Liquids = new Texture2D[5];
        static readonly Texture2D[] Fronts = new Texture2D[5];
        static readonly Vector2[] Steps =
        {
            new(.43f, .53f), new(.61f, .57f), new(.57f, .47f), new(.39f, .57f)
        };

        static Texture2D walls, ripple, drop, shadow;
        readonly RiftChambers chamber;
        readonly List<RiftStay> occupants = new();
        internal Thing Coat { get; }
        int roomStart = -1;
        readonly float openedAt = Time.realtimeSinceStartup;
        float previousElapsed, cycle, tissuePhase;
        public override Vector2 InitialSize => new Vector2(570f, 480f);

        public PenanceWindow(RiftChambers chamber, Thing coat)
        {
            this.chamber = chamber;
            Coat = coat;
            doCloseX = true;
            draggable = true;
            absorbInputAroundWindow = false;
        }

        public override void DoWindowContents(Rect rect)
        {
            chamber.CollectIsolation(Coat, occupants);
            int count = occupants.Count;
            string title = count == 1 ? $"태동실 · {occupants[0].Pawn.LabelShort}" : $"태동실 · {count}명";
            Widgets.Label(new Rect(0f, 0f, rect.width, 30f), title);
            float width = Mathf.Min(rect.width, (rect.height - 72f) * 4f / 3f);
            var viewport = new Rect((rect.width - width) / 2f, 37f, width, width * .75f);
            var area = new Rect(0f, 0f, viewport.width, viewport.height);
            float elapsed = Time.realtimeSinceStartup - openedAt;
            int now = Find.TickManager.TicksGame;
            if (count > 0 && roomStart != occupants[0].RoomStart)
            {
                roomStart = occupants[0].RoomStart;
                previousElapsed = elapsed;
                cycle = tissuePhase = 0f;
            }
            float fill = roomStart < 0 ? 0f : Mathf.Clamp01((now - roomStart) / (float)RiftStay.IsolationTicks);
            int ticks = int.MaxValue;
            foreach (var stay in occupants)
                ticks = Mathf.Min(ticks, stay.Ends - now);
            float progress = fill * Liquids.Length;
            int stage = Mathf.Min(Liquids.Length - 1, Mathf.FloorToInt(progress));
            float collapse = Ease((progress - (IncapacitationStage - 1)) / .10f);
            float cycleAdvance = (elapsed - previousElapsed) / CycleSeconds[stage];
            cycle = Mathf.Repeat(cycle + cycleAdvance, 1f);
            tissuePhase = Mathf.Repeat(tissuePhase + cycleAdvance * CycleSeconds[0] * 3.2f, 2f * Mathf.PI);
            previousElapsed = elapsed;
            bool relaxing = cycle >= .51f;
            float motion = relaxing ? Ease((cycle - .51f) / .35f) : Ease((cycle - .13f) / .31f);
            float closure = relaxing ? 1f - motion : motion;
            int frame = Mathf.RoundToInt(motion * (FramesPerMotion - 1)) + (relaxing ? FramesPerMotion : 0);
            float transition = stage == 0 ? 1f : Ease((progress - stage) / .18f);
            var sceneSize = area.size / InteriorCrop;
            var scene = new Rect(area.center - sceneSize * .5f, sceneSize);
            float liquidLine = area.yMax;
            if (count == 1)
            {
                var stay = occupants[0];
                GetOccupantPose(scene, elapsed, closure, stay.Pawn.Downed ? 1f : collapse,
                    Vector2.zero, 1f, out Rect body, out _, out _);
                liquidLine = Mathf.Clamp(body.y + body.height * Mathf.Lerp(.76f, .08f, Ease(fill)), area.y, area.yMax);
            }
            else if (count > 1)
            {
                liquidLine = Mathf.Lerp(area.y + area.height * .63f, area.y, Ease(fill));
            }

            GUI.BeginGroup(viewport);
            DrawCell(area, walls ??= Texture("PenanceWallLayers"), frame, 1f, tissuePhase);
            DrawLiquidSlice(area, frame, stage, transition, area.y, liquidLine, tissuePhase);

            int columns = Mathf.CeilToInt(Mathf.Sqrt(count));
            int rows = count == 0 ? 0 : (count + columns - 1) / columns;
            for (int i = 0; i < count; i++)
            {
                int row = i / columns;
                int rowSize = Mathf.Min(columns, count - row * columns);
                float spread = Mathf.Lerp(1f, .56f, closure);
                var slot = new Vector2(
                    (i % columns - (rowSize - 1) * .5f) * .38f / columns,
                    (row - (rows - 1) * .5f) * .31f / rows) * spread;
                float size = count == 1 ? 1f : Mathf.Min(1f, 1.65f / columns);
                var stay = occupants[i];
                DrawOccupant(stay.Pawn, scene, elapsed + i * .57f, closure, collapse, slot, size);
            }
            DrawLiquidSlice(area, frame, stage, transition, liquidLine, area.yMax, tissuePhase);
            DrawStageLayer(area, frame, stage, transition, Fronts, "PenanceFrontLayer", tissuePhase);
            if (count > 0) DrawDrops(scene, elapsed, fill, closure);
            GUI.EndGroup();

            Widgets.Label(new Rect(0f, rect.height - 30f, rect.width, 28f),
                count == 0 ? "격리 종료" : ticks <= 0 ? "입구가 열리는 중" :
                    $"{stage + 1}/5단계 · 다음 퇴실까지 {ticks / 2500f:0.00}시간");
        }

        static Texture2D Texture(string path) => ContentFinder<Texture2D>.Get(TextureRoot + path);
        static float Ease(float value) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(value));

        static void DrawLiquidSlice(Rect area, int frame, int stage, float transition, float top, float bottom,
            float tissuePhase)
        {
            if (bottom <= top) return;
            GUI.BeginGroup(new Rect(area.x, top, area.width, bottom - top));
            DrawStageLayer(new Rect(0f, area.y - top, area.width, area.height), frame, stage,
                transition, Liquids, "PenanceLiquidLayer", tissuePhase);
            GUI.EndGroup();
        }

        static void DrawStageLayer(Rect area, int frame, int stage, float transition, Texture2D[] layers, string path,
            float tissuePhase)
        {
            if (transition < 1f)
                DrawCell(area, layers[stage - 1] ??= Texture(path + stage), frame, 1f - transition, tissuePhase);
            if (transition > 0f)
                DrawCell(area, layers[stage] ??= Texture(path + (stage + 1)), frame, transition, tissuePhase);
        }

        static void DrawCell(Rect area, Texture atlas, int frame, float opacity, float tissuePhase)
        {
            float inset = (1f - InteriorCrop) * .5f;
            var uv = new Rect((frame % AtlasColumns + inset) / AtlasColumns,
                (AtlasRows - 1 - frame / AtlasColumns + inset) / AtlasRows,
                InteriorCrop / AtlasColumns, InteriorCrop / AtlasRows);
            var old = GUI.color;
            GUI.color = new Color(old.r, old.g, old.b, old.a * opacity);
            // Share the same wave across the wall, liquid and foreground so the seams stay aligned.
            float sourceTop = 0f;
            for (int band = 0; band < TissueBands; band++)
            {
                float top = band / (float)TissueBands;
                float bottom = (band + 1) / (float)TissueBands;
                float sourceBottom = bottom + Mathf.Sin(bottom * Mathf.PI) *
                    Mathf.Sin(tissuePhase - bottom * 8f) * .016f;
                float centre = (top + bottom) * .5f;
                float shift = Mathf.Sin(centre * Mathf.PI) * Mathf.Sin(tissuePhase - centre * 7f + 1f) * .006f;
                var strip = new Rect(area.x, area.y + top * area.height, area.width, (bottom - top) * area.height);
                var stripUv = new Rect(uv.x + shift * uv.width, uv.yMax - sourceBottom * uv.height,
                    uv.width, (sourceBottom - sourceTop) * uv.height);
                GUI.DrawTextureWithTexCoords(strip, atlas, stripUv);
                sourceTop = sourceBottom;
            }
            GUI.color = old;
        }

        static void GetOccupantPose(Rect area, float elapsed, float closure, float collapse,
            Vector2 slot, float scale, out Rect body, out Vector2 basePoint, out Rot4 facing)
        {
            float stepTime = elapsed / 2.4f;
            int step = Mathf.FloorToInt(stepTime) % Steps.Length;
            float stridePhase = stepTime - Mathf.Floor(stepTime);
            var start = Steps[step];
            var end = Steps[(step + 1) % Steps.Length];
            var center = new Vector2(.5f, .51f);
            bool walking = collapse == 0f && stridePhase is > .16f and < .83f && closure < .50f;
            Vector2 foot = Vector2.Lerp(start, end, Ease((stridePhase - .16f) / .67f));
            foot = Vector2.Lerp(foot, center, collapse);
            foot = Vector2.Lerp(foot, center, Ease(closure / .91f));
            Vector2 direction = closure > .2f ? center - start : end - start;
            facing = collapse > 0f || closure > .63f ? Rot4.South
                : Mathf.Abs(direction.x) > Mathf.Abs(direction.y)
                    ? (direction.x > 0 ? Rot4.East : Rot4.West)
                    : (direction.y > 0 ? Rot4.South : Rot4.North);
            foot = center + (foot - center) * scale + slot;
            float size = area.width * .27f * scale;
            float lift = walking ? Mathf.Abs(Mathf.Sin(elapsed * 12f)) * size * .021f : 0f;
            basePoint = new Vector2(area.x + foot.x * area.width, area.y + foot.y * area.height);
            body = new Rect(basePoint.x - size * .5f, basePoint.y - size * (.79f - collapse * .29f) - lift, size, size);
        }

        static void DrawOccupant(Pawn pawn, Rect area, float elapsed, float closure, float collapse, Vector2 slot, float scale)
        {
            if (pawn.Downed) collapse = 1f;
            GetOccupantPose(area, elapsed, closure, collapse, slot, scale,
                out Rect body, out Vector2 basePoint, out Rot4 facing);
            Paint(shadow ??= Texture("PenanceShadow"), basePoint, body.width * .48f, body.height * .16f, .85f);
            Texture portrait = PortraitsCache.Get(pawn, new Vector2(192f, 192f), facing,
                cameraZoom: .88f, supersample: false, compensateForUIScale: false);
            var matrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(-78f * collapse, body.center);
            Paint(portrait, body.center, body.width, body.height, 1f);
            GUI.matrix = matrix;
        }

        static void DrawDrops(Rect area, float elapsed, float fill, float closure)
        {
            Vector2 landing = Vector2.Lerp(new Vector2(.35f, .71f), new Vector2(.49f, .54f), fill);
            landing = Vector2.Lerp(landing, new Vector2(.5f, .51f), closure);
            var point = new Vector2(area.x + area.width * landing.x, area.y + area.height * landing.y);
            float cycle = Mathf.Repeat(elapsed, 1.75f - fill * .35f);
            if (cycle < .38f)
            {
                float approach = Ease(cycle / .38f);
                float size = Mathf.Lerp(3f, 9f, approach);
                Paint(drop ??= Texture("PenanceDrop"), point, size, size, approach * .78f);
            }
            else if (cycle < 1.05f)
            {
                float impact = (cycle - .38f) / .67f;
                float size = Mathf.Lerp(7f, 28f, Ease(impact));
                Paint(ripple ??= Texture("PenanceRipple"), point, size, size * .93f, (1f - impact) * .60f);
            }
        }

        static void Paint(Texture texture, Vector2 center, float width, float height, float opacity)
        {
            var old = GUI.color;
            GUI.color = new Color(old.r, old.g, old.b, old.a * opacity);
            GUI.DrawTexture(new Rect(center.x - width / 2f, center.y - height / 2f, width, height), texture);
            GUI.color = old;
        }
    }
}

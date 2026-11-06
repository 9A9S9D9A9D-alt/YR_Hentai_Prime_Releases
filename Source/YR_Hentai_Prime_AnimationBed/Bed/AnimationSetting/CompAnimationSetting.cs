using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace YR_Hentai_Prime_AnimationBed
{
    public class CompAnimationSetting : CompBaseOfAnimationBed, IAnimationPositionSource
    {
        public CompProperties_AnimationSetting Props => (CompProperties_AnimationSetting)props;

        public List<BedAnimationSettingAndTick> bedAnimationSettingAndTicks = new List<BedAnimationSettingAndTick>();

        public Pawn PositionPawn => HeldPawn;
        public IEnumerable<KeyValuePair<string,string>> PositionLayers {
            get {
                int i=0;
                foreach(var layer in bedAnimationSettingAndTicks){
                    var d=layer.parentBedAnimationDef;
                    yield return new KeyValuePair<string,string>(PositionKey(d),d.bodyOverlay?"몸체 애니메이션":d.isPawnTextureReplace?(d.pawnRenderNodeTagDef==PawnRenderNodeTagDefOf.Head?"머리":"몸체"):"효과 "+(++i));
                }
                foreach(var entry in BubblePortraitV57.Slots(Props)){
                    yield return new KeyValuePair<string,string>("photo:"+entry.Key,"말풍선 "+(entry.Key+1)+" · 전체");
                    yield return new KeyValuePair<string,string>("bubbleContent:"+entry.Key,"말풍선 "+(entry.Key+1)+" · 내부 영상");
                }
                foreach(var portrait in portraitIngredients)
                    if(!BubblePortraitV57.IsBubble(portrait.portraitSetting))yield return new KeyValuePair<string,string>("photo:"+portrait.slot,"얼굴 사진 "+(portrait.slot+1));
                for(int n=0;n<fillableBarIngredients.Count;n++)yield return new KeyValuePair<string,string>("liquid:"+n,"액체 표시 "+(n+1));
            }
        }
        public static string PositionKey(BedAnimationDef d) => d.bodyOverlay?"bodyOverlay":d.isPawnTextureReplace?(d.pawnRenderNodeTagDef==PawnRenderNodeTagDefOf.Head?"head":"body"):"animation:"+d.defName;

        public bool needMakeGraphics = true;
        public bool hasResidualEffect;
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref hasResidualEffect, "facilityResidualEffect", false);
        }
        public void RecordResidualEffect()
        {
            if (parent.def.defName != "YR_Building_GloryHole" && parent.def.defName != "YR_Building_Cumdump") return;
            hasResidualEffect = true;
            needMakeGraphics = true;
        }


        public List<PortraitIngredient> portraitIngredients = new List<PortraitIngredient>();
        public List<FillableBarIngredient> fillableBarIngredients = new List<FillableBarIngredient>();

        public override void PostDraw()
        {
            if (HeldPawn != null)
            {
                DrawBedAnimation();
            }
        }

        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            foreach (var ingredient in portraitIngredients) FacilityPortrait.Release(ingredient);
            portraitIngredients.Clear();
            base.PostDeSpawn(map, mode);
        }

        public void DrawBedAnimation()
        {

            foreach (var bedAnimationSettingAndTick in bedAnimationSettingAndTicks)
            {
                // These frames replace the native Body material; do not also draw an overlay.
                if (bedAnimationSettingAndTick.parentBedAnimationDef.fixedFacingBody) continue;
                int currentTick = bedAnimationSettingAndTick.currentTick;

                var closestSetting = bedAnimationSettingAndTick.bedAnimationSettings
                    .Where(b => b.tick <= currentTick)
                    .OrderByDescending(b => b.tick)
                    .FirstOrDefault();

                // 설정이 없을 경우 처리
                if (closestSetting == null && (bedAnimationSettingAndTick.autoDurationTicksSetting || currentTick <= bedAnimationSettingAndTick.durationTick))
                {
                    closestSetting = bedAnimationSettingAndTick.bedAnimationSettings
                        .OrderByDescending(x => x.tick)
                        .FirstOrDefault();

                    if (closestSetting == null)
                        continue; // 설정이 없으면 다음으로 넘어감
                }

                Vector3 pos = CalculatePos(bedAnimationSettingAndTick, closestSetting);

                var animationDef = bedAnimationSettingAndTick.parentBedAnimationDef;
                Rot4 facing = Rot4.North;
                // Facing selects directional textures; it must not rotate a single-frame graphic.
                bool hasDirectionalTextures = closestSetting.graphic is Graphic_Multi && !closestSetting.graphic.ShouldDrawRotated;
                if (hasDirectionalTextures && (animationDef.isPawnTextureReplace || animationDef.animationSynchro || animationDef.animationSynchrotoDummyForJoyAnimation))
                    facing = Building_AnimationBed.TryGetComp<CompFacilityFacing>()?.Resolve(facing) ?? facing;
                int bubbleSlot=BubblePortraitV57.SlotFor(Props,animationDef);
                var adjustment=parent.TryGetComp<CompAnimationPosition>();
                float scale=adjustment?.RenderScale(HeldPawn,PositionKey(animationDef))??1f;
                if(bubbleSlot>=0)scale*=adjustment?.DisplayScale(HeldPawn,"photo:"+bubbleSlot)??1f;
                if((bubbleSlot>=0||scale!=1f) && closestSetting.graphic!=null){
                    var graphic=closestSetting.graphic;
                    var matrix=Matrix4x4.TRS(pos+graphic.DrawOffset(facing),graphic.QuatFromRot(facing),new Vector3(scale,1f,scale));
                    GenDraw.DrawMeshNowOrLater(graphic.MeshAt(facing),matrix,graphic.MatAt(facing,HeldPawn),false);
                }else closestSetting.graphic?.Draw(pos, facing, Building_AnimationBed.HeldPawn);
            }

            // 포트레잇
            DrawPortrait();
            //액체 바
            DrawFillableBar();
            Building_AnimationBed.makePortrait = false;
        }

        private void DrawFillableBar()
        {
            foreach (var fillableBarIngredient in fillableBarIngredients)
            {
                GenDraw.FillableBarRequest r = default(GenDraw.FillableBarRequest);
                r.center = Building_AnimationBed.DrawPos + fillableBarIngredient.offset + fillableBarIngredient.testOffset;
                var adjustment=parent.TryGetComp<CompAnimationPosition>();
                if(adjustment!=null)r.center+=adjustment.Offset(HeldPawn,"all")+adjustment.Offset(HeldPawn,"liquid:"+fillableBarIngredients.IndexOf(fillableBarIngredient));
                r.size = (fillableBarIngredient.drawSize + fillableBarIngredient.testDrawSize)*(adjustment?.RenderScale(HeldPawn,"liquid:"+fillableBarIngredients.IndexOf(fillableBarIngredient))??1f);
                r.fillPercent = 1;
                r.filledMat = SolidColorMaterials.SimpleSolidColorMaterial(fillableBarIngredient.color + fillableBarIngredient.testColor, false);
                r.unfilledMat = SolidColorMaterials.SimpleSolidColorMaterial(new Color(0, 0, 0, 0), false);
                r.margin = 0f;
                Rot4 rotation = Rot4.South;
                rotation.Rotate(RotationDirection.Clockwise);
                r.rotation = rotation;
                GenDraw.DrawFillableBar(r);
            }
        }

        //포트레잇 그려내기
        private void DrawPortrait()
        {
            foreach (var portraitIngredient in portraitIngredients)
            {
                if (portraitIngredient.label != null)
                {
                    Log.Error(portraitIngredient.label);
                }

                Pawn pawn = portraitIngredient.pawn;
                Vector3 drawPos = Building_AnimationBed.DrawPos + portraitIngredient.offset + portraitIngredient.testOffset;
                var adjustment=parent.TryGetComp<CompAnimationPosition>();
                if(adjustment!=null)drawPos+=adjustment.Offset(HeldPawn,"all")+adjustment.Offset(HeldPawn,"photo:"+portraitIngredient.slot);
                Vector3 drawSize = new Vector3(portraitIngredient.drawSize.x + portraitIngredient.testDrawSize.x, 1, portraitIngredient.drawSize.y + portraitIngredient.testDrawSize.y);

                drawSize*=adjustment?.RenderScale(HeldPawn,"photo:"+portraitIngredient.slot)??1f;
                if (Building_AnimationBed.makePortrait)
                {
                    var portraitSetting = portraitIngredient.portraitSetting;
                    var cameraOffset = portraitIngredient.cameraOffset;

                    if (portraitSetting.animationSynchro)
                    {
                        var renderNode = FacilityRenderNodes.Find(pawn, portraitSetting.pawnRenderNodeTagDef);

                        if (renderNode != null)
                        {
                            var offset = renderNode.Worker.OffsetFor(renderNode, Building_AnimationBed.HeldPawnDrawParms, out var pivot);
                            cameraOffset -= offset - pivot;
                        }
                    }

                    cameraOffset += portraitIngredient.testCameraOffset;
                    if(BubblePortraitV57.IsBubble(portraitSetting) && adjustment!=null)
                        // Moving the camera left moves the framed image right.
                        cameraOffset-=adjustment.Offset(HeldPawn,"bubbleContent:"+portraitIngredient.slot);
                    TestLog.Error($"cameraOffset : {cameraOffset.x:F5}, {cameraOffset.y:F5}, {cameraOffset.z:F5}");

                    var cameraZoom = portraitIngredient.cameraZoom + portraitIngredient.testCameraZoom;
                    if(BubblePortraitV57.IsBubble(portraitSetting))cameraZoom*=adjustment?.DisplayScale(HeldPawn,"bubbleContent:"+portraitIngredient.slot)??1f;
                    var rotation = Props.pawnAnimationSetting.rotation;

                    foreach (var conditionPawnRotation in Props.pawnAnimationSetting.conditionPawnRotations)
                    {
                        if (Condition.ExecuteActionIfConditionMatches(pawn, Building_AnimationBed, conditionPawnRotation.condition,
                            () => rotation = conditionPawnRotation.rotation))
                        {
                            break;
                        }
                    }

                    portraitIngredient.iconMat.mainTexture = FacilityPortrait.GetTexture(portraitIngredient, cameraOffset, cameraZoom,
                        portraitSetting.headOnly ? null : Building_AnimationBed.TryGetComp<CompFacilityFacing>()?.Resolve(portraitSetting.rotation));
                }

                if (!Building_AnimationBed.UsesHeadPortrait && !portraitIngredient.portraitSetting.headOnly && pawn.Drawer.renderer.HasAnimation && pawn.Drawer.renderer.CurAnimation != YR_H_P_DefOf.YR_Global_Animation_NoMove)
                {
                    var pos = (pawn == HeldPawn) ? Building_AnimationBed.DrawPos + Building_AnimationBed.PawnDrawOffset : pawn.Drawer.DrawPos;
                    var pawnRotation = (pawn == HeldPawn) ? Props.pawnAnimationSetting.rotation : pawn.Rotation;
                    pawn.Drawer.renderer.DynamicDrawPhaseAt(DrawPhase.Draw, pos, pawnRotation, neverAimWeapon: true);
                }

                Matrix4x4 matrix = Matrix4x4.TRS(drawPos, Quaternion.AngleAxis(portraitIngredient.angle + portraitIngredient.testAngle, Vector3.up), drawSize);
                GenDraw.DrawMeshNowOrLater(portraitIngredient.portraitSetting.headOnly ? MeshPool.plane10 : portraitIngredient.portraitMesh, matrix, portraitIngredient.iconMat, PawnRenderFlags.None.FlagSet(PawnRenderFlags.DrawNow));
            }
        }

        private Vector3 CalculatePos(BedAnimationSettingAndTick bedAnimationSettingAndTick, BedAnimationSetting closestSetting)
        {
            // 기본 위치 계산
            Vector3 pos = Building_AnimationBed.DrawPos;

            // 렌더 노드의 오프셋 계산
            if (bedAnimationSettingAndTick.parentBedAnimationDef.animationSynchro && bedAnimationSettingAndTick.parentBedAnimationDef.pawnRenderNodeTagDef != null)
            {
                PawnRenderNode renderNode = FacilityRenderNodes.Find(HeldPawn, bedAnimationSettingAndTick.parentBedAnimationDef.pawnRenderNodeTagDef);

                if (renderNode != null)
                {
                    Vector3 offset = renderNode.Worker.OffsetFor(renderNode, Building_AnimationBed.HeldPawnDrawParms, out var pivot);
                    offset -= pivot;
                    pos += offset;

                    if (bedAnimationSettingAndTick.parentBedAnimationDef.logCurrentOffset)
                    {
                        TestLog.Error($"pawnRenderNodeTagDef offset : {offset.x:F5}, {offset.y:F5}, {offset.z:F5}");
                        TestLog.Error($"pawnRenderNodeTagDef pivot : {pivot.x:F5}, {pivot.y:F5}, {pivot.z:F5}");
                    }
                }
            }

            else if (bedAnimationSettingAndTick.parentBedAnimationDef.animationSynchrotoDummyForJoyAnimation && bedAnimationSettingAndTick.parentBedAnimationDef.pawnRenderNodeTagDef != null && Building_AnimationBed.dummyForJoyIsActive && Building_AnimationBed.dummyForJoyPawn != null)
            {
                var dummyPawn = Building_AnimationBed.dummyForJoyPawn;
                PawnRenderNode renderNode = FacilityRenderNodes.Find(dummyPawn, bedAnimationSettingAndTick.parentBedAnimationDef.pawnRenderNodeTagDef);

                if (renderNode != null)
                {
                    Vector3 offset = renderNode.Worker.OffsetFor(renderNode, Building_AnimationBed.HeldPawnDrawParms, out var pivot);
                    offset -= pivot;
                    pos += offset;

                    if (bedAnimationSettingAndTick.parentBedAnimationDef.logCurrentOffset)
                    {
                        Log.Error($"pawnRenderNodeTagDef offset : {offset.x:F5}, {offset.y:F5}, {offset.z:F5}");
                        Log.Error($"pawnRenderNodeTagDef pivot : {pivot.x:F5}, {pivot.y:F5}, {pivot.z:F5}");
                    }
                }
            }

            // 테스트용 오프셋 추가
            pos.y = Building_AnimationBed.DrawPos.y;
            pos += closestSetting.offset;
            pos += closestSetting.testOffset;
            var adjustment=parent.TryGetComp<CompAnimationPosition>();
            if(adjustment!=null){
                pos+=adjustment.Offset(HeldPawn,"all")+adjustment.Offset(HeldPawn,PositionKey(bedAnimationSettingAndTick.parentBedAnimationDef));
                int slot=BubblePortraitV57.SlotFor(Props,bedAnimationSettingAndTick.parentBedAnimationDef);
                if(slot>=0)pos+=adjustment.Offset(HeldPawn,"photo:"+slot);
            }
            if (bedAnimationSettingAndTick.parentBedAnimationDef.logCurrentOffset)
            {
                Log.Error($"pos : {pos:F10}");
            }
            return pos;
        }
    }

    public class FillableBarIngredient
    {
        public Pawn pawn;

        public Vector3 offset;
        public Vector2 drawSize;
        public Color color;

        public bool openTestGizmo;

        public Vector3 testOffset;
        public Vector2 testDrawSize;
        public Color testColor;
    }


    public class PortraitIngredient
    {
        public int slot;
        public BedAnimationDef bubbleSource;
        public List<BedAnimationSetting> bubbleFrames=new List<BedAnimationSetting>();
        public Graphic lastBubbleGraphic;
        public int lastBubbleNativeTick=-1;
        public Vector3 lastBubbleOffset;
        public float lastBubbleZoom;
        public Rot4 lastBubbleRotation;
        public string label;
        public Pawn pawn;
        public Material iconMat;
        public RenderTexture ownedTexture;
        public Mesh portraitMesh;

        public Vector3 offset;
        public float angle;
        public Vector2 drawSize;
        public Vector3 cameraOffset;
        public float cameraZoom;

        public PortraitSetting portraitSetting;

        public bool openTestGizmo;

        public Vector3 testOffset;
        public float testAngle;
        public Vector2 testDrawSize;
        public Vector3 testCameraOffset;
        public float testCameraZoom;
    }

    public class BedAnimationSettingAndTick
    {
        public List<BedAnimationSetting> bedAnimationSettings = new List<BedAnimationSetting>();

        public int durationTick;

        public int currentTick;

        public bool autoDurationTicksSetting = false;

        public BedAnimationDef parentBedAnimationDef;

        public bool openTestGizmo = false;

    }

    public class BedAnimationSetting
    {
        public Condition condition;
        public GraphicData graphicData;
        public List<ConditionGraphicData> conditionGraphicDatas = new List<ConditionGraphicData>();
        public string maskPathForPortrait;
        public Graphic graphic;
        public int tick;
        public Vector3 offset;
        public Rot4? rotation;
        public SoundDef soundDef;
        public int angle;
        //모종의 이유로 안하고 싶으면 xml에서 별도 조정
        //public bool animationSynchro;
        //public PawnRenderNodeTagDef pawnRenderNodeTagDef;
        public bool setPawnColor;
        public BedAnimationDef parentBedAnimationDef;

        public Vector3 testOffset = new Vector3();
        public Vector2 testDrawSize;

        public BedAnimationSetting() => graphic = new Graphic();

        public BedAnimationSetting Copy()
        {
            var copied = (BedAnimationSetting)this.MemberwiseClone();
            copied.graphic = new Graphic();
            return copied;
        }
    }

    public class ConditionGraphicData
    {
        public Condition condition;

        public GraphicData graphicData;
    }
}

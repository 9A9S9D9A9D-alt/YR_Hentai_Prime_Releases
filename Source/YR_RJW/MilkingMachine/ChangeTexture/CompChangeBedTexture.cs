using AlienRace;
using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using Verse;
using Graphic = Verse.Graphic;
using Random = System.Random;
using Type = System.Type;

namespace YR_RJW
{
    public class CompChangeBedTexture : Comp_Building_BaseBondageBed, YR_Hentai_Prime_AnimationBed.IAnimationPositionSource
    {
        public CompProperties_ChangeBedTexture Props => (CompProperties_ChangeBedTexture)props;

        // Only this graphics component sees coffin contents. Gameplay components keep
        // Comp_Building_BaseBondageBed.pawn (the real living occupant) unchanged.
        public new Pawn pawn => base.pawn ?? BBB.CoffinDisplayPawn;
        private bool CoffinVisual => base.pawn==null && BBB.CoffinDisplayPawn!=null;
        private Corpse preparedCoffinCorpse;
        public Pawn PositionPawn => pawn;
        public static string PositionKey(GraphicDataAndVec3 layer,int index) => layer.isPawnBodyReplacement||layer.isBodyGraphic?"body":layer.isHeadGraphic?"head":layer.isFaceAnimation?"face":"legacy:"+index;
        public IEnumerable<KeyValuePair<string,string>> PositionLayers {
            get {
                if(parent.TryGetComp<CompEggMilkingBodyV44>()?.BodyOverlay!=null)
                    yield return new KeyValuePair<string,string>("bodyOverlay","몸체 애니메이션");
                for(int i=0;i<GDAV3s.Count;i++){
                var layer=GDAV3s[i];string key=PositionKey(layer,i);
                yield return new KeyValuePair<string,string>(key,key=="body"?"몸체":key=="head"?"머리":key=="face"?"표정":string.IsNullOrEmpty(layer.gizmoString)?"효과 "+(i+1):layer.gizmoString);
            }}
        }
        public bool afterLoad = false;
        public bool unWatched = false;
        public bool stopAnimation = false;
        public bool resetComplete = false;
        public bool needMakeGrapgicList = false;
        public bool reverseTexture;

        private List<PathSetAndNum> compPathSetAndNums = new List<PathSetAndNum>();
        public List<GraphicDataAndVec3> GDAV3s = new List<GraphicDataAndVec3>();
        public Vector3 headOffSet = new Vector3();
        //바디의 위치를 움직이게 해주는거
        public Vector3 movedHeadOffSet = new Vector3();
        public Vector3 joyPawnMovingOffset = new Vector3();
        public Vector3 bodyOffSet = new Vector3();
        public bool followHeadOffset = false;
        private int headOffSetNum = 0;
        public int joyPawnOffsetNum = 0;
        private PawnCondition compPawnCondition = null;
        private PawnCondition originalPawnCondition = null;
        public Material iconMat;
        public Mesh portraitMesh;

        public Material bodyMat;

        public Rot4 bodyFacing = Rot4.South;
        public bool remakeIconMat = true;
        public int remakeIconMatTick = 100;
        public Vector2 DrawSizeMultipler => parent.Graphic.drawSize;

        internal static Graphic FrameAt(GraphicDataAndVec3 layer)
        {
            if(layer?.graphics==null || layer.internalNum<0 || layer.internalNum>=layer.graphics.Count)return null;
            return layer.graphics[layer.internalNum];
        }
        public Graphic BoundGraphic
        {
            get
            {
                foreach (GraphicDataAndVec3 graphicDataAndVec3 in GDAV3s)
                {
                    if (graphicDataAndVec3.isPawnBodyReplacement)
                    {
                        return FrameAt(graphicDataAndVec3);
                    }
                }
                return null;
            }
        }
        public List<Material> cachedMatsBodyBase = new List<Material>();
        public int cachedMatsBodyBaseHash = -1;
        public bool AnimationWork => BBB.PowerOn && !YR_RJW_Setting.YR_StopAnimation && !unWatched && !stopAnimation;

        public bool showHat = false;
        public bool showApparel = false;
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            afterLoad = true;
            needMakeGrapgicList = true;
            ticks = 1;
        }

        public override void PostRemoveOccupantByAnother(Pawn pawn)
        {
            base.PostRemoveOccupantByAnother(pawn);
            remakeIconMat = true;
        }
        public override void PostSetOccupantByAnother(Pawn pawn)
        {
            base.PostSetOccupantByAnother(pawn);
            remakeIconMat = true;
        }
        public override void PostDraw()
        {
            if(!parent.Spawned || parent.Map!=Find.CurrentMap || !Find.CameraDriver.CurrentViewRect.ExpandedBy(6).Overlaps(parent.OccupiedRect()))return;
            if (pawn != null && !YR_Hentai_Prime_AnimationBed.UnsupportedRacesV8.Allows(pawn.def)) return;
            if (pawn != null)
            {
                float angle = 0f;
                Rot4 facing = pawn.Rotation;
                int layerIndex=0;
                foreach (GraphicDataAndVec3 GDAV3 in GDAV3s)
                {
                    int stableLayer=layerIndex++;
                    Graphic currentFrame=FrameAt(GDAV3);
                    if(!GDAV3.isBodyGraphic && !GDAV3.isPawnBodyReplacement && !GDAV3.isHeadGraphic && !GDAV3.isWater && currentFrame==null)continue;
                    if (parent.def.defName == "YR_Building_BellyPunch" && GDAV3.isFaceAnimation &&
                        FacilityRender.TryDrawHeadOverlay(pawn, currentFrame)) continue;
                    Vector3 fixedPosition = GDAV3.fixedPosition;
                    fixedPosition += GDAV3.testOffset;
                    var adjustment=parent.TryGetComp<YR_Hentai_Prime_AnimationBed.CompAnimationPosition>();
                    float displayScale=adjustment?.RenderScale(pawn,PositionKey(GDAV3,stableLayer))??1f;
                    if(adjustment!=null&&!GDAV3.isBodyGraphic&&!GDAV3.isPawnBodyReplacement&&!GDAV3.isHeadGraphic)fixedPosition+=adjustment.Offset(pawn,"all")+adjustment.Offset(pawn,PositionKey(GDAV3,stableLayer));
                    if(!GDAV3.isBodyGraphic && !GDAV3.isPawnBodyReplacement && !GDAV3.isHeadGraphic && !GDAV3.isFaceAnimation)
                        fixedPosition.y=AltitudeLayer.PawnUnused.AltitudeFor()+.01f*GDAV3.fixedPosition.y+.00001f*stableLayer;
                    // Move only this facility's animation layers; leave the pawn origin unchanged.
                    if((parent.def.defName=="YR_Building_TentacleMilking" || parent.def.defName=="YR_Building_LoveRoom" || parent.def.defName=="YR_Building_EggMachine") && !GDAV3.isBodyGraphic && !GDAV3.isPawnBodyReplacement)
                        fixedPosition.z+=1f;

                    if (GDAV3.rotation != null)
                    {
                        facing = (Rot4)GDAV3.rotation;
                    }

                    Quaternion quaternion = Quaternion.AngleAxis(angle, Vector3.up);
                    PawnRenderFlags flags = PawnRenderFlags.None;

                    if (GDAV3.isBodyGraphic || GDAV3.isPawnBodyReplacement)
                    {
                        PawnRenderFlags drawFlags = PawnRenderFlags.Cache;
                        if (showHat)
                        {
                            drawFlags |= PawnRenderFlags.Headgear;
                        }
                        if (showApparel)
                        {
                            drawFlags |= PawnRenderFlags.Clothes;
                        }

                        Vector3 tempDrawPos = new Vector3(parent.DrawPos.x, parent.DrawPos.y + 0.5f, parent.DrawPos.z + 0.25f);
                        if(parent.def.defName=="YR_Building_LoveRoom")tempDrawPos.z+=.15f;
                        if(parent.def.defName=="YR_Building_TentacleMilking"){tempDrawPos.z-=.03f;tempDrawPos.x-=.04f;}
                        if (Props.hideHead)
                        {
                            tempDrawPos.z += 1000;
                        }
                        pawn.Rotation = facing;
                        bodyFacing = facing;


                        Vector3 pawnDrawPos = tempDrawPos + headOffSet;
                        pawnDrawPos += GDAV3.testOffset;
                        DrawInnerThing(pawn, pawnDrawPos, 0, true, bodyFacing, RotDrawMode.Fresh, drawFlags);
                    }
                    else if (GDAV3.isPortrait)
                    {
                        if (remakeIconMat)
                        {
                            iconMat = new Material(ShaderDatabase.DefaultShader)
                            {
                                mainTexture = CapturePortraitTexture(GDAV3.cameraOffset,2.5f),
                                color = Color.white
                            };
                            remakeIconMat = false;
                        }

                        if(GDAV3.configuredPortrait)
                        {
                            iconMat=iconMat??new Material(ShaderDatabase.CutoutComplex);
                            iconMat.mainTexture=ConfiguredPortraitTexture(GDAV3);
                            quaternion=Quaternion.AngleAxis(GDAV3.portraitAngle,Vector3.up);
                        }
                        if (iconMat != null)
                        {

                            Vector3 drawsize = new Vector3(currentFrame.drawSize.x, 1f, currentFrame.drawSize.y);

                            drawsize.x += GDAV3.testDrawSize.x;
                            drawsize.z += GDAV3.testDrawSize.y;

                            Vector3 vector = new Vector3(fixedPosition.x, fixedPosition.y, fixedPosition.z);

                            Matrix4x4 matrix4x = default;
                            matrix4x.SetTRS(vector, quaternion, new Vector3(drawsize.x*displayScale,1f,drawsize.z*displayScale));

                            Material material=iconMat;
                            if(GDAV3.configuredPortrait)
                            {
                                var request=new MaterialRequest{mainTex=iconMat.mainTexture,maskTex=GDAV3.portraitMask,color=Color.white,shader=ShaderDatabase.CutoutComplex};
                                material=MaterialPool.MatFrom(request);
                            }
                            GenDraw.DrawMeshNowOrLater(portraitMesh, matrix4x, material, flags.FlagSet(PawnRenderFlags.DrawNow));
                        }
                    }
                    else if (GDAV3.isHeadGraphic)
                    {
                        if (GDAV3.material != null)
                        {
                            Mesh mesh = HumanlikeMeshPoolUtility.GetHumanlikeHeadSetForPawn(pawn).MeshAt(facing);
                            GenDraw.DrawMeshNowOrLater(mesh, Matrix4x4.TRS(fixedPosition,quaternion,new Vector3(displayScale,1f,displayScale)), GDAV3.material, flags.FlagSet(PawnRenderFlags.DrawNow));
                        }
                    }
                    else if (GDAV3.isWater)
                    {
                        Vector2 drawsize = GDAV3.originalDrawSize;
                        drawsize.x += GDAV3.testDrawSize.x;
                        drawsize.y += GDAV3.testDrawSize.y;

                        Color color = GDAV3.originalColor;
                        color += GDAV3.colorTest;

                        LiquidDraw(color, fixedPosition, drawsize*displayScale, bodyFacing, 1);
                    }
                    else
                    {
                        if (GDAV3.graphics != null)
                        {
                            Graphic graphic = currentFrame;
                            if(graphic==null)continue;
                            Vector2 drawSize = graphic.drawSize;
                            graphic.drawSize.x += GDAV3.testDrawSize.x;
                            graphic.drawSize.y += GDAV3.testDrawSize.y;
                            if (GDAV3.useDrawSizeMultipler)
                            {
                                graphic.drawSize *= DrawSizeMultipler;
                            }
                            Color color = graphic.data.color;
                            graphic.data.color += GDAV3.colorTest;

                            bool alignToBuilding=parent.def.defName=="YR_Building_TentacleMilking" || parent.def.defName=="YR_Building_LoveRoom" || parent.def.defName=="YR_Building_EggMachine";
                            var drawRotation=alignToBuilding?parent.Rotation:parent.Rotation.Opposite;
                            if(displayScale==1f)graphic.Draw(fixedPosition,drawRotation,parent,0f);
                            else GenDraw.DrawMeshNowOrLater(graphic.MeshAt(drawRotation),
                                Matrix4x4.TRS(fixedPosition+graphic.DrawOffset(drawRotation),graphic.QuatFromRot(drawRotation),new Vector3(displayScale,1f,displayScale)),graphic.MatAt(drawRotation,parent),false);
                            graphic.drawSize = drawSize;
                            graphic.data.color = color;
                        }
                    }
                }
            }
        }

        private static readonly AccessTools.FieldRef<PawnRenderTree,PawnDrawParms> PreparedPortraitParms=
            AccessTools.FieldRefAccess<PawnRenderTree,PawnDrawParms>("oldParms");
        private Texture ConfiguredPortraitTexture(GraphicDataAndVec3 layer) =>
            CapturePortraitTexture(layer.cameraOffset,layer.cameraZoom,layer.portraitShowApparel,layer.portraitShowHat);
        private Texture CapturePortraitTexture(Vector3 cameraOffset,float cameraZoom,bool renderApparel=true,bool renderHat=true)
        {
            var tree=pawn.Drawer.renderer.renderTree;
            var prepared=PreparedPortraitParms(tree);
            try
            {
                return PortraitsCache.Get(pawn,new Vector2(256,256),Rot4.South,cameraOffset,cameraZoom,
                    renderClothes:renderApparel,renderHeadgear:renderHat,
                    stylingStation:false,healthStateOverride:PawnHealthState.Mobile);
            }
            finally {if(prepared.pawn!=null)tree.ParallelPreDraw(prepared);}
        }

        public virtual void DrawInnerThing(Pawn pawn, Vector3 rootLoc, float angle, bool renderBody, Rot4 bodyFacing, RotDrawMode bodyDrawType, PawnRenderFlags renderFlags)
        {
            FacilityRender.Draw(pawn, rootLoc, bodyFacing, renderFlags);
        }
        public virtual void LiquidDraw(Color color, Vector3 rootLoc, Vector2 drawSize, Rot4 bodyFacing, float fillPct)
        {
            GenDraw.FillableBarRequest r = default(GenDraw.FillableBarRequest);
            r.center = rootLoc;
            r.size = drawSize;
            r.fillPercent = fillPct;
            r.filledMat = SolidColorMaterials.SimpleSolidColorMaterial(color, false);
            r.unfilledMat = SolidColorMaterials.SimpleSolidColorMaterial(new Color(0, 0, 0, 0), false);
            r.margin = 0f;
            Rot4 rotation = bodyFacing;
            rotation.Rotate(RotationDirection.Clockwise);
            r.rotation = rotation;
            GenDraw.DrawFillableBar(r);
        }
        public int age = -1;
        public bool pawnIsAgeException = false;
        public override void CompTick()
        {
            // Rebuild on insertion/ejection/resurrection, including loading an old save.
            Corpse displayed=BBB.CoffinDisplayPawn!=null?BBB.CoffinCorpse:null;
            if(preparedCoffinCorpse!=displayed){Reset();preparedCoffinCorpse=displayed;}
            if (pawn != null && !CoffinVisual)
            {
                if (!pawn.Downed)
                {
                    //{0}가 의도치 않은 이유로 풀려났습니다. 이는 속박한 건물이 {0}의 {1}에 있기 때문일 수도 있습니다. {0}의 {2} 설정을 확인해주세요.
                    Messages.Message("YR_OutSideAllowedArea".Translate(pawn.LabelShort, "ForbiddenOutsideAllowedAreaLower".Translate(), "AllowedArea".Translate()), MessageTypeDefOf.NeutralEvent, false);
                    BBB.RemoveOccupant();
                    return;
                }
                if (age != pawn.ageTracker.AgeBiologicalYears || age == -1)
                {
                    age = pawn.ageTracker.AgeBiologicalYears;

                    LifeStageDef lifeStageDef = pawn.RaceProps.lifeStageAges.FirstOrDefault((LifeStageAge lifeStageAge) => lifeStageAge.def.developmentalStage.Adult()).def;
                    bool isAdult = pawn.ageTracker.Adult || lifeStageDef.defName.ToUpper().Contains("adult".ToUpper());
                    if (!isAdult && !pawnIsAgeException)
                    {
                        var compUsableAdvancedBondageBed = parent.TryGetComp<CompUsableAdvancedBondageBed>();

                        if (compUsableAdvancedBondageBed != null)
                        {
                            if (compUsableAdvancedBondageBed.Props.settingException.ageExceptionRaces.Contains(pawn.def))
                            {
                                pawnIsAgeException = true;
                            }
                            else
                            {
                                Messages.Message("YR_IsNotAdult".Translate(pawn.LabelShort, lifeStageDef.LabelCap), MessageTypeDefOf.NeutralEvent, false);
                                BBB.RemoveOccupant();
                                return;
                            }
                        }
                    }
                }
            }


            if (pawn == null || (!CoffinVisual && pawn.GetPosture() != PawnPosture.LayingInBed))
            {
                if (!resetComplete)
                {
                    Reset();
                }
                return;
            }

            if (Props.joyPawnSetting.startBedAniWhenJoying)
            {
                if (needMakeGrapgicList)
                {
                    ticks--;
                    remakeIconMatTick--;
                }
                else
                {
                    if (BBB.dummyForJoy?.user != null)
                    {
                        ticks--;
                        remakeIconMatTick--;
                    }

                }
            }
            else
            {
                ticks--;
                remakeIconMatTick--;
            }


            if (ticks <= 0)
            {
                //새로운 그래픽 리스트를 만들지 말지 체크
                CheckPawnAndCompPawnCondition();

                if ((!YR_RJW_Setting.YR_StopAnimation && !stopAnimation) || afterLoad)
                {
                    Stopwatch stopwatch = new Stopwatch(); //객체 선언
                    stopwatch.Start();// 시간측정 시작
                    //스타트, 미관측시 세팅
                    {
                        if (!afterLoad && (Find.CameraDriver.ZoomRootSize > YR_RJW_Setting.YR_zoom * 6 || parent.Map!=Find.CurrentMap || !Find.CameraDriver.CurrentViewRect.ExpandedBy(6).Overlaps(parent.OccupiedRect())))
                        {
                            unWatched = true;
                            return;
                        }

                        if (YR_RJW_Setting.YR_TestLog)
                        {
                            TestErrorLog($"@@@ Debug. Time : {stopwatch.ElapsedMilliseconds} @@@");
                        }
                        YR_RJW_Setting.SetTicksByCurTimeSpeed(ref ticks, Props.ticks);
                        //ticks *= Props.ticks;

                        if (afterLoad || unWatched)
                        {
                            Random rand = new Random();
                            ticks += rand.Next(0, ticks);
                            afterLoad = false;
                            unWatched = false;
                        }
                        resetComplete = false;
                    }

                    //그래픽경로 리스트랑 헤드 오프셋 조정
                    MakeCompPathSetAndNumsAndHeadOffSet();
                    if (YR_RJW_Setting.YR_TestLog)
                    {
                        TestErrorLog($"@@@ 1. Time : {stopwatch.ElapsedMilliseconds} @@@");
                    }

                    //그래픽 출력
                    MakeGraphicDataAndVec3s();
                    if (YR_RJW_Setting.YR_TestLog)
                    {
                        TestErrorLog($"@@@ 2. Time : {stopwatch.ElapsedMilliseconds} @@@");
                    }

                    stopwatch.Stop(); //시간측정 끝
                    if (YR_RJW_Setting.YR_TestLog)
                    {
                        TestErrorLog($"@@@ 3. Time : {stopwatch.ElapsedMilliseconds} @@@");
                        TestErrorLog($"parent.DrawPos.y : {parent.DrawPos.y}");
                    }
                    needMakeGrapgicList = false;

                    if ((bool)!Props.joyPawnSetting?.movingOffset?.NullOrEmpty())
                    {
                        NumAddAndReset(ref joyPawnOffsetNum, Props.joyPawnSetting.movingOffset);
                        joyPawnMovingOffset = Props.joyPawnSetting.movingOffset[joyPawnOffsetNum];
                    }

                }
            }
            if (remakeIconMatTick <= 0 && pawn != null)
            {
                remakeIconMatTick = 100;
                remakeIconMat = true;
            }
            //로컬 함수
            void CheckPawnAndCompPawnCondition()
            {
                //만약 comp용 폰 컨디션 없으면 제작
                List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
                List<Trait> traits = pawn.story.traits.allTraits;
                if (compPawnCondition == null)
                {
                    TestErrorLog($"Make new compPawnCondition");
                    compPawnCondition = new PawnCondition
                    {
                        race = pawn.def,
                        gender = pawn.gender,
                        hediffDefs = pawn.health.hediffSet.hediffs.Select(x => x.def).ToList(),
                        bodyTypeDef = pawn.story.bodyType,
                        traitDefs = pawn.story.traits.allTraits.Select(x => x.def).ToList()
                    };
                    TestErrorLog($"Let needMakeGrapgicList true");
                    needMakeGrapgicList = true;
                    return;
                }
                else
                {
                    originalPawnCondition = new PawnCondition
                    {
                        race = pawn.def,
                        gender = pawn.gender,
                        bodyTypeDef = pawn.story.bodyType
                    };
                }


                bool race = compPawnCondition.race == originalPawnCondition.race;

                bool gender = compPawnCondition.gender == originalPawnCondition.gender;

                bool hediffDef = true;
                //먼저 수량 체크
                if (hediffs.Count == compPawnCondition.hediffDefs.Count)
                {
                    for (int i = 0; i < hediffs.Count; i++)
                    {
                        if (hediffs[i].def != compPawnCondition.hediffDefs[i])
                        {
                            hediffDef = false;
                        }
                    }
                }
                else
                {
                    hediffDef = false;
                }

                //bool hediffDef = Enumerable.SequenceEqual(originalPawnCondition.hediffDefs, compPawnCondition.hediffDefs);

                bool bodyTypeDef = compPawnCondition.bodyTypeDef == originalPawnCondition.bodyTypeDef;

                bool traitDef = true;
                //먼저 수량 체크
                if (traits.Count == compPawnCondition.traitDefs.Count)
                {
                    for (int i = 0; i < traits.Count; i++)
                    {
                        if (traits[i].def != compPawnCondition.traitDefs[i])
                        {
                            traitDef = false;
                        }
                    }
                }
                else
                {
                    traitDef = false;
                }

                bool AllEquel = race && gender && hediffDef && bodyTypeDef && traitDef;

                //만약 comp용이랑 오리지널 차이시 신규 그래픽 리스트 제작용 bool 출력
                if (!AllEquel)
                {
                    TestErrorLog($"Let needMakeGrapgicList true");
                    compPawnCondition = new PawnCondition
                    {
                        race = pawn.def,
                        gender = pawn.gender,
                        hediffDefs = pawn.health.hediffSet.hediffs.Select(x => x.def).ToList(),
                        bodyTypeDef = pawn.story.bodyType,
                        traitDefs = pawn.story.traits.allTraits.Select(x => x.def).ToList()
                    };
                    needMakeGrapgicList = true;
                }
            }
            void MakeCompPathSetAndNumsAndHeadOffSet()
            {
                int listNum = 0;
                foreach (CompProperties_ChangeBedTexture.PathByRace pathByRace in Props.pathByRaces)
                {
                    listNum++;
                    //종족 부합시
                    if (YR_RJW_Setting.YR_TestLog)
                    {
                        TestErrorLog("++++ Strat pathByRace ++++");
                        TestErrorLog($"TryList : {listNum}");
                    }

                    if (ConditionMatch(pawn, pathByRace.condition))
                    {
                        //새롭게 그래픽 리스트를 뽑아야 하는 경우
                        MakseNewPathSetAndNums(pathByRace);
                        //내부 수치 올려서 다음 그래픽 표시 하게 하는거
                        CompPathSetAndNumsAdd();
                        //머리 위치 조정
                        SetHeadOffSet(pathByRace);
                    }
                }

                void MakseNewPathSetAndNums(CompProperties_ChangeBedTexture.PathByRace pathByRace)
                {
                    if (needMakeGrapgicList)
                    {
                        compPathSetAndNums = new List<PathSetAndNum>();
                        //조건 부합시
                        foreach (CompProperties_ChangeBedTexture.PathByRace.PathByCondition pathByCondition in pathByRace.pathByConditions)
                        {
                            if (ConditionMatch(pawn, pathByCondition.condition))
                            {
                                foreach (PathSet pathSet in pathByCondition.pathSets)
                                {
                                    if (ConditionMatch(pawn, pathSet.condition))
                                    {
                                        PathSetAndNum pathSetAndNum = new PathSetAndNum
                                        {
                                            pathSet = pathSet,
                                            selectedPaths = pathSet.PathsFor(pawn)
                                        };

                                        compPathSetAndNums.Add(pathSetAndNum);
                                    }
                                }
                            }
                        }
                    }
                }

                void CompPathSetAndNumsAdd()
                {
                    if (!compPathSetAndNums.NullOrEmpty())
                    {
                        if (BBB.PowerOn || BBB.PowerComp == null)
                        {
                            TestErrorLog($"PowerOn");
                            foreach (PathSetAndNum compPathSetAndNum in compPathSetAndNums)
                            {
                                var selected=compPathSetAndNum.pathSet.PathsFor(pawn);
                                if(!ReferenceEquals(selected,compPathSetAndNum.selectedPaths))
                                {
                                    compPathSetAndNum.selectedPaths=selected;
                                    compPathSetAndNum.internalNum=0;
                                    needMakeGrapgicList=true;
                                }
                                NumAddAndReset(ref compPathSetAndNum.internalNum, selected);

                                NumAddAndReset(ref compPathSetAndNum.individualAnimationOffSetNum, compPathSetAndNum.pathSet.individualAnimationOffSets);
                            }
                        }
                    }
                }

                void SetHeadOffSet(CompProperties_ChangeBedTexture.PathByRace pathByRace)
                {
                    //베이스 헤드 위치 세팅
                    if (!pathByRace.headOffSets.NullOrEmpty())
                    {
                        headOffSet = pathByRace.headOffSets[headOffSetNum];
                        movedHeadOffSet = pathByRace.headOffSets[headOffSetNum];
                        if (BBB.PowerOn || BBB.PowerComp == null)
                        {
                            NumAddAndReset(ref headOffSetNum, pathByRace.headOffSets);
                        }
                    }
                    else
                    {
                        headOffSet = Vector3.zero;
                        movedHeadOffSet = Vector3.zero;
                    }
                    //만약 따로 조건을 정해둘 경우, 추가 위치 조정
                    if (pathByRace.headOffsetConditions != null)
                    {
                        foreach (OffsetCondition headOffsetCondition in pathByRace.headOffsetConditions)
                        {
                            if (ConditionMatch(pawn, headOffsetCondition.condition))
                            {
                                if (headOffsetCondition.dontMove)
                                {
                                    headOffSet = Vector3.zero;
                                    movedHeadOffSet = Vector3.zero;
                                }
                                headOffSet.x += headOffsetCondition.offset.x;
                                headOffSet.z += headOffsetCondition.offset.y;

                                if (headOffsetCondition.Break)
                                {
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            void MakeGraphicDataAndVec3s()
            {
                if (!compPathSetAndNums.NullOrEmpty())
                {
                    TestErrorLog("=======");
                    if (needMakeGrapgicList || GDAV3s.NullOrEmpty())
                    {
                        GDAV3s = new List<GraphicDataAndVec3>();
                        foreach (PathSetAndNum compPathSetAndNum in compPathSetAndNums)
                        {
                            PathSet pathSet = compPathSetAndNum.pathSet;
                            GraphicDataAndVec3 GDAV3 = new GraphicDataAndVec3
                            {
                                fixedPosition = SetFixedPosition(compPathSetAndNum, pathSet, out Vector2 unprocessedOffSet)
                            };

                            bool specialPath = pathSet.pathIsPawnBody || pathSet.pathIsPortrait || pathSet.portraitSetting != null || pathSet.pathIsHeadGraphic || pathSet.pathIsWater;
                            bool nonSpecialPath = !pathSet.pathIsPawnBody && !pathSet.pathIsPortrait && pathSet.portraitSetting == null && !pathSet.pathIsHeadGraphic && !pathSet.pathIsWater;

                            var selectedPaths=pathSet.PathsFor(pawn);
                            // Facial expressions keep their original depth between face and fringe.
                            GDAV3.isFaceAnimation = selectedPaths != null && selectedPaths.Any(path =>
                                path != null && path.IndexOf("/Face/", System.StringComparison.OrdinalIgnoreCase) >= 0);
                            if (nonSpecialPath && !selectedPaths.NullOrEmpty() && compPathSetAndNum.internalNum >= 0)
                            {
                                GDAV3.rotation = Rot4ReverseByGizmo(pathSet);
                                if (GDAV3.rotation == null)
                                {
                                    GDAV3.rotation = Rot4.South;
                                }

                                Color color = pathSet.color;
                                if (pathSet.bodyColor)
                                {
                                    color = pawn.story.SkinColor;
                                }
                                ShaderTypeDef shaderType = Props.graphicData.shaderType;
                                if (pathSet.shaderType != null)
                                {
                                    shaderType = pathSet.shaderType;
                                }

                                for (int i = 0; i < selectedPaths.Count; i++)
                                {
                                    bool rotable = pathSet.pawnBodyReplacement || pathSet.rotable;
                                    string path = PathStringBuilder(selectedPaths[i], Rot4ReverseByGizmo(pathSet), rotable);
                                    TestErrorLog($" path : {path}");
                                    GraphicData graphicData = new GraphicData
                                    {
                                        color = color,
                                        graphicClass = Props.graphicData.graphicClass,
                                        drawSize = ConditionalDrawSize(pathSet),
                                        shaderType = shaderType,
                                        texPath = path
                                    };
                                    if (pathSet.pawnBodyReplacement)
                                    {
                                        GDAV3.isPawnBodyReplacement = pathSet.pawnBodyReplacement;
                                        graphicData.drawSize = pawn.def is ThingDef_AlienRace alienDef
                                            ? alienDef.alienRace.generalSettings.alienPartGenerator.customDrawSize * 1.5f
                                            : ConditionalDrawSize(pathSet) * 1.5f;
                                    }
                                    // Missing original layers must not construct a null material or a bad-texture square.
                                    if(ContentFinder<Texture2D>.Get(path,false)==null) {
                                        Log.WarningOnce("[Yuran] Missing facility layer: "+path,path.GetHashCode());
                                        GDAV3.graphics.Add(null); // Preserve the original frame number/timing.
                                        continue;
                                    }
                                    string maskPath = ContentFinder<Texture2D>.Get(path+"m",false)!=null ? path+"m" : null;
                                    Graphic graphic = GraphicDatabase.Get<Graphic_Single>(path, shaderType.Shader, graphicData.drawSize, color, color, graphicData, maskPath);

                                    GDAV3.graphics.Add(graphic);

                                    if (pathSet.forPortraitMesh)
                                    {
                                        portraitMesh = graphic.MeshAt(Rot4.South);
                                    }
                                }
                                TestErrorLog($" graphicDataSetted");
                            }

                            if (specialPath)
                            {
                                GDAV3.rotation = Rot4ReverseByGizmo(pathSet);
                                if (GDAV3.rotation == null)
                                {
                                    GDAV3.rotation = Rot4.South;
                                }

                                Graphic graphic = new Graphic
                                {
                                    data = new GraphicData()
                                };

                                if (pathSet.pathIsPawnBody)
                                {
                                    TestErrorLog($"Path Is PawnBody");
                                    GDAV3.isBodyGraphic = true;
                                }
                                else if (pathSet.pathIsPortrait || pathSet.portraitSetting != null)
                                {
                                    TestErrorLog($"Path Is Portrait");
                                    GDAV3.isPortrait = true;

                                    Vector3 cameraOffset = new Vector3();

                                    if (pathSet.offsetConditions != null)
                                    {
                                        foreach (OffsetCondition offsetCondition in pathSet.offsetConditions)
                                        {
                                            if (ConditionMatch(pawn, offsetCondition.condition))
                                            {
                                                cameraOffset.x = offsetCondition.offset.x;
                                                cameraOffset.y = 0;
                                                cameraOffset.z = offsetCondition.offset.y;

                                                if (offsetCondition.Break)
                                                {
                                                    break;
                                                }
                                            }
                                        }
                                    }

                                    //cameraOffset은 전체적인 위치 조정. 높이면 머리만~ 같은식, 카메라 줌이 높으면 그 캐릭터만 보이게

                                    iconMat = new Material(ShaderDatabase.DefaultShader)
                                    {
                                        mainTexture = CapturePortraitTexture(cameraOffset,2.5f),
                                        color = Color.white
                                    };

                                    if(pathSet.portraitSetting==null)GDAV3.fixedPosition = pathSet.offset;
                                    else
                                    {
                                        GDAV3.configuredPortrait=true;
                                        GDAV3.portraitShowApparel=pathSet.portraitSetting.showApparel;
                                        GDAV3.portraitShowHat=pathSet.portraitSetting.showHat;
                                        GDAV3.cameraZoom=3f;
                                        foreach(var portraitOffset in pathSet.portraitSetting.portraitOffsets)
                                            if(ConditionMatch(pawn,portraitOffset.condition))
                                            {
                                                cameraOffset=new Vector3(portraitOffset.offset.x,0f,portraitOffset.offset.y);
                                                GDAV3.cameraZoom=portraitOffset.cameraZoom;
                                                GDAV3.portraitAngle=portraitOffset.angle;
                                                if(portraitOffset.condition?.Break==true)break;
                                            }
                                        if(!pathSet.portraitSetting.maskPath.NullOrEmpty())
                                            GDAV3.portraitMask=ContentFinder<Texture2D>.Get(pathSet.portraitSetting.maskPath);
                                    }
                                    GDAV3.cameraOffset = cameraOffset;
                                }
                                else if (pathSet.pathIsHeadGraphic)
                                {
                                    TestErrorLog($"Path Is HeadGraphic");
                                    GDAV3.isHeadGraphic = true;
                                    if (pawn.Drawer?.renderer?.HeadGraphic == null)
                                    {
                                        pawn.Drawer.renderer.SetAllGraphicsDirty();
                                    }
                                    if (pawn.Drawer?.renderer?.HeadGraphic != null)
                                    {
                                        GDAV3.material = pawn.Drawer.renderer.HeadGraphic.MatAt((Rot4)GDAV3.rotation, null);
                                    }
                                }
                                else if (pathSet.pathIsWater)
                                {
                                    TestErrorLog($"Path Is Water");
                                    GDAV3.isWater = true;

                                    GraphicData graphicData = new GraphicData
                                    {
                                        color = pathSet.color,
                                        drawSize = ConditionalDrawSize(pathSet)
                                    };


                                }

                                if (!pathSet.pathIsPortrait && pathSet.portraitSetting == null && pawn.def != ThingDefOf.Human && pawn.def is ThingDef_AlienRace alienDef)
                                {
                                    TestErrorLog($"{pawn.def.defName} : is Alien");
                                    if (pathSet.pathIsPawnBody)
                                    {
                                        graphic.drawSize = alienDef.alienRace.generalSettings.alienPartGenerator.customDrawSize * 1.5f;
                                        GDAV3.useDrawSizeMultipler = false;
                                    }
                                    if (pathSet.pathIsHeadGraphic)
                                    {
                                        graphic.drawSize = pawn.Drawer.renderer.HeadGraphic.drawSize;
                                        GDAV3.useDrawSizeMultipler = false;
                                    }
                                }
                                //else if (!pathSet.pathIsPortrait && pathSet.portraitSetting == null && pawn.def != ThingDefOf.Human)
                                //{
                                //    graphic.drawSize =
                                //}
                                else
                                {
                                    graphic.drawSize = ConditionalDrawSize(pathSet);
                                }

                                TestErrorLog($" drawSize : {graphic.drawSize}");
                                GDAV3.graphics.Add(graphic);
                            }


                            GDAV3.gizmoString = pathSet.makeTestGizmoString;
                            GDAV3.testColor = pathSet.testColor;
                            GDAV3.originalPosition.x = unprocessedOffSet.x;
                            GDAV3.originalPosition.y = pathSet.layerOffset;
                            GDAV3.originalPosition.z = unprocessedOffSet.y;

                            GDAV3.originalDrawSize = ConditionalDrawSize(pathSet);
                            GDAV3.originalColor = pathSet.color;

                            GDAV3s.Add(GDAV3);

                            TestErrorLog("=======");
                        }
                    }
                    else
                    {
                        int i = 0;
                        foreach (GraphicDataAndVec3 graphicDataAndVec3 in GDAV3s)
                        {
                            PathSetAndNum compPathSetAndNum = compPathSetAndNums[i];
                            PathSet pathSet = compPathSetAndNum.pathSet;

                            Vector3 fixedPosition = SetFixedPosition(compPathSetAndNum, pathSet, out Vector2 unprocessedOffSet);
                            graphicDataAndVec3.fixedPosition = fixedPosition;

                            if (!pathSet.pathIsPawnBody && pathSet.PathsFor(pawn).Count != 1)
                            {
                                graphicDataAndVec3.internalNum = compPathSetAndNum.internalNum;
                            }

                            i++;
                            TestErrorLog("=======");
                        }
                    }

                    string PathStringBuilder(string pathString, Rot4? rotation = null, bool rotable = false)
                    {
                        StringBuilder pathStringBuilder = new StringBuilder(pathString);
                        if (rotation != null)
                        {
                            Rot4 tempRotaion = (Rot4)rotation;
                            pathStringBuilder.Append("_");
                            pathStringBuilder.Append(tempRotaion.ToStringWord().ToLower());
                        }
                        else if (rotable)
                        {
                            pathStringBuilder.Append("_");
                            pathStringBuilder.Append(parent.Rotation.ToStringWord().ToLower());
                        }
                        return pathStringBuilder.ToString();
                    }
                }

                //이하 로컬 함수
                Vector3 SetFixedPosition(PathSetAndNum compPathSetAndNum, PathSet pathSet, out Vector2 unprocessedOffSet)
                {
                    Vector3 fixedPosition = SetPosition(pathSet.layerOffset, pathSet.offset, pathSet.isHeadPart, out bool dontmove, out unprocessedOffSet, pathSet.offsetConditions, pathSet, !pathSet.pathIsPortrait);

                    if ((pathSet.pathIsPawnBody || pathSet.pawnBodyReplacement) && !dontmove)
                    {
                        if (pathSet.individualAnimationOffSets.NullOrEmpty())
                        {
                            followHeadOffset = true;
                            bodyOffSet = movedHeadOffSet;
                        }
                        else
                        {
                            bodyOffSet = pathSet.individualAnimationOffSets[compPathSetAndNum.individualAnimationOffSetNum];
                        }
                    }
                    if (pathSet.autoSetBodyPosition)
                    {
                        fixedPosition += bodyOffSet;
                    }
                    else if (!pathSet.individualAnimationOffSets.NullOrEmpty() && !dontmove && compPathSetAndNum.individualAnimationOffSetNum >= 0 && pathSet.individualAnimationOffSets[compPathSetAndNum.individualAnimationOffSetNum] != null)
                    {
                        fixedPosition += pathSet.individualAnimationOffSets[compPathSetAndNum.individualAnimationOffSetNum];
                    }

                    //stirng.Format 최적화 문제로 일케 해둠
                    if (YR_RJW_Setting.YR_TestLog)
                    {
                        TestErrorLog($"fixedPosition : {fixedPosition}");
                    }
                    return fixedPosition;
                }
                Vector3 SetPosition(float layerOffset, Vector2 offset, bool isHeadPart, out bool dontMove, out Vector2 unprocessedOffSet, List<OffsetCondition> offsetConditions = null, PathSet pathSet = null, bool useOffsetConditions = true)
                {
                    bool alignToBuilding=parent.def.defName=="YR_Building_TentacleMilking" || parent.def.defName=="YR_Building_LoveRoom" || parent.def.defName=="YR_Building_EggMachine";
                    Vector3 headOriginalPosition = alignToBuilding ? parent.DrawPos : GenThing.TrueCenter(parent.Position, parent.Rotation.Opposite, parent.def.size, layerOffset);

                    Vector2 offSet = offset;

                    dontMove = false;

                    if (offsetConditions != null && useOffsetConditions == true)
                    {
                        foreach (OffsetCondition offsetCondition in offsetConditions)
                        {
                            if (ConditionMatch(pawn, offsetCondition.condition))
                            {
                                if (offsetCondition.dontMove)
                                {
                                    dontMove = true;
                                }
                                else
                                {
                                    offSet = offsetCondition.offset;
                                }
                                if (offsetCondition.Break)
                                {
                                    break;
                                }
                            }
                        }
                    }
                    unprocessedOffSet = offSet;

                    FollowPreviousSettings? followPreviousSettings = pathSet?.followPreviousSettings;
                    if (followPreviousSettings == FollowPreviousSettings.InversionX)
                    {
                        offSet.x *= -1;
                    }
                    else if (followPreviousSettings == FollowPreviousSettings.InversionY)
                    {
                        offSet.y *= -1;
                    }
                    else if (followPreviousSettings == FollowPreviousSettings.InversionXY)
                    {
                        offSet.x *= -1;
                        offSet.y *= -1;
                    }
                    else if (followPreviousSettings == FollowPreviousSettings.FollowX)
                    {
                        offSet.x *= 1;
                        offSet.y *= 0;
                    }
                    else if (followPreviousSettings == FollowPreviousSettings.FollowY)
                    {
                        offSet.x *= 0;
                        offSet.y *= 1;
                    }
                    else if (followPreviousSettings == FollowPreviousSettings.FollowInversionX)
                    {
                        offSet.x *= -1;
                        offSet.y *= 0;
                    }
                    else if (followPreviousSettings == FollowPreviousSettings.FollowInversionY)
                    {
                        offSet.x *= 0;
                        offSet.y *= -1;
                    }

                    Vector3 position = headOriginalPosition + new Vector3(offSet.x, 0, offSet.y);
                    position.y = layerOffset;

                    //if (bodyAddon != null)
                    //{
                    //    var bodyAddonOffset = bodyAddon.offsets.GetOffset(Pawn.Rotation).offset;

                    //    position.x += bodyAddonOffset.x;
                    //    position.z += bodyAddonOffset.y;
                    //}

                    if (isHeadPart)
                    {
                        position += movedHeadOffSet;
                    }
                    //stirng.Format 최적화 문제로 일케 해둠
                    if (YR_RJW_Setting.YR_TestLog)
                    {
                        TestErrorLog($"This pathSet position : {position}");
                    }
                    return position;
                }


                Rot4? Rot4ReverseByGizmo(PathSet pathSet)
                {
                    Rot4? rot4 = pathSet.rotation;
                    if (rot4 != null && pathSet.rotationReverseByGizmo && reverseTexture)
                    {
                        if (rot4 == Rot4.South)
                        {
                            rot4 = Rot4.North;
                        }
                        else if (rot4 == Rot4.North)
                        {
                            rot4 = Rot4.South;
                        }
                        else if (rot4 == Rot4.East)
                        {
                            rot4 = Rot4.West;
                        }
                        else if (rot4 == Rot4.West)
                        {
                            rot4 = Rot4.East;
                        }
                    }

                    return rot4;
                }

            }
        }

        private void Reset()
        {
            TestErrorLog("Reset");
            ticks = 1;
            compPathSetAndNums = new List<PathSetAndNum>();
            GDAV3s = new List<GraphicDataAndVec3>();
            resetComplete = true;
            headOffSet = new Vector3();
            joyPawnMovingOffset = new Vector3();
            joyPawnOffsetNum = 0;
            headOffSetNum = 0;
            afterLoad = true;
            unWatched = false;
            compPawnCondition = null;
            originalPawnCondition = null;
            needMakeGrapgicList = true;
            pawnIsAgeException = false;
        }

        private void NumAddAndReset<T>(ref int Num, List<T> List)
        {
            if (List.Count == 1)
            {
                Num = 0;
            }
            else if (List.Count > 0)
            {
                if (Num < List.Count - 1)
                {
                    Num++;
                }
                else
                {
                    Num = 0;
                }
            }
        }

        private void TestErrorLog(string arr1)
        {
            if (YR_RJW_Setting.YR_TestLog)
            {
                Log.Error(arr1);
            }
        }
        private static void TestErrorLogStatic(string arr1)
        {
            if (YR_RJW_Setting.YR_TestLog)
            {
                Log.Error(arr1);
            }
        }
        private Vector2 ConditionalDrawSize(PathSet pathSet)
        {
            Vector2 size = pathSet.drawSize;
            if (pathSet.offsetConditions != null)
                foreach (var condition in pathSet.offsetConditions)
                    if (ConditionMatch(pawn, condition.condition))
                    {
                        if (condition.drawSize != Vector2.zero) size = condition.drawSize;
                        if (condition.Break) break;
                    }
            return size;
        }
        public static bool ConditionMatch(Pawn pawn, Condition condition)
        {
            if(condition?.ordinaryHumanlikeOnly==true&&!YR_Hentai_Prime_AnimationBed.UnsupportedRacesV8.OrdinaryHumanlike(pawn))return false;
            if (condition == null || pawn == null)
            {
                return true;
            }

            CheckNeverCondition(out bool neverBodyTypeMatch, pawn.story.bodyType, condition.neverBodyTypeDefs, condition);

            CheckNeverCondition(out bool neverGenderMatch, pawn.gender, condition.neverGenders, condition);

            CheckNeverCondition(out bool neverRaceDefMatch, pawn.def, condition.neverRaces, condition);


            bool neverHediffDefMatch = !condition.neverHediffDefs.NullOrEmpty() && HediffCheck(pawn, condition.neverHediffDefs);

            bool neverTraitDefMatch = !condition.neverTraitDefs.NullOrEmpty() && TraitCheck(pawn, condition.neverTraitDefs);

            bool never = neverBodyTypeMatch || neverGenderMatch || neverRaceDefMatch || neverHediffDefMatch || neverTraitDefMatch ||
                condition.neverIntelligences.Contains(pawn.RaceProps.intelligence) || SeverityMatch(condition.neverHediffAndSeverities);


            if (never)
            {
                TestErrorLogStatic($"neverBodyTypeMatch : {neverBodyTypeMatch}, neverGenderMatch : {neverGenderMatch}, neverRaceDefMatch : {neverRaceDefMatch}, neverHediffDefMatch : {neverHediffDefMatch}, neverTraitDefMatch : {neverTraitDefMatch}");
                return false;
            }

            CheckCondition(out bool bodyTypeMatch, pawn.story.bodyType, condition.bodyTypeDefs, condition);

            CheckCondition(out bool genderMatch, pawn.gender, condition.genders, condition);

            CheckCondition(out bool raceDefMatch, pawn.def, condition.races, condition);

            bool hediffDefMatch = !condition.allMatch ? HediffCheck(pawn, condition.hediffDefs) : (condition.hediffDefs.NullOrEmpty() && condition.allMatch) || HediffCheck(pawn, condition.hediffDefs);

            bool traitDefMatch = !condition.allMatch ? TraitCheck(pawn, condition.traitDefs) : (condition.traitDefs.NullOrEmpty() && condition.allMatch) || TraitCheck(pawn, condition.traitDefs);


            bool severityMatch=condition.hediffAndSeverities.NullOrEmpty()?condition.allMatch:SeverityMatch(condition.hediffAndSeverities);
            bool intelligenceMatch=condition.intelligences.NullOrEmpty()?condition.allMatch:condition.intelligences.Contains(pawn.RaceProps.intelligence);
            bool SeverityMatch(List<HediffAndSeverity> rules) => !rules.NullOrEmpty() && pawn.health.hediffSet.hediffs.Any(h=>
                rules.Any(rule=>h.def==rule.hediffDef&&rule.severityRange.min<=h.Severity&&h.Severity<=rule.severityRange.max));
            bool match = condition.allMatch
                ? bodyTypeMatch && hediffDefMatch && genderMatch && raceDefMatch && traitDefMatch && severityMatch && intelligenceMatch
                : bodyTypeMatch || hediffDefMatch || genderMatch || raceDefMatch || traitDefMatch || severityMatch || intelligenceMatch;
            return condition.reverseCondition ? !match : match;

            //로컬 함수
            static bool HediffCheck(Pawn pawn, List<HediffDef> hediffDefs)
            {
                foreach (Hediff pawnHediff in pawn.health.hediffSet.hediffs.ToList())
                {
                    foreach (HediffDef hediffDef in hediffDefs)
                    {
                        if (pawnHediff.def == hediffDef)
                        {
                            return true;
                        }
                    }
                }
                return false;
            }
            static bool TraitCheck(Pawn pawn, List<TraitDef> traitDefs)
            {
                foreach (Trait pawnTrait in pawn.story.traits.allTraits.ToList())
                {
                    foreach (TraitDef traitDef in traitDefs)
                    {
                        if (pawnTrait.def == traitDef)
                        {
                            return true;
                        }
                    }
                }
                return false;
            }
            static bool CheckCondition<T>(out bool match, T pawnCondition, List<T> conditionList, Condition condition)
            {
                //NullOrEmpty가 걸리면 은근 틱 먹음
                match = !condition.allMatch
                    ? conditionList.Contains(pawnCondition)
                    : (conditionList.NullOrEmpty() && condition.allMatch) || conditionList.Contains(pawnCondition);

                return match;
            }

            static bool CheckNeverCondition<T>(out bool match, T pawnCondition, List<T> conditionList, Condition condition)
            {
                match = !conditionList.NullOrEmpty() && conditionList.Contains(pawnCondition);

                return match;
            }
        }

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref stopAnimation, "stopAnimation", false, false);
            Scribe_Values.Look(ref reverseTexture, "reverseTexture", false, false);

            Scribe_Values.Look(ref showHat, "showHat", false, false);
            Scribe_Values.Look(ref showApparel, "showApparel", false, false);
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            string commandSetting;

            string modSetting;

            Texture icon;

            commandSetting = stopAnimation ? (string)"Disable".Translate() : (string)"Enable".Translate();

            modSetting = YR_RJW_Setting.YR_StopAnimation ? (string)"Disable".Translate() : (string)"Enable".Translate();

            icon = stopAnimation || YR_RJW_Setting.YR_StopAnimation
                ? ContentFinder<Texture2D>.Get("Ui/Timecontrols/TimeSpeedButton_Pause")
                : (Texture)ContentFinder<Texture2D>.Get("Ui/Timecontrols/TimeSpeedButton_Normal");


            yield return new Command_Action
            {
                defaultLabel = Props.animationCommandLabel.Translate(),
                defaultDesc = Props.animationCommandDesc.Translate(commandSetting, modSetting),
                icon = icon,
                action = delegate ()
                {
                    stopAnimation = !stopAnimation;
                }
            };

            if (Props.hasReverseBodyTextureCommand)
            {
                Texture reverseBodyTextureCommandIcon = ContentFinder<Texture2D>.Get("UI/Widgets/RotRight");
                yield return new Command_Action
                {
                    defaultLabel = Props.reverseBodyTextureCommandLabel.Translate(),
                    defaultDesc = Props.reverseBodyTextureCommandDesc.Translate(),
                    icon = reverseBodyTextureCommandIcon,
                    action = delegate ()
                    {
                        if (reverseTexture)
                        {
                            reverseTexture = false;
                            needMakeGrapgicList = true;
                            afterLoad = true;
                        }
                        else
                        {
                            reverseTexture = true;
                            needMakeGrapgicList = true;
                            afterLoad = true;
                        }
                    }
                };
            }

            yield return new Command_Action
            {
                defaultLabel = "YR_ResetLabel".Translate(),
                defaultDesc = "YR_ResetDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get("Things/Mote/ColonistFleeing"),
                action = delegate ()
                {
                    if (BBB.occupant != null)
                    {
                        Pawn tempPawn = BBB.occupant;
                        Reset();
                        HediffRemover.RemoveBondadgeHediff(tempPawn);
                        BBB.RemoveOccupant();
                        BBB.SetOccupant(tempPawn, true);
                        tempPawn.health.AddHediff(YR_RJW_DefOf.YR_MilkingMachineAddon_Hediff);
                        needMakeGrapgicList = true;
                    }
                }
            };

            yield return new Command_Action
            {
                defaultLabel = "DEBUG: Remove Pawn",
                icon = TexCommand.Install,
                action = delegate ()
                {
                    if (BBB.occupant != null)
                    {
                        HediffRemover.RemoveBondadgeHediff(BBB.occupant);
                        BBB.RemoveOccupant();
                    }
                }
            };

            if (Prefs.DevMode && GenCommandLine.CommandLineArgPassed("yrpFacilityTextureEditor"))
            {

                yield return new Command_Action
                {
                    defaultLabel = "Micro Control : " + movef.ToString(),
                    icon = ContentFinder<Texture2D>.Get("UI/Icons/GDAV3/Micro"),
                    action = delegate ()
                    {
                        movef = movef <= 0.002f ? 0.01f : movef <= 0.02f ? 0.1f : 0.001f;
                    }
                };

                foreach (GraphicDataAndVec3 GDAV3 in GDAV3s)
                {
                    if (!GDAV3.gizmoString.NullOrEmpty())
                    {
                        string gizmoString = GDAV3.gizmoString;


                        yield return new Command_Action
                        {
                            defaultLabel = gizmoString + " : Right",
                            icon = ContentFinder<Texture2D>.Get("UI/Icons/GDAV3/Right"),
                            action = delegate ()
                            {
                                GDAV3.testOffset.x += movef;
                                GDVA3OffsetMessage(GDAV3);
                            }
                        };
                        yield return new Command_Action
                        {
                            defaultLabel = gizmoString + " : Left",
                            icon = ContentFinder<Texture2D>.Get("UI/Icons/GDAV3/Left"),
                            action = delegate ()
                            {
                                GDAV3.testOffset.x -= movef;
                                GDVA3OffsetMessage(GDAV3);
                            }
                        };
                        yield return new Command_Action
                        {
                            defaultLabel = gizmoString + " : Up",
                            icon = ContentFinder<Texture2D>.Get("UI/Icons/GDAV3/Up"),
                            action = delegate ()
                            {
                                GDAV3.testOffset.z += movef;
                                GDVA3OffsetMessage(GDAV3);
                            }
                        };
                        yield return new Command_Action
                        {
                            defaultLabel = gizmoString + " : Down",
                            icon = ContentFinder<Texture2D>.Get("UI/Icons/GDAV3/Down"),
                            action = delegate ()
                            {
                                GDAV3.testOffset.z -= movef;
                                GDVA3OffsetMessage(GDAV3);
                            }
                        };
                        yield return new Command_Action
                        {
                            defaultLabel = gizmoString + " : Front",
                            icon = ContentFinder<Texture2D>.Get("UI/Icons/GDAV3/Front"),
                            action = delegate ()
                            {
                                GDAV3.testOffset.y += movef;
                                GDVA3OffsetMessage(GDAV3);
                            }
                        };
                        yield return new Command_Action
                        {
                            defaultLabel = gizmoString + " : Back",
                            icon = ContentFinder<Texture2D>.Get("UI/Icons/GDAV3/Back"),
                            action = delegate ()
                            {
                                GDAV3.testOffset.y -= movef;
                                GDVA3OffsetMessage(GDAV3);
                            }
                        };
                        yield return new Command_Action
                        {
                            defaultLabel = gizmoString + " : x.Small",
                            icon = ContentFinder<Texture2D>.Get("UI/Icons/GDAV3/draw_x_Small"),
                            action = delegate ()
                            {
                                GDAV3.testDrawSize.x -= movef;
                                GDVA3DrawSizeMessage(GDAV3);
                            }
                        };
                        yield return new Command_Action
                        {
                            defaultLabel = gizmoString + " : x.Big",
                            icon = ContentFinder<Texture2D>.Get("UI/Icons/GDAV3/draw_x_Big"),
                            action = delegate ()
                            {
                                GDAV3.testDrawSize.x += movef;
                                GDVA3DrawSizeMessage(GDAV3);
                            }
                        };
                        yield return new Command_Action
                        {
                            defaultLabel = gizmoString + " : y.Small",
                            icon = ContentFinder<Texture2D>.Get("UI/Icons/GDAV3/draw_y_Small"),
                            action = delegate ()
                            {
                                GDAV3.testDrawSize.y -= movef;
                                GDVA3DrawSizeMessage(GDAV3);
                            }
                        };
                        yield return new Command_Action
                        {
                            defaultLabel = gizmoString + " : y.Big",
                            icon = ContentFinder<Texture2D>.Get("UI/Icons/GDAV3/draw_y_Big"),
                            action = delegate ()
                            {
                                GDAV3.testDrawSize.y += movef;
                                GDVA3DrawSizeMessage(GDAV3);
                            }
                        };
                        yield return new Command_Action
                        {
                            defaultLabel = gizmoString + " : Reset",
                            icon = ContentFinder<Texture2D>.Get("UI/Icons/GDAV3/Reset"),
                            action = delegate ()
                            {
                                GDAV3.testOffset = new Vector3();

                                GDAV3.testDrawSize = new Vector2();

                                GDAV3.colorTest = new Color();

                                GDVA3OffsetMessage(GDAV3);
                            }
                        };

                        if (GDAV3.testColor)
                        {
                            yield return new Command_Action
                            {
                                defaultLabel = gizmoString + " : R+",
                                icon = ContentFinder<Texture2D>.Get("UI/Icons/GDAV3/RP"),
                                action = delegate ()
                                {
                                    GDAV3.colorTest.r += movef;
                                    GDVA3ColorMessage(GDAV3);
                                }
                            };
                            yield return new Command_Action
                            {
                                defaultLabel = gizmoString + " : R-",
                                icon = ContentFinder<Texture2D>.Get("UI/Icons/GDAV3/RM"),
                                action = delegate ()
                                {
                                    GDAV3.colorTest.r -= movef;
                                    GDVA3ColorMessage(GDAV3);
                                }
                            };
                            yield return new Command_Action
                            {
                                defaultLabel = gizmoString + " : G+",
                                icon = ContentFinder<Texture2D>.Get("UI/Icons/GDAV3/GP"),
                                action = delegate ()
                                {
                                    GDAV3.colorTest.g += movef;
                                    GDVA3ColorMessage(GDAV3);
                                }
                            }; yield return new Command_Action
                            {
                                defaultLabel = gizmoString + " : G-",
                                icon = ContentFinder<Texture2D>.Get("UI/Icons/GDAV3/GM"),
                                action = delegate ()
                                {
                                    GDAV3.colorTest.g -= movef;
                                    GDVA3ColorMessage(GDAV3);
                                }
                            };
                            yield return new Command_Action
                            {
                                defaultLabel = gizmoString + " : B+",
                                icon = ContentFinder<Texture2D>.Get("UI/Icons/GDAV3/BP"),
                                action = delegate ()
                                {
                                    GDAV3.colorTest.b += movef;
                                    GDVA3ColorMessage(GDAV3);
                                }
                            }; yield return new Command_Action
                            {
                                defaultLabel = gizmoString + " : B-",
                                icon = ContentFinder<Texture2D>.Get("UI/Icons/GDAV3/BM"),
                                action = delegate ()
                                {
                                    GDAV3.colorTest.b -= movef;
                                    GDVA3ColorMessage(GDAV3);
                                }
                            };
                            yield return new Command_Action
                            {
                                defaultLabel = gizmoString + " : A+",
                                icon = ContentFinder<Texture2D>.Get("UI/Icons/GDAV3/AP"),
                                action = delegate ()
                                {
                                    GDAV3.colorTest.a += movef;
                                    GDVA3ColorMessage(GDAV3);
                                }
                            }; yield return new Command_Action
                            {
                                defaultLabel = gizmoString + " : A-",
                                icon = ContentFinder<Texture2D>.Get("UI/Icons/GDAV3/AM"),
                                action = delegate ()
                                {
                                    GDAV3.colorTest.a -= movef;
                                    GDVA3ColorMessage(GDAV3);
                                }
                            };
                        }
                    }
                }
            }

            yield break;

            static void GDVA3OffsetMessage(GraphicDataAndVec3 GDAV3)
            {

                if (GDAV3.isBodyGraphic || GDAV3.isPawnBodyReplacement)
                {
                    Messages.Message($"x : {GDAV3.originalPosition.x + GDAV3.testOffset.x}, y : {GDAV3.originalPosition.y + GDAV3.testOffset.y}, z : {GDAV3.originalPosition.z + GDAV3.testOffset.z}, (pawnBody : just add this vector to headOffsetConditions)", MessageTypeDefOf.SilentInput, false);
                }
                else
                {
                    Messages.Message($"x : {GDAV3.originalPosition.x + GDAV3.testOffset.x}, y : {GDAV3.originalPosition.y + GDAV3.testOffset.y}, z : {GDAV3.originalPosition.z + GDAV3.testOffset.z}", MessageTypeDefOf.SilentInput, false);

                }
            }
            static void GDVA3DrawSizeMessage(GraphicDataAndVec3 GDAV3)
            {
                Messages.Message($"x : {GDAV3.originalDrawSize.x + GDAV3.testDrawSize.x}, y : {GDAV3.originalDrawSize.y + GDAV3.testDrawSize.y}", MessageTypeDefOf.SilentInput, false);
            }
            static void GDVA3ColorMessage(GraphicDataAndVec3 GDAV3)
            {
                Messages.Message($"r : {GDAV3.originalColor.r + GDAV3.colorTest.r}, g : {GDAV3.originalColor.g + GDAV3.colorTest.g}, b : {GDAV3.originalColor.b + GDAV3.colorTest.b}, a : {GDAV3.originalColor.a + GDAV3.colorTest.a}", MessageTypeDefOf.SilentInput, false);
            }
        }

        private float movef = 0.001f;
    }
}


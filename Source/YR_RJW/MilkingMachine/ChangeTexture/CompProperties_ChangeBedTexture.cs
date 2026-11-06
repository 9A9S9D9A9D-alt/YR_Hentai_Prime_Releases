using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Graphic = Verse.Graphic;

namespace YR_RJW
{
    public class CompProperties_ChangeBedTexture : CompProperties_Building_BaseBondageBed
    {
        public GraphicData graphicData = null;
        public GraphicData boundBedGraphicData;
        public bool drawOnlySouth = false;
        public List<PathByRace> pathByRaces = new List<PathByRace>();
        public string animationCommandLabel = "YR_AnimationCommandLabel";
        public string animationCommandDesc = "YR_AnimationCommandDesc";
        public string reverseBodyTextureCommandLabel = "YR_ReverseBodyTextureCommandLabel";
        public string reverseBodyTextureCommandDesc = "YR_ReverseBodyTextureCommandDesc";
        public bool hideHead = false;
        public Rot4 faceRotation = Rot4.South;
        public JoyPawnSetting joyPawnSetting = new JoyPawnSetting();
        public bool hasReverseBodyTextureCommand = false;
        public bool showBodyAddon = true;

        public CompProperties_ChangeBedTexture()
        {
            compClass = typeof(CompChangeBedTexture);
        }
        public class JoyPawnSetting
        {
            public List<OffsetCondition> offsetConditions = new List<OffsetCondition>();
            public List<Vector3> movingOffset = new List<Vector3>();
            public bool startBedAniWhenJoying = false;
        }
        public class PathByRace
        {
            public Condition condition;
            public List<PathByCondition> pathByConditions = new List<PathByCondition>();
            public List<Vector3> headOffSets = new List<Vector3>();
            public List<OffsetCondition> headOffsetConditions = new List<OffsetCondition>();
            public class PathByCondition
            {
                public Condition condition;
                public List<PathSet> pathSets = new List<PathSet>();
            }
        }
    }

    public class PathSet : ICloneable
    {
        public List<string> paths = new List<string>();
        public FollowPreviousSettings followPreviousSettings = FollowPreviousSettings.False;

        public Condition condition;
        public Vector2 offset = new Vector2(0, 0);
        public List<OffsetCondition> offsetConditions = new List<OffsetCondition>();
        public float layerOffset;
        public bool rotable = false;
        public bool isHeadPart = false;
        public List<Vector3> individualAnimationOffSets = new List<Vector3>();
        public bool bodyColor = false;
        public bool pawnBodyReplacement = false;
        public bool pathIsPawnBody = false;
        public bool pathIsPortrait = false;
        public LegacyPortraitSetting portraitSetting;
        public List<ConditionAndPath> conditionAndPaths = new List<ConditionAndPath>();
        public bool pathIsHeadGraphic = false;
        public bool pathIsWater = false;
        public bool forPortraitMesh = false;
        public Color color = Color.white;
        public Vector2 drawSize = Vector2.one;
        public ShaderTypeDef shaderType;
        public Rot4? rotation = null;
        public bool rotationReverseByGizmo = false;
        public bool autoSetBodyPosition = false;

        public string makeTestGizmoString = null;
        public bool testColor = false;

        public List<string> PathsFor(Pawn pawn)
        {
            var result=paths;
            foreach(var alternate in conditionAndPaths)
                if(CompChangeBedTexture.ConditionMatch(pawn,alternate.condition))
                {
                    result=alternate.paths;
                    if(alternate.condition?.Break==true)break;
                }
            return result;
        }
        public object Clone() => MemberwiseClone();
    }
    public class ConditionAndPath
    {
        public Condition condition;
        public List<string> paths=new List<string>();
    }
    public class LegacyPortraitSetting
    {
        public bool showApparel=true,showHat=true;
        public string maskPath="";
        public List<LegacyPortraitOffset> portraitOffsets=new List<LegacyPortraitOffset>();
    }
    public class LegacyPortraitOffset
    {
        public Condition condition;
        public float cameraZoom=3f;
        public Vector2 offset;
        public int angle;
    }
    public class HediffAndSeverity
    {
        public HediffDef hediffDef;
        public FloatRange severityRange;
    }
    public enum FollowPreviousSettings
    {
        True,
        False,
        InversionX,
        InversionY,
        InversionXY,
        FollowX,
        FollowY,
        FollowInversionX,
        FollowInversionY
    }
    public class OffsetCondition : ICloneable
    {
        public Vector2 drawSize;
        public Condition condition;
        public Vector2 offset = new Vector2(0, 0);
        public Vector3 vector3 = new Vector3(0, 0, 0);
        public bool Break = false;
        public bool dontMove = false;

        public object Clone()
        {
            OffsetCondition copy = new OffsetCondition
            {
                drawSize = drawSize,
                condition = condition,
                offset = offset,
                Break = Break,
                dontMove = dontMove,
            };
            return copy;
        }

    }
    public class Condition
    {
        public bool ordinaryHumanlikeOnly;
        public List<BodyTypeDef> bodyTypeDefs = new List<BodyTypeDef>();
        public List<HediffDef> hediffDefs = new List<HediffDef>();
        public List<HediffAndSeverity> hediffAndSeverities = new List<HediffAndSeverity>();
        public List<HediffAndSeverity> neverHediffAndSeverities = new List<HediffAndSeverity>();
        public List<Intelligence> intelligences = new List<Intelligence>();
        public List<Intelligence> neverIntelligences = new List<Intelligence>();
        public bool Break=true;
        public List<Gender> genders = new List<Gender>();
        public List<ThingDef> races = new List<ThingDef>();
        public List<TraitDef> traitDefs = new List<TraitDef>();
        public bool allMatch = false;
        public bool reverseCondition = false;

        public List<BodyTypeDef> neverBodyTypeDefs = new List<BodyTypeDef>();
        public List<HediffDef> neverHediffDefs = new List<HediffDef>();
        public List<Gender> neverGenders = new List<Gender>();
        public List<ThingDef> neverRaces = new List<ThingDef>();
        public List<TraitDef> neverTraitDefs = new List<TraitDef>();
    }
    public class PawnCondition
    {
        public BodyTypeDef bodyTypeDef = null;
        public List<HediffDef> hediffDefs = new List<HediffDef>();
        public Gender? gender = null;
        public ThingDef race = null;
        public List<TraitDef> traitDefs;
    }
    public class GraphicDataAndVec3
    {
        public List<Graphic> graphics = new List<Graphic>();
        public Vector3 fixedPosition;
        public Vector3 cameraOffset;
        public int internalNum = 0;
        public Rot4? rotation = null;
        public Material material;
        public bool isPawnBodyReplacement = false;
        public bool isBodyGraphic = false;
        public bool isPortrait = false;
        public bool configuredPortrait;
        public float cameraZoom=2.5f,portraitAngle;
        public bool portraitShowApparel=true,portraitShowHat=true;
        public Texture2D portraitMask;
        public bool isHeadGraphic = false;
        public bool isFaceAnimation;

        public string gizmoString = null;

        public Vector3 originalPosition;
        public Vector2 originalDrawSize;

        public Vector3 testOffset;
        public Vector2 testDrawSize;
        public bool testColor;
        public bool useDrawSizeMultipler = true;
        public bool isWater;
        public Color originalColor;
        public Color colorTest;
    }
    public class PathSetAndNum
    {
        public PathSet pathSet;
        public List<string> selectedPaths;
        public int internalNum = 0;
        public int individualAnimationOffSetNum = 0;
    }
}


using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace YR_Hentai_Prime_AnimationBed
{
    public static class BubblePortraitV57
    {
        [ThreadStatic] private static Pawn capturePawn;
        [ThreadStatic] private static Graphic captureBody;
        [ThreadStatic] private static bool captureOverlay;
        [ThreadStatic] private static Pawn rearBodyPawn;
        public static bool HideHairFor(Pawn pawn) => pawn!=null && pawn==rearBodyPawn;
        private static readonly AccessTools.FieldRef<PawnRenderTree,PawnDrawParms> Prepared =
            AccessTools.FieldRefAccess<PawnRenderTree,PawnDrawParms>("oldParms");
        public static bool IsBubble(PortraitSetting setting) => setting != null && !setting.headOnly &&
            setting.maskPath?.IndexOf("BackGround/",StringComparison.OrdinalIgnoreCase)>=0;
        public static IEnumerable<KeyValuePair<int,PortraitSetting>> Slots(CompProperties_AnimationSetting props)
        {
            for(int n=0;n<props.pawnPortraitSettings.Count;n++){
                var p=props.pawnPortraitSettings[n];
                var s=IsBubble(p.portraitSetting)?p.portraitSetting:
                    p.conditonPortraitSettings.Select(c=>c.portraitSetting).FirstOrDefault(IsBubble);
                if(s!=null)yield return new KeyValuePair<int,PortraitSetting>(n,s);
            }
        }
        public static int SlotFor(CompProperties_AnimationSetting props,BedAnimationDef effect)
        {
            string path=effect.bedAnimationSettings.FirstOrDefault()?.graphicData?.texPath;
            if(path==null)return -1;
            foreach(var entry in Slots(props)){
                string mask=entry.Value.maskPath;
                string folder=mask.Substring(0,mask.LastIndexOf('/')+1);
                if(path.StartsWith(folder,StringComparison.Ordinal))return entry.Key;
            }
            return -1;
        }
        public static Graphic Body(Pawn pawn,bool overlay) => pawn==capturePawn && captureOverlay==overlay?captureBody:null;
        public static bool Capturing(Pawn pawn) => pawn!=null && pawn==capturePawn;
        public static void Prepare(PortraitIngredient ingredient,CompAnimationPosition adjustment)
        {
            if(!IsBubble(ingredient.portraitSetting) || !adjustment.BubbleBodyAllowed)return;
            var source=DefDatabase<BedAnimationDef>.GetNamedSilentFail(adjustment.BubbleChoice(ingredient.pawn,ingredient.slot)??"");
            if(source==null || !FacilityBodyCatalogV47.Choices().Contains(source))return;
            ingredient.bubbleSource=source;
            foreach(var frame in source.bedAnimationSettings){
                if(!Condition.Match(ingredient.pawn,adjustment.parent as Building_AnimationBed,frame.condition,out _))continue;
                var data=frame.graphicData;
                foreach(var choice in frame.conditionGraphicDatas)
                    if(Condition.Match(ingredient.pawn,adjustment.parent as Building_AnimationBed,choice.condition,out bool stop)){
                        data=choice.graphicData;if(stop)break;
                    }
                if(data==null || ContentFinder<Texture2D>.Get(data.texPath,false)==null)continue;
                var copy=frame.Copy();
                var bodyNode=source.setPawnColor?FacilityRenderNodes.Find(ingredient.pawn,PawnRenderNodeTagDefOf.Body):null;
                var color=source.setPawnColor?(bodyNode?.ColorFor(ingredient.pawn)??Color.white):data.color;
                copy.graphic=GraphicDatabase.Get<Graphic_Single>(data.texPath,data.shaderType?.Shader??ShaderDatabase.Cutout,
                    data.drawSize,color,data.colorTwo,data,data.maskPath);
                ingredient.bubbleFrames.Add(copy);
            }
        }
        public static Texture Capture(PortraitIngredient ingredient,Vector3 cameraOffset,float zoom,Rot4 rotation)
        {
            bool rearBody=IsBubble(ingredient.portraitSetting) &&
                ingredient.portraitSetting.pawnRenderNodeTagDef==PawnRenderNodeTagDefOf.Body && rotation==Rot4.North;
            bool customBody=ingredient.bubbleFrames.Count!=0;
            if(!customBody&&!rearBody)return null;
            var pawn=ingredient.pawn;
            var tree=pawn.Drawer.renderer.renderTree;
            BedAnimationSetting frame=null;
            if(customBody){
                int duration=ingredient.bubbleSource.autoDurationTicksSetting?(pawn.Drawer.renderer.CurAnimation?.durationTicks??60):ingredient.bubbleSource.durationTicks;
                duration=Math.Max(duration,ingredient.bubbleFrames.Max(f=>f.tick)+1);
                int tick=tree.AnimationTick%Math.Max(1,duration);
                frame=ingredient.bubbleFrames.Where(f=>f.tick<=tick).OrderByDescending(f=>f.tick).FirstOrDefault()??ingredient.bubbleFrames[0];
            }
            int nativeTick=Find.TickManager.TicksGame;
            if(ingredient.ownedTexture!=null && (customBody||ingredient.lastBubbleNativeTick==nativeTick) && ingredient.lastBubbleGraphic==frame?.graphic &&
                ingredient.lastBubbleOffset==cameraOffset && ingredient.lastBubbleZoom==zoom && ingredient.lastBubbleRotation==rotation)return ingredient.ownedTexture;
            if(ingredient.ownedTexture==null){
                ingredient.ownedTexture=new RenderTexture(256,256,24,RenderTextureFormat.ARGB32);
                ingredient.ownedTexture.Create();
            }
            var prepared=Prepared(tree);
            var oldPawn=capturePawn;var oldBody=captureBody;bool oldOverlay=captureOverlay;var oldRear=rearBodyPawn;
            try{
                capturePawn=pawn;captureBody=frame?.graphic;captureOverlay=ingredient.bubbleSource?.bodyOverlay??false;
                rearBodyPawn=rearBody?pawn:null;
                // Separate camera target: the existing portrait material applies the bubble mask.
                Find.PawnCacheRenderer.RenderPawn(pawn,ingredient.ownedTexture,cameraOffset,zoom,0f,rotation,
                    renderHeadgear:ingredient.portraitSetting.renderHeadgear,renderClothes:ingredient.portraitSetting.renderClothes,portrait:true);
                ingredient.lastBubbleNativeTick=nativeTick;ingredient.lastBubbleGraphic=frame?.graphic;ingredient.lastBubbleOffset=cameraOffset;
                ingredient.lastBubbleZoom=zoom;ingredient.lastBubbleRotation=rotation;
            }finally{
                capturePawn=oldPawn;captureBody=oldBody;captureOverlay=oldOverlay;rearBodyPawn=oldRear;
                if(prepared.pawn!=null)tree.ParallelPreDraw(prepared);
            }
            return ingredient.ownedTexture;
        }
    }
}

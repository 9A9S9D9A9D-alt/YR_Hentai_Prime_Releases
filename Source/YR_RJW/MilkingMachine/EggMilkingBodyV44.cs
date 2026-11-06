using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using YR_Hentai_Prime_AnimationBed;

namespace YR_RJW
{
    public class CompProperties_EggMilkingBodyV44 : CompProperties_Building_BaseBondageBed
    {
        public CompProperties_EggMilkingBodyV44(){compClass=typeof(CompEggMilkingBodyV44);}
    }
    // Shares only the milking machine's body selection/timing, never its building/face/effect layers.
    public class CompEggMilkingBodyV44 : Comp_Building_BaseBondageBed, IBodyOverlaySourceV46, IBodyChoiceRefreshV47
    {
        private struct Frame { public int tick; public Graphic graphic; }
        private List<Frame> frames=new List<Frame>();
        private BedAnimationDef selected;
        private Pawn owner;
        private BodyTypeDef body;
        private int phase,duration=60;
        public volatile Graphic CurrentFrame;
        public Graphic BodyOverlay => selected?.bodyOverlay==true?CurrentFrame:null;
        public bool ReplacesBody => selected?.bodyOverlay!=true;
        private static bool Matches(Pawn p,YR_Hentai_Prime_AnimationBed.Condition c)
            => c?.dummyForJoyIsActive!=true && YR_Hentai_Prime_AnimationBed.Condition.Match(p,null,c,out _);
        public void RefreshBodyChoice(){CompTick();}
        public override void PostExposeData(){base.PostExposeData();Scribe_Values.Look(ref phase,"eggMilkingBodyPhaseV44",0);}
        public override void CompTick()
        {
            var p=pawn;
            if(!UnsupportedRacesV8.OrdinaryHumanlike(p)||!AdultEligibility.IsAllowed(p)){
                CurrentFrame=null;owner=null;selected=null;return;
            }
            var props=DefDatabase<ThingDef>.GetNamedSilentFail("YR_Building_MilkingMachine")?.GetCompProperties<CompProperties_AnimationSetting>();
            var source=props?.bedAnimationList.FirstOrDefault();
            if(source==null){CurrentFrame=null;return;}
            var chosen=source.bedAnimationDef;
            foreach(var rule in source.conditionBedAnimationDefs)
                if(rule.bedAnimationDef!=null && Matches(p,rule.condition))chosen=rule.bedAnimationDef;
            chosen=FacilityBodyCatalogV47.Resolve(parent,p,chosen);
            if(chosen==null||!chosen.isPawnTextureReplace||chosen.pawnRenderNodeTagDef!=PawnRenderNodeTagDefOf.Body){
                CurrentFrame=null;owner=null;selected=null;return;
            }
            if(owner!=p||selected!=chosen||body!=p.story?.bodyType){
                owner=p;selected=chosen;body=p.story?.bodyType;
                BuildFrames(p,chosen,props);phase=0;
            }
            if(frames.Count==0){CurrentFrame=null;return;}
            int tick=phase%Math.Max(1,duration);Frame current=frames[0];
            foreach(var frame in frames)if(frame.tick<=tick && frame.tick>=current.tick)current=frame;
            CurrentFrame=current.graphic;
            if(parent.TryGetComp<CompChangeBedTexture>()?.AnimationWork==true)phase=(phase+1)%Math.Max(1,duration);
        }
        private void BuildFrames(Pawn p,BedAnimationDef animation,CompProperties_AnimationSetting source)
        {
            frames=new List<Frame>();
            var bodyNode=FacilityRenderNodes.Find(p,PawnRenderNodeTagDefOf.Body);
            if(bodyNode==null)return;
            foreach(var setting in animation.bedAnimationSettings){
                if(!Matches(p,setting.condition))continue;
                var data=setting.graphicData;
                foreach(var alternative in setting.conditionGraphicDatas)
                    if(Matches(p,alternative.condition))data=alternative.graphicData;
                if(data==null||data.texPath.NullOrEmpty())continue;
                // Body definitions provide explicit source frames (south or non-directional).
                // Validate on the main thread; retain the preceding frame if an old asset is absent.
                if(ContentFinder<Texture2D>.Get(data.texPath,false)==null)continue;
                Color color=animation.setPawnColor?bodyNode.ColorFor(p):data.color;
                Graphic graphic=GraphicDatabase.Get<Graphic_Single>(data.texPath,data.shaderType?.Shader??ShaderDatabase.CutoutComplex,
                    data.drawSize,color,data.colorTwo,data,data.maskPath);
                frames.Add(new Frame {tick=setting.tick,graphic=graphic});
            }
            frames=frames.OrderBy(f=>f.tick).ToList();
            var pawnAnimation=source.pawnAnimationSetting.pawnAnimationDef;
            foreach(var rule in source.pawnAnimationSetting.conditonPawnAnimations)
                if(Matches(p,rule.condition))pawnAnimation=rule.pawnAnimationDef;
            duration=animation.autoDurationTicksSetting?(pawnAnimation?.durationTicks??60):animation.durationTicks;
            if(frames.Count!=0)duration=Math.Max(duration,frames.Max(f=>f.tick)+1);
        }
    }
    [HarmonyPatch(typeof(CompChangeBedTexture),nameof(CompChangeBedTexture.BoundGraphic),MethodType.Getter)]
    internal static class EggMilkingBodyMaterialV44
    {
        private static bool Prefix(CompChangeBedTexture __instance,ref Graphic __result)
        {
            var shared=__instance.parent.TryGetComp<CompEggMilkingBodyV44>();
            if(shared==null)return true;
            __result=shared.ReplacesBody?shared.CurrentFrame:null;return false;
        }
    }
}

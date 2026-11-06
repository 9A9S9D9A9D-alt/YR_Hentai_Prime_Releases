using System;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace YR_Hentai_Prime_AnimationBed
{
    public interface IBodyChoiceRefreshV47 { void RefreshBodyChoice(); }
    public static class FacilityBodyCatalogV47
    {
        private static readonly Dictionary<BedAnimationDef,BedAnimationDef> Manual=new Dictionary<BedAnimationDef,BedAnimationDef>();
        public static IEnumerable<BedAnimationDef> Choices() => DefDatabase<BedAnimationDef>.AllDefs
            .Where(d=>d.isPawnTextureReplace && d.pawnRenderNodeTagDef==PawnRenderNodeTagDefOf.Body && d.defName.IndexOf("Milking_Body",StringComparison.Ordinal)>=0)
            .OrderBy(d=>Label(d.defName));
        public static BedAnimationDef Resolve(ThingWithComps device,Pawn pawn,BedAnimationDef automatic)
        {
            var comp=device?.TryGetComp<CompAnimationPosition>();
            if(comp?.SupportsBodyChoice!=true)return automatic;
            string name=comp.BodyChoice(pawn);
            if(string.IsNullOrEmpty(name))return automatic;
            if(name=="native")return DefDatabase<BedAnimationDef>.GetNamedSilentFail("YR_Dummy_BedAnimation")??automatic;
            var source=DefDatabase<BedAnimationDef>.GetNamedSilentFail(name);
            if(source==null||!source.isPawnTextureReplace||source.pawnRenderNodeTagDef!=PawnRenderNodeTagDefOf.Body)return automatic;
            // Use the exact authored frame and force a fixed facing only for this manual display.
            if(!Manual.TryGetValue(source,out var result)){
                result=new BedAnimationDef {
                    defName=source.defName+"_ManualV47",bedAnimationSettings=source.bedAnimationSettings,
                    durationTicks=source.durationTicks,autoDurationTicksSetting=source.autoDurationTicksSetting,
                    isPawnTextureReplace=true,fixedFacingBody=true,bodyOverlay=source.bodyOverlay,
                    setPawnColor=source.setPawnColor,pawnRenderNodeTagDef=source.pawnRenderNodeTagDef,
                    animationSynchro=source.animationSynchro,offset=source.offset
                };
                Manual[source]=result;
            }
            return result;
        }
        public static string Label(string name)
        {
            if(string.IsNullOrEmpty(name))return "자동";
            if(name=="native")return "기본 몸체만 사용";
            string text=name.Replace("YR_Milking_Body_V36_","").Replace("YR_Milking_Body_V37_","").Replace("YR_Milking_Body_","");
            if(text.StartsWith("BEPRace_"))text=text.Replace("BEPRace_","").Replace("YR_Milking_Body_","").Replace("Resur_","소생 요정 ").Replace("Small","요정 소형").Replace("Big","요정 대형");
            text=text.Replace("Thin_Female","마른 여성").Replace("Thin_Male","마른 남성")
                .Replace("Miho","미호").Replace("Moyo","모요").Replace("Human","인간형")
                .Replace("Anty","앤티").Replace("AOC","AOC").Replace("Dragonian","드래고니안")
                .Replace("Maru","마루").Replace("Yuran","유란").Replace("Female","여성").Replace("Male","남성").Replace("Thin","마른 체형")
                .Replace("PowerA","강도 1").Replace("PowerB","강도 2").Replace("_"," ");
            return text;
        }
    }
}

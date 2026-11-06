using System;
using System.Collections.Generic;
using System.Linq;
using AlienRace;
using RimWorld;
using UnityEngine;
using Verse;
using YR_Hentai_Prime_AnimationBed;

namespace YR_RJW
{
    [StaticConstructorOnStartup]
    public static class OtherHarMihoMotionV40
    {
        private static readonly HashSet<string> KeepPackages=new HashSet<string>(StringComparer.OrdinalIgnoreCase){
            "rooandgloomy.yuranracemod","sutsutman.minchothemintchocoslimeharver","sutsutman.marilyntheminchoworshipperwitchharver",
            "rooandgloomy.dragonianracemod","zhiyue.doctordragonian","roo.antyracemod","vamv.maruracemod","vamv.furmaru","nemonian.my",
            "baskerville.hellsingarmsultimate"};
        public static bool Eligible(ThingDef def)
        {
            if(!(def is ThingDef_AlienRace)||def.race==null||!def.race.Humanlike||!def.race.IsFlesh||
                def.race.IsMechanoid||def.race.IsAnomalyEntity||def.race.IsDrone||def.entityCodexEntry!=null||!UnsupportedRacesV8.Allows(def))return false;
            string name=def.defName??"";
            // HAR also converts Core's Human definition to ThingDef_AlienRace.
            // A HAR runtime type alone does not mean a mod-added alien race.
            string package=def.modContentPack?.PackageIdPlayerFacing;
            if(name=="Human"||string.IsNullOrEmpty(package)||package.StartsWith("ludeon.rimworld",StringComparison.OrdinalIgnoreCase))return false;
            if(KeepPackages.Contains(def.modContentPack?.PackageIdPlayerFacing??""))return false;
            if(new[]{"mimic","monster","fleshbeast","shambler","ghoul","undead","entity","creepjoiner"}.Any(part=>name.IndexOf(part,StringComparison.OrdinalIgnoreCase)>=0))return false;
            return !name.StartsWith("Yuran_Race",StringComparison.OrdinalIgnoreCase)&&!name.StartsWith("Mincho",StringComparison.OrdinalIgnoreCase)&&
                name.IndexOf("Dragonian",StringComparison.OrdinalIgnoreCase)<0&&name!="Anty"&&name!="Maru"&&name!="Furmaru"&&name!="Alien_Moyo";
        }
        static OtherHarMihoMotionV40()
        {
            var races=DefDatabase<ThingDef>.AllDefs.Where(Eligible).ToList();
            if(races.Count==0)return;
            var machine=DefDatabase<ThingDef>.GetNamedSilentFail("YR_Building_MilkingMachine")?.GetCompProperties<CompProperties_AnimationSetting>();
            var body=machine?.bedAnimationList.FirstOrDefault();
            if(body!=null)foreach(var old in body.conditionBedAnimationDefs){
                if(old.condition==null)old.condition=new YR_Hentai_Prime_AnimationBed.Condition { allMatch=true };
                if(old.condition.neverRaces==null)old.condition.neverRaces=new List<ThingDef>();
                old.condition.neverRaces.AddRange(races.Where(d=>!old.condition.neverRaces.Contains(d)));
            }
            if(body!=null)foreach(var gender in new[]{Gender.Female,Gender.Male}){
                var animation=DefDatabase<BedAnimationDef>.GetNamedSilentFail("YR_Milking_Body_V36_Miho_"+gender);
                if(animation==null)continue;
                body.conditionBedAnimationDefs.Add(new ConditionBedAnimation {
                    bedAnimationDef=animation,
                    condition=new YR_Hentai_Prime_AnimationBed.Condition { races=races,genders=new List<Gender>{gender},allMatch=true,ordinaryHumanlikeOnly=true }
                });
            }
            Log.Message("[YRP V40 HAR MIHO] Milking-only humanlike HAR races: "+string.Join(",",races.Select(d=>d.defName)));
        }
    }
}

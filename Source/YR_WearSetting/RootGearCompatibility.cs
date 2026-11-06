using System.Collections.Generic;
using System.Linq;
using AlienRace;
using Verse;

namespace YR_WearSetting
{
    internal static class RootGearCompatibility
    {
        internal static void Prepare(List<ThingDef> races,List<WearSettingDef> settings)
        {
            var template=DefDatabase<WearSettingDef>.GetNamedSilentFail("Alien_Tentacle_Setting");
            var body=DefDatabase<ThingDef>.GetNamedSilentFail("YR_AP_Tentacle");
            if(template==null || body==null)return;
            string[] apparels={"YR_AP_Tentacle","YR_AP_Tentacle_Rooted","YR_AP_Tentacle_Headgear","YR_AP_Tentacle_Headgear_Mask"};
            string[] weapons={"YR_Tentacle_Sword","YR_Tentacle_BullWhip","OY_Tentacle_Dagger","OY_Tentacle_Bow"};
            foreach(var race in races.Where(r=>r.race?.Humanlike==true))
            {
                string package=race.modContentPack?.PackageIdPlayerFacing?.ToLowerInvariant()??"";
                string raceName=race.defName;
                if(raceName=="AT_TriJaw"||raceName=="AT_Hiveguard"||raceName=="AT_Lacerator"||raceName=="AT_Oppressor"||raceName=="AT_dreadnought"||raceName=="Mengxi_Race"||raceName.StartsWith("BS_")||raceName.StartsWith("DC_")||raceName.StartsWith("Rjw_Slime_")||package=="rs.sl"||package=="pumpkin.mengximagic"||package=="jk.rmt.soulchronicle"||package=="jk.rmt.dungeoncore"||package=="redmattis.bigsmall.core"||package=="redmattis.betterprerequisites")continue;
                if(race is ThingDef_AlienRace alien)
                {
                    var restriction=alien.alienRace.raceRestriction;
                    foreach(string name in apparels)
                    {
                        var item=DefDatabase<ThingDef>.GetNamedSilentFail(name);
                        if(item!=null && !restriction.whiteApparelList.Contains(item))restriction.whiteApparelList.Add(item);
                    }
                    foreach(string name in weapons)
                    {
                        var item=DefDatabase<ThingDef>.GetNamedSilentFail(name);
                        if(item!=null && !restriction.whiteWeaponList.Contains(item))restriction.whiteWeaponList.Add(item);
                    }
                }
                bool hasRule=settings.Any(d=>(d.targetRaces==null || d.targetRaces.Contains(race)) &&
                    d.wearSetting.Any(w=>w.wearList.Contains(body) && w.makeWeapon!=null));
                if(!hasRule)
                    race.comps.Add(new CompProperties_WearSetting{targetRace=race,wearSetting=template.wearSetting});
            }
        }
    }
}

using RimWorld;
using System.Collections.Generic;
using Verse;

namespace YR_RJW
{
    public class CompProperties_Usable_BondageBed : CompProperties_Usable
    {
        public List<ThingDef> races = new List<ThingDef>();
        public List<HediffDef> forbiddenHediffDefs = new List<HediffDef>();
        public List<HediffDef> canWorkHediffDefs = new List<HediffDef>();
        public bool neverRelease = true;
        public Condition condition;
        public JobDef selfUseJob;

        public SettingException settingException = new SettingException();
    }

    public class SettingException
    {
        public bool femaleOnly = false;
        public bool maleOnly = false;
        public List<ThingDef> genderExceptionRaces = new List<ThingDef>();
        public List<ThingDef> ageExceptionRaces = new List<ThingDef>();
    }
}

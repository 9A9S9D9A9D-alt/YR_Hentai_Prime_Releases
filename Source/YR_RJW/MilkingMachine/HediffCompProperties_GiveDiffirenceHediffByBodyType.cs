using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace YR_RJW
{
    public class HediffCompProperties_GiveDiffirenceHediff : HediffCompProperties
    {
        public HediffCompProperties_GiveDiffirenceHediff()
        {
            compClass = typeof(HediffComp_GiveDiffirenceHediff);
        }
        public List<ConditionAndHediff> conditionAndHediffs = new List<ConditionAndHediff>();
    }

    public class ConditionAndHediff
    {
        public List<ThingDef> bedDefs = new List<ThingDef>();
        public List<BodyTypeDef> bodyTypes = new List<BodyTypeDef>();
        public List<ThingDef> races = new List<ThingDef>();
        public List<HediffDef> hediffDefs = new List<HediffDef>();
        public bool Break = false;
    }

    public class HediffComp_GiveDiffirenceHediff : HediffComp
    {
        private Hediff hediff;

        private HediffCompProperties_GiveDiffirenceHediff Props => (HediffCompProperties_GiveDiffirenceHediff)props;

        public override void CompPostTick(ref float severityAdjustment)
        {
            if (parent.pawn.GetPosture() != PawnPosture.LayingInBed)
            {
                HediffRemover.RemoveBondadgeHediff(Pawn);

                HediffDef hediffBed = parent.def;
                using List<Hediff>.Enumerator enumerator = Pawn.health.hediffSet.hediffs.Where(x => x.def == hediffBed).ToList().GetEnumerator();
                while (enumerator.MoveNext())
                {
                    Hediff current = enumerator.Current;
                    Pawn.health.RemoveHediff(current);
                }

                return;
            }
        }
        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            foreach (ConditionAndHediff conditionAndHediff in Props.conditionAndHediffs)
            {
                foreach (ThingDef bedDef in conditionAndHediff.bedDefs)
                {
                    if (Pawn.CurrentBed() != null && Pawn.CurrentBed().def == bedDef)
                    {
                        foreach (HediffDef hediffDef in conditionAndHediff.hediffDefs)
                        {
                            hediff = Pawn.health.AddHediff(hediffDef);
                        }
                        if (conditionAndHediff.Break)
                        {
                            return;
                        }
                    }
                }
            }

            foreach (ConditionAndHediff conditionAndHediff in Props.conditionAndHediffs)
            {
                foreach (ThingDef race in conditionAndHediff.races)
                {
                    if (Pawn.def == race)
                    {
                        foreach (HediffDef hediffDef in conditionAndHediff.hediffDefs)
                        {
                            hediff = Pawn.health.AddHediff(hediffDef);
                        }
                        if (conditionAndHediff.Break)
                        {
                            return;
                        }
                    }
                }
            }
            foreach (ConditionAndHediff conditionAndHediff in Props.conditionAndHediffs)
            {
                foreach (BodyTypeDef bodyType in conditionAndHediff.bodyTypes)
                {
                    if (Pawn.story.bodyType == bodyType)
                    {
                        foreach (HediffDef hediffDef in conditionAndHediff.hediffDefs)
                        {
                            hediff = Pawn.health.AddHediff(hediffDef);
                        }
                        if (conditionAndHediff.Break)
                        {
                            return;
                        }
                    }
                }
            }
        }
    }
}

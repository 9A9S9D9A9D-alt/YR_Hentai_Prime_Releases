using System.Collections.Generic;
using Verse;

namespace YR_RJW
{

    public class HediffCompProperties_GiveHediffs : HediffCompProperties
    {
        public HediffCompProperties_GiveHediffs()
        {
            compClass = typeof(HediffComp_GiveHediffs);
        }

        public int ticks = 100;
        public float severity = 0.005f;
        public List<HediffDef> hediffDefs = new List<HediffDef>();
        public bool reset = false;
    }


    public class HediffComp_GiveHediffs : HediffComp
    {
        private int ticks = 0;

        private HediffCompProperties_GiveHediffs Props => (HediffCompProperties_GiveHediffs)props;
        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            ticks = Props.ticks;
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            ticks--;

            if (ticks <= 0)
            {
                ticks = Props.ticks;

                foreach (HediffDef hediffDef in Props.hediffDefs)
                {
                    if (Pawn.health.hediffSet.HasHediff(hediffDef))
                    {
                        parent.Severity = parent.def.initialSeverity;
                        return;
                    }
                }

                parent.Severity += Props.severity;

                if (parent.Severity >= parent.def.maxSeverity)
                {
                    foreach (HediffDef hediffDef in Props.hediffDefs)
                    {
                        Pawn.health.AddHediff(hediffDef);
                    }
                    if (Props.reset)
                    {
                        parent.Severity = parent.def.initialSeverity;
                    }
                    else
                    {
                        HealthUtility.Cure(parent);
                    }
                    return;
                }
            }
        }
        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref ticks, "ticks");
        }
    }
}

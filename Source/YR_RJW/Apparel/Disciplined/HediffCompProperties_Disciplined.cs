using RimWorld;
using System.Collections.Generic;
using Verse;

namespace YR_RJW
{
    public class HediffCompProperties_Disciplined : HediffCompProperties
    {
        public HediffCompProperties_Disciplined()
        {
            compClass = typeof(HediffComp_Disciplined);
        }
        public int ticks = 600;
        public List<TraitDef> traitDefs;
        public float severity = 0.01f;
        public float addTraitSeverity = 1f;
        public bool removeHediffPostAddTrait;
        public List<ThingDef> apparelDefs = new List<ThingDef>();
    }

    public class HediffComp_Disciplined : HediffComp
    {
        private int ticks = 600;

        private HediffCompProperties_Disciplined Props => (HediffCompProperties_Disciplined)props;

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref ticks, "ticks");

        }
        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            ticks--;
            //테스트용

            if (ticks <= 0)
            {
                ticks = Props.ticks;

                if (!Props.apparelDefs.NullOrEmpty())
                {
                    if (ConvenientTool.CheckMatchAny(Props.apparelDefs, Pawn.apparel.WornApparel))
                    {
                        parent.Severity += Props.severity;
                    }
                    else
                    {
                        parent.Severity -= Props.severity;
                    }
                }

                if (parent.Severity >= Props.addTraitSeverity)
                {
                    foreach (TraitDef traitDef in Props.traitDefs)
                    {
                        if (!Pawn.story.traits.HasTrait(traitDef))
                        {
                            Pawn.story.traits.GainTrait(new Trait(traitDef));
                        }
                    }
                    if (Props.removeHediffPostAddTrait)
                    {
                        HealthUtility.Cure(parent);
                    }
                }
            }
        }

    }
}

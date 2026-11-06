using RimWorld;
using Verse;

namespace YR_RJW
{
    public class CompProperties_Disciplined_Apparel : CompProperties
    {
        public CompProperties_Disciplined_Apparel()
        {
            compClass = typeof(Comp_Disciplined_Apparel);
        }

        public HediffDef hediffDef;
        public float severity = 0.01f;
        public TraitDef stopAddHediffTraitDef;
    }

    public class Comp_Disciplined_Apparel : ThingComp
    {

        private CompProperties_Disciplined_Apparel Props => (CompProperties_Disciplined_Apparel)props;

        public override void Notify_Equipped(Pawn pawn)
        {
            base.Notify_Equipped(pawn);
            if (Props.stopAddHediffTraitDef != null)
            {
                foreach (Trait trait in pawn.story?.traits?.allTraits)
                {
                    if (trait.def == Props.stopAddHediffTraitDef)
                    {
                        return;
                    }
                }
            }

            Hediff hediff = HediffMaker.MakeHediff(Props.hediffDef, pawn);
            hediff.Severity = Props.severity;
            pawn.health.AddHediff(hediff);
        }
    }
}

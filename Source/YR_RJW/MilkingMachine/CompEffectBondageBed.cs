using RimWorld;
using Verse;

namespace YR_RJW
{
    public class CompEffectBondageBed : CompUseEffect
    {
        public override void DoEffect(Pawn usedBy)
        {
            base.DoEffect(usedBy);
            usedBy.health.AddHediff(YR_RJW_DefOf.YR_MilkingMachineAddon_Hediff);
        }

        public override void CompTickRare()
        {
            base.CompTickRare();
            if (parent is Pawn pawn)
            {
                HediffRemover.RemoveBondadgeHediff(pawn);
            }
        }
    }
}

using RimWorld;
using Verse;

namespace YR_RJW
{
    public class CompRemoveEffectBondageBed : CompUseEffect
    {
        public override void DoEffect(Pawn usedBy)
        {
            base.DoEffect(usedBy);
            Building_BaseBondageBed building_BondageBed = (Building_BaseBondageBed)parent;

            HediffRemover.RemoveBondadgeHediff(usedBy);

            building_BondageBed.RemoveOccupant();
        }
    }
}

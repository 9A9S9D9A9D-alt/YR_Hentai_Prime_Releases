using RimWorld;
using Verse;

namespace YR_HPF
{
    public class ConstraintPawnStatGreater : ConstraintPawnStat
    {
        public override bool CheckActiveCondition(ThingComp comp, Pawn pawn, ThingWithComps equipment)
        {
            if (pawn == null || !this.equal)
            {
                return pawn.GetStatValue(this.stat, true) > this.value;
            }
            return pawn.GetStatValue(this.stat, true) >= this.value;
        }
    }
}

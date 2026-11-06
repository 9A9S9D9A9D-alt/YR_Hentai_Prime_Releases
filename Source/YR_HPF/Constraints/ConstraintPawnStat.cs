using RimWorld;

namespace YR_HPF
{
    public abstract class ConstraintPawnStat : Constraint
    {
        public StatDef stat;

        public float value;

        public bool equal;
    }
}

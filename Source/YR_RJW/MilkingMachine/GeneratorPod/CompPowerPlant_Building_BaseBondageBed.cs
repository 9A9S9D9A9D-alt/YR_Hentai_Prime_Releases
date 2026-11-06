using RimWorld;
using Verse;

namespace YR_RJW
{
    [StaticConstructorOnStartup]
    public class CompPowerPlant_Building_BaseBondageBed : CompPowerPlant
    {
        protected override float DesiredPowerOutput => RoofedPowerOutputFactor;
        private float RoofedPowerOutputFactor
        {
            get
            {
                if (parent is Building_BaseBondageBed BBB)
                {
                    if (BBB.occupant != null)
                    {
                        return -base.Props.PowerConsumption;
                    }
                }

                return 0;
            }
        }
    }
}

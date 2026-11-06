using Verse;

namespace YR_Booster
{
    public class HediffCompProperties_Booster : HediffCompProperties
    {
        public HediffCompProperties_Booster()
        {
            compClass = typeof(HediffComp_Booster);
        }
    }


    public class HediffComp_Booster : HediffComp
    {
        private HediffCompProperties_Booster Props
        {
            get
            {
                return (HediffCompProperties_Booster)props;
            }
        }

        public override void Notify_KilledPawn(Pawn victim, DamageInfo? dinfo)
        {
            base.Notify_KilledPawn(victim, dinfo);

            foreach (var apparel in parent.pawn.apparel.WornApparel)
            {
                var comp = apparel.TryGetComp<CompBooster>();
                if (comp != null)
                {
                    if (comp.Props.hediffDefForCheckKillEnemy != null)
                    {
                        if (comp.Props.hediffDefForCheckKillEnemy == parent.def)
                        {
                            comp.cooldownTicks = 0;
                        }
                    }
                }
            }
        }
    }
}

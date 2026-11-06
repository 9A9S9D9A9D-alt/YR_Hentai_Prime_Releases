using RimWorld;
using Verse;

namespace YR_RJW
{
    public class CompProperties_QuickNeed : CompProperties
    {
        public CompProperties_QuickNeed()
        {
            compClass = typeof(CompQuickNeed);
        }
        public NeedDef need;

        public float offset = -0.01f;

        public int ticks = 100;
    }

    public class CompQuickNeed : ThingComp
    {
        public int ticks;

        private CompProperties_QuickNeed Props => (CompProperties_QuickNeed)props;

        public override void PostPostMake()
        {
            base.PostPostMake();
            ticks = Props.ticks;
        }

        public void Reset()
        {
            ticks = Props.ticks;
        }

        public override void CompTick()
        {
            base.CompTick();
            if (Props.need?.defName == "Sex" && !LegacyEligibility.Allows(parent as Pawn)) return;

            ticks--;

            if (ticks <= 0)
            {
                if (Props.need != null)
                {
                    Pawn pawn = (Pawn)parent;

                    if (pawn.needs != null)
                    {
                        Need need = pawn.needs.TryGetNeed(Props.need);
                        if (need == null)
                        {
                            Reset();
                            return;
                        }
                        float num = Props.offset;
                        need.CurLevel += num;
                    }
                }
                Reset();
            }
        }
    }
}

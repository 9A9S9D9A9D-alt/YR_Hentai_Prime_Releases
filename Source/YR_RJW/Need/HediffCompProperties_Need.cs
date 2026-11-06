using RimWorld;
using Verse;

namespace YR_RJW
{

    public class HediffCompProperties_Need : HediffCompProperties
    {
        public HediffCompProperties_Need()
        {
            compClass = typeof(HediffComp_Need);
        }

        public float offset;

        public int ticks = 100;

        public NeedDef need;

        public RaceOnly raceOnly;
    }


    public class HediffComp_Need : HediffComp
    {
        private HediffCompProperties_Need Props => (HediffCompProperties_Need)props;
        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            if (Pawn.IsHashIntervalTick(Props.ticks))
            {
                if (Props.need != null && Pawn.needs != null)
                {
                    Need need = Pawn.needs.TryGetNeed(Props.need);
                    if (need != null)
                    {
                        float num = Props.offset;

                        if (Props.raceOnly != null && !Props.raceOnly.race.NullOrEmpty())
                        {
                            if (Props.raceOnly.race.Contains(Pawn.def))
                            {
                                num = Props.raceOnly.offset;
                            }
                        }
                        need.CurLevel += num;
                    }
                }
            }
        }
    }
}

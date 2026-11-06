using System.Linq;
using Verse;

namespace YR_RJW
{
    public class HediffRemover
    {
        public static void RemoveBondadgeHediff(Pawn pawn)
        {
            HediffDef hediffBed = YR_RJW_DefOf.YR_MilkingMachineAddon_Hediff;
            using System.Collections.Generic.List<Hediff>.Enumerator enumerator = pawn.health.hediffSet.hediffs.Where(x => x.def == hediffBed).ToList().GetEnumerator();
            while (enumerator.MoveNext())
            {
                Hediff current = enumerator.Current;
                pawn.health.RemoveHediff(current);
            }
        }
    }
}

using System.Linq;
using RimWorld;
using Verse;

namespace YR_RJW;

public class HediffGiver_RandomAgeCurved_NoAlram : HediffGiver
{
	public SimpleCurve ageFractionMtbDaysCurve;

	public int minPlayerPopulation;

	public override void OnIntervalPassed(Pawn pawn, Hediff cause)
	{
		float x = (float)pawn.ageTracker.AgeBiologicalYears / pawn.RaceProps.lifeExpectancy;
		if (Rand.MTBEventOccurs(ageFractionMtbDaysCurve.Evaluate(x), 60000f, 60f) && (minPlayerPopulation <= 0 || pawn.Faction != Faction.OfPlayer || PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists_NoSuspended.Count() >= minPlayerPopulation))
		{
			TryApply(pawn);
		}
	}
}

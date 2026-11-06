using Verse;
namespace YR_RJW {
 public class CompUsableRecovered22Bed : CompUsableAdvancedBondageBed {
  protected override bool TargetConditionAllows(Pawn target)=>LegacyEligibility.Allows(target) && target.RaceProps.Humanlike &&
   (parent.def.defName!="YR_Building_TentacleMilking" || target.gender==Gender.Female) && CompChangeBedTexture.ConditionMatch(target,Props.condition);
 }
}

using System.Linq;
using AlienRace.ExtendedGraphics;
using RimWorld;
using Verse;
namespace YR_RJW
{
    // HAR 1.7 supplies the former wrapper implementation; retain the original overrides.
    public class ExtendedGraphicsPawnWrapper_Ex : ExtendedGraphicsPawnWrapper
    {
        public ExtendedGraphicsPawnWrapper_Ex(Pawn pawn) : base(pawn) { }
        public ExtendedGraphicsPawnWrapper_Ex() { }
        public override Hediff HasHediffOfDefAndPart(HediffDef hediffDef, BodyPartDef part, string partLabel)
            => GetHediffList().FirstOrDefault(h => h.def != YR_RJW_DefOf.YR_HideHair_Hediff && h.def == hediffDef &&
                (h.Part == null || IsBodyPart(h.Part, part, partLabel)));
        public override System.Collections.Generic.IEnumerable<float> SeverityOfHediffsOnPart(HediffDef hediffDef, BodyPartDef part, string partLabel)
            => GetHediffList().Where(h => h.def != YR_RJW_DefOf.YR_HideHair_Hediff && h.def == hediffDef &&
                (h.Part == null || IsBodyPart(h.Part, part, partLabel))).Select(h => h.Severity);
        public override bool VisibleInBed(bool defaultValue = true)
            => WrappedPawn.CurrentBed() is Building_BaseBondageBed bed
                ? bed.TryGetComp<CompChangeBedTexture>()?.Props.showBodyAddon ?? defaultValue
                : base.VisibleInBed(defaultValue);
    }
}

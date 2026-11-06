using System.Collections.Generic;
using Verse;
namespace YR_Hentai_Prime_AnimationBed
{
    // Module boundary: legacy furniture lives in YR_RJW, which already references this assembly.
    public interface IFacilityAssignmentTarget
    {
        bool AvailableForAssignment { get; }
        IEnumerable<FloatMenuOption> AssignmentOptions(Pawn target, Pawn carrier = null);
    }
}
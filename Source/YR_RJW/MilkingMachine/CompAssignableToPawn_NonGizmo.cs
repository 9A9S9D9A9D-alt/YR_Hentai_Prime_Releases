using RimWorld;

namespace YR_RJW
{
    public class CompAssignableToPawn_NonGizmo : CompAssignableToPawn
    {
        protected override bool ShouldShowAssignmentGizmo()
        {
            return false;
        }
    }
}

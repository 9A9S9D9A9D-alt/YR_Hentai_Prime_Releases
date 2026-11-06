using RimWorld;
using Verse;

namespace YR_RJW
{
    public class CompEffectSpawnTentacleGirl : CompUseEffect
    {
        public override void DoEffect(Pawn usedBy)
        {
            base.DoEffect(usedBy);

            Pawn pawn = SamokDaughterRules.Create(usedBy.Faction);

            GenSpawn.Spawn(pawn, usedBy.Position, usedBy.Map);
        }
    }
}

using Verse;
namespace YR_RJW
{
    public class PlaceWorker_SamokCreep : PlaceWorker
    {
        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
        {
            if (map == null || !loc.InBounds(map)) return false;
            foreach (var offset in GenAdj.AdjacentCells)
            {
                var neighbor = loc + offset;
                if (neighbor.InBounds(map) && neighbor.GetTerrain(map) == checkingDef)
                    return true;
            }
            return "주변 8칸 중 한 칸에 완성된 사목 뿌리 지반이 필요합니다.";
        }
    }
    public class PlaceWorker_OnSamokCreep : PlaceWorker
    {
        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
        {
            var creep = SamokCreepMap.Creep;
            if (map == null || creep == null) return false;
            foreach (var cell in GenAdj.OccupiedRect(loc, rot, checkingDef.Size))
                if (!cell.InBounds(map) || cell.GetTerrain(map) != creep)
                    return "봉사낭의 건설 부지 전체가 사목 뿌리 지반이어야 합니다.";
            return true;
        }
    }
}

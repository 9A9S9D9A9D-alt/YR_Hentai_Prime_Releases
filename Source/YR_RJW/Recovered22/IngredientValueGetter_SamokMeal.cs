using System.Linq;
using RimWorld;
using Verse;
namespace YR_RJW
{
    // The original recipes mix a count-based seed with nutrition-based vegetables.
    public class IngredientValueGetter_SamokMeal : IngredientValueGetter_Nutrition
    {
        public override float ValuePerUnitOf(ThingDef def) => def.defName == "YR_Tentacle_Egg" ? 1f : base.ValuePerUnitOf(def);
        public override string BillRequirementsDescription(RecipeDef recipe, IngredientCount ingredient)
        {
            var seed=DefDatabase<ThingDef>.GetNamedSilentFail("YR_Tentacle_Egg");
            return seed!=null && ingredient.filter.AllowedThingDefs.Count()==1 && ingredient.filter.Allows(seed)
                ? ingredient.GetBaseCount()+" × "+seed.LabelCap : base.BillRequirementsDescription(recipe,ingredient);
        }
    }
}
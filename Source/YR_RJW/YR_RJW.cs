using Verse;

namespace YR_RJW
{
    [StaticConstructorOnStartup]
    public static class YR_RJW
    {
        static YR_RJW()
        {
            foreach (ThingDef thingDef in DefDatabase<ThingDef>.AllDefsListForReading.FindAll(delegate (ThingDef x)
            {
                bool result;
                if (x.recipes != null)
                {
                    result = x.recipes.Any((RecipeDef y) => y.defName == "CookMealLavish");

                    if (result)
                    {
                        x.recipes.Add(YR_RJW_DefOf.YR_Tentacle_Meal_Cook);
                        x.recipes.Add(YR_RJW_DefOf.YR_Tentacle_Meal_CookBulk);
                    }
                }
                else
                {
                    result = false;
                }
                return result;
            }))
            {
            }
        }

    }
}

using RimWorld;
using Verse;

namespace YR_RJW
{
    [DefOf]
    public static class YR_RJW_DefOf
    {
        static YR_RJW_DefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(YR_RJW_DefOf));
        }

        public static HediffDef YR_MilkingMachineAddon_Hediff;

        public static HediffDef YR_Pregnant_Tentacle_Birth;

        public static HediffDef YR_HideHat_Hediff;

        public static HediffDef YR_HideApparel_Hediff;

        public static HediffDef YR_HideHair_Hediff;

        public static JobDef YR_Job_ReleaseBed;

        public static TraitDef YR_Disciplined;

        public static ThingDef YR_AP_Tentacle;

        public static RecipeDef YR_Tentacle_Meal_Cook;

        public static RecipeDef YR_Tentacle_Meal_CookBulk;

        public static HeadTypeDef YR_Dummy_Stump;

        public static PawnKindDef YR_Tentacle_Girl;





        public static PawnRelationDef YR_BreedingSource;

        public static PawnRelationDef YR_Byproduct;
    }
}

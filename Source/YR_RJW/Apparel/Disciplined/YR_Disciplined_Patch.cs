using RimWorld;
using Verse;

namespace YR_RJW
{
    [StaticConstructorOnStartup]
    public static class YR_Disciplined_Patch
    {
        static YR_Disciplined_Patch()
        {
            foreach (ThoughtDef thoughtDef in DefDatabase<ThoughtDef>.AllDefsListForReading)
            {
                if (thoughtDef.nullifyingTraits != null)
                {
                    if (thoughtDef.nullifyingTraits.Contains(TraitDefOf.Nudist))
                    {
                        thoughtDef.nullifyingTraits.Add(YR_RJW_DefOf.YR_Disciplined);
                    }
                }
            }
        }
    }
}

using Verse;

namespace YR_RJW
{
    [StaticConstructorOnStartup]
    public static class FollowPreviousConditionSettings
    {
        static FollowPreviousConditionSettings()
        {
            foreach (ThingDef thingDef in DefDatabase<ThingDef>.AllDefs)
            {
                CompProperties_ChangeBedTexture compProperties_ChangeBedTexture = thingDef.GetCompProperties<CompProperties_ChangeBedTexture>();

                if (compProperties_ChangeBedTexture != null)
                {
                    foreach (CompProperties_ChangeBedTexture.PathByRace pathByRace in compProperties_ChangeBedTexture.pathByRaces)
                    {
                        foreach (CompProperties_ChangeBedTexture.PathByRace.PathByCondition pathByConditions in pathByRace.pathByConditions)
                        {

                            for (int i = 0; i < pathByConditions.pathSets.Count; i++)
                            {
                                if (pathByConditions.pathSets[i].followPreviousSettings != FollowPreviousSettings.False)
                                {

                                    PathSet pathSet = (PathSet)pathByConditions.pathSets[i].Clone();

                                    PathSet previousPathSet = (PathSet)pathByConditions.pathSets[i - 1].Clone();

                                    for (int j = 0; j < previousPathSet.offsetConditions.Count; j++)
                                    {
                                        object offsetConditionClone = previousPathSet.offsetConditions[j].Clone();
                                        previousPathSet.offsetConditions[j] = (OffsetCondition)offsetConditionClone;
                                    }

                                    pathByConditions.pathSets[i] = previousPathSet;
                                    pathByConditions.pathSets[i].paths = pathSet.paths;
                                    pathByConditions.pathSets[i].layerOffset = pathSet.layerOffset;
                                    pathByConditions.pathSets[i].followPreviousSettings = pathSet.followPreviousSettings;
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
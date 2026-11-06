using AlienRace;
using AlienRace.ExtendedGraphics;
using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
namespace YR_RJW
{
    public class SubBodyTypesDefs : Def { public List<RaceAndBodyType> raceAndBodyTypes = new List<RaceAndBodyType>(); }
    public class RaceAndBodyType
    {
        public List<ThingDef> raceDefs = new List<ThingDef>();
        public List<BodyTypeDef> bodyTypeDefs = new List<BodyTypeDef>();
    }
    [HarmonyPatch(typeof(AlienPartGenerator), "GenerateMeshsAndMeshPools")]
    public static class GenerateMeshsAndMeshPools_Patch
    {
        public static void Prefix()
        {
            foreach (var def in DefDatabase<SubBodyTypesDefs>.AllDefs)
            foreach (var mapping in def.raceAndBodyTypes)
            foreach (var race in mapping.raceDefs.OfType<ThingDef_AlienRace>())
            {
                var parts = race.alienRace.generalSettings.alienPartGenerator;
                var paths = race.alienRace.graphicPaths;
                var types = mapping.bodyTypeDefs.Concat(parts.bodyTypes).Distinct();
                AddPaths(paths.body, types, true);
                foreach (var path in paths.apparel.individualPaths.Values) AddPaths(path, types, false);
            }
        }
        private static void AddPaths(AlienPartGenerator.ExtendedGraphicTop top, IEnumerable<BodyTypeDef> types, bool body)
        {
            // HAR 1.7 represents body/gender variants as condition graphics.
            if (top.GetSubGraphics().Any()) return;
            foreach (var type in types)
            {
                var textureType = body && type == BodyTypeDefOf.Baby ? BodyTypeDefOf.Child : type;
                string path = top.path;
                var variant = new AlienPartGenerator.ExtendedConditionGraphic {
                    path = body ? path + "Naked_" + textureType.defName : path + "_" + textureType.defName,
                    conditions = new List<AlienRace.ExtendedGraphics.Condition> { new ConditionBodyType { bodyType = type } }
                };
                foreach (var gender in new[] { Gender.Male, Gender.Female })
                    variant.extendedGraphics.Add(new AlienPartGenerator.ExtendedConditionGraphic {
                        path = body ? path + gender + "_Naked_" + textureType.defName : path + "_" + gender + "_" + textureType.defName,
                        conditions = new List<AlienRace.ExtendedGraphics.Condition> { new ConditionGender { gender = gender } }
                    });
                top.extendedGraphics.Add(variant);
            }
        }
    }
}

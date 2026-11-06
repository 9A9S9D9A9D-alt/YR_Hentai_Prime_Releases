using AlienRace;
using AlienRace.ExtendedGraphics;
using System.Collections.Generic;
using System.Linq;
using Verse;
using static AlienRace.AlienPartGenerator;

namespace YR_RJW
{
    public class ExtendedHediffGraphic : ExtendedConditionGraphic
    {
        public HediffDef hediff;
        public void ResolveLegacyCondition()
        {
            if (hediff != null && !conditions.OfType<ConditionHediff>().Any(c => c.hediff == hediff))
                conditions.Add(new ConditionHediff { hediff = hediff });
        }
    }
    public class HairAddonPatchDef : Def
    {
        public List<AddonPatch> addonPatchs = new List<AddonPatch>();
        //public List<ThingDef> targetThingDefs;
    }

    public class AddonPatch
    {
        public ThingDef_AlienRace race = null;
        public List<AddHediffGraphic> addHediffGraphics = new List<AddHediffGraphic>();
    }

    public class AddHediffGraphic
    {
        public int listNum;
        public List<ExtendedHediffGraphic> hediffGraphics = new List<ExtendedHediffGraphic>();
    }


    [StaticConstructorOnStartup]
    public class HairAddonPatch
    {
        static HairAddonPatch()
        {
            foreach (HairAddonPatchDef hairAddonPatchDef in DefDatabase<HairAddonPatchDef>.AllDefs)
            {
                foreach (AddonPatch addonPatch in hairAddonPatchDef.addonPatchs)
                {
                    if (addonPatch.race == null)
                    {
                        Log.Error("AddHediffGraphics need race");
                        continue;
                    }
                    List<BodyAddon> bodyAddons = addonPatch.race.alienRace.generalSettings.alienPartGenerator.bodyAddons;
                    foreach (AddHediffGraphic addHediffGraphic in addonPatch.addHediffGraphics)
                    {
                        BodyAddon originalBodyAddon = bodyAddons[addHediffGraphic.listNum];
                        if (originalBodyAddon != null)
                        {
                            ////복사 바디 애드온 삽입
                            //if (addHediffGraphic.copy)
                            //{
                            //    tempBodyAddon = DeepCopyBodyAddon(originalBodyAddon);
                            //    addonPatch.race.alienRace.generalSettings.alienPartGenerator.bodyAddons.Add(tempBodyAddon);

                            //    tempbodyAddonInfo.offset = addHediffGraphic.offset;
                            //    tempbodyAddonInfo.copyNum = addonPatch.race.alienRace.generalSettings.alienPartGenerator.bodyAddons.Count - 1;
                            //    bodyAddonInfos.Add(tempbodyAddonInfo);
                            //}

                            if (!addHediffGraphic.hediffGraphics.NullOrEmpty())
                            {
                                if (originalBodyAddon.extendedGraphics.NullOrEmpty())
                                {
                                    originalBodyAddon.extendedGraphics = new List<AbstractExtendedGraphic>();
                                }
                                //기존 헤디프 그래픽에 전용 헤디프 그래픽 삽입
                                foreach (ExtendedHediffGraphic hediffGraphic in addHediffGraphic.hediffGraphics)
                                {
                                    hediffGraphic.ResolveLegacyCondition();
                                    originalBodyAddon.extendedGraphics.Insert(0, hediffGraphic);
                                }
                            }
                            Log.Message($"HairAddon Patch : {addonPatch.race.defName} : {originalBodyAddon.path} : {originalBodyAddon.extendedGraphics.Count}");
                        }
                        else
                        {
                            Log.Error($"{addonPatch.race.defName}'s {addHediffGraphic.listNum}th bodyAddon is null, bodyAddon Count : {addonPatch.race.alienRace.generalSettings.alienPartGenerator.bodyAddons.Count} (from 0)");
                        }
                    }

                    {
                        //        if (addonPatch.copyBodyPart)
                        //        {
                        //            List<ThingDef_AlienRace> thingDef_AlienRaces = new List<ThingDef_AlienRace>();
                        //            if (addonPatch.allRace)
                        //            {
                        //                foreach (var race in DefDatabase<ThingDef_AlienRace>.AllDefs)
                        //                {
                        //                    thingDef_AlienRaces.Add(race);
                        //                }
                        //            }
                        //            if (addonPatch.race != null)
                        //            {
                        //                thingDef_AlienRaces.Add(addonPatch.race);
                        //            }

                        //            foreach (var race in thingDef_AlienRaces)
                        //            {
                        //                List<BodyPartRecord> headParts = new List<BodyPartRecord>();
                        //                List<string> headPartsLabels = new List<string>();
                        //                List<BodyPartDef> headPartDefs = new List<BodyPartDef>();
                        //                foreach (var part in race.race.body.corePart.parts)
                        //                {
                        //                    if (!TorsoPart(race, part))
                        //                    {
                        //                        AddHeadList(headParts, headPartsLabels, part);
                        //                        foreach (var headPart in part.parts)
                        //                        {
                        //                            AddHeadList(headParts, headPartsLabels, headPart);
                        //                            foreach (var insidePart in headPart.parts)
                        //                            {
                        //                                AddHeadList(headParts, headPartsLabels, insidePart);
                        //                                foreach (var deepPart in insidePart.parts)
                        //                                {
                        //                                    AddHeadList(headParts, headPartsLabels, deepPart);
                        //                                }
                        //                            }
                        //                        }
                        //                    }
                        //                }

                        //                //Log.Message("@" + race.defName + "@");
                        //                foreach (var headPart in headParts)
                        //                {
                        //                    //Log.Message("부위 : " + headPart.def.defName);
                        //                    headPartDefs.Add(headPart.def);
                        //                }



                        //                foreach (var bodyAddon in race.alienRace.generalSettings.alienPartGenerator.bodyAddons)
                        //                {
                        //                    bool bodyPart = false;
                        //                    if (bodyAddon.bodyPart != null)
                        //                    {
                        //                        bodyPart = headPartDefs.Contains(bodyAddon.bodyPart);
                        //                        //Log.Message($"{bodyAddon.bodyPart} : {bodyPart}");
                        //                    }

                        //                    bool bodyPartLabel = false;
                        //                    if (!bodyAddon.bodyPartLabel.NullOrEmpty())
                        //                    {
                        //                        bodyPartLabel = headPartsLabels.Contains(bodyAddon.bodyPartLabel);
                        //                        //Log.Message($"{bodyAddon.bodyPartLabel} : {bodyPartLabel}");
                        //                    }

                        //                    if (!bodyPart && !bodyPartLabel)
                        //                    {
                        //                        foreach (var targetThingDef in hairAddonPatchDef.targetThingDefs)
                        //                        {
                        //                            bodyAddon.hediffGraphics ??= new List<AbstractExtendedGraphic>();
                        //                            copyTAB.hediffGraphics = bodyAddon.hediffGraphics.ToList();
                        //                            copyTAB.copyBodyAddons.Add(bodyAddon);
                        //                            copyTAB.thingDef = targetThingDef;
                        //                            copyTAB.race = race;
                        //                            TABs.Add(copyTAB);
                        //                            copyTAB = new ThingDefAndBodyAddon();
                        //                        }
                        //                    }
                        //                }
                        //            }
                        //        }

                    }
                }
            }

        }

        public static BodyAddon DeepCopyBodyAddon(BodyAddon originalBodyAddon)
        {
            var copy = (BodyAddon)HarmonyLib.AccessTools.Method(typeof(object), "MemberwiseClone").Invoke(originalBodyAddon, null);
            copy.extendedGraphics = originalBodyAddon.extendedGraphics?.ToList();
            copy.conditions = originalBodyAddon.conditions.ToList();
            var posture = new AlienRace.ExtendedGraphics.ConditionPosture();
            HarmonyLib.AccessTools.Field(posture.GetType(), "drawnInBed").SetValue(posture, false);
            copy.conditions.Add(posture);
            return copy;
        }
    }
}

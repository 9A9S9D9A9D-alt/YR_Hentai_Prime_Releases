using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace YR_RJW
{
    public class CompProperties_Spawner_MilkingMachine : CompProperties_Building_BaseBondageBed
    {
        public CompProperties_Spawner_MilkingMachine()
        {
            compClass = typeof(CompSpawner_MilkingMachine);
        }

        public List<ThingToSpawnByCondition> thingToSpawnByConditions = new List<ThingToSpawnByCondition>();

        public List<PawnKindToSpawnByCondition> pawnKindToSpawnByConditions = new List<PawnKindToSpawnByCondition>();

        public IntRange spawnIntervalRange = new IntRange(100, 100);

        public int spawnMaxAdjacent = -1;

        public bool spawnForbidden;

        public bool writeTimeLeftToSpawn;

        public bool showMessageIfOwned;

        public string saveKeysPrefix;

        public bool inheritFaction;

        public bool spawnThingStackToCNMS = true;
    }

    public abstract class SpawnByCondition
    {
        public Condition condition;
        public IntRange spawnAmountRange = new IntRange(1, 1);
        public bool Break = false;
    }

    public class ThingToSpawnByCondition : SpawnByCondition
    {
        public ThingDef thingToSpawn;
    }
    public class PawnKindToSpawnByCondition : SpawnByCondition
    {
        public PawnKindDef thingToSpawn;
        public string femaleMessage = "YR_MessageCompSpawner_MilkingMachine_GaveBirth";
        public string maleMessage = "YR_MessageCompSpawner_MilkingMachine_GaveBirthByMilkingMachine";
    }
    public class CompSpawner_MilkingMachine : Comp_Building_BaseBondageBed
    {
        private ThingDef thingToSpawn;
        private int thingSpawnCount;
        private IntRange thingSpawnIntervalRange;

        private PawnKindDef pawnKindToSpawn;
        private int pawnKindSpawnCount;
        private IntRange pawnKindSpawnIntervalRange;
        private string message;
        public CompProperties_Spawner_MilkingMachine PropsSpawner => (CompProperties_Spawner_MilkingMachine)props;

        private bool PowerOn
        {
            get
            {
                CompPowerTrader comp = parent.GetComp<CompPowerTrader>();
                return comp != null && comp.PowerOn;
            }
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (!respawningAfterLoad)
            {
                ResetCountdown();
            }
        }

        public override void TickInterval(int interval)
        {
            if (!CheckCanUse())
            {
                if (sustainerAmbient != null)
                {
                    sustainerAmbient.End();
                    sustainerAmbient = null;
                }
                return;
            }
            ticksUntilSpawn -= interval;
            CheckShouldSpawn();
            if (sustainerAmbient == null || sustainerAmbient.Ended)
            {
                sustainerAmbient = PropsSpawner.soundAmbient?.TrySpawnSustainer(SoundInfo.InMap(parent, MaintenanceType.PerTick));
            }
            sustainerAmbient?.Maintain();
        }

        private void CheckShouldSpawn()
        {
            if (ticksUntilSpawn <= 0)
            {
                ResetCountdown();
                TryDoSpawn();
            }
        }

        public bool TryDoSpawn()
        {
            bool result = false;
            if (!parent.Spawned)
            {
                return result;
            }

            CheckSpawnThing();

            if (thingToSpawn != null)
            {
                if (parent is Building_BaseBondageBed && PropsSpawner.spawnThingStackToCNMS)
                {
                    CompAffectedByFacilities compAffectedByFacilities = parent.TryGetComp<CompAffectedByFacilities>();
                    if (compAffectedByFacilities != null)
                    {
                        foreach (Thing buildingThing in compAffectedByFacilities.LinkedFacilitiesListForReading)
                        {
                            CompCNMS compCNMS = buildingThing.TryGetComp<CompCNMS>();

                            if (compCNMS != null)
                            {
                                Thing thing = ThingMaker.MakeThing(thingToSpawn, null);
                                thing.stackCount = thingSpawnCount;

                                compCNMS.innerContainer.TryAdd(thing);

                                return false;
                            }
                        }
                    }
                }
                if (PropsSpawner.spawnMaxAdjacent >= 0)
                {
                    int num = 0;
                    for (int i = 0; i < 9; i++)
                    {
                        IntVec3 c = parent.Position + GenAdj.AdjacentCellsAndInside[i];
                        if (c.InBounds(parent.Map))
                        {
                            List<Thing> thingList = c.GetThingList(parent.Map);
                            for (int j = 0; j < thingList.Count; j++)
                            {
                                if (thingList[j].def == thingToSpawn)
                                {
                                    num += thingList[j].stackCount;
                                    if (num >= PropsSpawner.spawnMaxAdjacent)
                                    {
                                        return false;
                                    }
                                }
                            }
                        }
                    }
                }
                if (TryFindSpawnCell(parent, thingToSpawn, thingSpawnCount, out IntVec3 center))
                {
                    Thing thing = ThingMaker.MakeThing(thingToSpawn, null);
                    thing.stackCount = thingSpawnCount;
                    if (thing == null)
                    {
                        Log.Error("Could not spawn anything for " + parent);
                    }
                    if (PropsSpawner.inheritFaction && thing.Faction != parent.Faction)
                    {
                        thing.SetFaction(parent.Faction, null);
                    }
                    GenPlace.TryPlaceThing(thing, center, parent.Map, ThingPlaceMode.Direct, out Thing t, null, null, default);
                    if (PropsSpawner.spawnForbidden)
                    {
                        t.SetForbidden(true, true);
                    }
                    if (PropsSpawner.showMessageIfOwned && parent.Faction == Faction.OfPlayer)
                    {
                        Messages.Message("MessageCompSpawnerSpawnedItem".Translate(thingToSpawn.LabelCap), thing, MessageTypeDefOf.PositiveEvent, true);
                    }
                    result = true;
                }
            }

            if (pawnKindToSpawn != null)
            {
                PawnGenerationRequest request = new PawnGenerationRequest(pawnKindToSpawn, faction: parent.Faction, allowDowned: true, fixedBiologicalAge: 0);

                for (int i = 0; i < pawnKindSpawnCount; i++)
                {
                    Pawn generatedPawn = PawnGenerator.GeneratePawn(request);

                    generatedPawn.relations.AddDirectRelation(YR_RJW_DefOf.YR_BreedingSource, pawn);
                    pawn.relations.AddDirectRelation(YR_RJW_DefOf.YR_Byproduct, generatedPawn);

                    GenSpawn.Spawn(generatedPawn, CellFinder.RandomClosewalkCellNear(this.parent.Position, this.parent.Map, 1, null), this.parent.Map, WipeMode.Vanish);

                }
                if (PropsSpawner.showMessageIfOwned && parent.Faction == Faction.OfPlayer)
                {
                    Messages.Message(message.Translate(pawn.Label, pawnKindToSpawn.label, parent.Label), pawn, MessageTypeDefOf.PositiveEvent, true);
                }
                result = true;
            }
            return result;
        }

        private void CheckSpawnThing()
        {
            if (pawn == null)
            {
                return;
            }
            foreach (ThingToSpawnByCondition thingToSpawnByCondition in PropsSpawner.thingToSpawnByConditions)
            {
                if (CompChangeBedTexture.ConditionMatch(pawn, thingToSpawnByCondition.condition))
                {
                    thingToSpawn = thingToSpawnByCondition.thingToSpawn;
                    thingSpawnCount = thingToSpawnByCondition.spawnAmountRange.RandomInRange;
                    thingSpawnIntervalRange = thingToSpawnByCondition.spawnAmountRange;

                    if (thingToSpawnByCondition.Break)
                    {
                        break;
                    }
                }
            }
            foreach (PawnKindToSpawnByCondition pawnKindToSpawnByCondition in PropsSpawner.pawnKindToSpawnByConditions)
            {
                if (CompChangeBedTexture.ConditionMatch(pawn, pawnKindToSpawnByCondition.condition))
                {
                    pawnKindToSpawn = pawnKindToSpawnByCondition.thingToSpawn;
                    pawnKindSpawnCount = pawnKindToSpawnByCondition.spawnAmountRange.RandomInRange;
                    pawnKindSpawnIntervalRange = pawnKindToSpawnByCondition.spawnAmountRange;
                    if (pawn.gender == Gender.Male)
                    {
                        message = pawnKindToSpawnByCondition.maleMessage;
                    }
                    else
                    {
                        message = pawnKindToSpawnByCondition.femaleMessage;
                    }

                    if (pawnKindToSpawnByCondition.Break)
                    {
                        break;
                    }
                }
            }
        }

        public static bool TryFindSpawnCell(Thing parent, ThingDef thingToSpawn, int spawnCount, out IntVec3 result)
        {
            foreach (IntVec3 intVec in GenAdj.CellsAdjacent8Way(parent).InRandomOrder(null))
            {
                if (intVec.Walkable(parent.Map))
                {
                    Building edifice = intVec.GetEdifice(parent.Map);
                    if (edifice == null || !thingToSpawn.IsEdifice())
                    {
                        Building_Door building_Door = edifice as Building_Door;
                        if ((building_Door == null || building_Door.FreePassage) && (parent.def.passability == Traversability.Impassable || GenSight.LineOfSight(parent.Position, intVec, parent.Map, false, null, 0, 0)))
                        {
                            bool flag = false;
                            List<Thing> thingList = intVec.GetThingList(parent.Map);
                            for (int i = 0; i < thingList.Count; i++)
                            {
                                Thing thing = thingList[i];
                                if (thing.def.category == ThingCategory.Item && (thing.def != thingToSpawn || thing.stackCount > thingToSpawn.stackLimit - spawnCount))
                                {
                                    flag = true;
                                    break;
                                }
                            }
                            if (!flag)
                            {
                                result = intVec;
                                return true;
                            }
                        }
                    }
                }
            }
            result = IntVec3.Invalid;
            return false;
        }

        private void ResetCountdown()
        {
            ticksUntilSpawn = PropsSpawner.spawnIntervalRange.RandomInRange;
        }

        public override void PostExposeData()
        {
            string str = PropsSpawner.saveKeysPrefix.NullOrEmpty() ? null : (PropsSpawner.saveKeysPrefix + "_");
            Scribe_Values.Look(ref ticksUntilSpawn, str + "ticksUntilSpawn", 0, false);
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (Prefs.DevMode)
            {
                Texture icon = ContentFinder<Texture2D>.Get("Ui/Widgets/CheckOff");
                if (thingToSpawn != null)
                {
                    icon = thingToSpawn.uiIcon;
                    //icon = ContentFinder<Texture2D>.Get(thingToSpawn.graphic.path);
                }
                yield return new Command_Action
                {
                    defaultLabel = "DEBUG: Spawn " + thingToSpawn?.label ?? "Nothing",
                    icon = icon,
                    action = delegate ()
                    {
                        ResetCountdown();
                        TryDoSpawn();
                    }
                };
            }
            yield break;
        }

        public override string CompInspectStringExtra()
        {
            CheckSpawnThing();

            if (pawn == null || BBB.occupant == null)
            {
                return "";
            }

            if (PropsSpawner.writeTimeLeftToSpawn)
            {
                string milkingMachine = "";

                if (pawn != null)
                {
                    if (thingToSpawn != null)
                    {
                        milkingMachine += "\n" + "YR_SpawnThing".Translate() + " : " + thingToSpawn.label + "\n" + "YR_SpawnCount".Translate() + " : " + thingSpawnIntervalRange;
                    }
                    if (pawnKindToSpawn != null)
                    {
                        milkingMachine += "\n" + "YR_SpawnThing".Translate() + " : " + pawnKindToSpawn.label + "\n" + "YR_SpawnCount".Translate() + " : " + pawnKindSpawnIntervalRange;
                    }
                }
                return "NextSpawnedResourceIn".Translate() + " : " + ticksUntilSpawn.ToStringTicksToPeriod(true, false, true, true, false) + milkingMachine;
            }
            return "";
        }

        public void CompleteCountdown() { ticksUntilSpawn = 0; }
        private int ticksUntilSpawn;
    }
}


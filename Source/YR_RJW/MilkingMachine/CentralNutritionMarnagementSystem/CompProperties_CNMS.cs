using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace YR_RJW
{
    public class CompProperties_CNMS : CompProperties
    {
        public CompProperties_CNMS()
        {
            compClass = typeof(CompCNMS);
        }

        public int ticks = 100;
        public int spawnMaxAdjacent = -1;
        public bool inheritFaction;
        public bool spawnForbidden;
        public bool showMessageIfOwned;
        public int stackLimit = 50;
        public float nutrition = 0.09f;
        public float selfNutritionRecovery;
        public List<ThingDef> anotherCNMSDefnames = new List<ThingDef>();
        public string spawnMilkCommandLabel = "YR_SpawnMilkCommandLabel";
        public string spawnMilkCommandDesc = "YR_SpawnMilkCommandDesc";

        public string mainCNMSCommandLabel = "YR_MainCNMSCommandLabel";
        public string mainCNMSCommandDesc = "YR_MainCNMSCommandDesc";
    }
    public class CompCNMS : ThingComp, IThingHolder
    {
        private Building Building => (Building)parent;
        public List<Thing> LinkedBuildings
        {
            get
            {
                CompFacility compFacility = parent.TryGetComp<CompFacility>();
                return compFacility?.LinkedBuildings;
            }
        }
        public ThingOwner innerContainer;
        private readonly List<Thing> tmpThings = new List<Thing>();
        private int ticks = 1;
        private CompPowerTrader powerComp;
        private CompRefuelable refuelableComp;
        public bool mainCNMS = false;

        public CompProperties_CNMS Props => (CompProperties_CNMS)props;

        public bool PowerOn => powerComp == null || powerComp.PowerOn;
        public bool AnotherCNMS(Thing other) => other != null && (other.def == parent.def || Props.anotherCNMSDefnames.Contains(other.def));
        public override void PostPostMake()
        {
            powerComp = Building.GetComp<CompPowerTrader>();
            refuelableComp = Building.GetComp<CompRefuelable>();
        }
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            powerComp = Building.GetComp<CompPowerTrader>();
            refuelableComp = Building.GetComp<CompRefuelable>();
        }
        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            base.PostDeSpawn(map, mode);
            innerContainer.TryDropAll(parent.Position, map, ThingPlaceMode.Near, null, null, true);
        }
        public CompCNMS()
        {
            innerContainer = new ThingOwner<Thing>(this);
        }
        public override void CompTick()
        {
            if (!parent.Spawned || !PowerOn) return;
            if (--ticks > 0) return;
            ticks = Mathf.Max(1, Props.ticks);

            if (refuelableComp != null)
            {
                if (Props.selfNutritionRecovery > 0f) refuelableComp.Refuel(Props.selfNutritionRecovery);
                if (LinkedBuildings != null)
                    foreach (Thing linked in LinkedBuildings)
                    {
                        var bed = linked as Building_BaseBondageBed;
                        var food = bed?.occupant?.needs?.food;
                        if (food != null && LegacyEligibility.Allows(bed.occupant) && refuelableComp.Fuel >= 1f &&
                            food.MaxLevel - food.CurLevel >= 0.1f)
                        {
                            refuelableComp.ConsumeFuel(1f);
                            food.CurLevel += Props.nutrition;
                        }
                    }
            }

            if (CNMSNetwork.TryTransfer(this)) return;
            // Reverse iteration keeps holder mutations from skipping or duplicating stacks.
            for (int i = innerContainer.Count - 1; i >= 0; i--)
            {
                Thing thing = innerContainer[i];
                if (thing.stackCount >= thing.def.stackLimit || thing.stackCount >= Props.stackLimit)
                    TryDoSpawn(thing);
            }
        }
        public bool TryDoSpawn(Thing thing)
        {
            if (!parent.Spawned || thing == null)
            {
                return false;
            }
            if (Props.spawnMaxAdjacent >= 0)
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
                            if (thingList[j].def == thing.def)
                            {
                                num += thingList[j].stackCount;
                                if (num >= Props.spawnMaxAdjacent)
                                {
                                    return false;
                                }
                            }
                        }
                    }
                }
            }
            if (CompSpawner_MilkingMachine.TryFindSpawnCell(parent, thing.def, thing.stackCount, out IntVec3 center))
            {
                if (thing == null)
                {
                    Log.Error("Could not spawn anything for " + parent);
                }
                if (Props.inheritFaction && thing.Faction != parent.Faction)
                {
                    thing.SetFaction(parent.Faction, null);
                }
                if (!innerContainer.TryDrop(thing, center, parent.Map, ThingPlaceMode.Direct, out Thing t)) return false;
                if (Props.spawnForbidden)
                {
                    t.SetForbidden(true, true);
                }
                if (Props.showMessageIfOwned && parent.Faction == Faction.OfPlayer)
                {
                    Messages.Message("MessageCompSpawnerSpawnedItem".Translate(thing.def.LabelCap), thing, MessageTypeDefOf.PositiveEvent, true);
                }
                return true;
            }
            return false;
        }
        public override void PostExposeData()
        {
            base.PostExposeData();
            bool flag = !parent.SpawnedOrAnyParentSpawned;
            if (flag && Scribe.mode == LoadSaveMode.Saving)
            {
                tmpThings.Clear();
                tmpThings.AddRange(innerContainer);
                tmpThings.Clear();
            }
            Scribe_Deep.Look(ref innerContainer, parent.TryGetComp<YR_Hentai_Prime_AnimationBed.CompFacility_CNMS>() != null ? "legacyCNMSInnerContainer" : "innerContainer", new object[]
            {
                this
            });

            if (innerContainer == null) innerContainer = new ThingOwner<Thing>(this);
            Scribe_Values.Look(ref ticks, "ticks", 0, false);
            Scribe_Values.Look(ref mainCNMS, "mainCNMS");
        }

        public override string CompInspectStringExtra()
        {
            return "Contents".Translate() + ": " + innerContainer.ContentsString.CapitalizeFirst();
        }

        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
        }

        public ThingOwner GetDirectlyHeldThings()
        {
            return innerContainer;
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {

            if (parent.TryGetComp<YR_Hentai_Prime_AnimationBed.CompFacility_CNMS>() != null) yield break;
            Command_Action trySpawnInneContainer = new Command_Action
            {
                defaultLabel = Props.spawnMilkCommandLabel.Translate(),
                defaultDesc = Props.spawnMilkCommandDesc.Translate(),
                icon = ContentFinder<Texture2D>.Get("Things/Item/Resource/Milk"),
                action = delegate ()
                {
                    innerContainer.TryDropAll(parent.Position, parent.Map, ThingPlaceMode.Near, null, null, true);
                }
            };
            yield return trySpawnInneContainer;

            Texture2D icon = ContentFinder<Texture2D>.Get("Ui/Heroart/ExpansionIcon_Royalty");
            if (!mainCNMS)
            {
                icon = ContentFinder<Texture2D>.Get("Ui/Heroart/NotOwned_Royalty");
            }

            Command_Action mainCNMSCommand = new Command_Action
            {
                defaultLabel = Props.mainCNMSCommandLabel.Translate(),
                defaultDesc = Props.mainCNMSCommandDesc.Translate(),
                icon = icon,
                action = delegate ()
                {
                    if (mainCNMS)
                    {
                        mainCNMS = false;
                    }
                    else
                    {
                        mainCNMS = true;
                        CNMSNetwork.ClearModernMains(this);

                        foreach (Thing CNMS in parent.Map.listerThings.AllThings)
                        {
                            if (CNMS != parent && AnotherCNMS(CNMS))
                            {
                                CompCNMS compCNMS = CNMS.TryGetComp<CompCNMS>();
                                if (compCNMS != null)
                                {
                                    compCNMS.mainCNMS = false;
                                }
                            }
                        }
                    }
                }
            };
            yield return mainCNMSCommand;
        }
    }
}


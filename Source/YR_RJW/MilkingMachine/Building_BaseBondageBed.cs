
using RimWorld;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Verse;

namespace YR_RJW
{
    public partial class Building_BaseBondageBed : Building_Bed, ISuspendableThingHolder
    {
        public Pawn occupant;
        public override Graphic Graphic => occupant!=null && changeBedTextureComp?.Props.boundBedGraphicData!=null
            ? changeBedTextureComp.Props.boundBedGraphicData.GraphicColoredFor(this) : base.Graphic;


        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref occupant, "occupant");
            Scribe_References.Look(ref dummyForJoy, "dummyForJoy");
            ExposeCoffinData();
        }

        //public override void Draw()
        //{

        //}
        private CompPowerTrader powerComp;
        public CompChangeBedTexture changeBedTextureComp;
        public CompSpawnDummyForJoy spawnDummyForJoyComp;
        public DummyForJoy dummyForJoy;

        public bool PowerOn => powerComp == null || powerComp.PowerOn;
        public bool GetChangeBedTextureComp => changeBedTextureComp != null;
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            Medical = false;
            powerComp = GetComp<CompPowerTrader>();
            changeBedTextureComp = GetComp<CompChangeBedTexture>();
            //this.fixInteractionCellComp = GetComp<CompFixInteractionCell>();
            spawnDummyForJoyComp = GetComp<CompSpawnDummyForJoy>();
        }
        public override void PostMake()
        {
            base.PostMake();
            powerComp = GetComp<CompPowerTrader>();
            changeBedTextureComp = GetComp<CompChangeBedTexture>();
            //this.fixInteractionCellComp = GetComp<CompFixInteractionCell>();
            spawnDummyForJoyComp = GetComp<CompSpawnDummyForJoy>();
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            if (CoffinCorpse != null && !TryEjectCoffinCorpse()) return;
            if (occupant != null)
            {
                CompRemoveEffectBondageBed compRemoveEffectBondageBed = GetComp<CompRemoveEffectBondageBed>() ??
                                                 throw new Exception("cant find comp : CompRemoveEffectBondageBed");
                compRemoveEffectBondageBed.DoEffect(occupant);
            }

            base.DeSpawn(mode);
        }

        protected override void Tick()
        {
            base.Tick();
            TickCoffin();
            if (occupant != null && !LegacyEligibility.Allows(occupant))
            {
                var old = occupant;
                HediffRemover.RemoveBondadgeHediff(old);
                RemoveOccupant();
                old.jobs?.EndCurrentJob(Verse.AI.JobCondition.InterruptForced);
            }
        }

        public void SetOccupant(Pawn newOccupant, bool reset = false)
        {
            if (CoffinCorpse != null) return;
            if (!LegacyEligibility.Allows(newOccupant)) return;
            if (occupant != null)
            {
                GetComp<CompAssignableToPawn>().ForceRemovePawn(occupant);
            }

            if (newOccupant == null)
            {
                return;
            }

            occupant = newOccupant;
            if(changeBedTextureComp?.Props.boundBedGraphicData!=null && Spawned)DirtyMapMesh(Map);
            GetComp<CompAssignableToPawn>().TryAssignPawn(newOccupant);

            if (spawnDummyForJoyComp != null && spawnDummyForJoyComp.Props.spawnDummy != null)
            {
                if (!reset)
                {
                    var position = Position;
                    position.z -= (this.def.size.z - 1);
                    dummyForJoy = (DummyForJoy)GenSpawn.Spawn(ThingMaker.MakeThing(spawnDummyForJoyComp.Props.spawnDummy), position, Map, spawnDummyForJoyComp.Props.spawnDummy.defaultPlacingRot);
                }
                if (dummyForJoy != null)
                {
                    dummyForJoy.occupant = occupant;
                    dummyForJoy.parent = this;
                }
            }

            foreach (ThingComp thingComp in AllComps)
            {
                if (thingComp is Comp_Building_BaseBondageBed comp_Building_BaseBondageBed)
                {
                    comp_Building_BaseBondageBed.PostSetOccupant(occupant);
                }
            }

            foreach (Thing thing in Map.spawnedThings)
            {
                if (thing is Building_BaseBondageBed BBB)
                {
                    foreach (ThingComp thingComp in BBB.AllComps)
                    {
                        if (thingComp is Comp_Building_BaseBondageBed comp_Building_BaseBondageBed)
                        {
                            comp_Building_BaseBondageBed.PostSetOccupantByAnother(occupant);
                        }
                    }
                }
            }
        }

        public void RemoveOccupant()
        {
            if (occupant == null) return;
            GetComp<CompAssignableToPawn>().TryUnassignPawn(occupant);

            foreach (ThingComp thingComp in AllComps)
            {
                if (thingComp is Comp_Building_BaseBondageBed comp_Building_BaseBondageBed)
                {
                    comp_Building_BaseBondageBed.PostRemoveOccupant(occupant);
                }
            }
            foreach (Thing thing in Map.spawnedThings)
            {
                if (thing is Building_BaseBondageBed BBB)
                {
                    foreach (ThingComp thingComp in BBB.AllComps)
                    {
                        if (thingComp is Comp_Building_BaseBondageBed comp_Building_BaseBondageBed)
                        {
                            comp_Building_BaseBondageBed.PostRemoveOccupantByAnother(occupant);
                        }
                    }
                }
            }
            occupant = null;
            if(changeBedTextureComp?.Props.boundBedGraphicData!=null && Spawned)DirtyMapMesh(Map);
        }

        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn myPawn)
        {
            foreach (FloatMenuOption option in CoffinFloatMenuOptions(myPawn)) yield return option;
            if (CoffinCorpse != null) yield break;
            if (AllComps == null)
            {
                yield break;
            }

            foreach (ThingComp thingComp in AllComps)
            {
                foreach (FloatMenuOption item in thingComp.CompFloatMenuOptions(myPawn))
                {
                    yield return item;
                }
            }
        }

        private static readonly Color SheetColorNormal = new Color(0.6313726f, 71f / 85f, 0.7058824f);


        public override Color DrawColorTwo => SheetColorNormal;
        public override void DrawGUIOverlay()
        {
            if (occupant == null || !YR_RJW_Setting.YR_BoundGUI)
            {
                return;
            }

            GenMapUI.DrawThingLabel(this, "YR_Bound".Translate(), Color.green);
        }
        public override string GetInspectString()
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append(base.GetInspectString());
            if (CoffinCorpse != null)
            {
                stringBuilder.AppendLine();
                stringBuilder.Append("YR_SamokCoffinProgress".Translate(CoffinCorpse.InnerPawn.LabelShort,
                    (CoffinResurrectionTicks - coffinTicks).ToStringTicksToPeriod()));
            }

            return stringBuilder.ToString();
        }
        //public override IntVec3 InteractionCell
        //{
        //    get
        //    {
        //        if(fixInteractionCellComp != null && this.def.interactionCellOffset != IntVec3.Zero)
        //        {
        //            return this.Position - this.def.interactionCellOffset;
        //        }

        //        IntVec3? intVec = this.FindPreferredInteractionCell(base.Position, null);
        //        if (intVec == null)
        //        {
        //            return base.InteractionCell;
        //        }
        //        return intVec.GetValueOrDefault();
        //    }
        //}
    }
}

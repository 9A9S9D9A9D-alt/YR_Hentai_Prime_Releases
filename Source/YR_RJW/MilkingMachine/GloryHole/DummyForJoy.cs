using HarmonyLib;
using RimWorld;
using System;
using System.Reflection;
using UnityEngine;
using Verse;

namespace YR_RJW
{
    public class DummyForJoy : Thing
    {
        public Building parent;
        public Pawn occupant;
        public Pawn user;
        public PawnRenderFlags drawFlags;
        public Vector3 offset;
        public bool startAni;
        public bool iconReset = false;
        public int tickForPrisoner = 500;
        public bool resetTick = true;

        protected override void Tick()
        {
            if (parent == null || occupant == null || (occupant != null && !(occupant.CurrentBed() is Building_BaseBondageBed)))
            {
                Destroy();
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (user != null && startAni)
            {
                DrawInnerThing(user, offset, 0, true, Rot4.North, RotDrawMode.Fresh, drawFlags);
            }
            if (iconReset)
            {
                foreach (Thing thing in Map.spawnedThings)
                {
                    if (thing is Building_BaseBondageBed BBB)
                    {
                        foreach (ThingComp thingComp in BBB.AllComps)
                        {
                            if (thingComp is Comp_Building_BaseBondageBed comp_Building_BaseBondageBed)
                            {
                                comp_Building_BaseBondageBed.PostSetOccupantByAnother(user);
                            }
                        }
                    }
                }
                iconReset = false;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_References.Look(ref parent, "parent");
            Scribe_References.Look(ref occupant, "occupant");
            Scribe_References.Look(ref user, "user");
            Scribe_Values.Look(ref drawFlags, "drawFlags");
            Scribe_Values.Look(ref offset, "offset");
            Scribe_Values.Look(ref startAni, "startAni");
            Scribe_Values.Look(ref tickForPrisoner, "tickForPrisoner");
            Scribe_Values.Look(ref resetTick, "resetTick");
        }
        public virtual void DrawInnerThing(Pawn pawn, Vector3 rootLoc, float angle, bool renderBody, Rot4 bodyFacing, RotDrawMode bodyDrawType, PawnRenderFlags renderFlags)
        {
            FacilityRender.Draw(pawn, rootLoc, bodyFacing, renderFlags);
        }

    }
}

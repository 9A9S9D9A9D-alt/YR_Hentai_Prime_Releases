using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.Sound;

namespace YR_RJW
{

    public class CompProperties_Building_BaseBondageBed : CompProperties
    {
        public List<HediffDef> hediffDefs = new List<HediffDef>();

        public bool showMessage;

        public int ticks = 100;

        public bool requiresJoyAction;

        public SoundDef soundAmbient;

        public List<ThingDef> moteDefs;
    }

    public class Comp_Building_BaseBondageBed : ThingComp
    {
        public int ticks;

        public Pawn pawn => BBB.occupant;

        public Sustainer sustainerAmbient;

        CompProperties_Building_BaseBondageBed Props => (CompProperties_Building_BaseBondageBed)props;

        public Building_BaseBondageBed BBB => (Building_BaseBondageBed)parent;

        public CompCanBeDormant compCanBeDormant;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            compCanBeDormant = parent.GetComp<CompCanBeDormant>();
        }
        public virtual void ResetCountdown(int baseTicks)
        {
            ticks = baseTicks;
        }

        public virtual bool CheckCanUse()
        {
            if (!parent.Spawned)
            {
                return false;
            }
            if (!LegacyEligibility.Allows(pawn))
            {
                return false;
            }
            if (compCanBeDormant != null)
            {
                if (!compCanBeDormant.Awake)
                {
                    return false;
                }
            }
            else if (parent.Position.Fogged(parent.Map))
            {
                return false;
            }
            if (Props.requiresJoyAction)
            {
                var comp = parent.TryGetComp<CompChangeBedTexture>();
                if (comp == null)
                {
                    Log.ErrorOnce("requiresJoyAction is true, but CompChangeBedTexture is null", 1020);
                    return false;
                }
                else if (!comp.Props.joyPawnSetting.startBedAniWhenJoying)
                {
                    Log.ErrorOnce("requiresJoyAction is true, but joyPawnSetting.startBedAniWhenJoying is false", 1021);
                    return false;
                }
                else if (BBB.dummyForJoy.user == null)
                {
                    return false;
                }
            }
            return true;
        }

        public override void CompTick()
        {
            base.CompTick();
            TickInterval(1);
        }
        public override void CompTickRare()
        {
            base.CompTickRare();
            TickInterval(250);
        }

        public virtual void TickInterval(int interval)
        {

        }

        public virtual void PostSetOccupant(Pawn pawn)
        {
        }
        public virtual void PostRemoveOccupant(Pawn pawn)
        {
        }

        public virtual void PostSetOccupantByAnother(Pawn pawn)
        {
        }
        public virtual void PostRemoveOccupantByAnother(Pawn pawn)
        {
        }
    }
}
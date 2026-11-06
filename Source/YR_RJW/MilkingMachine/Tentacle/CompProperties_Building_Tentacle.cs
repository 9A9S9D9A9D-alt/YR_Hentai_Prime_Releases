using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.Sound;

namespace YR_RJW
{
    public class CompProperties_Building_Tentacle : CompProperties_Building_BaseBondageBed
    {
        public CompProperties_Building_Tentacle()
        {
            compClass = typeof(Comp_Building_Tentacle);
        }
    }

    public class Comp_Building_Tentacle : Comp_Building_BaseBondageBed
    {
        private int spareTicks = 5;

        public CompProperties_Building_Tentacle Props => (CompProperties_Building_Tentacle)props;

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
                spareTicks--;
                if (spareTicks <= 0)
                {
                    if (sustainerAmbient != null)
                    {
                        sustainerAmbient.End();
                        sustainerAmbient = null;
                    }
                    ResetCountdown();
                }
                return;
            }
            spareTicks = 5;
            ticks -= interval;
            if (ticks <= 0)
            {
                AddHediffAndRemoveOccupant();
            }
            if (sustainerAmbient == null || sustainerAmbient.Ended)
            {
                sustainerAmbient = Props.soundAmbient.TrySpawnSustainer(SoundInfo.InMap(parent, MaintenanceType.PerTick));
            }
            sustainerAmbient.Maintain();
        }

        private void AddHediffAndRemoveOccupant()
        {
            ResetCountdown();
            foreach (HediffDef hediffDef in Props.hediffDefs)
            {
                if (!pawn.health.hediffSet.HasHediff(hediffDef))
                {
                    pawn.health.AddHediff(hediffDef);
                }
            }
            if (parent.def.defName == "YR_Building_Tentacle") SamokHostGenderV8.Complete(pawn);
            if (Props.showMessage)
            {
                Messages.Message("MessageComp_Building_Tentacle".Translate(pawn), MessageTypeDefOf.PositiveEvent, true);
            }

            HediffRemover.RemoveBondadgeHediff(pawn);
            BBB.RemoveOccupant();
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (Prefs.DevMode)
            {
                yield return new Command_Action
                {
                    defaultLabel = "DEBUG: Finish Process",
                    icon = TexCommand.Install,
                    action = delegate ()
                    {
                        ResetCountdown();
                        if (BBB.occupant != null)
                        {
                            AddHediffAndRemoveOccupant();
                        }
                    }
                };
            }
            yield break;
        }

        public override void PostRemoveOccupant(Pawn pawn)
        {
            base.PostRemoveOccupant(pawn);
            ResetCountdown();
        }

        public override string CompInspectStringExtra()
        {
            return BBB.occupant == null
                ? ""
                : (string)"YR_FinishEggInsert".Translate(ticks.ToStringTicksToPeriod(true, false, true, true).Colorize(ColoredText.DateTimeColor));
        }
        private void ResetCountdown()
        {
            ticks = Props.ticks;
        }

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref ticks, "ticks", 0, false);
        }
    }
}



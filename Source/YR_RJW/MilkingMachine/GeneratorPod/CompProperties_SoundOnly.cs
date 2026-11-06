using RimWorld;
using UnityEngine;
using Verse.Sound;
using Random = System.Random;

namespace YR_RJW
{

    public class CompProperties_SoundOnly : CompProperties_Building_BaseBondageBed
    {
        public CompProperties_SoundOnly()
        {
            compClass = typeof(Comp_SoundOnly);
        }
    }

    public class Comp_SoundOnly : Comp_Building_BaseBondageBed
    {
        public CompProperties_SoundOnly Props => (CompProperties_SoundOnly)props;
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
            ticks--;

            if (sustainerAmbient == null || sustainerAmbient.Ended)
            {
                sustainerAmbient = Props.soundAmbient?.TrySpawnSustainer(SoundInfo.InMap(parent, MaintenanceType.PerTick));
            }
            sustainerAmbient?.Maintain();

            if (ticks <= 0 && Props.moteDefs != null && Props.moteDefs.Count > 0)
            {
                Random rand = new Random();

                ticks = Props.ticks + rand.Next(-30, 30);

                MoteMaker.MakeAttachedOverlay(parent, Props.moteDefs[rand.Next(Props.moteDefs.Count)], new Vector3(), 1f, -1f);

            }
        }
    }
}
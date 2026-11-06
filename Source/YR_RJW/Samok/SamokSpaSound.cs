using System.Linq;
using Verse;
using Verse.Sound;

namespace YR_RJW
{
    public class CompProperties_SamokSpaSound : CompProperties
    {
        public SoundDef soundAmbient;
        public CompProperties_SamokSpaSound() { compClass = typeof(CompSamokSpaSound); }
    }

    public class CompSamokSpaSound : ThingComp
    {
        // Transient audio is recreated on the first occupied tick after loading.
        private Sustainer sustainer;
        public CompProperties_SamokSpaSound Props => (CompProperties_SamokSpaSound)props;

        public override void CompTick()
        {
            base.CompTick();
            if (!(parent is Building_SamokSwimmingPool pool) || !pool.Spawned || !pool.ActiveUsers.Any())
            {
                StopSound();
                return;
            }
            // One voice per pool, independent of the number and kind of actual users.
            if (sustainer == null || sustainer.Ended)
                sustainer = Props.soundAmbient?.TrySpawnSustainer(SoundInfo.InMap(parent, MaintenanceType.PerTick));
            sustainer?.Maintain();
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            StopSound();
        }

        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            StopSound();
            base.PostDeSpawn(map, mode);
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            StopSound();
            base.PostDestroy(mode, previousMap);
        }

        public override void Notify_MapRemoved()
        {
            StopSound();
            base.Notify_MapRemoved();
        }

        private void StopSound()
        {
            if (sustainer != null && !sustainer.Ended) sustainer.End();
            sustainer = null;
        }
    }
}

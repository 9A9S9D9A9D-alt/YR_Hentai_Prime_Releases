using RimWorld;
using System.Collections.Generic;
using Verse;

namespace YR_RJW
{
    public class CompProperties_SpawnBed : CompProperties
    {
        public CompProperties_SpawnBed()
        {
            compClass = typeof(CompSpawnBed);
        }
        public ThingDef bedDef;
    }
    public class CompSpawnBed : Comp_Building_BaseBondageBed
    {
        private Building_BaseBondageBed bed = null;
        public Building_BaseBondageBed SpawnedBed => bed;
        public CompProperties_SpawnBed Props => (CompProperties_SpawnBed)props;
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach(var gizmo in base.CompGetGizmosExtra())yield return gizmo;
            var adjustment=bed?.TryGetComp<YR_Hentai_Prime_AnimationBed.CompAnimationPosition>();
            if(adjustment!=null)foreach(var gizmo in adjustment.CompGetGizmosExtra())yield return gizmo;
        }
        public override void PostExposeData()
        {
            Scribe_References.Look(ref bed, "bed");
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            CheckBed();
        }

        public void CheckBed()
        {
            if (Props.bedDef != null)
            {
                if (!bed.DestroyedOrNull() && bed.Spawned)
                {
                    return;
                }

                List<Thing> list = parent.Map.thingGrid.ThingsListAt(parent.Position);
                CompRespawnBed compRespawnBed;
                foreach (Thing thing in list)
                {
                    if (thing.def == Props.bedDef)
                    {
                        bed = (Building_BaseBondageBed)thing;
                        compRespawnBed = bed.TryGetComp<CompRespawnBed>();
                        if (compRespawnBed != null)
                        {
                            compRespawnBed.parentTree = (Building_Tentacle_Altar)parent;
                        }
                        return;
                    }
                }
                bed = (Building_BaseBondageBed)GenSpawn.Spawn(Props.bedDef, parent.Position, parent.Map);
                bed.SetFaction(Faction.OfPlayer, null);
                bed.Rotation = Rot4.South;
                compRespawnBed = bed.TryGetComp<CompRespawnBed>();
                if (compRespawnBed != null)
                {
                    compRespawnBed.parentTree = (Building_Tentacle_Altar)parent;
                }
                return;
            }
        }

        public override void CompTick()
        {
            if (parent.Spawned && parent.IsHashIntervalTick(250)) CheckBed();
        }
        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            if (!bed.DestroyedOrNull())
            {
                var link = bed.TryGetComp<CompRespawnBed>();
                if (link != null) link.parentTreeDestroyed = true;
                bed.Destroy();
            }
        }
    }
}
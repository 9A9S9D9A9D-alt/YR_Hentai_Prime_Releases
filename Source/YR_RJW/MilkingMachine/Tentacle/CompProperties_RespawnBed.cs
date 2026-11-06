using UnityEngine;
using Verse;

namespace YR_RJW
{
    public class CompProperties_RespawnBed : CompProperties
    {
        public CompProperties_RespawnBed()
        {
            compClass = typeof(CompRespawnBed);
        }
        public Vector2 drawsize = new Vector2(5, 5);
        public Vector3 adjustOffsetBeforeSetOccupant = new Vector3(0, 0, -1);
    }
    public class CompRespawnBed : Comp_Building_BaseBondageBed
    {
        public Building_Tentacle_Altar parentTree = null;
        public bool parentTreeDestroyed = false;
        public CompProperties_RespawnBed Props => (CompProperties_RespawnBed)props;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_References.Look(ref parentTree, "parentTree");
        }
        //public override void PostExposeData()
        //{
        //    Scribe_References.Look(ref parentTree, "parentTree");
        //}

        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            if (!parentTreeDestroyed && !parentTree.DestroyedOrNull())
            {
                CompSpawnBed compSpawnBed = parentTree.TryGetComp<CompSpawnBed>();
                if (compSpawnBed != null)
                {
                    compSpawnBed.CheckBed();
                }
            }
        }

        //public override void PostSpawnSetup(bool respawningAfterLoad)
        //{
        //    base.PostSpawnSetup(respawningAfterLoad);

        //    if (parent is Building_BaseBondageBed bbb)
        //    {
        //        if (bbb.occupant != null)
        //        {
        //            Log.Error("3");
        //            if (parentTree != null)
        //            {
        //                parentTree.TempDrawSize = Props.drawsize;
        //                parentTree.drawSizeChange = true;
        //                parentTree.Draw();
        //            }

        //        }
        //        else
        //        {
        //            Log.Error("4");
        //            if (parentTree != null)
        //            {
        //                parentTree.TempOffset = parentTree.DrawPos + Props.adjustOffsetBeforeSetOccupant;
        //                parentTree.drawSizeChange = false;
        //                parentTree.Draw();
        //            }
        //        }
        //    }
        //}

        //public override void PostSetOccupant()
        //{
        //    base.PostSetOccupant();
        //    if (parent is Building_BaseBondageBed bbb && bbb.occupant != null)
        //    {
        //        Log.Error("5");
        //        if (parentTree != null)
        //        {
        //            Log.Error("6");
        //            parentTree.TempDrawSize = Props.drawsize;
        //            parentTree.drawSizeChange = true;
        //            parentTree.Draw();
        //        }
        //    }
        //}
        //public override void PostRemoveOccupant()
        //{
        //    base.PostRemoveOccupant();
        //    if (parent is Building_BaseBondageBed bbb && bbb.occupant == null)
        //    {
        //        Log.Error("7");
        //        if (parentTree != null)
        //        {
        //            Log.Error("8");
        //            parentTree.TempOffset = parentTree.DrawPos + Props.adjustOffsetBeforeSetOccupant;
        //            parentTree.drawSizeChange = false;
        //            parentTree.Draw();
        //        }
        //    }
        //}
    }
}
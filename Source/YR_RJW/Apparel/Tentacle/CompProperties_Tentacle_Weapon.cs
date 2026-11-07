using Verse;

namespace YR_RJW
{
    public class CompProperties_Tentacle_Weapon : CompProperties
    {
        public CompProperties_Tentacle_Weapon()
        {
            compClass = typeof(Comp_Tentacle_Weapon);
        }
    }

    public class Comp_Tentacle_Weapon : ThingComp
    {
        private Pawn statePawn;
        public override void Notify_Equipped(Pawn pawn)
        {
            base.Notify_Equipped(pawn);
            if (parent.def.defName == "OY_Tentacle_Dagger" || parent.def.defName == "OY_Tentacle_Bow")
                Hediff_SamokAssassin.Ensure(pawn);
        }
        public override void CompTick()
        {
            if (ParentHolder is Pawn_EquipmentTracker tracker && tracker.pawn != statePawn)
            {
                statePawn = tracker.pawn;
                if (parent.def.defName == "OY_Tentacle_Dagger" || parent.def.defName == "OY_Tentacle_Bow")
                    Hediff_SamokAssassin.Ensure(statePawn);
            }
        }
        private bool destroty = false;
        private bool crafted;

        public override void Notify_RecipeProduced(Pawn pawn)
        {
            base.Notify_RecipeProduced(pawn);
            crafted = true;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref crafted, "samokWeaponCrafted", false);
        }

        private CompProperties_Tentacle_Weapon Props => (CompProperties_Tentacle_Weapon)props;
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (!crafted && (destroty || !(ParentHolder is Pawn_EquipmentTracker)))
            {
                if (!parent.Destroyed)
                {
                    parent.Destroy();
                }
            }
        }

        public override void Notify_Unequipped(Pawn pawn)
        {
            Hediff_SamokAssassin.Get(pawn)?.Reveal();
            statePawn = null;
            if (!parent.Destroyed)
            {
                destroty = true;
            }
        }
    }
}

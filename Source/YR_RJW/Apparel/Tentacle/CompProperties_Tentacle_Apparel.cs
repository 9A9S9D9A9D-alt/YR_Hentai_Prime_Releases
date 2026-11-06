using RimWorld;
using Verse;

namespace YR_RJW
{
    public class CompProperties_Tentacle_Apparel : CompProperties
    {
        public CompProperties_Tentacle_Apparel()
        {
            compClass = typeof(Comp_Tentacle_Apparel);
        }

        public int ticks = 10;
        public int hpRecovery = 1;
    }

    public class Comp_Tentacle_Apparel : ThingComp
    {
        public bool addon25Style;
        public override void PostExposeData(){base.PostExposeData();Scribe_Values.Look(ref addon25Style,"tentacleArmorAddon25",false);}
        public static bool UsesAddon25(Pawn pawn)=>pawn?.apparel?.WornApparel.Exists(a=>a.def.defName=="YR_AP_Tentacle" && a.TryGetComp<Comp_Tentacle_Apparel>()?.addon25Style==true)==true;
        public override System.Collections.Generic.IEnumerable<Gizmo> CompGetWornGizmosExtra(){
            foreach(var gizmo in base.CompGetWornGizmosExtra())yield return gizmo;
            if(parent.def.defName!="YR_AP_Tentacle")yield break;
            yield return new Command_Toggle {defaultLabel="촉수아머: 25형",
                defaultDesc="기존 원본과 제공된 25형 외형을 전환합니다. 능력과 장착 상태는 유지됩니다.",icon=ApparelCommandIcons.For(parent.def),iconDrawScale=.85f,
                isActive=()=>addon25Style,toggleAction=()=>{addon25Style=!addon25Style;(parent as Apparel)?.Wearer?.Drawer?.renderer?.SetAllGraphicsDirty();}};
        }
        private int ticks = 0;
        private bool destroty = false;
        private CompProperties_Tentacle_Apparel Props => (CompProperties_Tentacle_Apparel)props;
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (destroty || !(ParentHolder is Pawn_ApparelTracker))
            {
                if (!parent.Destroyed)
                {
                    parent.Destroy();
                }
            }
        }

        public override void Notify_Unequipped(Pawn pawn)
        {
            if (!parent.Destroyed)
            {
                destroty = true;
            }
        }

        public override void CompTick()
        {
            ticks--;
            if (ticks <= 0)
            {
                ticks = Props.ticks;
                if (parent is Apparel ap)
                {
                    if (ap.Wearer != null)
                    {
                        ap.Wearer.apparel.Lock(ap);

                        if (parent.HitPoints < parent.MaxHitPoints)
                        {
                            if (parent.MaxHitPoints - parent.HitPoints < Props.hpRecovery)
                            {
                                parent.HitPoints += parent.MaxHitPoints - parent.HitPoints;
                            }
                            else
                            {
                                parent.HitPoints += Props.hpRecovery;
                            }
                        }
                    }
                }
            }
        }
    }
}

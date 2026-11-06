using RimWorld;
using System.Collections.Generic;
using Verse;

namespace YR_RJW
{
    // Port of the recovered 1.4 altar: burial contributions and fuel are separate resources.
    public class CompProperties_Building_Tentacle_Altar : CompProperties
    {
        public int needCorpseCount = 2;
        public int maxCorpseCount = 4;
        public ThingDef thingDef;
        public GraphicData graphicData;
        public CompProperties_Building_Tentacle_Altar() => compClass = typeof(Comp_Building_Tentacle_Altar);
    }

    public class Comp_Building_Tentacle_Altar : ThingComp
    {
        public CompProperties_Building_Tentacle_Altar Props => (CompProperties_Building_Tentacle_Altar)props;
        public bool IsAltar => Props.thingDef != null;
        public int Required => YR_Hentai_Prime_AnimationBed.TreeRules.Required(IsAltar
            ? YR_Hentai_Prime_AnimationBed.YR_Hentai_Prime_AnimationBed_Mod.Settings?.altarCorpses ?? Props.needCorpseCount
            : YR_Hentai_Prime_AnimationBed.YR_Hentai_Prime_AnimationBed_Mod.Settings?.growthCorpses ?? Props.needCorpseCount);
        public int Maximum => System.Math.Max(Required, Props.maxCorpseCount);
        public override string CompInspectStringExtra()
        {
            int offered = ((Building_Tentacle_Altar)parent).offeredCorpse;
            if (offered >= Maximum) return "YRP_OfferingMaximum".Translate(offered, Maximum);
            if (offered < Required) return "YRP_OfferingNeeded".Translate(offered, Required - offered, Required, Maximum);
            return "YRP_OfferingExtra".Translate(offered, Maximum - offered, Maximum);
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (!Prefs.DevMode) yield break;
            var altar = (Building_Tentacle_Altar)parent;
            var add = new Command_Action {
                defaultLabel = "YRP_DebugAddOffering".Translate(),
                defaultDesc = "YRP_DebugOfferingDescription".Translate(),
                icon = ContentFinder<UnityEngine.Texture2D>.Get("Yuran/UI/Commands/SamokOfferingAdd"),
                action = () => altar.DebugAddOffering(false)
            };
            var maximum = new Command_Action {
                defaultLabel = "YRP_DebugMaxOffering".Translate(),
                defaultDesc = "YRP_DebugOfferingDescription".Translate(),
                icon = ContentFinder<UnityEngine.Texture2D>.Get("Yuran/UI/Commands/SamokOfferingMax"),
                action = () => altar.DebugAddOffering(true)
            };
            if (!(YR_Hentai_Prime_AnimationBed.YR_Hentai_Prime_AnimationBed_Mod.Settings?.enableSamok ?? true))
            {
                add.Disable("YRP_Disabled".Translate());
                maximum.Disable("YRP_Disabled".Translate());
            }
            else if (altar.offeredCorpse >= Maximum)
            {
                add.Disable("YRP_OfferingMaximum".Translate(altar.offeredCorpse, Maximum));
                maximum.Disable("YRP_OfferingMaximum".Translate(altar.offeredCorpse, Maximum));
            }
            yield return add;
            yield return maximum;
        }
    }

    public class Building_Tentacle_Altar : Building_Grave
    {
        public int offeredCorpse;
        private Comp_Building_Tentacle_Altar Altar => GetComp<Comp_Building_Tentacle_Altar>();
        public bool CorpseFulfilled => Altar != null && offeredCorpse >= Altar.Required;
        public override bool Accepts(Thing thing) =>
            (YR_Hentai_Prime_AnimationBed.YR_Hentai_Prime_AnimationBed_Mod.Settings?.enableSamok ?? true) &&
            Altar != null && offeredCorpse < Altar.Maximum && base.Accepts(thing);

        // RimWorld 1.7 replaced Notify_CorpseBuried with Notify_HauledTo.
        public override void Notify_HauledTo(Pawn worker, Thing thing, int count)
        {
            base.Notify_HauledTo(worker, thing, count);
            if (Corpse == null || Altar == null) return;
            Corpse.Destroy();
            offeredCorpse++;
            CompleteOffering();
        }

        public void DebugAddOffering(bool maximum)
        {
            if (!Prefs.DevMode || !Spawned || Altar == null ||
                !(YR_Hentai_Prime_AnimationBed.YR_Hentai_Prime_AnimationBed_Mod.Settings?.enableSamok ?? true)) return;
            offeredCorpse = maximum ? Altar.Maximum : System.Math.Min(offeredCorpse + 1, Altar.Maximum);
            CompleteOffering();
        }

        private void CompleteOffering()
        {
            if (!CorpseFulfilled || Altar.Props.thingDef == null) return;
            Thing tree = ThingMaker.MakeThing(Altar.Props.thingDef);
            tree.SetFaction(Faction);
            Map map = Map;
            IntVec3 position = Position;
            Destroy(DestroyMode.Vanish);
            GenSpawn.Spawn(tree, position, map);
            Messages.Message("YRP_TreeSummoned".Translate(), tree, MessageTypeDefOf.PositiveEvent);
        }

        public override Graphic Graphic => CorpseFulfilled && Altar.Props.graphicData != null
            ? Altar.Props.graphicData.Graphic : base.Graphic;
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref offeredCorpse, "offeredCorpse", 0);
        }
    }
}

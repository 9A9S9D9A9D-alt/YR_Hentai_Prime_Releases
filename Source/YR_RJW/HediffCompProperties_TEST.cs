using AlienRace;
using Verse;

namespace YR_RJW
{

    public class HediffCompProperties_TEST : HediffCompProperties
    {
        public HediffCompProperties_TEST()
        {
            compClass = typeof(HediffComp_TEST);
        }
    }


    public class HediffComp_TEST : HediffComp
    {
        private HediffCompProperties_TEST Props => (HediffCompProperties_TEST)props;
        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            Pawn.health.RemoveHediff(parent);
        }
        public override void CompPostMake()
        {
            base.CompPostMake();

            Log.Error("========");
            Log.Error(Pawn.Label);
            Log.Error(Pawn.def.defName);
            if (Pawn.def is ThingDef_AlienRace alienDef)
            {
                AlienPartGenerator alienPartGenerator = alienDef.alienRace.generalSettings.alienPartGenerator;
                ThingDef_AlienRace.AlienSettings alienRace = alienPartGenerator.alienProps.alienRace;

                Log.Error("1 : bodytypeGraphics : ");
                AlienPartGenerator.ExtendedGraphicTop body = alienRace.graphicPaths.body;
                foreach (AlienRace.ExtendedGraphics.IExtendedGraphic bodytypeGraphic in body.GetSubGraphics())
                {
                    Log.Error(bodytypeGraphic.GetPath());
                }
                Log.Error("-------");

                Log.Error("2 : individualPaths : ");
                System.Collections.Generic.Dictionary<ThingDef, AlienPartGenerator.ExtendedGraphicTop> individualPaths = alienRace.graphicPaths.apparel.individualPaths;
                foreach (AlienPartGenerator.ExtendedGraphicTop extendedGraphicTop in individualPaths.Values)
                {
                    foreach (AlienRace.ExtendedGraphics.IExtendedGraphic bodytypeGraphic in extendedGraphicTop.GetSubGraphics())
                    {
                        Log.Error(bodytypeGraphic.GetPath());
                    }
                }
            }
            Log.Error("========");
        }
    }
}

using RimWorld;
using Verse;

namespace YR_RJW
{
    public class CompProperties_Golem_Apparel : CompProperties
    {
        public CompProperties_Golem_Apparel()
        {
            compClass = typeof(Comp_Golem_Apparel);
        }
    }

    public class Comp_Golem_Apparel : ThingComp
    {
        private CompProperties_Golem_Apparel Props => (CompProperties_Golem_Apparel)props;

        public Apparel Apparel => parent as Apparel;
        public Pawn Wearer => Apparel.Wearer;

        //public override void PostDraw()
        //{
        //    base.PostDraw();
        //    if (Wearer != null)
        //    {
        //        Log.Error("1");
        //        this.parent.Graphic.Draw(Wearer.DrawPos, Wearer.Rotation, Wearer);
        //    }
        //}
    }
}

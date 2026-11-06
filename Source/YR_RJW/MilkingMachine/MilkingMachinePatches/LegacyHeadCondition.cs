using System;
using AlienRace;
using AlienRace.ExtendedGraphics;

namespace YR_RJW
{
    // Yuran 1.6+ replaced four HeadTypeDefs with numbered graphic variants.
    // Keep the four original expression choices without referring to deleted defs.
    public class LegacyHeadCondition : AlienRace.ExtendedGraphics.Condition
    {
        public string legacyHead;
        public override bool Static => true;
        public override bool Satisfied(ExtendedGraphicsPawnWrapper wrapper, ref ResolveData data)
        {
            var pawn=wrapper.WrappedPawn;
            string current=pawn.story?.headType?.defName;
            if (current==legacyHead) return true;
            if (current==null || legacyHead==null) return false;
            bool miko=legacyHead.EndsWith("Miko",StringComparison.Ordinal);
            if (current!=(miko ? "YR_Female_AverageNormalAzeroMiko" : "YR_Female_AverageNormalAzero")) return false;
            string[] names={"one","two","three","four"};
            var comp=pawn.GetComp<AlienPartGenerator.AlienComp>();
            if (comp==null) return false;
            for(int i=0;i<names.Length;i++)
                if(legacyHead=="YR_Female_AverageNormalA"+names[i]+(miko?"Miko":""))
                    return comp.headVariant==i+1;
            return false;
        }
    }
}

using UnityEngine;
using Verse;

namespace YR_RJW
{
    public class CompProperties_SamokCoffinVisual : CompProperties
    {
        public CompProperties_SamokCoffinVisual()
        {
            compClass = typeof(CompSamokCoffinVisual);
        }
    }

    // Cosmetic fallback only. The shared facility graphics now draws the bound corpse.
    // No saved state: the source is always the deeply saved coffin contents.
    public class CompSamokCoffinVisual : ThingComp
    {
        public override void PostDraw()
        {
            var bed = parent as Building_BaseBondageBed;
            Corpse corpse = bed?.CoffinCorpse;
            if (bed?.IsSamokCoffin != true || !bed.Spawned || corpse?.InnerPawn?.Dead != true ||
                corpse.Destroyed || !YR_Hentai_Prime_AnimationBed.UnsupportedRacesV8.Allows(corpse.InnerPawn.def) || bed.Position.Fogged(bed.Map)) return;

            var animation=bed.TryGetComp<CompChangeBedTexture>();
            if(animation!=null && animation.GDAV3s.Exists(layer=>layer.isBodyGraphic||layer.isPawnBodyReplacement))return;
            Vector3 position = bed.DrawPos + new Vector3(0f, 0.05f, 0f);
            // Rebuild native draw results here rather than reuse the last ground origin.
            corpse.DynamicDrawPhaseAt(DrawPhase.EnsureInitialized, position);
            corpse.DynamicDrawPhaseAt(DrawPhase.ParallelPreDraw, position);
            corpse.DynamicDrawPhaseAt(DrawPhase.Draw, position);
        }
    }
}

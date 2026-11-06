using HarmonyLib;
using UnityEngine;
using Verse;

namespace YR_Hentai_Prime_AnimationBed

{
    [StaticConstructorOnStartup]
    public class YR_Hentai_Prime_AnimationBed_Mod : Mod
    {
        public YR_Hentai_Prime_AnimationBed_Mod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<YR_Hentai_Prime_AnimationBed_Setting>();

            Harmony harmony = new Harmony("YR_Hentai_Prime_AnimationBed_Mod.Patch");
            harmony.PatchAll();
        }

        public override string SettingsCategory()
        {
            return "YR_Hentai_Prime_AnimationBed_Mod".Translate();
        }
        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);
            listing.Label("YRP_AdultRule".Translate());
            listing.Label("YRP_MinimumAge".Translate(Settings.minimumAge));
            Settings.minimumAge = Mathf.RoundToInt(listing.Slider(Settings.minimumAge, 18, 100));
            listing.CheckboxLabeled("YRP_Animations".Translate(), ref Settings.enableAnimations);
            listing.CheckboxLabeled("YRP_Sounds".Translate(), ref Settings.enableSounds);
            listing.CheckboxLabeled("YRP_Portraits".Translate(), ref Settings.enablePortraits);
            listing.CheckboxLabeled("YRP_RjwGuard".Translate(), ref Settings.preventRjwJobConflicts);
            listing.Gap();
            listing.CheckboxLabeled("YRP_Samok".Translate(), ref Settings.enableSamok);
            listing.Label("YRP_AltarCost".Translate(Settings.altarCorpses));
            Settings.altarCorpses = Mathf.RoundToInt(listing.Slider(Settings.altarCorpses, 1, 100));
            listing.Label("YRP_GrowthCost".Translate(Settings.growthCorpses));
            Settings.growthCorpses = Mathf.RoundToInt(listing.Slider(Settings.growthCorpses, 1, 100));
            listing.End();
        }

        // Token: 0x04000001 RID: 1
        public static YR_Hentai_Prime_AnimationBed_Setting Settings;
    }
}

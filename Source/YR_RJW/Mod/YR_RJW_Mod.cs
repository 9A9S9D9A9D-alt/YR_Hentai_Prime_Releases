using HarmonyLib;
using UnityEngine;
using Verse;

namespace YR_RJW

{
    [StaticConstructorOnStartup]
    public class YR_RJW_Mod : Mod
    {
        public YR_RJW_Mod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<YR_RJW_Setting>();

            Harmony harmony = new Harmony("YR_RJW_Mod.Patch");
            harmony.PatchAll();
        }

        public override string SettingsCategory()
        {
            return "YR_RJW_Mod".Translate();
        }
        public override void DoSettingsWindowContents(Rect inRect)
        {
            Settings.DoSettingsWindowContents(inRect);
        }

        // Token: 0x04000001 RID: 1
        public static YR_RJW_Setting Settings;
    }
}
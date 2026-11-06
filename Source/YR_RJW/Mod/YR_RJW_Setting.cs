using UnityEngine;
using Verse;


namespace YR_RJW
{
    [StaticConstructorOnStartup]
    public class YR_RJW_Setting : ModSettings
    {
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref YR_Original_Ticks, "YR_Original_Ticks", 3f);
            Scribe_Values.Look(ref YR_Original_Normal, "YR_Original_Normal", 1f);
            Scribe_Values.Look(ref YR_Original_Fast, "YR_Original_Fast", 3f);
            Scribe_Values.Look(ref YR_Original_Superfast, "YR_Original_Superfast", 4f);
            Scribe_Values.Look(ref YR_Original_Ultrafast, "YR_Original_Ultrafast", 7f);
            Scribe_Values.Look(ref YR_StopAnimation, "YR_StopAnimation", false);
            Scribe_Values.Look(ref YR_ShowHat, "YR_ShowHat", false);
            Scribe_Values.Look(ref YR_ShowApparel, "YR_ShowApparel", false);
            Scribe_Values.Look(ref YR_SexSound, "YR_SexSound", false);
            Scribe_Values.Look(ref YR_TestLog, "YR_TestLog", false);
            Scribe_Values.Look(ref YR_zoom, "YR_zoom", 4f);
            Scribe_Values.Look(ref YR_BoundGUI, "YR_BoundGUI");
            Scribe_Values.Look(ref YR_PerformanceMod, "YR_PerformanceMod");
        }
        public void DoSettingsWindowContents(Rect canvas)
        {
            Listing_Standard listing_Standard = new Listing_Standard
            {
                ColumnWidth = (canvas.width - 34f) / 2f
            };
            listing_Standard.Begin(canvas);

            listing_Standard.CheckboxLabeled("YR_TestLog".Translate(), ref YR_TestLog);

            listing_Standard.CheckboxLabeled("YR_StopAnimation".Translate(), ref YR_StopAnimation);

            listing_Standard.CheckboxLabeled("YR_ShowHat".Translate(), ref YR_ShowHat);

            listing_Standard.CheckboxLabeled("YR_ShowApparel".Translate(), ref YR_ShowApparel);

            listing_Standard.CheckboxLabeled("YR_SexSound".Translate(), ref YR_SexSound);

            listing_Standard.CheckboxLabeled("YR_BoundGUI".Translate(), ref YR_BoundGUI);

            //listing_Standard.CheckboxLabeled("YR_PerformanceMod".Translate(), ref YR_PerformanceMod);

            listing_Standard.Label("YR_Speed_Warning".Translate(), -1f, null);
            listing_Standard.Gap(10f);

            SpeedSlider(listing_Standard, "YR_Speed_Ticks", ref YR_Original_Ticks, false, 5f);
            SpeedSlider(listing_Standard, "YR_Speed_Normal", ref YR_Original_Normal, false, 5f);
            SpeedSlider(listing_Standard, "YR_Speed_Fast", ref YR_Original_Fast, false, 5f);
            SpeedSlider(listing_Standard, "YR_Speed_Superfast", ref YR_Original_Superfast, false, 5f);
            SpeedSlider(listing_Standard, "YR_Speed_Ultrafast", ref YR_Original_Ultrafast, false, 5f);

            listing_Standard.Label("YR_zoom".Translate((YR_zoom * 6).ToString()), -1f, null);
            YR_zoom = (int)listing_Standard.Slider(YR_zoom, 1f, 10f);
            listing_Standard.Gap(5f);



            //리셋
            if (listing_Standard.ButtonText("YR_ResetAllSetting".Translate(), null))
            {
                YR_StopAnimation = false;
                YR_ShowHat = true;
                YR_ShowApparel = true;
                YR_SexSound = true;
                YR_TestLog = false;
                YR_BoundGUI = true;
                YR_PerformanceMod = false;
                YR_Original_Ticks = 3;
                YR_Original_Normal = 1;
                YR_Original_Fast = 3;
                YR_Original_Superfast = 4;
                YR_Original_Ultrafast = 7;
                YR_zoom = 4;
            }

            //끝
            listing_Standard.End();
        }
        static YR_RJW_Setting()
        {
            LongEventHandler.ExecuteWhenFinished(delegate ()
            {
                //Mincho_Mintchoco.ingestible.optimalityOffsetHumanlikes = MintchocoOOH;
            });
        }

        private static void SpeedSlider(Listing_Standard listing_Standard, string speedLabel, ref float OriginalMuitiple, bool writeGapLine = true, float linegap = 10f)
        {
            listing_Standard.Label(speedLabel.Translate(OriginalMuitiple.ToString()), -1f, null);
            OriginalMuitiple = (int)listing_Standard.Slider(OriginalMuitiple, 1f, 100f);
            listing_Standard.Gap(linegap);
            if (writeGapLine)
            {
                listing_Standard.GapLine(10f);
            }
        }


        public static float YR_Original_Ticks = 3;
        public static float YR_Original_Normal = 1;
        public static float YR_Original_Fast = 3;
        public static float YR_Original_Superfast = 4;
        public static float YR_Original_Ultrafast = 7f;
        public static bool YR_StopAnimation = false;
        public static bool YR_ShowHat = true;
        public static bool YR_ShowApparel = true;
        public static bool YR_SexSound = true;
        public static bool YR_TestLog = false;
        public static float YR_zoom = 4f;
        public static bool YR_BoundGUI = false;
        public static bool YR_PerformanceMod = false;

        private Vector2 scrollPosition = new Vector2(0f, 0f);

        public static void SetTicksByCurTimeSpeed(ref int ticks, int propsTicks)
        {
            TickManager tickManager = Find.TickManager;

            int baseTicks = (int)(YR_Original_Ticks + propsTicks);

            if (tickManager.CurTimeSpeed == TimeSpeed.Normal)
            {
                ticks = (int)(baseTicks * YR_Original_Normal);
            }
            else if (tickManager.CurTimeSpeed == TimeSpeed.Fast)
            {
                ticks = (int)(baseTicks * YR_Original_Fast);
            }
            else if (tickManager.CurTimeSpeed == TimeSpeed.Superfast)
            {
                ticks = (int)(baseTicks * YR_Original_Superfast);
            }
            else if (tickManager.CurTimeSpeed == TimeSpeed.Ultrafast)
            {
                ticks = (int)(baseTicks * YR_Original_Ultrafast);
            }
        }

    }
}
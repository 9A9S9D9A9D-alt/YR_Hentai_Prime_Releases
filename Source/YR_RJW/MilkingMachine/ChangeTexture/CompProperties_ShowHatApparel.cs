using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace YR_RJW
{
    public class CompProperties_ShowHatApparel : CompProperties
    {
        public CompProperties_ShowHatApparel()
        {
            compClass = typeof(CompShowHatApparel);
        }

        public bool showHatNonGizmo;
        public bool showApparelNonGizmo;
        public bool showHatGizmo = true;
        public string showHatCommandLabel = "YR_ShowHatCommandLabel";
        public string showHatCommandDesc = "YR_ShowHatCommandDesc";
        public string showHatIconPath = "Yuran/Icon/ShowHat";
        public string dontShowHatIconPath = "Yuran/Icon/DontShowHat";
        public string noHatPath = "Yuran/Yuranlike/Bodies/Dummy/YR_Bote";

        public bool butShowHatAddon = false;

        public bool showApparelGizmo = true;
        public string showApparelCommandLabel = "YR_ShowApparelCommandLabel";
        public string showApparelCommandDesc = "YR_ShowApparelCommandDesc";
        public string showApparelIconPath = "Yuran/Icon/ShowApparel";
        public string dontShowApprelIconPath = "Yuran/Icon/DontShowApparel";
        public string noApparelPath = "Yuran/Yuranlike/Bodies/Dummy/YR_Bote";

        public bool butShowApparelAddon = false;
    }
    public class CompShowHatApparel : Comp_Building_BaseBondageBed
    {
        public bool showHat = false;
        public bool showApparel = false;

        public CompProperties_ShowHatApparel Props => (CompProperties_ShowHatApparel)props;

        public override void PostSetOccupant(Pawn pawn)
        {
            if (!showHat && !Props.butShowHatAddon)
            {
                pawn.health.AddHediff(YR_RJW_DefOf.YR_HideHat_Hediff);
            }
            if (!showApparel && !Props.butShowApparelAddon)
            {
                pawn.health.AddHediff(YR_RJW_DefOf.YR_HideApparel_Hediff);
            }
            var texture = parent.TryGetComp<CompChangeBedTexture>();
            if (Props.showHatNonGizmo)
            {
                showHat = true;
                if (texture != null) texture.showHat = true;
            }
            if (Props.showApparelNonGizmo)
            {
                showApparel = true;
                if (texture != null) texture.showApparel = true;
            }
        }
        public override void PostRemoveOccupant(Pawn pawn)
        {
            foreach (Hediff hediff in pawn.health.hediffSet.hediffs.ToList())
            {
                if (hediff.def == YR_RJW_DefOf.YR_HideHat_Hediff)
                {
                    pawn.health.RemoveHediff(hediff);
                }
            }
            foreach (Hediff hediff in pawn.health.hediffSet.hediffs.ToList())
            {
                if (hediff.def == YR_RJW_DefOf.YR_HideApparel_Hediff)
                {
                    pawn.health.RemoveHediff(hediff);
                }
            }
        }

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref showHat, "showHat", false);
            Scribe_Values.Look(ref showApparel, "showApparel", false);
        }
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            Pawn pawn = null;
            if (parent is Building_BaseBondageBed BBB)
            {
                pawn = BBB.occupant;
            }

            ShowSetting(showHat, YR_RJW_Setting.YR_ShowHat, Props.showHatIconPath, Props.dontShowHatIconPath, Props.showHatCommandDesc, out string showHatCommandSetting, out string showHatModSetting, out Texture showHatIcon, out string showHatDefaultDesc);


            ShowSetting(showApparel, YR_RJW_Setting.YR_ShowApparel, Props.showApparelIconPath, Props.dontShowApprelIconPath, Props.showApparelCommandDesc, out string showApparelCommandSetting, out string showApparelModSetting, out Texture showApparelIcon, out string showApparelDefaultDesc);


            if (Props.showHatGizmo)
            {
                Command_Action showHatCommand = new Command_Action
                {
                    defaultLabel = Props.showHatCommandLabel.Translate(),
                    defaultDesc = showHatDefaultDesc,
                    icon = showHatIcon,
                    action = delegate ()
                    {
                        CompChangeBedTexture compChangeBedTexture = parent.TryGetComp<CompChangeBedTexture>();

                        if (showHat)
                        {
                            showHat = false;
                            if (compChangeBedTexture != null)
                            {
                                compChangeBedTexture.ticks = 0;
                                compChangeBedTexture.needMakeGrapgicList = true;
                            }

                            pawn.health.AddHediff(YR_RJW_DefOf.YR_HideHat_Hediff);
                        }
                        else
                        {
                            showHat = true;
                            if (compChangeBedTexture != null)
                            {
                                compChangeBedTexture.ticks = 0;
                                compChangeBedTexture.needMakeGrapgicList = true;
                            }
                            foreach (Hediff hediff in pawn.health.hediffSet.hediffs.ToList())
                            {
                                if (hediff.def == YR_RJW_DefOf.YR_HideHat_Hediff)
                                {
                                    pawn.health.hediffSet.hediffs.Remove(hediff);
                                }
                            }
                        }
                        compChangeBedTexture.showHat = showHat;
                        if (pawn != null)
                        {
                            pawn.Drawer.renderer.SetAllGraphicsDirty();
                        }
                    }
                };
                yield return showHatCommand;
            }


            if (Props.showApparelGizmo)
            {
                Command_Action showApparelCommand = new Command_Action
                {
                    defaultLabel = Props.showApparelCommandLabel.Translate(),
                    defaultDesc = showApparelDefaultDesc,
                    icon = showApparelIcon,
                    action = delegate ()
                    {
                        CompChangeBedTexture compChangeBedTexture = parent.TryGetComp<CompChangeBedTexture>();
                        if (showApparel)
                        {
                            showApparel = false;
                            if (compChangeBedTexture != null)
                            {
                                compChangeBedTexture.ticks = 0;
                                compChangeBedTexture.needMakeGrapgicList = true;
                            }

                            pawn.health.AddHediff(YR_RJW_DefOf.YR_HideApparel_Hediff);
                        }
                        else
                        {
                            showApparel = true;
                            if (compChangeBedTexture != null)
                            {
                                compChangeBedTexture.ticks = 0;
                                compChangeBedTexture.needMakeGrapgicList = true;
                            }
                            foreach (Hediff hediff in pawn.health.hediffSet.hediffs.ToList())
                            {
                                if (hediff.def == YR_RJW_DefOf.YR_HideApparel_Hediff)
                                {
                                    pawn.health.hediffSet.hediffs.Remove(hediff);
                                }
                            }
                        }

                        if (pawn != null)
                        {
                            pawn.Drawer.renderer.SetAllGraphicsDirty();
                        }
                        compChangeBedTexture.showApparel = showApparel;
                    }
                };
                yield return showApparelCommand;
            }
        }

        private void ShowSetting(bool show, bool showSetting, string iconPath, string fobiddenIconPath, string showCommandDesc, out string showCommandSetting, out string showModSetting, out Texture showIcon, out string showDefaultDesc)
        {
            showCommandSetting = !show ? (string)"Disable".Translate() : (string)"Enable".Translate();

            showModSetting = !showSetting ? (string)"Disable".Translate() : (string)"Enable".Translate();

            showIcon = !show || !showSetting ? ContentFinder<Texture2D>.Get(fobiddenIconPath) : (Texture)ContentFinder<Texture2D>.Get(iconPath);

            showDefaultDesc = showCommandDesc.Translate(showCommandSetting, showModSetting);
        }
    }
}

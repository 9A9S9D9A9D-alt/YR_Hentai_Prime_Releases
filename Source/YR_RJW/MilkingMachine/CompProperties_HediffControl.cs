using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace YR_RJW
{
    public class CompProperties_HediffControl : CompProperties_Building_BaseBondageBed
    {
        public CompProperties_HediffControl()
        {
            compClass = typeof(Comp_HediffControl);
        }
        public List<HediffGizmo> hediffGizmos = new List<HediffGizmo>();
    }
    public class HediffGizmo
    {
        public List<HediffDef> hediffDefs = new List<HediffDef>();
        public Condition condition;
        public string disabledReason = "";
        public string label = "";
        public string iconPath = "Ui/Widgets/RadioButOn";
        //null화 시키는거 여부 추가해야 하나?
    }

    public class Comp_HediffControl : Comp_Building_BaseBondageBed
    {
        public CompProperties_HediffControl Props => (CompProperties_HediffControl)props;
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (var hediffGizmo in Props.hediffGizmos)
            {
                var command = new Command_Action
                {
                    defaultLabel = hediffGizmo.label.Translate(),
                    disabledReason = hediffGizmo.disabledReason.Translate(),
                    icon = ContentFinder<Texture2D>.Get(hediffGizmo.iconPath),
                    action = delegate ()
                    {
                        if (!LegacyEligibility.Allows(pawn) || !CompChangeBedTexture.ConditionMatch(pawn, hediffGizmo.condition)) return;
                        var pawnHediffDefs = pawn.health.hediffSet.hediffs.Select(x => x.def).ToList();
                        List<HediffDef> hediffDefs = hediffGizmo.hediffDefs;


                        int index = hediffDefs.FindIndex(item => pawnHediffDefs.Contains(item));

                        //만약 있을 경우

                        if (index >= 0)
                        {
                            foreach (var hediff in pawn.health.hediffSet.hediffs.ToList())
                            {
                                if (hediff.def == hediffDefs[index])
                                {
                                    pawn.health.RemoveHediff(hediff);
                                }
                            }
                        }

                        HediffDef hediffDef = null;
                        //만약 헤디프가 있으면 리스트의 다음 헤디프를 부여
                        if (index >= 0 && index < hediffDefs.Count - 1)
                        {
                            //Log.Error("1");
                            hediffDef = hediffDefs[index + 1];
                        }
                        else if (index == -1)
                        {
                            //Log.Error("2");
                            hediffDef = hediffDefs[0];
                            // do something with firstItem
                        }
                        else
                        {
                            //Log.Error("3");
                        }
                        if (hediffDef != null)
                        {
                            //Log.Error("Add");
                            //Log.Error(hediffDef.defName);
                            pawn.health.AddHediff(hediffDef);
                        }
                        else
                        {
                            //Log.Error("null");
                        }
                        //Log.Error("------");
                    }
                };
                if (!LegacyEligibility.Allows(pawn) || !CompChangeBedTexture.ConditionMatch(pawn, hediffGizmo.condition))
                    command.Disable(hediffGizmo.disabledReason.Translate());
                yield return command;
            }

            yield break;
        }
    }
}




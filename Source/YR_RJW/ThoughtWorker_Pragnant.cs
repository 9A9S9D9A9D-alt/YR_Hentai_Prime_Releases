using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace YR_RJW
{
    public class ThoughtWorker_Pregnant : ThoughtWorker
    {
        private readonly List<string> pregnantHediffDefnameList = new List<string>
        {
        "Pregnant",
        "RJW_pregnancy",
        "RJW_pregnancy_beast",
        "RJW_pregnancy_mech"
        };

        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            Hediff firstHediffOfDef = null;
            foreach (string hediffDefname in pregnantHediffDefnameList)
            {
                foreach (Hediff pawnHediff in p.health.hediffSet.hediffs)
                {
                    if (hediffDefname == pawnHediff.def.defName)
                    {
                        firstHediffOfDef = pawnHediff;
                    }
                }
            }

            return firstHediffOfDef == null || firstHediffOfDef.def.stages == null
                ? ThoughtState.Inactive
                : ThoughtState.ActiveAtStage(Mathf.Min(new int[]
            {
                firstHediffOfDef.CurStageIndex,
                firstHediffOfDef.def.stages.Count - 1,
                def.stages.Count - 1
            }));
        }
    }
}

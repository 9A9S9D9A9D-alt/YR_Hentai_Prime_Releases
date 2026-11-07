using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace YR_RJW {
 public sealed partial class HediffComp_CoccyxCombat {
  int emergencyTendWork;
  static readonly Func<SkillNeed_BaseBonus,int,float> AtLevel=AccessTools.MethodDelegate<Func<SkillNeed_BaseBonus,int,float>>(AccessTools.Method(typeof(SkillNeed_BaseBonus),"ValueAtLevel"));
  static float MedicineFive(StatDef stat){float value=stat.defaultBaseValue;foreach(var need in stat.skillNeedFactors.OfType<SkillNeed_BaseBonus>())if(need.skill==SkillDefOf.Medicine)value*=AtLevel(need,5);return stat.postProcessCurve?.Evaluate(value)??value;}
  public static float EmergencyTendQuality=>Mathf.Min(.7f,MedicineFive(StatDefOf.MedicalTendQuality));
  public static int EmergencyTendDuration=>Mathf.CeilToInt((int)AccessTools.Field(typeof(JobDriver_TendPatient),"BaseTendDuration").GetRawConstantValue()/MedicineFive(StatDefOf.MedicalTendSpeed));
  public void TickEmergencyTend(int delta){
   if(Pawn.Dead||!Pawn.Downed){emergencyTendWork=0;return;}
   var wounds=Pawn.health.hediffSet.hediffs.Where(h=>h is Hediff_Injury&&h.TendableNow()).ToList();
   if(wounds.Count==0){emergencyTendWork=0;return;}
   emergencyTendWork+=delta;if(emergencyTendWork<EmergencyTendDuration)return;
   emergencyTendWork-=EmergencyTendDuration;var batch=new List<Hediff>();TendUtility.GetOptimalHediffsToTendWithSingleTreatment(Pawn,false,batch,wounds);
   for(int i=0;i<batch.Count;i++)batch[i].Tended(EmergencyTendQuality,.7f,i);
  }
 }
}

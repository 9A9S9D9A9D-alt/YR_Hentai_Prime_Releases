using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace YR_RJW {
 [HarmonyPatch(typeof(Verb_MeleeAttackDamage),"ApplyMeleeDamageToTarget")]
 public static class SamokAmbushImpact {
  public struct ImpactState {public Map map;public Vector3 position;public float angle;}
  static void Prefix(Verb_MeleeAttackDamage __instance,LocalTargetInfo target,out ImpactState __state){__state=default(ImpactState);if(Hediff_SamokAssassin.Get(__instance.CasterPawn)?.ambushVerb!=__instance||!target.HasThing||!target.Thing.Spawned)return;__state.map=target.Thing.Map;__state.position=target.Thing.DrawPos;__state.angle=(target.Thing.DrawPos-__instance.CasterPawn.DrawPos).AngleFlat();}
  static void Postfix(ImpactState __state,DamageWorker.DamageResult __result){if(__state.map!=null&&__result!=null)Show(__state.map,__state.position,__state.angle);}
  public static void Show(Map map,Vector3 position,float angle){
   var impact=FleckMaker.GetDataStatic(position-Quaternion.AngleAxis(angle,Vector3.up)*new Vector3(.6f,0,.55f),map,DefDatabase<FleckDef>.GetNamed("YR_SamokAmbushImpact"),2.3f);impact.rotation=angle;map.flecks.CreateFleck(impact);
   var flash=FleckMaker.GetDataStatic(position,map,DefDatabase<FleckDef>.GetNamed("YR_SamokAmbushFlash"),1.2f);map.flecks.CreateFleck(flash);
   for(int i=0;i<8;i++){var petal=FleckMaker.GetDataStatic(position,map,DefDatabase<FleckDef>.GetNamed("YR_SamokSakuraPetal"),.24f+.04f*(i%3));petal.rotation=angle+i*47;petal.rotationRate=i%2==0?130:-110;petal.velocityAngle=angle+i*45;petal.velocitySpeed=1.1f+.2f*(i%3);map.flecks.CreateFleck(petal);}
  }
 }
}

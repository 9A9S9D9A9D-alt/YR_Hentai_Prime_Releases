using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace YR_RJW {
 public sealed class HediffComp_SamokStealth : HediffComp_Invisibility {
  // This cloak has explicitly defined release conditions; being injured does
  // not add the vanilla invisibility power's temporary reveal condition.
  public override void Notify_PawnPostApplyDamage(DamageInfo info,float totalDamageDealt) { }
 }
 public sealed class Hediff_SamokAssassin : HediffWithComps {
  public const int MaxCharges=5,RechargeTicks=7500;
  public int charges=MaxCharges,nextCharge=-1;
  public Verb ambushVerb;
  bool wasDagger;
  public static Hediff_SamokAssassin Get(Pawn p)=>p?.health?.hediffSet.GetFirstHediffOfDef(HediffDef.Named("YR_SamokAssassinState")) as Hediff_SamokAssassin;
  public static Hediff_SamokAssassin Ensure(Pawn p){var state=Get(p);if(state==null){state=(Hediff_SamokAssassin)HediffMaker.MakeHediff(HediffDef.Named("YR_SamokAssassinState"),p);p.health.AddHediff(state);}state.Refresh();return state;}
  public bool HasDagger=>pawn?.equipment?.Primary?.def.defName=="OY_Tentacle_Dagger";
  Hediff Cloak=>pawn.health.hediffSet.GetFirstHediffOfDef(HediffDef.Named("YR_SamokAssassinCloak"));
  public bool Hidden=>Cloak!=null;
  public override HediffStage CurStage=>HasDagger?base.CurStage:null;
  public void Refresh(){if(wasDagger!=HasDagger){wasDagger=HasDagger;pawn.health.Notify_HediffChanged(this);}if(!HasDagger||!pawn.Drafted||pawn.Downed||pawn.Dead)Reveal();}
  public void UpdateCharges(int now){if(charges>=MaxCharges){nextCharge=-1;return;}if(nextCharge<0)nextCharge=now+RechargeTicks;while(charges<MaxCharges&&now>=nextCharge){charges++;nextCharge+=RechargeTicks;}if(charges==MaxCharges)nextCharge=-1;}
  public bool TryHide(){Refresh();int now=Find.TickManager.TicksGame;UpdateCharges(now);if(Hidden||!HasDagger||!pawn.Drafted||pawn.Downed||charges==0)return false;charges--;if(nextCharge<0)nextCharge=now+RechargeTicks;var cloak=pawn.health.AddHediff(HediffDef.Named("YR_SamokAssassinCloak"));cloak.TryGetComp<HediffComp_Invisibility>()?.BecomeInvisible(true);return true;}
  public void Reveal(){var cloak=Cloak;if(cloak!=null)pawn.health.RemoveHediff(cloak);}
  public void BeginAttack(Verb verb){if(!verb.verbProps.violent&&!verb.IsMeleeAttack)return;ambushVerb=Hidden&&HasDagger&&verb.IsMeleeAttack?verb:null;Reveal();}
  public override void PostTickInterval(int delta){base.PostTickInterval(delta);Refresh();UpdateCharges(Find.TickManager.TicksGame);}
  public override void Notify_KilledPawn(Pawn victim,DamageInfo? dinfo){base.Notify_KilledPawn(victim,dinfo);if(dinfo?.Instigator!=pawn||victim==pawn)return;UpdateCharges(Find.TickManager.TicksGame);charges=Math.Min(MaxCharges,charges+1);if(charges==MaxCharges)nextCharge=-1;}
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref charges,"samokStealthCharges",MaxCharges);Scribe_Values.Look(ref nextCharge,"samokStealthNextCharge",-1);}
  public override IEnumerable<Gizmo> GetGizmos(){foreach(var g in base.GetGizmos())yield return g;if(!HasDagger||pawn.Faction!=Faction.OfPlayer)yield break;UpdateCharges(Find.TickManager.TicksGame);var cmd=new Command_Action{defaultLabel="생체 은신 "+charges+"/5"+(Hidden?" · 사용 중":""),defaultDesc="공격·활 변형·소집 해제 시 은신 해제. 3시간마다 1스택 회복, 직접 처치 시 1스택 회복.\n첫 기습 공격 명중 판정 95%·상대 회피율 절반.\n초안: 단검 이동속도 +25%, 은신 첫 근접 공격 피해 3배."+(nextCharge>=0?"\n다음 회복: "+((nextCharge-Find.TickManager.TicksGame)/2500f).ToString("0.0")+"시간":""),icon=ContentFinder<Texture2D>.Get("Yuran/UI/Commands/SamokStealth"),action=()=>{TryHide();}};if(Hidden)cmd.Disable("이미 은신 중입니다.");else if(!pawn.Drafted)cmd.Disable("소집 중에 사용할 수 있습니다.");else if(charges==0)cmd.Disable("은신 스택이 없습니다.");yield return cmd;}
 }
 // Scope the multiplier to one actual verb execution, including misses and
 // exceptions. No lasting damage modifier can leak into a later attack.
 [HarmonyPatch(typeof(Verb),"TryCastNextBurstShot")]
 static class SamokStealthAttack {
  static void Prefix(Verb __instance,out Hediff_SamokAssassin __state){__state=__instance.CasterIsPawn?Hediff_SamokAssassin.Get(__instance.CasterPawn):null;__state?.BeginAttack(__instance);}
  static Exception Finalizer(Exception __exception,Hediff_SamokAssassin __state){if(__state!=null)__state.ambushVerb=null;return __exception;}
 }
 [HarmonyPatch(typeof(Verb_MeleeAttackDamage),"DamageInfosToApply")]
 static class SamokAmbushDamage {
  static void Postfix(Verb_MeleeAttackDamage __instance,ref IEnumerable<DamageInfo> __result){if(Hediff_SamokAssassin.Get(__instance.CasterPawn)?.ambushVerb==__instance)__result=Boost(__result);}
  static IEnumerable<DamageInfo> Boost(IEnumerable<DamageInfo> source){foreach(var value in source){var info=value;info.SetAmount(info.Amount*3f);yield return info;}}
 }
 [HarmonyPatch(typeof(Verb_MeleeAttack),"GetNonMissChance")]
 static class SamokAmbushAccuracy {
  static void Postfix(Verb_MeleeAttack __instance,ref float __result){if(Hediff_SamokAssassin.Get(__instance.CasterPawn)?.ambushVerb==__instance)__result=.95f;}
 }
 [HarmonyPatch(typeof(Verb_MeleeAttack),"GetDodgeChance")]
 static class SamokAmbushDodge {
  static void Postfix(Verb_MeleeAttack __instance,ref float __result){if(Hediff_SamokAssassin.Get(__instance.CasterPawn)?.ambushVerb==__instance)__result*=.5f;}
 }
 [HarmonyPatch(typeof(Pawn_DraftController),"set_Drafted")]
 static class SamokStealthUndraft {
  static void Postfix(Pawn ___pawn){if(!___pawn.Drafted)Hediff_SamokAssassin.Get(___pawn)?.Reveal();}
 }
}

using System;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
namespace YR_RJW {
 [StaticConstructorOnStartup]
 public static class CoccyxSocket {
  public static BodyPartDef Def=>DefDatabase<BodyPartDef>.GetNamed("YR_CoccyxSocket");
  static CoccyxSocket(){LongEventHandler.ExecuteWhenFinished(()=>{foreach(var body in DefDatabase<ThingDef>.AllDefsListForReading.Where(d=>d.race?.Humanlike==true).Select(d=>d.race.body).Where(b=>b!=null).Distinct())Ensure(body);});}
  public static void Ensure(BodyDef body){
   if(body.corePart==null||body.AllParts.Any(p=>p.def==Def))return;
   // Append at the root's end: retain every pre-existing flattened body-part index.
   var slot=body.corePart.parts.FirstOrDefault(p=>p.def==Def);
   if(slot==null){slot=new BodyPartRecord{def=Def,body=body,parent=body.corePart,coverage=0,depth=BodyPartDepth.Inside,height=BodyPartHeight.Bottom};body.corePart.parts.Add(slot);}
   body.AllParts.Add(slot);body.ClearCachedData();
  }
  public static BodyPartRecord Part(Pawn pawn){Ensure(pawn.RaceProps.body);return pawn.RaceProps.body.AllParts.First(p=>p.def==Def);}
 }
 public sealed class CompUseEffect_CoccyxSeed:CompUseEffect {
  public override AcceptanceReport CanBeUsedBy(Pawn p){if(!p.RaceProps.Humanlike)return "인간형만 사용할 수 있습니다.";if(p.health.hediffSet.HasHediff(DefDatabase<HediffDef>.GetNamed("YR_CoccyxTentacleImplant")))return "이미 촉수 이식체가 있습니다.";return true;}
  public override void DoEffect(Pawn p){if(!CanBeUsedBy(p).Accepted)return;p.health.AddHediff(DefDatabase<HediffDef>.GetNamed("YR_CoccyxTentacleImplant"),CoccyxSocket.Part(p));}
 }
 [HarmonyPatch(typeof(Pawn_HealthTracker),"PreApplyDamage")]
 static class CoccyxSocketCannotTakeDamage {
  static bool Prefix(DamageInfo dinfo,ref bool absorbed){if(dinfo.HitPart?.def!=CoccyxSocket.Def)return true;absorbed=true;return false;}
 }
 [HarmonyPatch(typeof(Pawn_HealthTracker),"AddHediff",new[]{typeof(Hediff),typeof(BodyPartRecord),typeof(DamageInfo?),typeof(DamageWorker.DamageResult)})]
 static class CoccyxSocketPlacement {
  static bool Prefix(Pawn ___pawn,Hediff hediff,ref BodyPartRecord part){
   if(hediff.def.defName=="YR_CoccyxTentacleImplant"){if(___pawn.health.hediffSet.HasHediff(hediff.def))return false;part=CoccyxSocket.Part(___pawn);}
   return (part??hediff.Part)?.def!=CoccyxSocket.Def||(!(hediff is Hediff_Injury)&&!(hediff is Hediff_MissingPart));
  }
 }
}

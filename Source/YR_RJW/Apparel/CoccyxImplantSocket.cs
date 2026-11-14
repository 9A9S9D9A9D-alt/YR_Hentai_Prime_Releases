using System;
using System.Linq;
using AlienRace;
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
 public sealed class CompBioArmorCoccyx : ThingComp {
  private static HediffDef ImplantDef => HediffDef.Named("YR_CoccyxTentacleImplant");

  public override void Notify_Equipped(Pawn pawn) {
   if(!pawn.RaceProps.Humanlike||pawn.Dead||pawn.health.hediffSet.HasHediff(ImplantDef))return;
   var implant=HediffMaker.MakeHediff(ImplantDef,pawn);
   implant.TryGetComp<HediffComp_CoccyxCombat>().GrantFromArmor((Apparel)parent);
   pawn.health.AddHediff(implant,CoccyxSocket.Part(pawn));
  }

  public override void Notify_Unequipped(Pawn pawn) {
   var implant=pawn.health.hediffSet.GetFirstHediffOfDef(ImplantDef);
   if(implant?.TryGetComp<HediffComp_CoccyxCombat>()?.GrantingArmor==parent)
    pawn.health.RemoveHediff(implant);
  }

  public override void PostExposeData() {
   base.PostExposeData();
   if(Scribe.mode==LoadSaveMode.PostLoadInit)
    LongEventHandler.ExecuteWhenFinished(()=>{
     if(((Apparel)parent).Wearer is Pawn wearer)Notify_Equipped(wearer);
    });
  }
 }

 [StaticConstructorOnStartup]
 internal static class CoccyxBioArmorCoverage {
  static CoccyxBioArmorCoverage() {
   var armor=ThingDef.Named(CoccyxGasArmor.ApparelName);
   var extra=DefDatabase<BodyPartGroupDef>.GetNamed("YR_BioArmorExtraCoverage");
   var groups=armor.apparel.bodyPartGroups;
   foreach(var body in DefDatabase<ThingDef>.AllDefsListForReading
    .Where(def=>def.race?.Humanlike==true).Select(def=>def.race.body).Where(body=>body!=null).Distinct())
    foreach(var part in body.AllParts)
     if(part.depth==BodyPartDepth.Outside&&!part.groups.Any(groups.Contains))
      part.groups.Add(extra);
  }
 }

 [HarmonyPatch(typeof(RaceRestrictionSettings),nameof(RaceRestrictionSettings.CanWear))]
 internal static class CoccyxBioArmorRaceRestriction {
  private static bool Prefix(ThingDef apparel,ThingDef race,ref bool __result) {
   if(apparel?.defName!=CoccyxGasArmor.ApparelName&&apparel?.defName!=CoccyxGasArmor.HelmetName)return true;
   __result=race?.race?.Humanlike==true;
   return false;
  }
 }

 [HarmonyPatch(typeof(ApparelGraphicRecordGetter),nameof(ApparelGraphicRecordGetter.TryGetGraphicApparel))]
 internal static class CoccyxBioArmorWornGraphic {
  private static bool Prefix(Apparel apparel,ref ApparelGraphicRecord rec,ref bool __result) {
   if(apparel.def.defName!=CoccyxGasArmor.ApparelName)return true;
   var graphic=GraphicDatabase.Get<Graphic_Multi>(apparel.WornGraphicPath+"_Thin",
    ShaderDatabase.Cutout,apparel.def.graphicData.drawSize,apparel.DrawColor);
   rec=new ApparelGraphicRecord(graphic,apparel);
   __result=true;
   return false;
  }
 }

 public sealed partial class HediffComp_CoccyxCombat {
  private bool grantedByArmor;
  private Apparel grantingArmor;
  internal Apparel GrantingArmor => grantingArmor;

  internal void GrantFromArmor(Apparel armor) {
   grantedByArmor=true;
   grantingArmor=armor;
  }

  public override bool CompShouldRemove => base.CompShouldRemove||
   (grantedByArmor&&(grantingArmor==null||grantingArmor.Wearer!=Pawn));

  private void ExposeArmorGrant() {
   Scribe_Values.Look(ref grantedByArmor,"coccyxGrantedByArmor");
   Scribe_References.Look(ref grantingArmor,"coccyxGrantingArmor");
  }
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

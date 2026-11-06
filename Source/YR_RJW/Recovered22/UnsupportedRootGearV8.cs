using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using YR_Hentai_Prime_AnimationBed;
namespace YR_RJW {
 internal static class UnsupportedRootGearV8 {
  private static readonly HashSet<string> Roots=new HashSet<string>{"YR_AP_Tentacle","YR_AP_Tentacle_Rooted","YR_AP_Tentacle_Headgear","YR_AP_Tentacle_Headgear_Mask"};
  public static bool Skip(Apparel apparel,Pawn pawn)=>pawn!=null&&Roots.Contains(apparel?.def?.defName??"")&&(pawn.def.defName=="Milian_Race"||!UnsupportedRacesV8.Allows(pawn.def));
 }
 [HarmonyPatch(typeof(Apparel),nameof(Apparel.PawnCanWear))]
 internal static class UnsupportedRootWearV8 {
  private static void Postfix(Apparel __instance,Pawn pawn,ref bool __result){if(UnsupportedRootGearV8.Skip(__instance,pawn))__result=false;}
 }
 [HarmonyPatch(typeof(ApparelGraphicRecordGetter),nameof(ApparelGraphicRecordGetter.TryGetGraphicApparel))]
 internal static class UnsupportedRootDrawV8 {
  private static bool Prefix(Apparel apparel,ref ApparelGraphicRecord rec,ref bool __result){
   if(!UnsupportedRootGearV8.Skip(apparel,apparel?.Wearer))return true;
   rec=default(ApparelGraphicRecord);__result=false;return false;
  }
 }
}

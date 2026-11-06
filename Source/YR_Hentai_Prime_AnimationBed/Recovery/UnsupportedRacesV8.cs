using System;
using System.Collections.Generic;
using Verse;
namespace YR_Hentai_Prime_AnimationBed {
 public static class UnsupportedRacesV8 {
  private static readonly HashSet<string> Packages=new HashSet<string>(StringComparer.OrdinalIgnoreCase){"pumpkin.mengximagic","jk.rmt.soulchronicle","jk.rmt.dungeoncore","redmattis.betterprerequisites","redmattis.bigsmall.core","rs.sl"};
  public static bool IsExcludedPackage(string package)=>package!=null&&Packages.Contains(package);
  public static bool OrdinaryHumanlike(Pawn pawn) => pawn!=null && pawn.RaceProps.Humanlike && pawn.RaceProps.IsFlesh &&
   !pawn.RaceProps.IsMechanoid && !pawn.RaceProps.IsAnomalyEntity && !pawn.RaceProps.IsDrone && !pawn.IsEntity && !pawn.IsMutant &&
   pawn.Faction!=RimWorld.Faction.OfEntities && pawn.def.defName.IndexOf("CreepJoiner",StringComparison.OrdinalIgnoreCase)<0;
  public static bool Allows(ThingDef race){
   if(race==null)return false;
   if(race.defName=="AT_TriJaw"||race.defName=="AT_Hiveguard"||race.defName=="AT_Lacerator"||race.defName=="AT_Oppressor"||race.defName=="AT_dreadnought")return false;
   string name=race.defName??"";
   return !IsExcludedPackage(race.modContentPack?.PackageIdPlayerFacing)&&name!="Mengxi_Race"&&!name.StartsWith("BS_",StringComparison.OrdinalIgnoreCase)&&!name.StartsWith("DC_",StringComparison.OrdinalIgnoreCase)&&!name.StartsWith("Rjw_Slime_",StringComparison.OrdinalIgnoreCase);
  }
 }
}

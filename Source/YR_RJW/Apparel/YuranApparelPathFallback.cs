using System;
using System.Reflection;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace YR_RJW {
 // Chezhou's final loader bypasses HAR's normal missing-body fallback.
 [HarmonyPatch]
 static class YuranApparelPathFallback {
  const string Resolver="ChezhouLib.HAR.Utils.HarApparelGraphicPathResolver";
  static string[] bodySuffixes;
  static bool Prepare()=>AccessTools.TypeByName(Resolver)!=null;
  static MethodBase TargetMethod()=>AccessTools.Method(AccessTools.TypeByName(Resolver),"LoadResolvedGraphic");
  static bool HasAnyDirection(string path)=>ContentFinder<Texture2D>.Get(path+"_south",false)!=null||ContentFinder<Texture2D>.Get(path+"_north",false)!=null||ContentFinder<Texture2D>.Get(path+"_east",false)!=null||ContentFinder<Texture2D>.Get(path+"_west",false)!=null;
  static void Prefix(ref string path){
   if(string.IsNullOrEmpty(path)||HasAnyDirection(path))return;
   bodySuffixes??=DefDatabase<BodyTypeDef>.AllDefsListForReading.Select(b=>"_"+b.defName).OrderByDescending(s=>s.Length).ToArray();
   foreach(string suffix in bodySuffixes){
    if(!path.EndsWith(suffix,StringComparison.Ordinal))continue;
    foreach(string body in new[]{"Thin","Female","Male"}){
     string candidate=path.Substring(0,path.Length-suffix.Length)+"_"+body;
     if(HasAnyDirection(candidate)){path=candidate;return;}
    }
    return;
   }
  }
 }
}

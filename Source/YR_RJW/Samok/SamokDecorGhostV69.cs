using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
namespace YR_RJW {
 // Native GhostUtility uses Get(graphic.GetType()), which cannot construct
 // Graphic_RandomRotated (no parameterless ctor). Recolor its existing wrapper.
 [HarmonyPatch(typeof(GhostUtility),nameof(GhostUtility.GhostGraphicFor))]
 internal static class SamokDecorGhostV69 {
  private static readonly ConditionalWeakTable<Graphic,Dictionary<Color,Graphic>> Cache=new ConditionalWeakTable<Graphic,Dictionary<Color,Graphic>>();
  private static bool Prefix(Graphic baseGraphic,ThingDef thingDef,Color ghostCol,ref Graphic __result){
   string n=thingDef?.defName;
   if(n!="YR_Tentacle_Rug_A" && n!="YR_Tentacle_Rug_B" && n!="YR_Tentacle_Rug_C" && n!="YR_Tentacle_Deco" && n!="YR_Tentacle_Light")return true;
   var source=baseGraphic??thingDef.graphicData.Graphic;
   var colors=Cache.GetValue(source,_=>new Dictionary<Color,Graphic>());
   if(!colors.TryGetValue(ghostCol,out var ghost)){
    ghost=source.GetColoredVersion(ShaderTypeDefOf.EdgeDetect.Shader,ghostCol,Color.white);colors.Add(ghostCol,ghost);
   }
   __result=ghost;return false;
  }
 }
}

using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using AlienRace;
using AlienRace.ApparelGraphics;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace YR_RJW
{
    // Resolve genuine worn apparel only; native race body graphics remain untouched underneath.
    [HarmonyPatch(typeof(ApparelGraphicUtility), nameof(ApparelGraphicUtility.GetPath))]
    internal static class RootApparelGraphicFallback
    {
        internal const string ArmorBody = "OY_Yuran_Addon/Equipment/TentacleArmor/TentacleArmor_Female";
        internal const string ArmorHead = "OY_Yuran_Addon/Equipment/TentacleArmor_HeadGear/TentacleArmor";
        private static readonly ConcurrentDictionary<(string,string),bool> dedicatedCache=new ConcurrentDictionary<(string,string),bool>();
        private static readonly Dictionary<string, bool> available = new Dictionary<string, bool>();
        private static readonly object graphicCacheLock = new object();

        private static bool HasGraphic(string path)
        {
            if (path.NullOrEmpty()) return false;
            lock (graphicCacheLock)
            {
                if (available.TryGetValue(path, out bool found)) return found;
                // Native Graphic_Multi mirrors east to west when west is not supplied.
                found = ContentFinder<Texture2D>.Get(path + "_north", false) != null ||
                    ContentFinder<Texture2D>.Get(path + "_south", false) != null ||
                    ContentFinder<Texture2D>.Get(path + "_east", false) != null ||
                    ContentFinder<Texture2D>.Get(path + "_west", false) != null ||
                    ContentFinder<Texture2D>.Get(path, false) != null;
                available[path] = found;
                return found;
            }
        }

        internal static bool RootBody(Apparel item) => item?.def.defName=="YR_AP_Tentacle"||item?.def.defName=="YR_AP_Tentacle_Rooted";
        internal static bool RootHead(Apparel item) => item?.def.defName=="YR_AP_Tentacle_Headgear"||item?.def.defName=="YR_AP_Tentacle_Headgear_Mask";
        internal static bool Dedicated(Apparel item,string resolved=null)
        {
            var pawn=item?.Wearer;
            if(pawn==null)return false;
            if(pawn.def.defName.StartsWith("Yuran_Race")||pawn.def.defName=="Mincho_ThingDef")return true;
            if(resolved==null)return dedicatedCache.TryGetValue((pawn.def.defName,item.def.defName),out var known)&&known;
            if(!(pawn.def is ThingDef_AlienRace alien)||alien.alienRace.graphicPaths.apparel.GetOverride(item)==null||resolved.NullOrEmpty())return false;
            string root=item.def.modContentPack.RootDir;
            foreach(string folder in new[]{"Content","YuranRaceMod","MinchoRaceMod","AntyRaceMod","Furmaru"})
                foreach(string suffix in new[]{"_south.png",".png"})
                    if(File.Exists(Path.Combine(root,folder,"Textures",resolved+suffix)))return true;
            return false;
        }
        private static void Postfix(string path, Apparel apparel, BodyTypeDef bodyType, ref string __result)
        {
            string name = apparel?.def?.defName;
            bool body = name == "YR_AP_Tentacle" || name == "YR_AP_Tentacle_Rooted";
            if (!body && name != "YR_AP_Tentacle_Headgear" && name != "YR_AP_Tentacle_Headgear_Mask") return;
            var wearer = apparel.Wearer;
            if (wearer != null && !wearer.RaceProps.Humanlike) return;
            if(name=="YR_AP_Tentacle_Headgear_Mask"){
                bool maskDedicated=Dedicated(apparel,__result);
                if(wearer!=null)dedicatedCache[(wearer.def.defName,name)]=maskDedicated;
                if(!maskDedicated||!HasGraphic(__result))
                    __result="Yuran/Yuranlike/Apparel/Bote/YR_AP_Tentacle_Headgear_Mask/Common/Texture";
                return;
            }
            if(Comp_Tentacle_Apparel.UsesAddon25(wearer)) {
                __result=name=="YR_AP_Tentacle" ? "Yuran/Apparel/TentacleArmor25/Armor" : BaseContent.PlaceholderGearImagePath;
                return;
            }
            bool dedicated = Dedicated(apparel, __result);
            if(wearer!=null)dedicatedCache[(wearer.def.defName,name)]=dedicated;
            if (!body && !dedicated) { __result = BaseContent.PlaceholderGearImagePath; return; }
            if (dedicated && HasGraphic(__result)) return;
            if (name=="YR_AP_Tentacle_Rooted") { __result=BaseContent.PlaceholderGearImagePath; return; }
            if (body) __result = ArmorBody;

        }
    }
}

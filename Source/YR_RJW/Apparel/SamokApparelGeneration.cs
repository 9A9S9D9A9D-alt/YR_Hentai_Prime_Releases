using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace YR_RJW {
 // Generation remains opt-in to this mod. Existing saves and ordinary player jobs are preserved.
 public static class SamokApparelGeneration {
  static readonly HashSet<Thing> rejected = new HashSet<Thing>();
  static readonly object sync = new object();
  public static bool Blocked(ThingDef def) => def != null &&
   (def.defName == "YR_AP_Tentacle" || def.defName == "YR_AP_Tentacle_Rooted" ||
    def.defName == "YR_AP_Tentacle_Headgear" || def.defName == "YR_AP_Tentacle_Headgear_Mask");

  internal static bool? SourceDecision(ThingDef def, Type type) {
   if (type == null) return null; // Harmony's generated wrappers have no declaring type.
   while (type.DeclaringType != null) type = type.DeclaringType;
   if (type == typeof(SamokApparelGeneration) || type == typeof(SamokApparelExternalCreation) ||
       type == typeof(SamokApparelExternalWear) || type == typeof(ThingMaker) || type == typeof(Pawn_ApparelTracker)) return null;
   if (type == typeof(GenRecipe) || type == typeof(JobDriver_Wear)) return true;
   if (type == typeof(PawnApparelGenerator)) return false;
   var assembly = type.Assembly;
   if (assembly == typeof(Thing).Assembly) return null; // Follow native helpers to their real caller.
   if (assembly == typeof(object).Assembly || assembly == typeof(Harmony).Assembly ||
       assembly.GetName().Name.StartsWith("MonoMod", StringComparison.Ordinal)) return null;
   return def.modContentPack?.assemblies?.loadedAssemblies.Contains(assembly) == true;
  }
  internal static bool SourceAllowed(ThingDef def) {
   foreach (var frame in new StackTrace(false).GetFrames()) {
    var decision = SourceDecision(def, frame.GetMethod()?.DeclaringType);
    if (decision.HasValue) return decision.Value;
   }
   return false;
  }
  internal static void Reject(Thing item) { lock(sync) rejected.Add(item); }
  internal static bool Rejected(Thing item) { lock(sync) return rejected.Contains(item); }
  internal static void DiscardRejected() {
   Thing[] items;
   lock(sync) { if(rejected.Count == 0)return;items=new Thing[rejected.Count];rejected.CopyTo(items);rejected.Clear(); }
   // Defer disposal until the event returns: never hand a null/destroyed result to a foreign factory.
   foreach(var item in items) if(!item.Destroyed) item.Destroy(DestroyMode.Vanish);
  }
 }

 [HarmonyPatch(typeof(ThingMaker), nameof(ThingMaker.MakeThing))]
 static class SamokApparelExternalCreation {
  static void Postfix(Thing __result) {
   if (__result != null && SamokApparelGeneration.Blocked(__result.def) && Scribe.mode == LoadSaveMode.Inactive &&
       !SamokApparelGeneration.SourceAllowed(__result.def)) SamokApparelGeneration.Reject(__result);
  }
 }
 [HarmonyPatch(typeof(Pawn_ApparelTracker), nameof(Pawn_ApparelTracker.Wear))]
 static class SamokApparelExternalWear {
  [HarmonyPrefix, HarmonyPriority(Priority.First)]
  static bool Prefix(Apparel newApparel) => !SamokApparelGeneration.Blocked(newApparel?.def) ||
   (!newApparel.Destroyed && !SamokApparelGeneration.Rejected(newApparel) && SamokApparelGeneration.SourceAllowed(newApparel.def));
 }
 public sealed class SamokApparelGenerationCleanup : GameComponent {
  public SamokApparelGenerationCleanup(Game game) { }
  public override void GameComponentUpdate() => SamokApparelGeneration.DiscardRejected();
 }
 [HarmonyPatch(typeof(PawnApparelGenerator), "CanUsePair")]
 static class SamokApparelRandomCandidate {
  static bool Prefix(ThingStuffPair pair, ref bool __result) {
   if (!SamokApparelGeneration.Blocked(pair.thing)) return true;
   __result = false; return false;
  }
 }
 [HarmonyPatch(typeof(PawnApparelGenerator), nameof(PawnApparelGenerator.GenerateApparelOfDefFor))]
 static class SamokApparelRequiredGeneration {
  static bool Prefix(ThingDef apparelDef, ref Apparel __result) {
   if (!SamokApparelGeneration.Blocked(apparelDef)) return true;
   __result = null; return false;
  }
 }
 [HarmonyPatch(typeof(PawnApparelGenerator), nameof(PawnApparelGenerator.GenerateStartingApparelFor))]
 static class SamokApparelStartingCleanup {
  [HarmonyPostfix, HarmonyPriority(Priority.Last)]
  static void Postfix(Pawn pawn) {
   var worn = pawn.apparel?.WornApparel;
   if (worn == null) return;
   for (int i = worn.Count - 1; i >= 0; i--) {
    var item = worn[i];
    if (!SamokApparelGeneration.Blocked(item.def)) continue;
    pawn.apparel.Remove(item); item.Destroy();
   }
  }
 }
 [HarmonyPatch(typeof(ThingSetMakerUtility), nameof(ThingSetMakerUtility.CanGenerate))]
 static class SamokApparelRandomReward {
  static bool Prefix(ThingDef __0, ref bool __result) {
   if (!SamokApparelGeneration.Blocked(__0)) return true;
   __result = false; return false;
  }
 }
}

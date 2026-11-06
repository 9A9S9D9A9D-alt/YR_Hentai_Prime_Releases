using System;
using System.Linq;
using System.Reflection;
using AlienRace;
using HarmonyLib;
using RimWorld;
using Verse;

namespace YR_RJW
{
    // Called only by the host bed's successful completion; no global or retroactive pawn patch.
    public static class SamokHostGenderV8
    {
        public static bool Complete(Pawn pawn)
        {
            if (!LegacyEligibility.Allows(pawn) || pawn.gender != Gender.Male ||
                pawn.health?.hediffSet.HasHediff(DefDatabase<HediffDef>.GetNamedSilentFail("YR_Tentacle_Hediff")) != true) return false;
            pawn.gender = Gender.Female;
            if (pawn.story != null) pawn.story.bodyType = FemaleBody(pawn);
            ConvertOptionalAnatomy(pawn);
            pawn.Drawer?.renderer?.SetAllGraphicsDirty();
            PortraitsCache.SetDirty(pawn);
            return true;
        }

        internal static BodyTypeDef FemaleBody(Pawn pawn)
        {
            var generator = (pawn.def as ThingDef_AlienRace)?.alienRace.generalSettings.alienPartGenerator;
            var allowed = generator?.bodyTypes;
            if (allowed == null || allowed.Count == 0) return BodyTypeDefOf.Female;
            if (generator.defaultFemaleBodyType != null && allowed.Contains(generator.defaultFemaleBodyType))
                return generator.defaultFemaleBodyType;
            if (allowed.Contains(BodyTypeDefOf.Female)) return BodyTypeDefOf.Female;
            // Preserve valid custom Yuran/Mincho shapes when the race has no vanilla female shape.
            if (allowed.Contains(pawn.story.bodyType) && pawn.story.bodyType != BodyTypeDefOf.Male)
                return pawn.story.bodyType;
            return allowed.FirstOrDefault(b => b != BodyTypeDefOf.Baby && b != BodyTypeDefOf.Child && b != BodyTypeDefOf.Male)
                ?? pawn.story.bodyType;
        }

        private static void ConvertOptionalAnatomy(Pawn pawn)
        {
            Type helper = AccessTools.TypeByName("rjw.Genital_Helper");
            Type adder = AccessTools.TypeByName("rjw.SexPartAdder");
            Type partDef = AccessTools.TypeByName("rjw.HediffDef_SexPart");
            if (helper == null && adder == null) return;
            var family = partDef == null ? null : AccessTools.Field(partDef, "genitalFamily");
            MethodInfo genitals = adder == null ? null : AccessTools.Method(adder, "add_genitals", new[] { typeof(Pawn), typeof(Pawn), typeof(Gender) });
            MethodInfo breasts = adder == null ? null : AccessTools.Method(adder, "add_breasts", new[] { typeof(Pawn), typeof(Pawn), typeof(Gender) });
            MethodInfo genitalSlot = helper == null ? null : AccessTools.Method(helper, "get_genitalsBPR", new[] { typeof(Pawn) });
            MethodInfo breastSlot = helper == null ? null : AccessTools.Method(helper, "get_breastsBPR", new[] { typeof(Pawn) });
            if (family == null || genitals == null || breasts == null || genitalSlot == null || breastSlot == null)
            {
                Log.Warning("[YRP v8] Optional RJW host anatomy API unavailable; existing anatomy preserved.");
                return;
            }
            Func<Hediff, string> kind = h => partDef.IsInstanceOfType(h.def) ? family.GetValue(h.def)?.ToString() : null;
            try
            {
                ConvertSlot(pawn, (BodyPartRecord)genitalSlot.Invoke(null, new object[] { pawn }), genitals,
                    h => kind(h) == "Penis" || kind(h) == "MaleOvipositor",
                    h => kind(h) == "Vagina" || kind(h) == "FemaleOvipositor");
                var chest = (BodyPartRecord)breastSlot.Invoke(null, new object[] { pawn });
                // RJW represents male nipples as stage 0 of the same organ definition.
                // Keep implant/transplant identity and metadata while selecting its first female stage.
                foreach (var part in pawn.health.hediffSet.hediffs.Where(h => h.Part == chest && kind(h) == "Breasts" && h.CurStageIndex == 0).ToList())
                    if (part.def.stages?.Count > 1) part.Severity = Math.Max(part.Severity, part.def.stages[1].minSeverity);
                var featurelessChest = AccessTools.Field(helper, "featureless_chest")?.GetValue(null) as HediffDef;
                ConvertSlot(pawn, chest, breasts,
                    h => (kind(h) == "Breasts" && h.CurStageIndex == 0) || h.def == featurelessChest,
                    h => kind(h) == "Breasts" && h.CurStageIndex > 0);
            }
            catch (Exception error)
            {
                Log.Warning("[YRP v8] Optional RJW host anatomy conversion failed: " + error.GetBaseException().Message);
            }
        }

        private static void ConvertSlot(Pawn pawn, BodyPartRecord slot, MethodInfo adder,
            Func<Hediff, bool> male, Func<Hediff, bool> female)
        {
            // A genuinely missing body part or a race without this slot is never restored here.
            if (slot == null || pawn.health.hediffSet.PartIsMissing(slot)) return;
            var before = pawn.health.hediffSet.hediffs.ToList();
            var removed = before.Where(h => h.Part == slot && male(h)).ToList();
            if (before.Any(h => h.Part == slot && female(h)))
            {
                foreach (var part in removed) pawn.health.RemoveHediff(part);
                return;
            }
            // Remove only the male-specific RJW part; all unrelated implants/anus/body parts remain.
            foreach (var part in removed) pawn.health.RemoveHediff(part);
            try
            {
                adder.Invoke(null, new object[] { pawn, null, Gender.Female });
                if (!pawn.health.hediffSet.hediffs.Any(h => h.Part == slot && female(h)))
                    throw new InvalidOperationException("Race API supplied no female part for " + slot.def.defName);
            }
            catch
            {
                // Never leave a previously present organ deleted if an optional race API fails.
                foreach (var added in pawn.health.hediffSet.hediffs.Where(h => h.Part == slot && !before.Contains(h)).ToList())
                    pawn.health.RemoveHediff(added);
                foreach (var part in removed) pawn.health.AddHediff(part, slot);
                throw;
            }
        }
    }
}


namespace YR_RJW
{
    [HarmonyPatch(typeof(TraitSet), nameof(TraitSet.GainTrait))]
    internal static class SamokBrideBodyV33
    {
        private static void Postfix(Trait __0, Pawn ___pawn)
        {
            if (__0?.def == null || (__0.def.defName != "YR_Tentalcle" && __0.def.defName != "YR_Tentalcle_NonColonist")) return;
            if (!SamokBrideRules.IsBride(___pawn) || ___pawn.story == null) return;
            var current = ___pawn.story.bodyType;
            if (current != BodyTypeDefOf.Hulk && current != BodyTypeDefOf.Fat) return;
            var female = SamokHostGenderV8.FemaleBody(___pawn);
            if (female == current || female == BodyTypeDefOf.Hulk || female == BodyTypeDefOf.Fat || female == BodyTypeDefOf.Male)
                female = BodyTypeDefOf.Female;
            ___pawn.story.bodyType = female;
            ___pawn.Drawer?.renderer?.SetAllGraphicsDirty();
            PortraitsCache.SetDirty(___pawn);
        }
    }
}

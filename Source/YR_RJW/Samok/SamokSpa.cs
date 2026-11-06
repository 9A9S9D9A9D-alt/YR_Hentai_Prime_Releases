using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace YR_RJW
{
    public class CompProperties_SamokSpa : CompProperties
    {
        public int injuryIntervalTicks = 250;
        public float injuryHealing = 1f;
        public int diseaseIntervalTicks = 2500;
        public float diseaseCureChance = 0.05f;
        public float abasiaCureChance = 0.01f;
        public int abasiaExtraRecoveryTicks = 2500;
        public CompProperties_SamokSpa() { compClass = typeof(CompSamokSpa); }
    }
    public class SamokSpaExposure : IExposable
    {
        public int injuryTicks;
        public int diseaseTicks;
        public int lastGameTick = -1;
        public void ExposeData()
        {
            Scribe_Values.Look(ref injuryTicks, "injuryTicks");
            Scribe_Values.Look(ref diseaseTicks, "diseaseTicks");
            Scribe_Values.Look(ref lastGameTick, "lastGameTick", -1);
        }
    }
    public class CompSamokSpa : ThingComp
    {
        private Dictionary<Pawn, SamokSpaExposure> exposure = new Dictionary<Pawn, SamokSpaExposure>();
        private List<Pawn> exposureKeys;
        private List<SamokSpaExposure> exposureValues;
        public CompProperties_SamokSpa Props => (CompProperties_SamokSpa)props;
        public SamokSpaExposure ExposureFor(Pawn pawn)
        {
            if (!exposure.TryGetValue(pawn, out SamokSpaExposure clock))
                exposure.Add(pawn, clock = new SamokSpaExposure());
            return clock;
        }
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Collections.Look(ref exposure, "samokSpaExposure", LookMode.Reference, LookMode.Deep, ref exposureKeys, ref exposureValues);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (exposure == null) exposure = new Dictionary<Pawn, SamokSpaExposure>();
                foreach (Pawn pawn in exposure.Where(kv => kv.Value == null).Select(kv => kv.Key).ToList()) exposure.Remove(pawn);
            }
        }
        public override void CompTick()
        {
            base.CompTick();
            if (parent is Building_SamokSwimmingPool pool)
                foreach (Pawn pawn in pool.ActiveUsers.ToList()) TickFor(pawn);
        }
        public void TickFor(Pawn pawn)
        {
            var pool = parent as Building_SamokSwimmingPool;
            if (pool == null || !pool.Spawned || pawn == null || !pawn.Spawned || pawn.Dead ||
                pawn.Map != pool.Map || !pool.WaterCells.Contains(pawn.Position) || !pool.IsUsing(pawn)) return;
            SamokSpaExposure clock = ExposureFor(pawn);
            int now = Find.TickManager.TicksGame;
            if (clock.lastGameTick == now) return;
            clock.lastGameTick = now;
            // Count actual use only. Time away never becomes recovery or a new entry roll.
            if (++clock.injuryTicks >= Math.Max(1, Props.injuryIntervalTicks))
            {
                clock.injuryTicks = 0;
                HealInjuries(pawn);
            }
            if (++clock.diseaseTicks >= Math.Max(1, Props.diseaseIntervalTicks))
            {
                clock.diseaseTicks = 0;
                RecoverDisease(pawn);
            }
        }
        private void HealInjuries(Pawn pawn)
        {
            float remaining = Math.Max(0f, Props.injuryHealing);
            foreach (Hediff_Injury injury in pawn.health.hediffSet.hediffs.OfType<Hediff_Injury>().Where(h => h.CanHealNaturally()).ToList())
            {
                float amount = Math.Min(remaining, injury.Severity);
                if (amount > 0f) injury.Heal(amount);
                remaining -= amount;
                if (remaining <= 0f) break;
            }
        }
        public static bool EligibleDisease(Hediff hediff)
        {
            HediffDef def = hediff.def;
            // Native illness flags are meaningful here; arbitrary mod state flags are not a cure contract.
            if (def.modContentPack?.IsOfficialMod != true || !def.isBad || def.IsAddiction ||
                def.countsAsAddedPartOrImplant || hediff is Hediff_Injury || hediff is Hediff_MissingPart ||
                def.defName == "Abasia") return false;
            return def.isInfection || def.HasComp(typeof(HediffComp_Immunizable)) ||
                def.defName == "GutWorms" || def.defName == "MuscleParasites" ||
                def.defName == "FibrousMechanites" || def.defName == "SensoryMechanites";
        }
        private void RecoverDisease(Pawn pawn)
        {
            List<Hediff> diseases = pawn.health.hediffSet.hediffs.Where(EligibleDisease).ToList();
            if (diseases.Count > 0 && Rand.Chance(Props.diseaseCureChance))
                pawn.health.RemoveHediff(diseases.RandomElement());
            Hediff abasia = pawn.health.hediffSet.hediffs.FirstOrDefault(h => h.def.defName == "Abasia" && h.def.modContentPack?.IsOfficialMod == true);
            if (abasia == null) return;
            if (Rand.Chance(Props.abasiaCureChance)) pawn.health.RemoveHediff(abasia);
            else
            {
                HediffComp_Disappears timer = abasia.TryGetComp<HediffComp_Disappears>();
                if (timer == null) return;
                timer.ticksToDisappear = Math.Max(0, timer.ticksToDisappear - Math.Max(0, Props.abasiaExtraRecoveryTicks));
                if (timer.ticksToDisappear == 0) pawn.health.RemoveHediff(abasia);
            }
        }
    }
}

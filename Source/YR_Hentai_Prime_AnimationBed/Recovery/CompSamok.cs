using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace YR_Hentai_Prime_AnimationBed
{
    public class CompProperties_Samok : CompProperties
    {
        public bool isAltar;
        public ThingDef treeDef;
        public CompProperties_Samok() => compClass = typeof(CompSamok);
    }

    // Defensive pulse adapter. Burial growth is handled by the recovered altar class.
    public class CompSamok : ThingComp
    {
        public int consumedCorpses;
        public int lastPulseTick = -TreeRules.PulseCooldown;
        public CompProperties_Samok Props => (CompProperties_Samok)props;
        private CompRefuelable Fuel => parent.GetComp<CompRefuelable>();
        private YR_Hentai_Prime_AnimationBed_Setting Settings => YR_Hentai_Prime_AnimationBed_Mod.Settings;
        private int Required => TreeRules.Required(Props.isAltar ? Settings?.altarCorpses ?? 20 : Settings?.growthCorpses ?? 12);
        private bool Enabled => Settings?.enableSamok ?? true;
        public float Growth => TreeRules.Growth((parent as YR_RJW.Building_Tentacle_Altar)?.offeredCorpse ?? 0, Required);

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (respawningAfterLoad && consumedCorpses > 0 &&
                parent is YR_RJW.Building_Tentacle_Altar altar && altar.offeredCorpse == 0)
            {
                altar.offeredCorpse = consumedCorpses;
                consumedCorpses = 0;
            }
        }

        public override string CompInspectStringExtra()
            => !Enabled ? "YRP_Disabled".Translate().ToString() : null;

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (Props.isAltar || parent.Faction != Faction.OfPlayer ||
                parent.AllComps.Any(comp => comp.GetType().FullName == "YR_RJW.CompTentacleAbility")) yield break;
            Command_Action pulse = new Command_Action
            {
                defaultLabel = "YRP_Pulse".Translate(),
                defaultDesc = "YRP_PulseDescription".Translate(TreeRules.PulseCost),
                icon = ContentFinder<Texture2D>.Get("UI/Commands/Attack"),
                action = Pulse
            };
            if (!Enabled) pulse.Disable("YRP_Disabled".Translate());
            else if (Growth < 1f) pulse.Disable("YRP_ImmatureTree".Translate());
            else if (Fuel == null || Fuel.Fuel < TreeRules.PulseCost) pulse.Disable("YRP_NeedFuel".Translate(TreeRules.PulseCost));
            else if (!TreeRules.CanPulse(Fuel.Fuel, Find.TickManager.TicksGame, lastPulseTick, true))
                pulse.Disable("YRP_Cooldown".Translate());
            yield return pulse;
        }

        private void Pulse()
        {
            if (!Enabled || !parent.Spawned || Fuel == null ||
                !TreeRules.CanPulse(Fuel.Fuel, Find.TickManager.TicksGame, lastPulseTick, Growth >= 1f)) return;
            var targets = parent.Map.mapPawns.AllPawnsSpawned.Where(pawn => !pawn.Dead).ToList();
            if (targets.Count == 0)
            {
                Messages.Message("YRP_NoTargets".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }
            Fuel.ConsumeFuel(TreeRules.PulseCost);
            lastPulseTick = Find.TickManager.TicksGame;
            foreach (Pawn pawn in targets)
                pawn.stances?.stunner?.StunFor(pawn.HostileTo(parent) ? 480 : 240, parent, false, true, false);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref consumedCorpses, "recoveryConsumedCorpses", 0);
            Scribe_Values.Look(ref lastPulseTick, "recoveryLastPulseTick", -TreeRules.PulseCooldown);
            consumedCorpses = Math.Max(0, consumedCorpses);
        }
    }
}

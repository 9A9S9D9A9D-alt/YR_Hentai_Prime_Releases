using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace YR_RJW
{
    public class CompProperties_TentacleAbility : CompProperties
    {
        public CompProperties_TentacleAbility()
        {
            compClass = typeof(CompTentacleAbility);
        }
        public int consumption = 10;
        public List<HediffDef> hediffDefs = new List<HediffDef>();

        public ThingDef filthDef;

        public int filthNum = 1;

        public float time = 4f;
        public float enemyTime = 4f;
        public SoundDef sound;

        public string label = "YR_TentacleAbility_Label";
        public string description = "YR_TentacleAbility_Desc";
        public string disabledReason = "YR_TentacleAbility_DisabledReason";

        public string message = "YR_TentacleAbility_Message";

        public string iconPath = "Yuran/Icon/Wiggle";
    }
    public class CompTentacleAbility : Comp_Building_BaseBondageBed
    {
        public Building_Tentacle_Altar BTA => (Building_Tentacle_Altar)parent;

        public CompProperties_TentacleAbility Props => (CompProperties_TentacleAbility)props;
        public override void PostExposeData()
        {
            //Scribe_References.Look(ref bed, "bed");
        }
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if ((YR_Hentai_Prime_AnimationBed.YR_Hentai_Prime_AnimationBed_Mod.Settings?.enableSamok ?? true) && BTA.Faction == Faction.OfPlayer && BTA.GetComp<CompRefuelable>() != null)
            {
                yield return new Command_Action
                {
                    defaultLabel = "YR_TentacleAbility_Label".Translate(),
                    defaultDesc = "YR_TentacleAbility_Desc".Translate(Props.consumption, Props.time, Props.enemyTime),
                    icon = ContentFinder<Texture2D>.Get(Props.iconPath),
                    Disabled = !BTA.CorpseFulfilled || BTA.GetComp<CompRefuelable>().Fuel < Props.consumption,
                    disabledReason = !BTA.CorpseFulfilled ? "사목의 성장 조건을 먼저 충족해야 합니다." : "YR_TentacleAbility_DisabledReason".Translate(Props.consumption, BTA.GetComp<CompRefuelable>().Fuel),
                    action = delegate ()
                    {
                        if (!BTA.Spawned || !BTA.CorpseFulfilled || BTA.GetComp<CompRefuelable>().Fuel < Props.consumption ||
                            !(YR_Hentai_Prime_AnimationBed.YR_Hentai_Prime_AnimationBed_Mod.Settings?.enableSamok ?? true)) return;
                        BTA.GetComp<CompRefuelable>().ConsumeFuel(Props.consumption);
                        if (!Props.message.NullOrEmpty())
                        {
                            Messages.Message(Props.message.Translate(BTA.Label), MessageTypeDefOf.PositiveEvent, true);
                        }
                        if (Props.sound != null)
                        {
                            Props.sound.PlayOneShot(new TargetInfo(BTA.Position, BTA.Map, false));
                        }
                        foreach (Pawn pawn in BTA.Map.mapPawns.AllPawnsSpawned.ToList())
                        {
                            if (pawn != null && !pawn.Dead && pawn.Spawned)
                            {
                                Verb_EggVibrator.SwitchOn(pawn, Props.time, Props.enemyTime, Props.hediffDefs, null, Props.filthDef, Props.filthNum);
                            }
                        }

                    }
                };
            }
            yield break;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace YR_RJW
{
    public class SamokOriginalTraitV48 : IExposable
    {
        public TraitDef def;public int degree;public bool forced;
        public void ExposeData(){Scribe_Defs.Look(ref def,"def");Scribe_Values.Look(ref degree,"degree");Scribe_Values.Look(ref forced,"forced");}
    }
    public class SamokBlessingRecordV48 : IExposable
    {
        public Pawn pawn;public int expires;public List<SamokOriginalTraitV48> original=new List<SamokOriginalTraitV48>();
        public void ExposeData(){Scribe_References.Look(ref pawn,"pawn");Scribe_Values.Look(ref expires,"expires");Scribe_Collections.Look(ref original,"original",LookMode.Deep);}
    }
    public class SamokBlessingV48 : GameComponent
    {
        public const int Cost=20,Duration=900000;
        private List<SamokBlessingRecordV48> records=new List<SamokBlessingRecordV48>();
        private int nextExpiry=int.MaxValue;
        public SamokBlessingV48(Game game){}
        public static bool IsOriginal(TraitDef def)=>def?.defName=="YR_Tentalcle"||def?.defName=="YR_Tentalcle_NonColonist";
        private static bool IsBlessedDef(TraitDef def)=>def?.defName=="YR_Tentalcle_Blessed"||def?.defName=="YR_Tentalcle_NonColonist_Blessed";
        public static bool IsBlessed(Pawn pawn)=>pawn?.story?.traits?.allTraits.Any(t=>IsBlessedDef(t.def))==true;
        private void Schedule()=>nextExpiry=records.Count==0?int.MaxValue:records.Min(r=>r.expires);
        public override void ExposeData(){Scribe_Collections.Look(ref records,"samokBlessingsV48",LookMode.Deep);if(Scribe.mode==LoadSaveMode.PostLoadInit){if(records==null)records=new List<SamokBlessingRecordV48>();records.RemoveAll(r=>r.pawn==null);Schedule();}}
        public override void GameComponentTick(){
            if(records.Count==0||Find.TickManager.TicksGame<nextExpiry)return;
            int now=Find.TickManager.TicksGame;
            for(int i=records.Count-1;i>=0;i--)if(records[i].expires<=now){Restore(records[i]);records.RemoveAt(i);}
            Schedule();
        }
        public bool Apply(Pawn pawn)
        {
            if(!SamokBrideRules.IsBride(pawn)||IsBlessed(pawn)||pawn.story?.traits==null)return false;
            var traits=pawn.story.traits;var old=traits.allTraits.Where(t=>IsOriginal(t.def)).ToList();if(old.Count==0)return false;
            string name=old.Any(t=>t.def.defName=="YR_Tentalcle_NonColonist")?"YR_Tentalcle_NonColonist_Blessed":"YR_Tentalcle_Blessed";
            var blessed=DefDatabase<TraitDef>.GetNamedSilentFail(name);if(blessed==null)return false;
            var record=new SamokBlessingRecordV48 {pawn=pawn,expires=Find.TickManager.TicksGame+Duration,
                original=old.Select(t=>new SamokOriginalTraitV48 {def=t.def,degree=t.Degree,forced=t.ScenForced}).ToList()};
            try{
                foreach(var trait in old)traits.RemoveTrait(trait,true);
                traits.GainTrait(new Trait(blessed,0,true));
                records.Add(record);Schedule();
                pawn.Notify_DisabledWorkTypesChanged();
                pawn.needs?.mood?.thoughts?.situational.Notify_SituationalThoughtsDirty();
                var creep=DefDatabase<HediffDef>.GetNamedSilentFail("YR_SamokCreepBlessing");
                var effect=creep==null?null:pawn.health?.hediffSet.GetFirstHediffOfDef(creep);
                if(effect!=null && SamokCreepMap.Protected(pawn))effect.Severity=2f;
                return true;
            }catch(Exception e){records.Remove(record);Schedule();Restore(record);Log.Error("[Samok blessing] Trait change rolled back: "+e);return false;}
        }
        private static void Restore(SamokBlessingRecordV48 record)
        {
            var p=record.pawn;var traits=p?.story?.traits;if(traits==null)return;
            foreach(var trait in traits.allTraits.Where(t=>IsBlessedDef(t.def)).ToList())traits.RemoveTrait(trait,true);
            foreach(var old in record.original??new List<SamokOriginalTraitV48>())
                if(old.def!=null && !traits.HasTrait(old.def))traits.GainTrait(new Trait(old.def,old.degree,old.forced));
            p.Notify_DisabledWorkTypesChanged();
            p.needs?.mood?.thoughts?.situational.Notify_SituationalThoughtsDirty();
            var creep=DefDatabase<HediffDef>.GetNamedSilentFail("YR_SamokCreepBlessing");
            var effect=creep==null?null:p.health?.hediffSet.GetFirstHediffOfDef(creep);if(effect!=null)effect.Severity=1f;
        }
        public float DaysLeft(Pawn p){var r=records.FirstOrDefault(x=>x.pawn==p);return r==null?0f:Math.Max(0,r.expires-Find.TickManager.TicksGame)/60000f;}
    }
    public class CompProperties_SamokBlessingV48 : CompProperties
    {public CompProperties_SamokBlessingV48(){compClass=typeof(CompSamokBlessingV48);}}
    public class CompSamokBlessingV48 : ThingComp
    {
        private CompRefuelable Gauge=>parent.TryGetComp<CompRefuelable>();
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if(!parent.Spawned || parent.Faction!=Faction.OfPlayer || !(parent is Building_Tentacle_Altar tree))yield break;
            var command=new Command_Action {
                defaultLabel="심연의 신부 축복",
                defaultDesc="공물 게이지 20을 사용해 15일간 축복으로 전환합니다. 신부 기능과 전투 제약을 유지하고 신부 특성의 비전투 결격을 해제합니다. 크립 위에서 추가 강화됩니다. 중복 적용하지 않습니다.",
                icon=ContentFinder<Texture2D>.Get("Yuran/Icon/BrideBlessingV48"),
                action=()=>{
                    var manager=Current.Game.GetComponent<SamokBlessingV48>();
                    var pawns=parent.Map.mapPawns.AllPawnsSpawned.Where(p=>SamokBrideRules.IsBride(p)).ToList();
                    var held=parent.TryGetComp<CompSpawnBed>()?.SpawnedBed?.occupant;if(held!=null&&!pawns.Contains(held)&&SamokBrideRules.IsBride(held))pawns.Add(held);
                    var options=new List<FloatMenuOption>();
                    foreach(var p in pawns){var target=p;
                        if(SamokBlessingV48.IsBlessed(target)){options.Add(new FloatMenuOption(target.LabelShort+" · 축복 "+manager.DaysLeft(target).ToString("0.0")+"일 남음",null));continue;}
                        options.Add(new FloatMenuOption(target.LabelShort,()=>{
                            if(!tree.CorpseFulfilled||Gauge==null||Gauge.Fuel<SamokBlessingV48.Cost)return;
                            if(manager.Apply(target)){Gauge.ConsumeFuel(SamokBlessingV48.Cost);Messages.Message(target.LabelShort+": 15일간 심연의 신부(축복) 적용",target,MessageTypeDefOf.PositiveEvent);}
                        }));
                    }
                    if(options.Count==0)options.Add(new FloatMenuOption("대상 심연의 신부가 없습니다.",null));
                    Find.WindowStack.Add(new FloatMenu(options));
                }
            };
            if(!tree.CorpseFulfilled)command.Disable("사목의 성장 조건을 먼저 충족해야 합니다.");
            else if(Gauge==null||Gauge.Fuel<SamokBlessingV48.Cost)command.Disable("공물 게이지가 20 필요합니다.");
            yield return command;
        }
    }
}

namespace YR_RJW
{
    public class ThoughtWorker_SamokBlessingV49 : RimWorld.ThoughtWorker
    {
        protected override RimWorld.ThoughtState CurrentStateInternal(Verse.Pawn pawn)
            => SamokBlessingV48.IsBlessed(pawn)?RimWorld.ThoughtState.ActiveAtStage(0):RimWorld.ThoughtState.Inactive;
    }
}

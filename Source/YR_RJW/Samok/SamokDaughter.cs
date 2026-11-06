using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using YR_Hentai_Prime_AnimationBed;

namespace YR_RJW {
 public partial class CompSamokGrowth {
  private ThingOwner<Thing> daughterInputs;
  private int daughterReadyAt=-1,daughterRetryAt;
  public CompSamokGrowth(){daughterInputs=new ThingOwner<Thing>(this);}
  public ThingOwner GetDirectlyHeldThings()=>daughterInputs;
  public void GetChildHolders(List<IThingHolder> children)=>ThingOwnerUtility.AppendThingHoldersFromThings(children,daughterInputs);
  public int DaughterReadyAt=>daughterReadyAt;
  private string DaughterStatus=>daughterReadyAt<0?"":"\n사목의 딸 생산: "+(Find.TickManager.TicksGame<daughterReadyAt?((daughterReadyAt-Find.TickManager.TicksGame)/2500f).ToString("0.0")+"시간 남음":"출현 공간 대기");
  private void ExposeDaughter(){Scribe_Deep.Look(ref daughterInputs,"samokDaughterInputs",this);Scribe_Values.Look(ref daughterReadyAt,"samokDaughterReadyAt",-1);if(Scribe.mode==LoadSaveMode.PostLoadInit)daughterInputs??=new ThingOwner<Thing>(this);}
  private Gizmo DaughterCommand(){
   var command=new Command_Action{defaultLabel="사목의 딸 생산",defaultDesc="연결된 공물함의 맥동조직 75개와 백윤액 50개를 사용합니다. 생산 시간 2시간, 수명 3일.",icon=ThingDef.Named("YR_Tentacle_Girl").uiIcon,action=()=>{if(!TryStartDaughter(out string reason))Messages.Message(reason,parent,MessageTypeDefOf.RejectInput,false);}};
   if(daughterReadyAt>=0)command.Disable("이미 생산 중입니다.");else if(LinkedCount==0)command.Disable("재료 공급원을 먼저 연결하세요.");return command;
  }
  public bool TryStartDaughter(out string reason){
   reason="";if(!parent.Spawned||parent.Faction!=Faction.OfPlayer){reason="배치된 아군 사목나무가 필요합니다.";return false;}
   if(daughterReadyAt>=0){reason="이미 생산 중입니다.";return false;}
   var stock=AvailableStock();var plan=new List<KeyValuePair<Thing,int>>();
   foreach(var cost in new[]{new KeyValuePair<string,int>("YR_PulsatingTissue",75),new KeyValuePair<string,int>("YR_BaekyunFluid",50)}){
    int needed=cost.Value;foreach(var item in stock.Where(t=>t.def.defName==cost.Key)){int take=Math.Min(needed,item.stackCount);if(take>0)plan.Add(new KeyValuePair<Thing,int>(item,take));needed-=take;if(needed==0)break;}
    if(needed>0){reason="연결된 공급원의 예약되지 않은 "+ThingDef.Named(cost.Key).label+"이(가) "+needed+"개 부족합니다.";return false;}
   }
   foreach(var part in plan)daughterInputs.TryAdd(part.Key.SplitOff(part.Value));
   daughterReadyAt=Find.TickManager.TicksGame+5000;daughterRetryAt=0;return true;
  }
  public Pawn TryFinishDaughter(int now){
   if(daughterReadyAt<0||now<daughterReadyAt||now<daughterRetryAt||!parent.Spawned)return null;
   var spaces=GenAdj.CellsAdjacent8Way(parent).Where(c=>c.InBounds(parent.Map)&&c.Standable(parent.Map)&&c.GetFirstPawn(parent.Map)==null).ToList();
   if(spaces.Count==0){daughterRetryAt=now+250;return null;}
   var pawn=SamokDaughterRules.Create(parent.Faction);
   GenSpawn.Spawn(pawn,spaces[0],parent.Map);
   if(!pawn.Spawned||pawn.Destroyed){daughterRetryAt=now+250;return null;}
   daughterInputs.ClearAndDestroyContents();daughterReadyAt=-1;return pawn;
  }
  public override void CompTick(){if(daughterReadyAt>=0)TryFinishDaughter(Find.TickManager.TicksGame);}
  public override void CompTickInterval(int delta){if(daughterReadyAt>=0)TryFinishDaughter(Find.TickManager.TicksGame);}
  public override void CompTickRare(){if(daughterReadyAt>=0)TryFinishDaughter(Find.TickManager.TicksGame);}
  public override void PostDestroy(DestroyMode mode,Map previousMap){if(previousMap!=null)daughterInputs.TryDropAll(parent.Position,previousMap,ThingPlaceMode.Near);base.PostDestroy(mode,previousMap);}
 }
 public class CompProperties_SamokDaughter : CompProperties {public CompProperties_SamokDaughter(){compClass=typeof(CompSamokDaughter);}}
 public class CompSamokDaughter : ThingComp {
  private int lastHeal=-1;
  public override void PostSpawnSetup(bool respawningAfterLoad){base.PostSpawnSetup(respawningAfterLoad);lastHeal=Find.TickManager.TicksGame;SamokDaughterRules.EnsureControl(parent as Pawn);}
  public override IEnumerable<Gizmo> CompGetGizmosExtra(){if(parent is Pawn pawn&&SamokDaughterRules.PlayerControls(pawn)&&pawn.drafter!=null)foreach(var gizmo in SamokDaughterRules.DraftGizmos(pawn))yield return gizmo;}
  public override void CompTick()=>HealAt(Find.TickManager.TicksGame);
  public override void CompTickInterval(int delta)=>HealAt(Find.TickManager.TicksGame);
  public override void CompTickRare()=>HealAt(Find.TickManager.TicksGame);
  public void HealAt(int now){
   if(!(parent is Pawn pawn)||pawn.Dead)return;
   if(lastHeal<0){lastHeal=now;return;}int elapsed=now-lastHeal;if(elapsed<60)return;lastHeal=now;
   float budget=20f*elapsed/60f;
   foreach(var wound in pawn.health.hediffSet.hediffs.OfType<Hediff_Injury>().OrderByDescending(h=>h.Severity).ToList()){
    if(budget<=0)break;float healed=Math.Min(budget,wound.Severity);wound.Heal(healed);budget-=healed;
   }
   var blood=pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss);if(blood!=null)blood.Severity=Math.Max(0,blood.Severity-0.1f*elapsed/60f);
  }
 }
 public static class SamokDaughterRules {
  private static readonly System.Reflection.MethodInfo draftGizmos=AccessTools.Method(typeof(Pawn_DraftController),"GetGizmos");
  public static IEnumerable<Gizmo> DraftGizmos(Pawn pawn)=>(IEnumerable<Gizmo>)draftGizmos.Invoke(pawn.drafter,null);
  public static bool IsDaughter(Pawn pawn)=>pawn?.def.defName=="YR_Tentacle_Girl";
  public static bool PlayerControls(Pawn pawn)=>IsDaughter(pawn)&&pawn.Spawned&&!pawn.Dead&&!pawn.Downed&&!pawn.InMentalState&&pawn.Faction==Faction.OfPlayer;
  public static void EnsureControl(Pawn pawn){
   if(!IsDaughter(pawn)||pawn.Dead||pawn.Faction!=Faction.OfPlayer)return;
   pawn.drafter??=new Pawn_DraftController(pawn);
   pawn.equipment??=new Pawn_EquipmentTracker(pawn);
   pawn.apparel??=new Pawn_ApparelTracker(pawn); // Gear tab reads apparel even for weapon-using animals.
   pawn.skills=null; // Animals do not have human backstories required by skill ticking.
   if(pawn.workSettings==null){pawn.workSettings=new Pawn_WorkSettings(pawn);pawn.workSettings.EnableAndInitialize();pawn.workSettings.SetPriority(WorkTypeDefOf.Hauling,1);}
  }
  public static Pawn Create(Faction faction){
   float age=Math.Max(18,YR_Hentai_Prime_AnimationBed_Mod.Settings?.minimumAge??18)+1;
   var pawn=PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDef.Named("YR_Tentacle_Girl"),faction,forceGenerateNewPawn:true,allowDead:false,allowDowned:false,canGeneratePawnRelations:false,fixedGender:Gender.Female,fixedBiologicalAge:age,fixedChronologicalAge:age));
   EnsureControl(pawn);
   return pawn;
  }
 }
 [HarmonyPatch(typeof(PawnComponentsUtility),nameof(PawnComponentsUtility.AddAndRemoveDynamicComponents))]
 static class DaughterControlComponents {static void Postfix(Pawn pawn)=>SamokDaughterRules.EnsureControl(pawn);}
 [HarmonyPatch]
 static class DaughterContainedJobs {
  static IEnumerable<System.Reflection.MethodBase> TargetMethods(){yield return AccessTools.Method(typeof(Pawn_JobTracker),"JobTrackerTickInterval");yield return AccessTools.Method(typeof(Pawn_JobTracker),"TryFindAndStartJob");}
  static bool Prefix(Pawn ___pawn)=>!SamokDaughterRules.IsDaughter(___pawn)||___pawn.Spawned;
 }
 [HarmonyPatch]
 static class DaughterPlayerOrders {
  static IEnumerable<System.Reflection.MethodBase> TargetMethods(){yield return AccessTools.PropertyGetter(typeof(Pawn),"CanTakeOrder");yield return AccessTools.PropertyGetter(typeof(Pawn),"IsPlayerControlled");}
  static void Postfix(Pawn __instance,ref bool __result){if(SamokDaughterRules.IsDaughter(__instance))__result=SamokDaughterRules.PlayerControls(__instance);}
 }
 [HarmonyPatch]
 static class DaughterAdultFacilityEligibility {
  [HarmonyPatch(typeof(AdultEligibility),nameof(AdultEligibility.IsAdult)),HarmonyPostfix]
  static void Adult(Pawn pawn,ref bool __result){if(SamokDaughterRules.IsDaughter(pawn))__result=!pawn.Dead&&pawn.ageTracker!=null&&EligibilityRules.Allows(pawn.ageTracker.AgeBiologicalYearsFloat,pawn.DevelopmentalStage==DevelopmentalStage.Adult,true,YR_Hentai_Prime_AnimationBed_Mod.Settings?.minimumAge??18);}
  [HarmonyPatch(typeof(UnsupportedRacesV8),nameof(UnsupportedRacesV8.OrdinaryHumanlike)),HarmonyPostfix]
  static void Body(Pawn pawn,ref bool __result){if(SamokDaughterRules.IsDaughter(pawn))__result=pawn.RaceProps.IsFlesh;}
 }
 [HarmonyPatch(typeof(PawnDiedOrDownedThoughtsUtility),"GetThoughts")]
 static class DaughterDeathThoughts {static bool Prefix(Pawn __0,PawnDiedOrDownedThoughtsKind __2)=>!SamokDaughterRules.IsDaughter(__0)||__2!=PawnDiedOrDownedThoughtsKind.Died;}
 [HarmonyPatch(typeof(PawnDiedOrDownedThoughtsUtility),"GiveVeneratedAnimalDiedThoughts")]
 static class DaughterVeneratedDeath {static bool Prefix(Pawn __0)=>!SamokDaughterRules.IsDaughter(__0);}
 [HarmonyPatch(typeof(MemoryThoughtHandler),nameof(MemoryThoughtHandler.TryGainMemory),new[]{typeof(Thought_Memory),typeof(Pawn)})]
 static class DaughterDeathMemories {static bool Prefix(Thought_Memory __0,Pawn __1)=>!(SamokDaughterRules.IsDaughter(__1)&&__1.Dead&&__0?.CurStage?.baseMoodEffect<0);}
 [HarmonyPatch(typeof(Pawn_NeedsTracker),"ShouldHaveNeed")]
 static class DaughterNeeds {static bool Prefix(Pawn ___pawn,ref bool __result){if(!SamokDaughterRules.IsDaughter(___pawn))return true;__result=false;return false;}static void Postfix(Pawn ___pawn,ref bool __result){if(SamokDaughterRules.IsDaughter(___pawn))__result=false;}}
 [HarmonyPatch(typeof(Pawn_NeedsTracker),"AddNeed")]
 static class DaughterExtraNeeds {static bool Prefix(Pawn ___pawn)=>!SamokDaughterRules.IsDaughter(___pawn);}
 [HarmonyPatch(typeof(ApparelUtility),nameof(ApparelUtility.HasPartsToWear))]
 static class DaughterApparelEligibility {static bool Prefix(Pawn __0,ref bool __result){if(!SamokDaughterRules.IsDaughter(__0))return true;__result=false;return false;}}
 [HarmonyPatch(typeof(Pawn_ApparelTracker),nameof(Pawn_ApparelTracker.Wear))]
 static class DaughterApparel {static bool Prefix(Pawn ___pawn)=>!SamokDaughterRules.IsDaughter(___pawn);}
}

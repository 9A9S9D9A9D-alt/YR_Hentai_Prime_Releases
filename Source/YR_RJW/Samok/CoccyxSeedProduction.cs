using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
namespace YR_RJW {
 public sealed class CompProperties_SamokCoccyxSeed:CompProperties {public CompProperties_SamokCoccyxSeed(){compClass=typeof(CompSamokCoccyxSeed);}}
 public sealed class CompSamokCoccyxSeed:ThingComp,IThingHolder {
  public const int ProductionTicks=20000;
  public static readonly KeyValuePair<string,int>[] Costs={new KeyValuePair<string,int>("YR_SamokTentacleRaw",200),new KeyValuePair<string,int>("YR_PulsatingTissue",150),new KeyValuePair<string,int>("YR_Baekgyojil",100),new KeyValuePair<string,int>("YR_BaekyunFluid",100)};
  ThingOwner<Thing> inputs;int readyAt=-1,retryAt;
  public CompSamokCoccyxSeed(){inputs=new ThingOwner<Thing>(this);}
  public int ReadyAt=>readyAt;
  public ThingOwner GetDirectlyHeldThings()=>inputs;
  public void GetChildHolders(List<IThingHolder> children)=>ThingOwnerUtility.AppendThingHoldersFromThings(children,inputs);
  public override void PostExposeData(){base.PostExposeData();Scribe_Deep.Look(ref inputs,"coccyxSeedInputs",this);Scribe_Values.Look(ref readyAt,"coccyxSeedReadyAt",-1);if(Scribe.mode==LoadSaveMode.PostLoadInit)inputs??=new ThingOwner<Thing>(this);}
  public override string CompInspectStringExtra()=>readyAt<0?(inputs.Count>0?"촉수 씨앗: 재료 반환 공간 대기":null):"촉수 씨앗 배양: "+(Find.TickManager.TicksGame<readyAt?((readyAt-Find.TickManager.TicksGame)/2500f).ToString("0.0")+"시간 남음":"완료 · 씨앗 배출 공간 대기");
  public override IEnumerable<Gizmo> CompGetGizmosExtra(){
   if(!parent.Spawned||parent.Faction!=Faction.OfPlayer)yield break;
   var start=new Command_Action{defaultLabel="촉수 이식 씨앗 생산",defaultDesc="연결된 공물함에서 사목의 촉수 200개·맥동조직 150개·백교질 100개·백윤액 100개를 사용합니다. 배양 8시간, 씨앗 1개. 예약된 재료는 사용하지 않습니다.",icon=ThingDef.Named("YR_CoccyxTentacleSeed").uiIcon,action=()=>{if(!TryStart(out string reason))Messages.Message(reason,parent,MessageTypeDefOf.RejectInput,false);}};
   if(readyAt>=0)start.Disable("이미 씨앗을 배양 중입니다.");else if(inputs.Count>0)start.Disable("남은 재료를 먼저 반환하세요.");else if(parent.TryGetComp<CompSamokGrowth>()?.LinkedCount==0)start.Disable("공물함을 먼저 연결하세요.");yield return start;
   if(readyAt>=0||inputs.Count>0)yield return new Command_Action{defaultLabel="씨앗 배양 취소",defaultDesc="배양을 중단하고 보관 중인 재료를 나무 주변에 반환합니다.",icon=TexCommand.ClearPrioritizedWork,action=()=>{if(!Cancel())Messages.Message("남은 재료를 반환할 공간을 확보하세요.",parent,MessageTypeDefOf.RejectInput,false);}};
  }
  public bool TryStart(out string reason){
   reason="";if(!parent.Spawned||parent.Faction!=Faction.OfPlayer){reason="배치된 아군 사목나무가 필요합니다.";return false;}
   if(readyAt>=0||inputs.Count>0){reason="진행 중인 배양이나 반환할 재료가 있습니다.";return false;}
   var network=parent.TryGetComp<CompSamokGrowth>();if(network==null||network.LinkedCount==0){reason="재료를 보관한 공물함을 먼저 연결하세요.";return false;}
   var stock=network.AvailableStock();var plan=new List<KeyValuePair<Thing,int>>();
   foreach(var cost in Costs){int needed=cost.Value;foreach(var item in stock.Where(t=>t.def.defName==cost.Key)){int count=Math.Min(needed,item.stackCount);if(count>0)plan.Add(new KeyValuePair<Thing,int>(item,count));needed-=count;if(needed==0)break;}if(needed>0){reason=ThingDef.Named(cost.Key).label+"이(가) "+needed+"개 부족합니다. 예약된 재료는 제외됩니다.";return false;}}
   var map=parent.Map;var parts=new List<KeyValuePair<Thing,IntVec3>>();
   try{foreach(var entry in plan){var pos=entry.Key.Position;var piece=entry.Key.SplitOff(entry.Value);parts.Add(new KeyValuePair<Thing,IntVec3>(piece,pos));if(!inputs.TryAdd(piece,false))throw new InvalidOperationException("Input escrow rejected a material stack.");}}
   catch(Exception e){foreach(var part in parts){var piece=part.Key;if(piece.Destroyed)continue;if(piece.holdingOwner==inputs)inputs.Remove(piece);if(!GenPlace.TryPlaceThing(piece,part.Value,map,ThingPlaceMode.Near))inputs.TryAdd(piece,false);}Log.Error("[Coccyx seed input] "+e);reason="재료 이관에 실패하여 반환했습니다. 반환 공간이 없으면 나무에 보관됩니다.";return false;}
   readyAt=Find.TickManager.TicksGame+ProductionTicks;retryAt=0;return true;
  }
  public Thing TryFinish(int now){
   if(readyAt<0||now<readyAt||now<retryAt||!parent.Spawned)return null;
   var seed=ThingMaker.MakeThing(ThingDef.Named("YR_CoccyxTentacleSeed"));
   if(!GenPlace.TryPlaceThing(seed,parent.Position,parent.Map,ThingPlaceMode.Near)){seed.Destroy();retryAt=now+250;return null;}
   inputs.ClearAndDestroyContents();readyAt=-1;return seed;
  }
  public bool Cancel(){if(!parent.Spawned)return false;readyAt=-1;inputs.TryDropAll(parent.Position,parent.Map,ThingPlaceMode.Near);return inputs.Count==0;}
  void Advance(){if(readyAt>=0)TryFinish(Find.TickManager.TicksGame);}
  public override void CompTick()=>Advance();
  public override void CompTickInterval(int delta)=>Advance();
  public override void CompTickRare()=>Advance();
  public override void PostDestroy(DestroyMode mode,Map previousMap){if(previousMap!=null)inputs.TryDropAll(parent.Position,previousMap,ThingPlaceMode.Near);base.PostDestroy(mode,previousMap);}
 }
}

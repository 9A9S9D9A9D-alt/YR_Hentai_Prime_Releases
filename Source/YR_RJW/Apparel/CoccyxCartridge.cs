using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
namespace YR_RJW {
 public sealed partial class HediffComp_CoccyxCombat:IThingHolder {
  public const string CartridgeDef="YR_BaekyunCartridge";
  public const int ReserveLimit=5,ChargesPerStock=6;
  ThingOwner<Thing> cartridges;
  ThingOwner<Thing> Cartridges=>cartridges??(cartridges=new ThingOwner<Thing>(this));
  public IThingHolder ParentHolder=>Pawn;
  public ThingOwner GetDirectlyHeldThings()=>Cartridges;
  public void GetChildHolders(List<IThingHolder> children)=>ThingOwnerUtility.AppendThingHoldersFromThings(children,Cartridges);
  public int ReserveCartridges=>cartridges?.TotalStackCount??0;
  public int FluidCharges=>fluidStock*ChargesPerStock+remotePaidShots;
  void ExposeCartridges(){Scribe_Deep.Look(ref cartridges,"coccyxCartridgeHolder",this);}
  public bool TryAutoLoadCartridge(){
   if(Pawn.Dead||FluidCharges>0||cartridges==null)return false;
   var item=Cartridges.FirstOrDefault(t=>t.def.defName==CartridgeDef);if(item==null)return false;
   item.SplitOff(1).Destroy();fluidStock=StockCapacity;remotePaidShots=0;return true;
  }
  public bool TryConsumeFluid(int charges,bool allowSwap=true){
   if(charges<=0||FluidCharges+(allowSwap?ReserveCartridges*StockCapacity*ChargesPerStock:0)<charges)return false;
   while(charges>0){if(FluidCharges==0&&(!allowSwap||!TryAutoLoadCartridge()))return false;int take=Math.Min(charges,FluidCharges),left=FluidCharges-take;fluidStock=left/ChargesPerStock;remotePaidShots=left%ChargesPerStock;charges-=take;}
   if(allowSwap)TryAutoLoadCartridge();return true;
  }
  public int LoadCartridges(Thing source){
   if(source?.def.defName!=CartridgeDef||Pawn.Dead)return 0;
   int loaded=0;
   TryAutoLoadCartridge();
   while(!source.Destroyed&&source.holdingOwner!=Cartridges&&source.stackCount>0&&ReserveCartridges<ReserveLimit){
    int count=Math.Min(ReserveLimit-ReserveCartridges,source.stackCount);
    int moved;
    if(source.Spawned){
     var map=source.Map;var cell=source.Position;
     var piece=source.SplitOff(count);
     if(piece.Spawned)piece.DeSpawn();
     if(!Cartridges.TryAdd(piece)){
      GenPlace.TryPlaceThing(piece,cell,map,ThingPlaceMode.Near);
      break;
     }
     moved=count;
    }else moved=Cartridges.TryAddOrTransfer(source,count);
    if(moved==0)break;
    loaded+=moved;
    TryAutoLoadCartridge();
   }
   return loaded;
  }
  public void ReleaseCartridges(){
   var map=Pawn.MapHeld??Pawn.Corpse?.MapHeld;var pos=Pawn.MapHeld!=null?Pawn.PositionHeld:Pawn.Corpse?.PositionHeld??IntVec3.Invalid;
   if(map!=null&&pos.IsValid)Cartridges.TryDropAll(pos,map,ThingPlaceMode.Near);
   if(Pawn.inventory!=null)foreach(var item in Cartridges.ToList())Cartridges.TryTransferToContainer(item,Pawn.inventory.innerContainer);
  }
  public override void Notify_PawnDied(DamageInfo? dinfo,Hediff culprit=null){EndEscort();ReleaseCartridges();base.Notify_PawnDied(dinfo,culprit);}
  public void OrderCartridgeLoad(){
   if(!Pawn.Spawned)return;
   var carried=Pawn.inventory?.innerContainer.FirstOrDefault(t=>t.def.defName==CartridgeDef);if(carried!=null){LoadCartridges(carried);return;}
   var source=Pawn.Map.listerThings.ThingsOfDef(ThingDef.Named(CartridgeDef)).Where(t=>!t.IsForbidden(Pawn)&&Pawn.CanReserveAndReach(t,PathEndMode.Touch,Danger.Some)).OrderBy(t=>t.Position.DistanceToSquared(Pawn.Position)).FirstOrDefault();
   if(source==null){Messages.Message("장전할 백윤액 카트리지가 없습니다.",MessageTypeDefOf.RejectInput,false);return;}
   Pawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("YR_CoccyxLoadCartridge"),source),JobTag.Misc);
  }
  IEnumerable<Gizmo> CartridgeGizmos(){
   var load=new Command_Action{defaultLabel="카트리지 "+ReserveCartridges+"/5",defaultDesc="장탄 "+(FluidCharges/6f).ToString("0.#")+"/20스톡 · 예비 "+ReserveCartridges+"/5개.\n카트리지를 가져와 이식체 전용 홀더에 장전합니다. 완전히 소진하면 예비 1개로 20스톡을 자동 충전합니다.",icon=ThingDef.Named(CartridgeDef).uiIcon,action=OrderCartridgeLoad};
   if(ReserveCartridges>=ReserveLimit)load.Disable("예비 홀더가 가득 찼습니다.");yield return load;
   if(ReserveCartridges>0)yield return new Command_Action{defaultLabel="카트리지 꺼내기",defaultDesc="홀더의 예비 카트리지를 실물로 꺼냅니다. 현재 장탄은 유지합니다.",icon=ThingDef.Named(CartridgeDef).uiIcon,action=ReleaseCartridges};
  }
 }
 [HarmonyPatch(typeof(Pawn),"GetChildHolders")]
 static class CoccyxCartridgeChildHolder {
  static void Postfix(Pawn __instance,List<IThingHolder> outChildren){var comp=__instance.health?.hediffSet.hediffs.Select(h=>h.TryGetComp<HediffComp_CoccyxCombat>()).FirstOrDefault(c=>c!=null);if(comp!=null&&!outChildren.Contains(comp))outChildren.Add(comp);}
 }
}

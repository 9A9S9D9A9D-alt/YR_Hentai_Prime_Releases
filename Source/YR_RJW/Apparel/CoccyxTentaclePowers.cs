using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
namespace YR_RJW {
 public sealed partial class HediffComp_CoccyxCombat {
  public const int FrenzyCooldown=417;
  public const int BatteryCapacity=17500,ChargeDuration=2500,StockCapacity=20;
  public bool electricOn,fluidOn;public float battery;public int fluidStock,jumpReady,frenzyReady,frenzyStart=-1;
  public IntVec3 fluidAim=IntVec3.Invalid,jumpAim=IntVec3.Invalid;public bool frenzyPending;
  int lastPowerTick=-1,nextSpray,frenzyHits;public int lastSprayTick=-9999;public Vector3 lastSprayDirection,lastSprayOrigin;
  public bool ElectricActive=>electricOn&&battery>0;
  public bool FrenzyActive=>frenzyStart>=0;
  public bool PowerPending=>jumpAim.IsValid||frenzyPending||FrenzyActive||fluidOn;
  public static float JumpRange=>(DefDatabase<ThingDef>.GetNamedSilentFail("Apparel_PackJump")?.GetStatValueAbstract(StatDefOf.JumpRange)??23.9f)+4;
  void ExposePowers(){
   ExposeCartridges();
   Scribe_Values.Look(ref emergencyTendWork,"tentacleEmergencyTendWork");
   Scribe_Values.Look(ref remoteFluidMode,"tentacleRemoteFluidMode");Scribe_Values.Look(ref continuousFluidMode,"tentacleContinuousFluidMode");Scribe_Values.Look(ref remotePaidShots,"tentacleRemotePaidShots");
   Scribe_Values.Look(ref electricOn,"tentacleElectricOn");Scribe_Values.Look(ref battery,"tentacleBattery");Scribe_Values.Look(ref fluidStock,"tentacleFluidStock");Scribe_Values.Look(ref fluidOn,"tentacleFluidOn");Scribe_Values.Look(ref fluidAim,"tentacleFluidAim",IntVec3.Invalid);Scribe_Values.Look(ref jumpAim,"tentacleJumpAim",IntVec3.Invalid);Scribe_Values.Look(ref jumpReady,"tentacleJumpReady");Scribe_Values.Look(ref frenzyReady,"tentacleFrenzyReady");Scribe_Values.Look(ref frenzyStart,"tentacleFrenzyStart",-1);Scribe_Values.Look(ref frenzyPending,"tentacleFrenzyPending");Scribe_Values.Look(ref frenzyHits,"tentacleFrenzyHits");Scribe_Values.Look(ref nextSpray,"tentacleNextSpray");Scribe_Values.Look(ref lastSprayTick,"tentacleLastSprayTick",-9999);Scribe_Values.Look(ref lastSprayDirection,"tentacleLastSprayDirection");Scribe_Values.Look(ref lastSprayOrigin,"tentacleLastSprayOrigin");
  }
  public void TickPowers(int now){
   int delta=lastPowerTick<0?0:Math.Max(0,now-lastPowerTick);lastPowerTick=now;
   TickEmergencyTend(delta);if(frenzyReady-now>FrenzyCooldown)frenzyReady=now+FrenzyCooldown;
   if(electricOn){battery=Mathf.Max(0,battery-delta);if(battery<=0)electricOn=false;}
   if(!Pawn.Spawned||Pawn.Dead)return;
   if(!Pawn.Downed&&(!continuousFluidMode||autoAttack)&&!limbs.Any(l=>l.Active&&l.continuous))TryAutoLoadCartridge();
   if(FrenzyActive){while(frenzyHits<9&&now-frenzyStart>=(frenzyHits+1)*10){frenzyHits++;CoccyxAttackSound.Play(Pawn,"Frenzy");foreach(var target in NearbyEnemies(5).ToArray())strike.Resolve(Pawn,target,4,ElectricActive);}if(now-frenzyStart>=90)frenzyStart=-1;}
   if(!limbs.Any(l=>l.Active)){
    if(jumpAim.IsValid){var cell=jumpAim;jumpAim=IntVec3.Invalid;if(now>=jumpReady&&CanJump(cell)){CoccyxAttackSound.Play(Pawn,"Jump");if(JumpUtility.DoJump(Pawn,cell,null,JumpProps(),null,LocalTargetInfo.Invalid,null))jumpReady=now+7500;}return;}
    if(frenzyPending){frenzyPending=false;if(now>=frenzyReady){frenzyStart=now;frenzyHits=0;frenzyReady=now+FrenzyCooldown;}}
   }
   if(fluidOn&&!limbs.Any(l=>l.Active)&&(FluidCharges>0||ReserveCartridges>0)&&Pawn.Drafted&&fluidAim.IsValid&&now>=nextSpray){Spray(fluidAim,now);nextSpray=now+15;}
   if(FluidCharges==0)fluidOn=false;
  }
  IEnumerable<Pawn> NearbyEnemies(float radius)=>Pawn.Map.mapPawns.AllPawnsSpawned.Where(p=>p!=Pawn&&!p.Dead&&!p.Downed&&p.HostileTo(Pawn)&&p.Position.DistanceToSquared(Pawn.Position)<=radius*radius&&GenSight.LineOfSight(Pawn.Position,p.Position,Pawn.Map));
  public bool CanJump(IntVec3 cell)=>Pawn.Spawned&&!Pawn.Downed&&cell.InBounds(Pawn.Map)&&cell.Walkable(Pawn.Map)&&!cell.Fogged(Pawn.Map)&&Pawn.Position.DistanceToSquared(cell)<=JumpRange*JumpRange&&JumpUtility.ValidJumpTarget(Pawn,Pawn.Map,cell);
  VerbProperties JumpProps()=>new VerbProperties{flyWithCarriedThing=true,flightEffecterDef=null,soundLanding=DefDatabase<SoundDef>.GetNamed("YR_Coccyx_Land")};
  public void Spray(IntVec3 aim,int now){
   if(!Pawn.Spawned||FluidCharges+ReserveCartridges*120<6)return;
   Vector3 direction=aim.ToVector3Shifted()-Pawn.DrawPos;direction.y=0;if(direction.sqrMagnitude<.01f)return;direction.Normalize();lastSprayDirection=direction;lastSprayOrigin=Pawn.DrawPos;lastSprayTick=now;if(!TryConsumeFluid(6))return;CoccyxAttackSound.Play(Pawn,"Fluid");
   var hitCells=new HashSet<IntVec3>();
   foreach(var cell in GenRadial.RadialCellsAround(Pawn.Position,6,true)){
    if(!cell.InBounds(Pawn.Map)||!cell.Walkable(Pawn.Map))continue;Vector3 d=cell.ToVector3Shifted()-Pawn.DrawPos;d.y=0;
    if(d.sqrMagnitude<1||Vector3.Dot(d.normalized,direction)<.866f||!GenSight.LineOfSight(Pawn.Position,cell,Pawn.Map))continue;
    hitCells.Add(cell);var puddle=cell.GetThingList(Pawn.Map).OfType<CoccyxSlimePuddle>().FirstOrDefault();
    if(puddle==null)puddle=(CoccyxSlimePuddle)GenSpawn.Spawn(ThingMaker.MakeThing(ThingDef.Named("YR_CoccyxSlimePuddle")),cell,Pawn.Map);puddle.expires=now+600;
   }
   foreach(var target in Pawn.Map.mapPawns.AllPawnsSpawned.Where(p=>p!=Pawn&&!p.Dead&&hitCells.Contains(p.Position))){var def=DefDatabase<HediffDef>.GetNamed("YR_CoccyxSlimeCoat");var h=target.health.hediffSet.GetFirstHediffOfDef(def)??target.health.AddHediff(def);var disappear=h.TryGetComp<HediffComp_Disappears>();if(disappear!=null)disappear.ticksToDisappear=600;CoccyxAttackSound.Play(target,"FluidImpact");}
  }
  public bool CanCharge(Thing source)=>source?.Spawned==true&&source.Map==Pawn.Map&&source.def.defName=="YR_SamokElectricSac"&&source.TryGetComp<CompPowerPlant_SamokElectric>()?.PowerOutput>0;
  public void ChargeTick(int delta){electricOn=false;battery=Mathf.Min(BatteryCapacity,battery+(float)BatteryCapacity/ChargeDuration*delta);}
  public int Refill(Thing resource){if(resource?.def.defName!="YR_BaekyunFluid")return 0;int count=Math.Min(StockCapacity-fluidStock-(remotePaidShots>0?1:0),resource.stackCount);if(count>0){resource.SplitOff(count).Destroy();fluidStock+=count;}return count;}
  Texture2D Icon(string name)=>ContentFinder<Texture2D>.Get("Yuran/Implants/CoccyxTentacles/"+name);
  void Service(bool charge){
   if(!Pawn.Spawned)return;Thing source=charge?Pawn.Map.listerBuildings.allBuildingsColonist.Where(b=>CanCharge(b)&&Pawn.CanReserveAndReach(b,PathEndMode.Touch,Danger.Some)).OrderBy(b=>b.Position.DistanceToSquared(Pawn.Position)).FirstOrDefault():Pawn.Map.listerThings.ThingsOfDef(ThingDef.Named("YR_BaekyunFluid")).Where(t=>!t.IsForbidden(Pawn)&&Pawn.CanReserveAndReach(t,PathEndMode.Touch,Danger.Some)).OrderBy(t=>t.Position.DistanceToSquared(Pawn.Position)).FirstOrDefault();
   if(source==null){Messages.Message(charge?"충전할 수 있는 전기낭이 없습니다.":"충전할 백윤액이 없습니다.",MessageTypeDefOf.RejectInput,false);return;}
   Pawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed(charge?"YR_CoccyxCharge":"YR_CoccyxRefill"),source),JobTag.Misc);
  }
  void DrawSprayRange(IntVec3 aim){
   if(!Pawn.Spawned)return;GenDraw.DrawRadiusRing(Pawn.Position,6);var direction=aim.ToVector3Shifted()-Pawn.DrawPos;direction.y=0;if(direction.sqrMagnitude<.01f)return;direction.Normalize();
   var cells=GenRadial.RadialCellsAround(Pawn.Position,6,true).Where(cell=>cell.InBounds(Pawn.Map)&&cell.Walkable(Pawn.Map)).Where(cell=>{var d=cell.ToVector3Shifted()-Pawn.DrawPos;d.y=0;return d.sqrMagnitude>=1&&Vector3.Dot(d.normalized,direction)>=.866f&&GenSight.LineOfSight(Pawn.Position,cell,Pawn.Map);}).ToList();GenDraw.DrawFieldEdges(cells);
  }
  IEnumerable<Gizmo> PowerGizmos(){
   int now=Find.TickManager.TicksGame;
   foreach(var gizmo in CartridgeGizmos())yield return gizmo;
   var jump=new CoccyxRangeTarget{origin=Pawn,radius=JumpRange,defaultLabel="촉수 점프",defaultDesc="점프팩보다 4칸 멀리 도약합니다. 재사용 3시간. 진행 중인 촉수 공격은 복귀 후 도약합니다.",icon=Icon("JumpIcon"),targetingParams=new TargetingParameters{canTargetLocations=true,canTargetPawns=false,canTargetBuildings=false,validator=t=>CanJump(t.Cell)},action=t=>jumpAim=t.Cell};if(now<jumpReady)jump.Disable("재사용 대기: "+((jumpReady-now)/2500f).ToString("0.0")+"시간");jump.onUpdate=t=>GenDraw.DrawRadiusRing(Pawn.Position,JumpRange);yield return jump;
   yield return new CoccyxRangeTarget{origin=Pawn,radius=6,onUpdate=t=>DrawSprayRange(t.Cell),defaultLabel="백윤액 난사 "+fluidStock+"/"+StockCapacity,defaultDesc="방향을 지정해 흰색 백윤액을 연속 분사합니다. 다시 선택하면 분사를 멈춥니다. 스톡을 소모하며 잠시 웅덩이를 남깁니다.",icon=Icon("SlimeSpray"),targetingParams=new TargetingParameters{canTargetLocations=true,canTargetPawns=true,canTargetBuildings=false},action=t=>{if(fluidOn)fluidOn=false;else if(FluidCharges+ReserveCartridges*120>=6){SetRemoteFluidMode(false);fluidAim=t.Cell;fluidOn=true;}}};
   yield return new Command_Action{defaultLabel="백윤액 충전",defaultDesc="백윤액 자원을 가져와 스톡을 충전합니다.",icon=Icon("SlimePuddle"),action=()=>Service(false)};
   yield return new Command_Toggle{defaultLabel="전기 촉수 "+(battery/2500f).ToString("0.0")+"시간",defaultDesc="ON인 동안 모든 촉수 공격에 전기 효과를 추가합니다. 풀충전 유지 7시간.",icon=Icon("ElectricOverlay"),isActive=()=>ElectricActive,toggleAction=()=>{if(electricOn)electricOn=false;else if(battery>0)electricOn=true;else Messages.Message("전기낭에서 배터리를 충전하세요.",MessageTypeDefOf.RejectInput,false);}};
   yield return new Command_Action{defaultLabel="전기낭에서 충전",defaultDesc="작동 중인 전기낭에 접근해 충전합니다. 완전 방전에서 풀충전까지 1시간.",icon=Icon("ChargeIcon"),action=()=>Service(true)};
   var frenzy=new CoccyxRangeAction{origin=Pawn,radius=5,defaultLabel="촉수 난무",defaultDesc="반경 5칸의 적에게 1.5초 동안 피해 4로 9회 근접 공격합니다. 재사용 10분.",icon=Icon("FrenzyIcon"),action=()=>{if(Pawn.Drafted&&!Pawn.Downed)frenzyPending=true;}};if(now<frenzyReady)frenzy.Disable("재사용 대기: "+((frenzyReady-now)*60f/2500f).ToString("0.0")+"분");yield return frenzy;
  }
 }
 public sealed class JobDriver_CoccyxService:JobDriver {
  HediffComp_CoccyxCombat Comp=>pawn.health.hediffSet.hediffs.Select(h=>h.TryGetComp<HediffComp_CoccyxCombat>()).FirstOrDefault(c=>c!=null);
  public override bool TryMakePreToilReservations(bool errorOnFailed)=>pawn.Reserve(job.targetA,job,1,-1,null,errorOnFailed);
  protected override IEnumerable<Toil> MakeNewToils(){
   yield return Toils_Goto.GotoThing(TargetIndex.A,PathEndMode.Touch);
   var toil=new Toil{defaultCompleteMode=ToilCompleteMode.Never};
   toil.tickIntervalAction=delta=>{var comp=Comp;if(comp==null||!job.targetA.HasThing||job.targetA.Thing.Destroyed){EndJobWith(JobCondition.Incompletable);return;}if(job.def.defName=="YR_CoccyxLoadCartridge"){comp.LoadCartridges(job.targetA.Thing);ReadyForNextToil();return;}if(job.def.defName=="YR_CoccyxRefill"){comp.Refill(job.targetA.Thing);ReadyForNextToil();return;}if(!comp.CanCharge(job.targetA.Thing)){EndJobWith(JobCondition.Incompletable);return;}comp.ChargeTick(delta);if(comp.battery>=HediffComp_CoccyxCombat.BatteryCapacity)ReadyForNextToil();};
   yield return toil;
  }
 }
 public sealed class CoccyxSlimePuddle:ThingWithComps {
  public int expires;
  static readonly Material[] wet=new Material[3];
  protected override void DrawAt(Vector3 drawLoc,bool flip=false){
   int seed=Math.Abs(Position.x*397^Position.z*7919);int v=seed%3;
   if(wet[v]==null)wet[v]=MaterialPool.MatFrom("Yuran/Implants/CoccyxTentacles/SlimePuddle"+(v==0?"":v==1?"B":"C"),ShaderDatabase.Transparent,new Color(1,1,1,.48f));
   drawLoc.x+=((seed%17)/16f-.5f)*.45f;drawLoc.z+=((seed%23)/22f-.5f)*.45f;
   float size=1.35f+(seed%11)*.045f;
   Graphics.DrawMesh(MeshPool.plane10,Matrix4x4.TRS(drawLoc,Quaternion.AngleAxis(seed%360,Vector3.up),new Vector3(size,1,size)),wet[v],0);
  }
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref expires,"coccyxSlimeExpires");}
  protected override void TickInterval(int delta){base.TickInterval(delta);if(Find.TickManager.TicksGame>=expires)Destroy();}
 }
 public sealed class PawnRenderNode_CoccyxSlime:PawnRenderNode {
  public PawnRenderNode_CoccyxSlime(Pawn p,PawnRenderNodeProperties props,PawnRenderTree tree):base(p,props,tree){}
  public override Graphic GraphicFor(Pawn p)=>GraphicDatabase.Get<Graphic_Single>("Yuran/Implants/CoccyxTentacles/SlimeCoat",ShaderDatabase.Transparent,Props.drawSize,new Color(1,1,1,.55f));
 }
 [HarmonyLib.HarmonyPatch(typeof(Pawn_PathFollower),"CostToMoveIntoCell",new[]{typeof(Pawn),typeof(IntVec3)})]
 static class CoccyxPuddleSlowdown {
  static void Postfix(Pawn __0,IntVec3 __1,ref float __result){if(__0.Spawned&&__1.InBounds(__0.Map)&&__1.GetThingList(__0.Map).Any(t=>t is CoccyxSlimePuddle))__result/=.3f;}
 }
}




namespace YR_RJW {
 public sealed class CoccyxRangeTarget:Command_Target {
  public Pawn origin;public float radius;
  public override void GizmoUpdateOnMouseover(){base.GizmoUpdateOnMouseover();if(origin.Spawned)GenDraw.DrawRadiusRing(origin.Position,radius);}
 }
 public sealed class CoccyxRangeAction:Command_Action {
  public Pawn origin;public float radius;
  public override void GizmoUpdateOnMouseover(){base.GizmoUpdateOnMouseover();if(origin.Spawned)GenDraw.DrawRadiusRing(origin.Position,radius);}
 }
 [HarmonyLib.HarmonyPatch(typeof(PawnRenderer),"GetDrawParms")]
 static class CoccyxFrenzySpin {
  static void Postfix(Pawn ___pawn,ref PawnDrawParms __result){if(__result.Portrait)return;var c=___pawn.health?.hediffSet.GetFirstHediffOfDef(DefDatabase<HediffDef>.GetNamed("YR_CoccyxTentacleImplant"))?.TryGetComp<HediffComp_CoccyxCombat>();if(c?.FrenzyActive==true)__result.facing=new Rot4(((Find.TickManager.TicksGame-c.frenzyStart)/6)%4);}
 }
}

namespace YR_RJW {
 public static class CoccyxAttackSound {
  sealed class Gate{public Gate(){}public readonly System.Collections.Generic.Dictionary<string,int> played=new System.Collections.Generic.Dictionary<string,int>();}
  static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Pawn,Gate> gates=new System.Runtime.CompilerServices.ConditionalWeakTable<Pawn,Gate>();
  public static void Play(Pawn pawn,string cue,IntVec3? at=null){
   if(pawn?.Spawned!=true)return;int tick=Find.TickManager.TicksGame;var gate=gates.GetOrCreateValue(pawn);int interval=cue=="Fluid"||cue=="FluidSpit"||cue=="FluidImpact"?15:cue=="Frenzy"?8:4;
   if(gate.played.TryGetValue(cue,out int previous)&&tick-previous<interval)return;gate.played[cue]=tick;
   var info=at.HasValue?Verse.Sound.SoundInfo.InMap(new TargetInfo(at.Value,pawn.Map)):Verse.Sound.SoundInfo.InMap(pawn);Verse.Sound.SoundStarter.PlayOneShot(DefDatabase<SoundDef>.GetNamed("YR_Coccyx_"+cue),info);
  }
 }
}

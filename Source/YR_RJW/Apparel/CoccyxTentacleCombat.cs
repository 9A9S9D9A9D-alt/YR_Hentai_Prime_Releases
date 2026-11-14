using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using HarmonyLib;
using UnityEngine;
using Verse;
namespace YR_RJW {
 public sealed class HediffCompProperties_CoccyxCombat:HediffCompProperties {public HediffCompProperties_CoccyxCombat(){compClass=typeof(HediffComp_CoccyxCombat);}}
 public sealed class CoccyxLimb:IExposable {
  public Pawn target;public int started=-1,nextReady;public bool impacted,electric,swung;public Vector3 endpoint;
  public bool remoteFluid,fluidLanded,continuous;
  public int streamStart=-1,streamStop=-1,nextStreamPulse;public int shotTick=-1,shotDuration=8;public Vector3 curveB,curveC,shotOrigin,shotEnd,firingOffset;
  public int trajectoryPattern,trajectoryVariation;
  public bool reinforced;
  public int reinforcedPulses;
  public float strikeWidth,strikeBend,strikeBias=.5f,reachEase=1f;
  public bool Active=>started>=0;
  public void ExposeData(){Scribe_Values.Look(ref reinforced,"reinforced");Scribe_Values.Look(ref reinforcedPulses,"reinforcedPulses");Scribe_Values.Look(ref continuous,"continuousFluidLimb");Scribe_Values.Look(ref streamStart,"fluidStreamStart",-1);Scribe_Values.Look(ref streamStop,"fluidStreamStop",-1);Scribe_Values.Look(ref nextStreamPulse,"fluidStreamPulse");Scribe_Values.Look(ref trajectoryPattern,"strikeTrajectoryPattern");Scribe_Values.Look(ref trajectoryVariation,"strikeTrajectoryVariation");Scribe_Values.Look(ref strikeWidth,"strikeTrajectoryWidth");Scribe_Values.Look(ref strikeBend,"strikeBend");Scribe_Values.Look(ref strikeBias,"strikeBias",.5f);Scribe_Values.Look(ref reachEase,"strikeReachEase",1f);Scribe_References.Look(ref target,"target");Scribe_Values.Look(ref started,"started",-1);Scribe_Values.Look(ref nextReady,"nextReady");Scribe_Values.Look(ref impacted,"impacted");Scribe_Values.Look(ref swung,"swung");Scribe_Values.Look(ref electric,"electric");Scribe_Values.Look(ref endpoint,"endpoint");Scribe_Values.Look(ref remoteFluid,"remoteFluid");Scribe_Values.Look(ref fluidLanded,"fluidLanded");Scribe_Values.Look(ref shotTick,"fluidShotTick",-1);Scribe_Values.Look(ref shotDuration,"fluidShotDuration",8);Scribe_Values.Look(ref firingOffset,"fluidFiringOffset");Scribe_Values.Look(ref curveB,"fluidCurveB");Scribe_Values.Look(ref curveC,"fluidCurveC");Scribe_Values.Look(ref shotOrigin,"fluidShotOrigin");Scribe_Values.Look(ref shotEnd,"fluidShotEnd");}
 }
 public sealed class Verb_CoccyxStrike:Verb_MeleeAttack {
  internal const int Type1Damage = 6;
  static readonly Func<Verb_MeleeAttack,LocalTargetInfo,float> Dodge=AccessTools.MethodDelegate<Func<Verb_MeleeAttack,LocalTargetInfo,float>>(AccessTools.Method(typeof(Verb_MeleeAttack),"GetDodgeChance"));
  public Verb_CoccyxStrike(){verbProps=new VerbProperties{verbClass=typeof(Verb_CoccyxStrike),meleeDamageDef=DamageDefOf.Blunt,meleeDamageBaseAmount=Type1Damage,meleeArmorPenetrationBase=.5f,defaultCooldownTime=3};}
  float amount=Type1Damage;
  float armorPenetration=.5f;
  public bool Resolve(Pawn owner,Pawn target,float damage=Type1Damage,bool electric=false) =>
   Resolve(owner,target,damage,electric,.5f);
  internal bool Resolve(Pawn owner,Pawn target,float damage,bool electric,float penetration,string impactCue=null) {
   amount=damage;
   armorPenetration=penetration;
   caster=owner;currentTarget=target;

   if(!Rand.Chance(owner.GetStatValue(StatDefOf.MeleeHitChance))||Rand.Chance(Dodge(this,target)))return false;
   CoccyxAttackSound.Play(owner,impactCue??"Impact",impactCue==null?(IntVec3?)null:target.Position);ApplyMeleeDamageToTarget(target);if(electric&&!target.Dead){CoccyxAttackSound.Play(owner,"Electric");FleckMaker.ThrowLightningGlow(target.DrawPos,target.Map,1.5f);FleckMaker.ThrowMicroSparks(target.DrawPos,target.Map);if(target.RaceProps.IsMechanoid)target.TakeDamage(new DamageInfo(DamageDefOf.EMP,1,0,-1,owner));else target.stances?.stunner.StunFor(30,owner);}return true;
  }
  protected override DamageWorker.DamageResult ApplyMeleeDamageToTarget(LocalTargetInfo target) {
   var info=new DamageInfo(DamageDefOf.Blunt,amount,armorPenetration,-1f,CasterPawn);
   info.SetWeaponHediff(DefDatabase<HediffDef>.GetNamed("YR_CoccyxTentacleImplant"));
   return target.Thing.TakeDamage(info);
  }
 }
 public sealed partial class HediffComp_CoccyxCombat:HediffComp {
  public const int CycleTicks=180,WarmupTicks=81,ImpactTicks=96,FinishTicks=129;
  public const float Type1Range = 17f;
  public const float Type2Range = 13f;
  public const float Type2SequentialRange = 8f;
  public static float Range=>ThingDef.Named("Bow_Short").Verbs[0].range-2.5f;
  public bool autoAttack,sequentialAttack;public Pawn focus;
  int nextSequentialAt,nextSequentialLimb;
  public const int SequentialInterval=CycleTicks/6;
  public void SetSequentialAttack(bool value){sequentialAttack=value;nextSequentialAt=Find.TickManager.TicksGame;nextSequentialLimb=0;}
  public List<CoccyxLimb> limbs=Enumerable.Range(0,6).Select(i=>new CoccyxLimb()).ToList();
  int nextSearch;readonly Verb_CoccyxStrike strike=new Verb_CoccyxStrike();
  internal readonly CoccyxTentacleDrawing drawing=new CoccyxTentacleDrawing();
  public int Attempts{get;private set;}public int Hits{get;private set;}
  public override void CompExposeData(){base.CompExposeData();ExposeArmorGrant();ExposePowers();ExposeEscort();ExposeCalibration();ExposeReinforcedAttack();ExposeThrowCarry();Scribe_Values.Look(ref autoAttack,"autoTentacleAttack");Scribe_Values.Look(ref sequentialAttack,"tentacleSequentialAttack");Scribe_Values.Look(ref nextSequentialAt,"tentacleNextSequentialAt");Scribe_Values.Look(ref nextSequentialLimb,"tentacleNextSequentialLimb");Scribe_References.Look(ref focus,"tentacleFocus");Scribe_Collections.Look(ref limbs,"tentacleLimbs",LookMode.Deep);if(Scribe.mode==LoadSaveMode.PostLoadInit){if(limbs==null||limbs.Count!=6)limbs=Enumerable.Range(0,6).Select(i=>new CoccyxLimb()).ToList();nextSequentialLimb=Math.Max(0,Math.Min(5,nextSequentialLimb));}}
  public override void CompPostTickInterval(ref float severityAdjustment,int delta){base.CompPostTickInterval(ref severityAdjustment,delta);TickCombat(Find.TickManager.TicksGame);}
  public override void CompPostTick(ref float severityAdjustment){base.CompPostTick(ref severityAdjustment);TickCombat(Find.TickManager.TicksGame);}
  public void BeginStrike(int index,Pawn target,int now){if(EscortTarget!=null||Hediff_CoccyxEscort.IsHeld(Pawn)||!Pawn.Spawned||Pawn.Dead||target?.Spawned!=true||target.Dead||target.Map!=Pawn.Map)return;if(remoteFluidMode&&AvailableRemoteShots<=0)return;var l=limbs[index];l.target=target;l.started=now;l.nextReady=now+CycleTicks;l.impacted=false;l.swung=false;l.electric=ElectricActive;l.endpoint=target.DrawPos;l.trajectoryPattern=SamokTrajectory.Next(l.trajectoryPattern);l.trajectoryVariation=Rand.Range(1,int.MaxValue);l.strikeWidth=Mathf.Clamp((l.endpoint-Pawn.DrawPos).magnitude*.36f,1.25f,3.4f)*(Rand.Bool?1:-1);float bend=Mathf.Clamp((target.DrawPos-Pawn.DrawPos).magnitude*.2f,.3f,2.5f);l.strikeBend=Rand.Range(-bend,bend);l.strikeBias=Rand.Range(-.7f,1.1f);l.reachEase=Rand.Range(.75f,1.3f);if(!PrepareRemote(index,l)){l.started=-1;l.target=null;}}
  public void TickCombat(int now){
   if(TickThrowCarry()||TickEscort(now)||Hediff_CoccyxEscort.IsHeld(Pawn))return;
   if(Type2Active)CancelFluidAttacks();
   TickPowers(now);if(!Pawn.Spawned||Pawn.Dead)return;
   TickReinforcedAttack(now);
   foreach(var l in limbs){if(!l.Active||l.reinforced)continue;
    if(!l.impacted&&l.target?.Spawned==true&&l.target.Map==Pawn.Map){l.endpoint=l.target.DrawPos+(l.remoteFluid?l.firingOffset:Vector3.zero);}
    int elapsed=now-l.started;
    if(!l.swung&&elapsed>=WarmupTicks){l.swung=true;CoccyxAttackSound.Play(Pawn,"Swing");if(l.electric)CoccyxAttackSound.Play(Pawn,"Electric");}
    if(l.continuous){TickContinuous(l,now,elapsed);continue;}
    if(!l.impacted&&elapsed>=(l.remoteFluid?RemoteFireAt:ImpactTicks)){l.impacted=true;if(l.target?.Spawned==true&&!l.target.Dead&&l.target.Map==Pawn.Map){Attempts++;if(l.remoteFluid){if(FireRemote(l,now))Hits++;}else if(strike.Resolve(Pawn,l.target,Verb_CoccyxStrike.Type1Damage,l.electric,.10f))Hits++;}}
    if(elapsed>=(l.remoteFluid?RemoteFinishAt:FinishTicks)){l.started=-1;l.target=null;}
   }
   UpdateRemoteShots(now);
   if(PowerPending||!autoAttack||!Pawn.Drafted||Pawn.Downed||now<nextSearch||(remoteFluidMode&&AvailableRemoteShots<=0))return;
   if(SprayAttackMode){
    if(now<nextSpray||limbs.Any(l=>l.Active)||FluidCharges+ReserveCartridges*StockCapacity*ChargesPerStock<ChargesPerStock)return;
    nextSearch=now+15;
    var sprayTarget=Viable(focus,true)&&focus.HostileTo(Pawn)?focus:
     Pawn.Map.mapPawns.AllPawnsSpawned.Where(p=>Viable(p,false)).OrderBy(p=>p.Position.DistanceToSquared(Pawn.Position)).FirstOrDefault();
    if(sprayTarget!=null){
     fluidAim=sprayTarget.Position;
     Spray(fluidAim,now);
     nextSpray=now+15;
    }
    return;
   }
   if(Type2Active){TryStartReinforcedAttack(now);return;}
   if(sequentialAttack){if(now<nextSequentialAt||!limbs.Any(l=>!l.Active&&now>=l.nextReady))return;}
   else if(limbs.Any(l=>l.Active||now<l.nextReady))return;
   nextSearch=now+15;
   var focused=Viable(focus,true)?focus:null;
   var candidates=focused!=null?new List<Pawn>{focused}:Pawn.Map.mapPawns.AllPawnsSpawned.Where(p=>Viable(p,false)).OrderBy(p=>p.Position.DistanceToSquared(Pawn.Position)).ToList();
   if(candidates.Count==0)return;
   var assigned=new Dictionary<Pawn,int>();foreach(var l in limbs)if(l.Active&&l.target!=null){assigned.TryGetValue(l.target,out int n);assigned[l.target]=n+1;}
   for(int scan=0;scan<6;scan++){int i=sequentialAttack?(nextSequentialLimb+scan)%6:scan;var l=limbs[i];if(l.Active||now<l.nextReady)continue;var target=candidates.OrderBy(p=>assigned.TryGetValue(p,out int n)?n:0).First();assigned.TryGetValue(target,out int count);assigned[target]=count+1;BeginStrike(i,target,now);if(sequentialAttack){nextSequentialLimb=(i+1)%6;nextSequentialAt=now+SequentialInterval;break;}}
  }
  bool Viable(Pawn target,bool manual)=>target!=null&&target!=Pawn&&target.Spawned&&target.Map==Pawn.Map&&!target.Dead&&(manual||!target.Downed)&&
   (manual?(target.Faction==null||target.HostileTo(Pawn)):target.HostileTo(Pawn))&&(!SprayAttackMode||target.Position!=Pawn.Position)&&target.Position.DistanceToSquared(Pawn.Position)<=AttackRange*AttackRange&&GenSight.LineOfSight(Pawn.Position,target.Position,Pawn.Map);
  public override IEnumerable<Gizmo> CompGetGizmos(){
   if(Pawn.Faction!=Faction.OfPlayer)yield break;
   if(Type2Active||CoccyxGasArmor.WornHelmet(Pawn)!=null)yield return GasArmorCommand();
   foreach(var gizmo in ThrowGizmos())yield return gizmo;
   if(ThrowHoldingPawn)yield break;
   foreach(var gizmo in EscortGizmos())yield return gizmo;
   if(EscortTarget!=null||Hediff_CoccyxEscort.IsHeld(Pawn))yield break;
   foreach(var gizmo in PowerGizmos())yield return gizmo;yield return CalibrationCommand();
   if(!Type2Active)yield return RemoteFluidCommand();
   yield return new CoccyxAutoAttackCommand(this);
   yield return new Command_Target{defaultLabel="촉수 집중 공격",defaultDesc="현재 공격 모드에서 우선 공격할 표적을 지정합니다. 자동 촉수 공격을 켜야 공격을 시작합니다.",icon=ContentFinder<Texture2D>.Get("Yuran/UI/Commands/TentacleFocus"),targetingParams=new TargetingParameters{canTargetPawns=true,canTargetAnimals=true,canTargetHumans=true,canTargetMechs=true,canTargetBuildings=false,validator=t=>t.Thing is Pawn p&&p!=Pawn&&!p.Dead&&(p.Faction==null||p.HostileTo(Pawn))},action=t=>focus=t.Pawn};
   if(focus!=null)yield return new Command_Action{defaultLabel="촉수 집중 해제",defaultDesc="집중 표적을 해제하고 표적을 나누어 공격합니다.",icon=ContentFinder<Texture2D>.Get("Yuran/UI/Commands/TentacleFocus"),action=()=>focus=null};
  }
  public override void CompPostPostRemoved(){ReleaseThrowCarry();EndEscort();ReleaseCartridges();drawing.Dispose();base.CompPostPostRemoved();}
 }
}






namespace YR_RJW {
 [HarmonyLib.HarmonyPatch(typeof(Pawn),nameof(Pawn.DrawExtraSelectionOverlays))]
 static class CoccyxAttackRange {
  static void Postfix(Pawn __instance){if(!__instance.Spawned)return;var h=__instance.health?.hediffSet.GetFirstHediffOfDef(DefDatabase<HediffDef>.GetNamed("YR_CoccyxTentacleImplant"));var c=h?.TryGetComp<HediffComp_CoccyxCombat>();if(c!=null)GenDraw.DrawRadiusRing(__instance.Position,c.AttackRange);}
 }
}

namespace YR_RJW {
 public sealed class CoccyxAutoAttackCommand:Command_Toggle {
  readonly HediffComp_CoccyxCombat comp;
  public CoccyxAutoAttackCommand(HediffComp_CoccyxCombat c){comp=c;defaultLabel=(c.Type2Active?"사목의 촉수 2식":"사목의 촉수 1식")+" · "+(c.SprayAttackMode?"난사":c.sequentialAttack?(c.Type2Active?"촉수 총난타":"연속"):"일제");defaultDesc=c.SprayAttackMode?"6칸 안의 적에게 백윤액을 아홉 갈래로 발사합니다. 장탄이 부족하면 발사를 멈춥니다.":c.Type2Active?"일제 사거리 13칸, 촉수 총난타 사거리 8칸. 일제는 굵은 촉수 네 가닥이 근처 적들을 한 번씩 타격합니다. 총난타는 네 가닥이 모두 비었을 때 한 대상에게 가닥당 여섯 번씩 총 24회 타격합니다. 일제는 가닥당 둔상 10. 총난타는 5틱 간격으로 내지르며, 1타 피해는 거리별 2/1.75/1.5/1.25, 마지막 타격은 10/9/8/7입니다. 총난타 방어구 관통력 55%. 공격 주기 3.9초. 맞은 적은 최대 4칸 날아갑니다. 벽이나 건물에 부딪히면 추가 피해를 받습니다. 우클릭으로 일제/연속을 선택합니다.":"좌클릭: 자동 공격 ON/OFF. 우클릭: 일제/연속 선택.\n일제는 여섯 가닥을 동시에, 연속은 0.5초 간격으로 한 가닥씩 공격합니다. 두 모드 모두 가닥당 둔상 피해 6, 방어구 관통 10%, 공격 주기 3초가 적용되며 이미 시작한 공격은 끝까지 복귀합니다.";icon=ContentFinder<Texture2D>.Get("Yuran/Implants/CoccyxTentacles/Rallied_north");isActive=()=>comp.autoAttack;toggleAction=()=>comp.autoAttack=!comp.autoAttack;}
  public override IEnumerable<FloatMenuOption> RightClickFloatMenuOptions {
   get{if(comp.SprayAttackMode)yield break;yield return new FloatMenuOption((comp.sequentialAttack?"":"● ")+"일제 공격",()=>comp.SetSequentialAttack(false));yield return new FloatMenuOption((comp.sequentialAttack?"● ":"")+(comp.Type2Active?"촉수 총난타 · 8칸":"연속 공격 · 0.5초 간격"),()=>comp.SetSequentialAttack(true));}
  }
 }
}

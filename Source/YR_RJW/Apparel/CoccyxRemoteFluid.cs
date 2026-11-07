using System;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace YR_RJW {
 public sealed partial class HediffComp_CoccyxCombat {
  public bool remoteFluidMode,continuousFluidMode;public int remotePaidShots;
  public int AvailableRemoteShots=>fluidStock*6+remotePaidShots-limbs.Count(l=>l.Active&&l.remoteFluid&&!l.impacted);
  public void SetRemoteFluidMode(bool value,bool continuous=false){remoteFluidMode=value;continuousFluidMode=value&&continuous;if(value&&fluidOn){fluidOn=false;autoAttack=true;}}
  public static Vector3 RemotePath(Vector3 root,CoccyxLimb l,float t){if(l.trajectoryPattern>0)return SamokTrajectory.Sample(l.trajectoryPattern,root,l.endpoint,l.curveB-root,l.endpoint-l.curveC,l.strikeWidth,t,l.trajectoryVariation);float u=1-t;return u*u*u*root+3*u*u*t*l.curveB+3*u*t*t*l.curveC+t*t*t*l.endpoint;}
  public const int RemoteFireAt=WarmupTicks+30,RemoteFinishAt=RemoteFireAt+38;
  public static float RemoteExtension(int age){if(age<WarmupTicks)return 0;if(age<RemoteFireAt)return SamokTrajectory.Accelerate((age-WarmupTicks)/30f);if(age<RemoteFireAt+8)return 1;return 1-Mathf.SmoothStep(0,1,(age-RemoteFireAt-8)/30f);}
  internal bool PrepareRemote(int index,CoccyxLimb l){
   l.remoteFluid=remoteFluidMode;l.continuous=continuousFluidMode&&remoteFluidMode;l.streamStart=-1;l.streamStop=-1;l.nextStreamPulse=0;l.shotTick=-1;l.shotDuration=8;l.fluidLanded=false;if(!l.remoteFluid)return true;
   var map=Pawn.Map;if(map==null||l.target?.Map!=map)return false;
   var root=Pawn.DrawPos;var target=l.target.DrawPos;var towardOwner=root-target;towardOwner.y=0;if(towardOwner.sqrMagnitude<.01f)towardOwner=Vector3.back;towardOwner.Normalize();
   for(int attempt=0;attempt<7;attempt++){
    if(attempt>0)l.trajectoryPattern=l.trajectoryPattern%SamokTrajectory.Count+1;
    float angle=(index<3?-1:1)*Rand.Range(92f+27*(index%3),119f+27*(index%3));float radius=Rand.Range(3f,7f);var radial=Quaternion.AngleAxis(angle,Vector3.up)*towardOwner;
    l.endpoint=target+radial*radius;l.firingOffset=l.endpoint-target;var cell=l.endpoint.ToIntVec3();
    if(!cell.InBounds(map)||!cell.Walkable(map)||!GenSight.LineOfSight(cell,l.target.Position,map))continue;
    var delta=target-root;var side=Vector3.Cross(Vector3.up,towardOwner)*(index<3?1:-1);
    l.curveB=root+delta*Rand.Range(.2f,.4f)+side*(radius+Rand.Range(1f,3f));l.curveC=l.endpoint+radial*Rand.Range(1.8f,3.2f);
    l.strikeWidth=Mathf.Clamp((l.endpoint-root).magnitude*.34f,1.25f,3.6f)*(index<3?-1:1);
    bool clear=true;for(int sample=1;sample<32;sample++){var point=RemotePath(root,l,sample/32f).ToIntVec3();if(!point.InBounds(map)||!point.Walkable(map)){clear=false;break;}}
    if(clear)return true;
   }
   return false;
  }
  bool FireRemote(CoccyxLimb l,int now){
   if(l.target?.Spawned!=true||l.target.Dead||l.target.Map!=Pawn.Map)return false;
   var target=l.target.DrawPos;float distance=Vector3.Distance(l.endpoint.WithY(0),target.WithY(0));
   var cell=l.endpoint.ToIntVec3();
   if(distance<2.95f||distance>7.05f||!cell.InBounds(Pawn.Map)||!cell.Walkable(Pawn.Map)||!GenSight.LineOfSight(cell,l.target.Position,Pawn.Map))return false;
   if(!TryConsumeFluid(1,!l.continuous))return false;
   l.shotOrigin=l.endpoint;l.shotEnd=target;l.shotDuration=Mathf.CeilToInt(distance/.32f);l.shotTick=now;CoccyxAttackSound.Play(Pawn,"FluidSpit",l.shotOrigin.ToIntVec3());return true;
  }
  public const int StreamPulseInterval=15,StreamReturnTicks=30;
  public static float LimbExtension(CoccyxLimb limb,int now){
   int age=now-limb.started;
   if(!limb.continuous||age<RemoteFireAt)return RemoteExtension(age);
   if(limb.streamStop<0)return 1;
   return 1-Mathf.SmoothStep(0,1,(now-limb.streamStop)/(float)StreamReturnTicks);
  }
  void TickContinuous(CoccyxLimb limb,int now,int elapsed){
   if(limb.streamStop>=0){
    if(now-limb.streamStop>=StreamReturnTicks){limb.started=-1;limb.target=null;}
    return;
   }
   if(!limb.impacted&&elapsed>=RemoteFireAt){
    limb.impacted=true;
    if(!FireRemote(limb,now)){limb.streamStop=now;return;}
    Attempts++;Hits++;
    limb.streamStart=now;
    limb.nextStreamPulse=now+limb.shotDuration+StreamPulseInterval;
   }
   if(limb.streamStart<0||!limb.fluidLanded)return;
   if(FluidCharges<=0){limb.streamStop=now;autoAttack=false;return;}
   if(now<limb.nextStreamPulse)return;
   var target=limb.target;var cell=limb.shotOrigin.ToIntVec3();
   if(target?.Spawned!=true||target.Dead||target.Map!=Pawn.Map||!GenSight.LineOfSight(cell,target.Position,Pawn.Map)){
    limb.streamStop=now;return;
   }
   if(!TryConsumeFluid(1,false)){limb.streamStop=now;autoAttack=false;return;}
   Attempts++;Hits++;RemoteImpact(limb);
   limb.nextStreamPulse=now+StreamPulseInterval;
   CoccyxAttackSound.Play(Pawn,"FluidSpit",cell);
   if(FluidCharges==0){limb.streamStop=now;autoAttack=false;}
  }
  void RemoteImpact(CoccyxLimb l){
   if(l.target?.Spawned!=true||l.target.Dead||l.target.Map!=Pawn.Map)return;var target=l.target;var map=target.Map;var position=target.Position;
   target.TakeDamage(new DamageInfo(DefDatabase<DamageDef>.GetNamed("YR_CoccyxFluidAcid"),l.continuous?5:10,0,(target.DrawPos-l.shotOrigin).AngleFlat(),Pawn));
   if(!target.Dead){var corrosion=target.health.hediffSet.GetFirstHediffOfDef(HediffDef.Named("YR_CoccyxFluidCorrosion"));if(corrosion==null)corrosion=target.health.AddHediff(HediffDef.Named("YR_CoccyxFluidCorrosion"));else corrosion.Severity=Mathf.Min(5,corrosion.Severity+1);corrosion.TryGetComp<HediffComp_Disappears>().ticksToDisappear=12500;var coat=target.health.hediffSet.GetFirstHediffOfDef(HediffDef.Named("YR_CoccyxSlimeCoat"))??target.health.AddHediff(HediffDef.Named("YR_CoccyxSlimeCoat"));coat.TryGetComp<HediffComp_Disappears>().ticksToDisappear=600;}
   if(position.InBounds(map)){var puddle=position.GetThingList(map).OfType<CoccyxSlimePuddle>().FirstOrDefault();if(puddle==null)puddle=(CoccyxSlimePuddle)GenSpawn.Spawn(ThingMaker.MakeThing(ThingDef.Named("YR_CoccyxSlimePuddle")),position,map);puddle.expires=Find.TickManager.TicksGame+600;}CoccyxAttackSound.Play(Pawn,"FluidImpact");
  }
  internal void UpdateRemoteShots(int now){foreach(var l in limbs)if(l.remoteFluid&&l.shotTick>=0&&!l.fluidLanded&&now-l.shotTick>=l.shotDuration){l.fluidLanded=true;RemoteImpact(l);}}
  internal Gizmo RemoteFluidCommand()=>new Command_Action{
   defaultLabel=continuousFluidMode?"백윤액 전량 사출":remoteFluidMode?"백윤액 포위 사격":"촉수 공격 · 타격",
   defaultDesc="타격 → 포위 사격 → 전량 사출 순으로 전환합니다. 포위 사격은 적의 측후방 3~7칸에서 발사합니다. 전량 사출은 최대 여섯 가닥이 위치를 유지하며 현재 장탄을 물줄기로 뿜어내며 피해 판정마다 산성 화상 5를 줍니다. 장탄이 비면 복귀하고 자동 공격이 꺼집니다.",
   icon=ContentFinder<Texture2D>.Get("Yuran/UI/Commands/RemoteFluid"),
   action=()=>{if(!remoteFluidMode)SetRemoteFluidMode(true);else if(!continuousFluidMode)SetRemoteFluidMode(true,true);else SetRemoteFluidMode(false);}
  };
 }
 public sealed class Hediff_CoccyxCorrosion:HediffWithComps {public override string LabelInBrackets=>Mathf.Clamp(Mathf.RoundToInt(Severity),1,5)+"/5 · 유효 방어력 -"+(Mathf.Clamp(Mathf.RoundToInt(Severity),1,5)*6)+"%";}
 [HarmonyPatch(typeof(ArmorUtility),"ApplyArmor")]
 public static class CoccyxCorrosionArmor {
  public static float Multiplier(Pawn pawn){var h=pawn?.health?.hediffSet.GetFirstHediffOfDef(HediffDef.Named("YR_CoccyxFluidCorrosion"));return h==null?1:1-.06f*Mathf.Clamp(Mathf.RoundToInt(h.Severity),1,5);}
  static void Prefix(Pawn pawn,ref float armorRating){armorRating*=Multiplier(pawn);}
 }
}

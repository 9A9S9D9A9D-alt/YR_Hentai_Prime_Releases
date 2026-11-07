using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace YR_RJW {
 // The original projectile survives each miss. Native collision still ends an
 // arrow that strikes an obstruction or another pawn; only empty misses turn.
 public sealed class Projectile_SamokGuidedArrow : Bullet {
  int retraces,curveTicks,trajectoryPattern,trajectoryVariation;
  float trajectoryWidth;
  bool curved,finished,wideRoute;
  Vector3 a,b,c,d,via,viaOut,approach;
  public int Retraces=>retraces;
  float RawProgress=>Mathf.Clamp01(1f-(float)ticksToImpact/Mathf.Max(1,curveTicks));
  float Progress=>trajectoryPattern>0?SamokTrajectory.Accelerate(RawProgress):RawProgress;
  static Vector3 Cubic(Vector3 a,Vector3 b,Vector3 c,Vector3 d,float t){float u=1-t;return u*u*u*a+3*u*u*t*b+3*u*t*t*c+t*t*t*d;}
  static Vector3 Derivative(Vector3 a,Vector3 b,Vector3 c,Vector3 d,float t)=>3*(1-t)*(1-t)*(b-a)+6*(1-t)*t*(c-b)+3*t*t*(d-c);
  Vector3 CurvePoint(float t)=>!wideRoute?Cubic(a,b,c,d,t):t<.5f?(trajectoryPattern>0?Clamp(SamokTrajectory.Sample(trajectoryPattern,a,via,b-a,via-c,trajectoryWidth,t*2,trajectoryVariation)):Cubic(a,b,c,via,t*2)):Cubic(via,viaOut,approach,d,(t-.5f)*2);
  Vector3 Tangent(float t)=>wideRoute&&trajectoryPattern>0&&t<.499f?CurvePoint(Mathf.Min(.5f,t+.0003f))-CurvePoint(Mathf.Max(0,t-.0003f)):!wideRoute?Derivative(a,b,c,d,t):t<.5f?Derivative(a,b,c,via,t*2):Derivative(via,viaOut,approach,d,(t-.5f)*2);
  public override Vector3 ExactPosition {
   get {if(!curved)return base.ExactPosition;var p=CurvePoint(Progress);p.y=base.ExactPosition.y;return p;}
  }
  public override Quaternion ExactRotation=>curved?Quaternion.LookRotation(Tangent(Progress).sqrMagnitude>.00000001f?Tangent(Progress):d-a):base.ExactRotation;
  protected override void DrawAt(Vector3 drawLoc,bool flip=false){
   if(curved&&trajectoryPattern>0){
    float progress=RawProgress;var last=ExactPosition;float altitude=last.y;
    for(int i=0;i<SamokTrajectory.TrailSegments;i++){
     float age=progress-(i+1)*2f/Mathf.Max(1,curveTicks);var point=CurvePoint(SamokTrajectory.Accelerate(age));point.y=altitude;
     SamokTrajectory.DrawTrailSegment(last,point,i);last=point;if(age<=0)break;
    }
   }
   base.DrawAt(drawLoc,flip);
  }
  bool ValidTarget=>intendedTarget.HasThing&&intendedTarget.Thing.Spawned&&intendedTarget.Thing.Map==Map&&!(intendedTarget.Thing is Pawn p&&p.Dead);
  protected override void TickInterval(int delta) {
   if(curved&&!ValidTarget){finished=true;Destroy();return;}
   base.TickInterval(delta);
  }
  protected override void ImpactSomething() {
   // Native targeted shots permit a moving target at its old destination.
   // A retrace must actually reach the target's current cell instead.
   if(curved&&(!ValidTarget||!intendedTarget.Thing.OccupiedRect().Contains(Position))){Impact(null);return;}
   base.ImpactSomething();
  }
  protected override void Impact(Thing hitThing,bool blockedByShield=false) {
   if(finished||Destroyed)return;
   if(hitThing==null&&!blockedByShield&&retraces<2&&ValidTarget){Turn();return;}
   finished=true;
   base.Impact(hitThing,blockedByShield);
  }
  void Turn() {
   var point=ExactPosition;var forward=curved?Tangent(1):destination-origin;forward.y=0;
   if(forward.sqrMagnitude<.001f)forward=Vector3.forward;forward.Normalize();
   a=point;d=intendedTarget.Thing.DrawPos;a.y=d.y=0;
   var towardOwner=(launcher?.Spawned==true?launcher.DrawPos:origin)-d;towardOwner.y=0;if(towardOwner.sqrMagnitude<.001f)towardOwner=-forward;towardOwner.Normalize();
   float radius=Rand.Range(6f,13f),sign=Rand.Bool?1:-1;
   var flank=Quaternion.AngleAxis(sign*Rand.Range(75f,120f),Vector3.up)*towardOwner;
   var rear=Quaternion.AngleAxis(sign*Rand.Range(130f,175f),Vector3.up)*towardOwner;
   via=Clamp(d+flank*radius);approach=Clamp(d+rear*radius*Rand.Range(.75f,1.25f));
   var joint=(approach-via).normalized;
   float handle=radius*Rand.Range(.4f,.7f);
   b=Clamp(a+forward*radius*Rand.Range(.7f,1.2f));c=Clamp(via-joint*handle);viaOut=Clamp(via+joint*handle);
   wideRoute=true;trajectoryPattern=SamokTrajectory.Next(trajectoryPattern);trajectoryWidth=radius*.48f*sign;trajectoryVariation=Rand.Range(1,int.MaxValue);
   float length=0;var last=a;
   for(int i=1;i<=64;i++){var next=CurvePoint(i/64f);length+=Vector3.Distance(last,next);last=next;}
   curveTicks=Mathf.Max(8,Mathf.CeilToInt(length/def.projectile.SpeedTilesPerTick));
   origin=a;destination=d;ticksToImpact=curveTicks;landed=false;curved=true;retraces++;
   usedTarget=intendedTarget;
   HitFlags|=ProjectileHitFlags.IntendedTarget;
  }
  Vector3 Clamp(Vector3 p){if(Map==null)return p;p.x=Mathf.Clamp(p.x,.1f,Map.Size.x-.1f);p.z=Mathf.Clamp(p.z,.1f,Map.Size.z-.1f);return p;}
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref trajectoryPattern,"samokTrajectoryPattern");Scribe_Values.Look(ref trajectoryVariation,"samokTrajectoryVariation");Scribe_Values.Look(ref trajectoryWidth,"samokTrajectoryWidth");Scribe_Values.Look(ref retraces,"samokRetraces");Scribe_Values.Look(ref curveTicks,"samokCurveTicks");Scribe_Values.Look(ref curved,"samokCurved");Scribe_Values.Look(ref wideRoute,"samokWideRoute");Scribe_Values.Look(ref via,"samokOrbitVia");Scribe_Values.Look(ref viaOut,"samokOrbitOut");Scribe_Values.Look(ref approach,"samokRearApproach");Scribe_Values.Look(ref finished,"samokArrowFinished");Scribe_Values.Look(ref a,"samokCurveA");Scribe_Values.Look(ref b,"samokCurveB");Scribe_Values.Look(ref c,"samokCurveC");Scribe_Values.Look(ref d,"samokCurveD");}
 }
 [HarmonyPatch(typeof(Projectile),"CheckForFreeIntercept")]
 static class SamokRetracePassesWalls {
  static bool Prefix(Projectile __instance,IntVec3 c,ref bool __result){
   if(!(__instance is Projectile_SamokGuidedArrow arrow)||arrow.Retraces<=0||arrow.Map==null||!c.InBounds(arrow.Map))return true;
   if(c.GetEdifice(arrow.Map)?.def.Fillage!=FillCategory.Full)return true;
   __result=false;return false;
  }
 }

}

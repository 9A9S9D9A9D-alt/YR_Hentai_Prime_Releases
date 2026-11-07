using System;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace YR_RJW {
 // A melee verb with reach, not a projectile-launching verb. The visual is an
 // ordinary Thing and cannot be captured by ammunition/projectile patches.
 public sealed class Verb_SamokWhip : Verb_MeleeAttack {
  static readonly Func<Verb_MeleeAttack,LocalTargetInfo,float> Dodge=AccessTools.MethodDelegate<Func<Verb_MeleeAttack,LocalTargetInfo,float>>(AccessTools.Method(typeof(Verb_MeleeAttack),"GetDodgeChance"));
  public override bool CanHitTargetFrom(IntVec3 root,LocalTargetInfo target)=>caster.Spawned&&target.HasThing&&target.Thing.Spawned&&target.Thing.Map==caster.Map&&root.DistanceToSquared(target.Cell)<=verbProps.range*verbProps.range&&GenSight.LineOfSight(root,target.Cell,caster.Map,true);
  public override void OrderForceTarget(LocalTargetInfo target){if(!CasterIsPawn)return;var job=JobMaker.MakeJob(JobDefOf.AttackStatic,target);job.verbToUse=this;CasterPawn.jobs.TryTakeOrderedJob(job,JobTag.Misc);}
  protected override bool TryCastShot(){
   if(!CanHitTargetFrom(caster.Position,currentTarget))return false;
   var strike=(SamokWhipStrike)ThingMaker.MakeThing(ThingDef.Named("YR_SamokWhipStrike"));
   strike.owner=CasterPawn;strike.target=currentTarget.Thing;strike.weapon=EquipmentSource?.def;strike.started=Find.TickManager.TicksGame;
   strike.endpoint=currentTarget.Thing.DrawPos;
   strike.travelTicks=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(caster.DrawPos,strike.endpoint)/ThingDef.Named("YR_Tentacle_BullWhip_Chain").projectile.SpeedTilesPerTick));
   strike.hit=Rand.Chance(CasterPawn.GetStatValue(StatDefOf.MeleeHitChance))&&!Rand.Chance(Dodge(this,currentTarget));
   GenSpawn.Spawn(strike,caster.Position,caster.Map);
   verbProps.soundCast?.PlayOneShot(new TargetInfo(caster.Position,caster.Map));
   return true;
  }
  protected override DamageWorker.DamageResult ApplyMeleeDamageToTarget(LocalTargetInfo target)=>target.Thing.TakeDamage(new DamageInfo(verbProps.meleeDamageDef,15,1,-1,caster,null,EquipmentSource?.def));
 }
 [StaticConstructorOnStartup]
 public sealed class SamokWhipStrike : Thing {
  static readonly Material Rope=MaterialPool.MatFrom("Yuran/Things/Item/Equipment/WeaponMelee/Tentacle_BullWhip_Rope",ShaderDatabase.Cutout);
  public Pawn owner;
  public Thing target;
  public ThingDef weapon;
  public int started,travelTicks;
  public bool hit,applied;
  public Vector3 endpoint;
  protected override void TickInterval(int delta){base.TickInterval(delta);Advance(Find.TickManager.TicksGame);}
  public void Advance(int now){
   if(owner?.Spawned!=true||owner.Map!=Map){Destroy();return;}
   int age=now-started;
   if(!applied&&target?.Spawned==true&&target.Map==Map)endpoint=target.DrawPos;
   if(!applied&&age>=travelTicks){applied=true;if(hit&&target?.Spawned==true&&target.Map==Map&&!(target is Pawn p&&p.Dead))target.TakeDamage(new DamageInfo(DefDatabase<DamageDef>.GetNamed("YR_Tentacle_BullWhip_ChainDamageDef"),15,1,-1,owner,null,weapon));}
   if(age>=travelTicks*2+6)Destroy();
  }
  protected override void DrawAt(Vector3 drawLoc,bool flip=false){
   if(owner?.Spawned!=true)return;
   int age=Find.TickManager.TicksGame-started;float extent=age<=travelTicks?Mathf.Clamp01((float)age/travelTicks):1-Mathf.Clamp01((float)(age-travelTicks-6)/travelTicks);
   var from=owner.DrawPos;from.y=AltitudeLayer.Projectile.AltitudeFor();var end=Vector3.Lerp(from,endpoint,extent);end.y=from.y;
   var perpendicular=new Vector3(-(end-from).z,0,(end-from).x).normalized;
   var last=from;
   for(int i=1;i<=24;i++){float t=i/24f;var next=Vector3.Lerp(from,end,t)+perpendicular*Mathf.Sin(t*Mathf.PI)*Mathf.Sin(age*.2f+t*6)*.18f;GenDraw.DrawLineBetween(last,next,Rope,.12f);last=next;}
  }
  public override void ExposeData(){base.ExposeData();Scribe_References.Look(ref owner,"samokWhipOwner");Scribe_References.Look(ref target,"samokWhipTarget");Scribe_Defs.Look(ref weapon,"samokWhipWeapon");Scribe_Values.Look(ref started,"samokWhipStarted");Scribe_Values.Look(ref travelTicks,"samokWhipTravelTicks");Scribe_Values.Look(ref hit,"samokWhipHit");Scribe_Values.Look(ref applied,"samokWhipApplied");Scribe_Values.Look(ref endpoint,"samokWhipEndpoint");}
 }
}

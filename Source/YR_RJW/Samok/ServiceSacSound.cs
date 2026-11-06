using RimWorld;
using Verse;
using Verse.Sound;
namespace YR_RJW {
 public class CompProperties_ServiceSacSound : CompProperties {
  public SoundDef soundClosed,soundOpened,soundOccupied,soundMotion;
  public CompProperties_ServiceSacSound(){compClass=typeof(CompServiceSacSound);}
 }
 public class CompServiceSacSound : ThingComp {
  private Sustainer voice,motion;
  private ServiceSacPhase lastPhase;
  private CompProperties_ServiceSacSound Props=>(CompProperties_ServiceSacSound)props;
  public override void PostSpawnSetup(bool respawningAfterLoad){
   base.PostSpawnSetup(respawningAfterLoad);Stop();lastPhase=(parent as Building_ServiceSac)?.AudioPhase??ServiceSacPhase.Idle;
  }
  public override void CompTick(){
   base.CompTick();var sac=parent as Building_ServiceSac;
   if(sac==null || !sac.Spawned){Stop();return;}
   var phase=sac.AudioPhase;
   if(phase!=lastPhase){
    if(phase==ServiceSacPhase.Closing)(Props.soundClosed??SoundDefOf.CryptosleepCasket_Accept).PlayOneShot(SoundInfo.InMap(parent));
    else if(phase==ServiceSacPhase.Opening)(Props.soundOpened??SoundDefOf.CryptosleepCasket_Eject).PlayOneShot(SoundInfo.InMap(parent));
    lastPhase=phase;
   }
   if(phase!=ServiceSacPhase.Working || sac.InteriorPawn==null){Stop();return;}
   // Independent voice and motion sustainers are maintained together. Native grain scheduling handles
   // random selection, pauses and positive intervals, without per-pawn timers.
   if(voice==null || voice.Ended)voice=Props.soundOccupied?.TrySpawnSustainer(SoundInfo.InMap(parent,MaintenanceType.PerTick));
   voice?.Maintain();
   if(motion==null || motion.Ended)motion=Props.soundMotion?.TrySpawnSustainer(SoundInfo.InMap(parent,MaintenanceType.PerTick));
   motion?.Maintain();
  }
  private void Stop(){
   if(voice!=null && !voice.Ended)voice.End();voice=null;
   if(motion!=null && !motion.Ended)motion.End();motion=null;
  }
  public override void PostDeSpawn(Map map,DestroyMode mode=DestroyMode.Vanish){Stop();base.PostDeSpawn(map,mode);}
  public override void PostDestroy(DestroyMode mode,Map previousMap){Stop();base.PostDestroy(mode,previousMap);}
  public override void Notify_MapRemoved(){Stop();base.Notify_MapRemoved();}
 }
}

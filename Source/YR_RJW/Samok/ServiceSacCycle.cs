using System;
namespace YR_RJW {
 public enum ServiceSacPhase { Idle, Closing, Working, Opening }
 // Only deterministic cycle bookkeeping; the building owns and saves the pawn/output.
 public sealed class ServiceSacCycle {
  public const int TransitionDuration=120, HourTicks=2500, Duration=15000, UnitsPerHour=25;
  public ServiceSacPhase Phase;
  public int WorkTicks, TransitionTicks, ProducedUnits;
  public int FluidStage => Math.Max(0,Math.Min(6,ProducedUnits/UnitsPerHour));
  public float OpeningProgress => Phase==ServiceSacPhase.Opening?Math.Min(1f,TransitionTicks/(float)TransitionDuration):0f;
  public float LiquidLevel {
   get {
    if(WorkTicks<HourTicks || Phase==ServiceSacPhase.Idle || Phase==ServiceSacPhase.Closing)return 0f;
    float fill=Math.Min(1f,0.1f+0.9f*(WorkTicks-HourTicks)/(float)(Duration-HourTicks));
    return Phase==ServiceSacPhase.Opening?fill*(1f-OpeningProgress):fill;
   }
  }
  public void Begin(){Phase=ServiceSacPhase.Closing;WorkTicks=TransitionTicks=ProducedUnits=0;}
  public void Cancel(){if(Phase==ServiceSacPhase.Closing || Phase==ServiceSacPhase.Working){Phase=ServiceSacPhase.Opening;TransitionTicks=0;}}
  public int Advance(int delta){
   int before=ProducedUnits;
   while(delta>0 && Phase!=ServiceSacPhase.Idle){
    if(Phase==ServiceSacPhase.Working){
     int used=Math.Min(delta,Duration-WorkTicks);WorkTicks+=used;delta-=used;
     ProducedUnits=(WorkTicks/HourTicks)*UnitsPerHour;
     if(WorkTicks>=Duration){Phase=ServiceSacPhase.Opening;TransitionTicks=0;}
    }else{
     int used=Math.Min(delta,TransitionDuration-TransitionTicks);TransitionTicks+=used;delta-=used;
     if(TransitionTicks>=TransitionDuration){Phase=Phase==ServiceSacPhase.Closing?ServiceSacPhase.Working:ServiceSacPhase.Idle;TransitionTicks=0;}
    }
   }
   return ProducedUnits-before;
  }
 }
}

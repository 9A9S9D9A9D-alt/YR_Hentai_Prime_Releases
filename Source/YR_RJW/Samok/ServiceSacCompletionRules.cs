using System;
namespace YR_RJW {
 public enum ServiceSacCompletion {None,Exhaustion,Death,BrideMood}
 public static class ServiceSacCompletionRules {
  public static ServiceSacCompletion Resolve(bool completed,bool bride,bool risky){
   if(!completed)return ServiceSacCompletion.None;
   return bride?ServiceSacCompletion.BrideMood:risky?ServiceSacCompletion.Death:ServiceSacCompletion.Exhaustion;
  }
  public static int NextMoodStage(int activeStage)=>Math.Min(2,Math.Max(0,activeStage+1));
 }
}

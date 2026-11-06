using System.Linq;
using RimWorld;
using Verse;
namespace YR_RJW {
 internal static class ServiceSacEffects {
  internal static bool HasExhaustion(Pawn p)=>p?.health?.hediffSet?.hediffs.Any(h=>h.def.defName=="YR_ServiceSac_Exhaustion")==true;
  internal static void Complete(Pawn pawn,bool risky){
   if(pawn==null || pawn.Dead)return;
   switch(ServiceSacCompletionRules.Resolve(true,SamokBrideRules.IsBride(pawn),risky)){
    case ServiceSacCompletion.Death:pawn.Kill(null);break;
    case ServiceSacCompletion.Exhaustion:
     pawn.health.AddHediff(DefDatabase<HediffDef>.GetNamed("YR_ServiceSac_Exhaustion"));break;
    case ServiceSacCompletion.BrideMood:
     var memories=pawn.needs?.mood?.thoughts?.memories;if(memories==null)return;
     var def=DefDatabase<ThoughtDef>.GetNamed("YR_ServiceSac_BrideMood");
     int previous=memories.Memories.Where(m=>m.def==def && m.age<25000).Select(m=>m.CurStageIndex).DefaultIfEmpty(-1).Max();
     int stage=ServiceSacCompletionRules.NextMoodStage(previous);
     memories.RemoveMemoriesOfDef(def);
     var memory=(Thought_Memory)ThoughtMaker.MakeThought(def,stage);memory.Renew();memories.TryGainMemory(memory);
     break;
   }
  }
 }
}

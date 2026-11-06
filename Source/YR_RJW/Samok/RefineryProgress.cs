using System;
namespace YR_RJW {
 // Integer work credit avoids drift: 35 raw units per 2500 game ticks.
 public sealed class RefineryProgress {
  public int Credit;
  public int BatchElapsed;
  public int AdvanceHourly(int elapsed,int available,int ratio){
   if(elapsed<=0||ratio<=0)return 0;
   if(available<ratio){BatchElapsed=0;Credit=0;return 0;}
   long total=(long)BatchElapsed+elapsed;
   BatchElapsed=(int)(total%2500);
   int made=Advance((int)(total/2500)*2500,available,ratio);
   if(available-made*ratio<ratio)BatchElapsed=0;
   return made;
  }
  public int Advance(int elapsed,int available,int ratio){
   if(elapsed<=0 || ratio<=0)return 0;
   if(available<ratio){Credit=0;return 0;}
   long work=(long)Credit+(long)elapsed*35;
   int batches=(int)Math.Min(available/ratio,work/(ratio*2500L));
   Credit=available-batches*ratio<ratio?0:(int)(work-batches*ratio*2500L);
   return batches;
  }
 }
}

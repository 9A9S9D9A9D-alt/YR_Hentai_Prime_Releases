using System.Collections.Concurrent;
using RimWorld;
using UnityEngine;
using Verse;
namespace YR_RJW {
 // Worker phases only read ready graphics. Resource creation and refresh run in the main game update.
 public sealed class GolemMaterialCacheV8 : GameComponent {
  private static readonly ConcurrentDictionary<(string,Color,Vector2),Graphic> Ready=new ConcurrentDictionary<(string,Color,Vector2),Graphic>();
  private static readonly ConcurrentDictionary<(string,Color,Vector2),byte> Queued=new ConcurrentDictionary<(string,Color,Vector2),byte>();
  private static readonly ConcurrentQueue<((string,Color,Vector2) key,Apparel apparel)> Pending=new ConcurrentQueue<((string,Color,Vector2),Apparel)>();
  public GolemMaterialCacheV8(Game game){}
  private static Graphic Prepare((string,Color,Vector2) key){
   var graphic=GraphicDatabase.Get<Graphic_Multi>(key.Item1,ShaderDatabase.Cutout,key.Item3,key.Item2);
   if(graphic==null)return null;
   for(int i=0;i<4;i++)graphic.MatAt(new Rot4(i));
   Ready[key]=graphic;return graphic;
  }
  public static bool TryGet(Apparel apparel,Rot4 facing,out Material material){
   material=null;
   var key=(apparel.WornGraphicPath,apparel.DrawColor,apparel.def.graphicData.drawSize);
   if(!Ready.TryGetValue(key,out var graphic)){
    if(UnityData.IsInMainThread)graphic=Prepare(key);
    else if(Queued.TryAdd(key,0))Pending.Enqueue((key,apparel));
   }
   if(graphic==null)return false;
   material=graphic.MatAt(facing);return material!=null;
  }
  public override void GameComponentUpdate(){
   if(!UnityData.IsInMainThread)return;
   RootArmorFitV8.ProcessPending();
   int budget=32;
   while(budget-->0&&Pending.TryDequeue(out var request)){
    Queued.TryRemove(request.key,out _);
    if(Prepare(request.key)!=null)request.apparel?.Wearer?.Drawer?.renderer?.SetAllGraphicsDirty();
   }
  }
 }
}

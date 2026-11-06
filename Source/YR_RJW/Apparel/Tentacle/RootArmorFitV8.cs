using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
namespace YR_RJW {
 // Match the existing naked body/head frame. Body graphics and biological parts are never replaced.
 internal static class RootArmorFitV8 {
  private sealed class Fit {public Vector3 scale,offset;}
  private static readonly ConditionalWeakTable<PawnRenderNode,ConcurrentDictionary<int,Fit>> Fits=new ConditionalWeakTable<PawnRenderNode,ConcurrentDictionary<int,Fit>>();
  private static readonly ConcurrentQueue<(PawnRenderNode node,PawnDrawParms parms)> Pending=new ConcurrentQueue<(PawnRenderNode,PawnDrawParms)>();
  private static readonly ConcurrentDictionary<(PawnRenderNode,int),byte> Queued=new ConcurrentDictionary<(PawnRenderNode,int),byte>();
  private static readonly ConditionalWeakTable<Texture2D,Box> Boxes=new ConditionalWeakTable<Texture2D,Box>();
  private sealed class Box {public Rect rect;}
  private static Rect Bounds(Texture texture){
   var tex=texture as Texture2D;if(tex==null)return new Rect(0,0,1,1);
   if(Boxes.TryGetValue(tex,out var found))return found.rect;
   Rect rect=new Rect(0,0,1,1);
   if(tex.isReadable){
    var pixels=tex.GetPixels32();int l=tex.width,r=-1,b=tex.height,t=-1;
    for(int y=0;y<tex.height;y++)for(int x=0;x<tex.width;x++)if(pixels[y*tex.width+x].a>32){l=Math.Min(l,x);r=Math.Max(r,x);b=Math.Min(b,y);t=Math.Max(t,y);}
    if(r>=l&&t>=b)rect=new Rect((float)l/tex.width,(float)b/tex.height,(float)(r-l+1)/tex.width,(float)(t-b+1)/tex.height);
   }
   Boxes.Add(tex,new Box{rect=rect});return rect;
  }
  private static bool Applies(PawnRenderNode n){
   if(n?.apparel==null)return false;
   // Miho's authored cheek marks already use the native head canvas. Fitting their
   // sparse alpha bounds to a whole head magnifies/repositions the decoration.
   if(n.apparel.Wearer?.def.defName=="Alien_Miho")return false;
   return RootApparelGraphicFallback.RootHead(n.apparel)&&RootApparelGraphicFallback.Dedicated(n.apparel)&&n.PrimaryGraphic?.path!=BaseContent.PlaceholderGearImagePath;
  }
  private static Fit Get(PawnRenderNode n,PawnDrawParms p){
   if(!Applies(n))return null;
   var states=Fits.GetOrCreateValue(n);
   if(states.TryGetValue(p.facing.AsInt,out var fit))return fit;
   if(UnityData.IsInMainThread){fit=Measure(n,p);if(fit!=null)states[p.facing.AsInt]=fit;return fit;}
   if(Queued.TryAdd((n,p.facing.AsInt),0))Pending.Enqueue((n,p));
   return null;
  }
  internal static void ProcessPending(){
   if(!UnityData.IsInMainThread)return;
   int budget=64;
   while(budget-->0&&Pending.TryDequeue(out var item)){
    Queued.TryRemove((item.node,item.parms.facing.AsInt),out _);
    var fit=Measure(item.node,item.parms);
    if(fit!=null){Fits.GetOrCreateValue(item.node)[item.parms.facing.AsInt]=fit;var tree=item.parms.pawn?.Drawer?.renderer?.renderTree;if(tree!=null)AccessTools.Field(typeof(PawnRenderTree),"oldParms").SetValue(tree,default(PawnDrawParms));}
   }
  }
  private static Fit Measure(PawnRenderNode node,PawnDrawParms parms){
   bool head=RootApparelGraphicFallback.RootHead(node.apparel);
   PawnRenderNode anchor=node.parent;
   while(anchor!=null&&!(head?anchor is PawnRenderNode_Head:anchor is PawnRenderNode_Body))anchor=anchor.parent;
   if(anchor?.PrimaryGraphic==null||node.PrimaryGraphic==null)return null;
   var sourceMesh=node.GetMesh(parms);var targetMesh=anchor.GetMesh(parms);
   if(sourceMesh==null||targetMesh==null)return null;
   Vector3 sourceSize=sourceMesh.bounds.size,targetSize=targetMesh.bounds.size;
   if(sourceSize.x<=0||sourceSize.z<=0)return null;
   Rect target=Bounds(anchor.PrimaryGraphic.MatAt(parms.facing).mainTexture),reference;
   Vector2 targetCenter=target.center;
   if(head){
    reference=Bounds(node.PrimaryGraphic.MatAt(parms.facing).mainTexture);
    targetCenter.y=target.yMin+target.height*.32f;
    target=new Rect(targetCenter.x-target.width*.5f,targetCenter.y-target.height*.2f,target.width,target.height*.4f);
   }else{
    string path="Things/Pawn/Humanlike/Bodies/Naked_Female_"+parms.facing.ToString().ToLowerInvariant();
    var texture=ContentFinder<Texture2D>.Get(path,false)??ContentFinder<Texture2D>.Get("Things/Pawn/Humanlike/Bodies/Naked_Female_south",false);
    reference=Bounds(texture);
   }
   float sx=targetSize.x/sourceSize.x*target.width/Mathf.Max(.001f,reference.width);
   float sz=targetSize.z/sourceSize.z*target.height/Mathf.Max(.001f,reference.height);
   return new Fit{scale=new Vector3(sx,1,sz),offset=new Vector3(targetSize.x*(target.center.x-.5f)-sourceSize.x*sx*(reference.center.x-.5f),0,targetSize.z*(target.center.y-.5f)-sourceSize.z*sz*(reference.center.y-.5f))};
  }
  [HarmonyPatch(typeof(PawnRenderNodeWorker),nameof(PawnRenderNodeWorker.ScaleFor))]
  private static class Scale {private static void Postfix(PawnRenderNode node,PawnDrawParms parms,ref Vector3 __result){var f=Get(node,parms);if(f!=null){__result.x*=f.scale.x;__result.z*=f.scale.z;}}}
  [HarmonyPatch(typeof(PawnRenderNodeWorker),nameof(PawnRenderNodeWorker.OffsetFor))]
  private static class Offset {private static void Postfix(PawnRenderNode node,PawnDrawParms parms,ref Vector3 __result){
   var f=Get(node,parms);if(f!=null)__result+=f.offset;
   // Original armor collar is low in its transparent canvas. Move only the garment, never the body or scale.
   if(node?.apparel?.def.defName=="YR_AP_Tentacle" && node.PrimaryGraphic?.path.StartsWith("OY_Yuran_Addon/Equipment/TentacleArmor")==true)
    __result.z+=0.22f;
  }}
  [HarmonyPatch(typeof(PawnRenderNodeWorker),nameof(PawnRenderNodeWorker.CanDrawNow))]
  private static class Visibility {
   private static void Postfix(PawnRenderNode node,PawnDrawParms parms,ref bool __result){
    if(!__result||node.apparel==null)return;
    if((RootApparelGraphicFallback.RootHead(node.apparel)||RootApparelGraphicFallback.RootBody(node.apparel))&&node.PrimaryGraphic?.path==BaseContent.PlaceholderGearImagePath){__result=false;return;}
    if(RootApparelGraphicFallback.RootBody(node.apparel)||RootApparelGraphicFallback.RootHead(node.apparel))return;
    if(parms.pawn.apparel.WornApparel.Exists(a=>RootApparelGraphicFallback.RootBody(a)&&!RootApparelGraphicFallback.Dedicated(a)))__result=false;
   }
  }
 }
}

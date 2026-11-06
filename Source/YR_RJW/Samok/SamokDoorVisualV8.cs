using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
namespace YR_RJW {
 [HarmonyPatch(typeof(Building_Door),"DrawAt")]
 internal static class SamokDoorVisualV8 {
  private static readonly System.Reflection.MethodInfo Pre=AccessTools.Method(typeof(Building_Door),"DoorPreDraw");
  private static readonly System.Reflection.PropertyInfo Pct=AccessTools.Property(typeof(Building_Door),"OpenPct");
  private static readonly Mesh[] Panels=new Mesh[42];
  private static readonly Mesh[] Outlines=new Mesh[42];
  private static void Quad(List<Vector3> v,List<Vector2> uv,List<int> t,float l,float b,float r,float top,float ul,float ub,float ur,float ut){
   if(r<=l||top<=b)return;int i=v.Count;
   v.Add(new Vector3(l,0,b));v.Add(new Vector3(l,0,top));v.Add(new Vector3(r,0,top));v.Add(new Vector3(r,0,b));
   uv.Add(new Vector2(ul,ub));uv.Add(new Vector2(ul,ut));uv.Add(new Vector2(ur,ut));uv.Add(new Vector2(ur,ub));
   t.Add(i);t.Add(i+1);t.Add(i+2);t.Add(i);t.Add(i+2);t.Add(i+3);
  }
  private static Mesh Make(int step,int side){
   var v=new List<Vector3>();var uv=new List<Vector2>();var t=new List<int>();
   // Sample only the plain purple center. No surrounding tentacles or fixed overlay frame.
   float shift=(side==0?-1f:1f)*.5f*(step/20f);
   float start=side==0?-.5f:0f,end=side==0?0f:.5f;
   float l=Mathf.Max(-.5f,start+shift),r=Mathf.Min(.5f,end+shift);
   // Expand the center art to the same 1x1 drawing area as a standard door.
   float ul=.34f+.32f*(l-shift+.5f),ur=.34f+.32f*(r-shift+.5f);
   Quad(v,uv,t,l,-.5f,r,.5f,ul,.23f,ur,.74f);
   var mesh=new Mesh();mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(t,0);mesh.RecalculateBounds();mesh.RecalculateNormals();return mesh;
  }
  private static Mesh MakeOutline(int step,int side){
   var v=new List<Vector3>();var uv=new List<Vector2>();var t=new List<int>();
   float shift=(side==0?-1f:1f)*.5f*(step/20f);
   float start=(side==0?-.5f:0f)+shift,end=(side==0?0f:.5f)+shift;
   float l=Mathf.Max(-.5f,start),r=Mathf.Min(.5f,end);const float width=.018f;
   if(r>l){
    Quad(v,uv,t,l,-.5f,r,-.5f+width,0,0,1,1);
    Quad(v,uv,t,l,.5f-width,r,.5f,0,0,1,1);
    Quad(v,uv,t,Mathf.Max(l,start),-.5f,Mathf.Min(r,start+width),.5f,0,0,1,1);
    Quad(v,uv,t,Mathf.Max(l,end-width),-.5f,Mathf.Min(r,end),.5f,0,0,1,1);
   }
   var mesh=new Mesh();mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(t,0);mesh.RecalculateBounds();mesh.RecalculateNormals();return mesh;
  }
  private static bool Prefix(Building_Door __instance,Vector3 drawLoc){
   if(__instance.def.defName!="YR_SamokDoor")return true;
   Pre.Invoke(__instance,null);
   int step=Mathf.Clamp(Mathf.RoundToInt((float)Pct.GetValue(__instance)*20f),0,20);
   var mat=__instance.Graphic.MatAt(__instance.Rotation,__instance);
   drawLoc.y=AltitudeLayer.DoorMoveable.AltitudeFor();
   for(int side=0;side<2;side++){
    int key=step*2+side;if(Panels[key]==null)Panels[key]=Make(step,side);
    Graphics.DrawMesh(Panels[key],drawLoc,__instance.Rotation.AsQuat,mat,0);
    if(Outlines[key]==null)Outlines[key]=MakeOutline(step,side);
    Graphics.DrawMesh(Outlines[key],drawLoc+Vector3.up*.001f,__instance.Rotation.AsQuat,SolidColorMaterials.SimpleSolidColorMaterial(Color.black),0);
   }
   foreach(var comp in __instance.AllComps)comp.PostDraw();
   return false;
  }
 }
}

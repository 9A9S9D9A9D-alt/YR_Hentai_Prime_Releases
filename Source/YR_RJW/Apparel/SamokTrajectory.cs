using UnityEngine;
using Verse;

namespace YR_RJW {
 public static class SamokTrajectory {
  public const int Count=7,TrailSegments=10;
  static readonly Material[] trailMaterials=new Material[TrailSegments];
  public static float Accelerate(float t){t=Mathf.Clamp01(t);return .62f*t+.38f*t*t*t*t;}
  public static void DrawTrailSegment(Vector3 from,Vector3 to,int age){
   if((from-to).sqrMagnitude<.0001f)return;
   float opacity=1f-(float)age/TrailSegments;
   var material=trailMaterials[age]??(trailMaterials[age]=SolidColorMaterials.SimpleSolidColorMaterial(new Color(.78f,.47f,1f,opacity*.65f),false));
   GenDraw.DrawLineBetween(from,to,material,.045f+.065f*opacity);
  }
  static readonly Vector2[][] paths={
   new[]{new Vector2(.20f,.72f),new Vector2(.50f,0),new Vector2(.78f,-.72f)}, // S
   new[]{new Vector2(.12f,.65f),new Vector2(.48f,1),new Vector2(.86f,.65f)}, // C
   new[]{new Vector2(-.12f,.65f),new Vector2(.23f,1),new Vector2(.77f,1),new Vector2(1.13f,.55f)}, // U
   new[]{new Vector2(.40f,0),new Vector2(.76f,0),new Vector2(.98f,.63f),new Vector2(.65f,1.05f),new Vector2(.39f,.55f),new Vector2(.69f,.18f)}, // P
   new[]{new Vector2(.18f,.85f),new Vector2(.36f,-.75f),new Vector2(.58f,.75f),new Vector2(.78f,-.65f)}, // W
   new[]{new Vector2(.10f,.82f),new Vector2(.62f,1.1f),new Vector2(1.13f,.72f),new Vector2(1.14f,-.30f),new Vector2(.72f,-.40f),new Vector2(.64f,.05f),new Vector2(.84f,.15f)}, // G
   new[]{new Vector2(.26f,.65f),new Vector2(.58f,.65f),new Vector2(.39f,-.60f),new Vector2(.76f,-.60f)} // Z
  };
  public static int Next(int previous){
   if(previous<1||previous>Count)return Rand.Range(1,Count+1);
   int choice=Rand.Range(1,Count);return choice>=previous?choice+1:choice;
  }
  static float Variation(int seed,int index,int axis){
   uint bits=unchecked((uint)seed+(uint)index*0x9e3779b9u+(uint)axis*0x85ebca6bu);
   bits=(bits^(bits>>16))*0x7feb352du;bits=(bits^(bits>>15))*0x846ca68bu;
   return ((bits^(bits>>16))&65535)/32767.5f-1f;
  }
  static Vector2 Knot(Vector2[] path,int index,int variation){
   if(index<=0)return Vector2.zero;
   if(index>path.Length)return Vector2.right;
   var point=path[index-1];
   if(variation!=0){point.x+=Variation(variation,index,0)*.055f;point.y+=Variation(variation,index,1)*.22f;}
   return point;
  }
  public static Vector3 Sample(int pattern,Vector3 start,Vector3 end,Vector3 departure,Vector3 arrival,float width,float progress,int variation=0){
   if(progress<=0)return start;
   if(progress>=1)return end;
   var knots=paths[pattern-1];int segments=knots.Length+1;
   float scaled=progress*segments;int index=Mathf.Min(segments-1,(int)scaled);float t=scaled-index;
   var axis=end-start;var side=Vector3.Cross(Vector3.up,axis).normalized*width;
   Vector3 World(Vector2 point)=>axis*point.x+side*point.y;
   var p0=World(Knot(knots,index,variation));var p1=World(Knot(knots,index+1,variation));
   float tension=pattern==7||pattern==5?.28f:.46f;
   var m0=index==0?departure.normalized*(p1-p0).magnitude:World(Knot(knots,index+1,variation)-Knot(knots,index-1,variation))*tension;
   var m1=index==segments-1?arrival.normalized*(p1-p0).magnitude:World(Knot(knots,index+2,variation)-Knot(knots,index,variation))*tension;
   float t2=t*t,t3=t2*t;
   var position=start+(2*t3-3*t2+1)*p0+(t3-2*t2+t)*m0+(-2*t3+3*t2)*p1+(t3-t2)*m1;
   if(variation==0)return position;
   var tangent=(6*t2-6*t)*p0+(3*t2-4*t+1)*m0+(-6*t2+6*t)*p1+(3*t2-2*t)*m1;
   var perpendicular=Vector3.Cross(Vector3.up,tangent).normalized;
   float phase=(variation&1023)*.006135923f;
   float clock=progress*(1+.35f*progress)*2*Mathf.PI;
   float wave=.145f*Mathf.Sin(2.3f*clock+phase)+.052f*Mathf.Sin(8.1f*clock-phase*1.37f);
   float envelope=progress*(1-progress);
   return position+perpendicular*(Mathf.Abs(width)*16*envelope*envelope*wave);
  }
 }
}

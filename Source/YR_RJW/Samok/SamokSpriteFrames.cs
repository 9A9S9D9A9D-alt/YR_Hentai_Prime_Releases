using UnityEngine;
using Verse;
namespace YR_RJW {
 // All frames are static: only the selected UV cell changes, never the building's vertices.
 [StaticConstructorOnStartup]
 internal static class SamokSpriteFrames {
  internal static readonly Material ApertureMaterial=MaterialPool.MatFrom("Yuran/Things/Building/SamokRefinery/ApertureAtlas",ShaderDatabase.Cutout);
  internal static readonly Material ClosingMaterial=MaterialPool.MatFrom("Yuran/Things/Building/ServiceSac/ClosingAtlas",ShaderDatabase.Cutout);
  internal static readonly Material OpeningMaterial=MaterialPool.MatFrom("Yuran/Things/Building/ServiceSac/OpeningAtlas",ShaderDatabase.Cutout);
  internal static readonly Material WorkingMaterial=MaterialPool.MatFrom("Yuran/Things/Building/ServiceSac/WorkingAtlas",ShaderDatabase.Cutout);
  private static readonly Material[] WorkingStages=LoadWorkingStages();
  internal static Material WorkingForStage(int stage)=>WorkingStages[Mathf.Clamp(stage,0,6)];
  private static Material[] LoadWorkingStages(){
   var result=new Material[7];
   for(int i=0;i<result.Length;i++)result[i]=MaterialPool.MatFrom("Yuran/Things/Building/ServiceSac/WorkingStage"+i+"Atlas",ShaderDatabase.Cutout);
   return result;
  }
  internal static readonly Mesh[] WorkingFrames=BuildWorking();
  internal static readonly Mesh[] CocoonFrames=Build(false);
  internal static readonly Mesh[] ApertureFrames=Build(true);
  internal static readonly Mesh CocoonInterior=Interior();
  internal const int EntryMoveTicks=24;
  internal static int ClosingFrame(int ticks)=>Mathf.Clamp((ticks-EntryMoveTicks)*8/(ServiceSacCycle.TransitionDuration-EntryMoveTicks),0,7);
  internal static int TransitionFrame(int ticks)=>Mathf.Clamp(ticks*8/ServiceSacCycle.TransitionDuration,0,7);
  private static Mesh[] BuildWorking(){
   var frames=new Mesh[16];
   for(int frame=0;frame<frames.Length;frame++){
    const float inset=.0003f;
    float left=(frame%4)*.25f,right=left+.25f,top=1-(frame/4)*.25f,bottom=top-.25f;
    var m=new Mesh{name="SamokWorkingFrame"+frame};
    m.vertices=new[]{new Vector3(-.5f,0,-.5f),new Vector3(-.5f,0,.5f),new Vector3(.5f,0,.5f),new Vector3(.5f,0,-.5f)};
    m.uv=new[]{new Vector2(left+inset,bottom+inset),new Vector2(left+inset,top-inset),new Vector2(right-inset,top-inset),new Vector2(right-inset,bottom+inset)};
    m.triangles=new[]{0,1,2,0,2,3};m.RecalculateNormals();m.RecalculateBounds();frames[frame]=m;
   }
   return frames;
  }
  private static Mesh[] Build(bool aperture){
   var frames=new Mesh[8];
   for(int frame=0;frame<8;frame++){
    float left=(frame%4)/4f,right=left+0.25f,top=1-(frame/4)*0.5f,bottom=top-0.5f;
    var m=new Mesh{name=aperture?"SamokApertureFrame"+frame:"SamokCocoonFrame"+frame};
    if(!aperture){
     const float inset=0.0008f;
     m.vertices=new[]{new Vector3(-.5f,0,-.5f),new Vector3(-.5f,0,.5f),new Vector3(.5f,0,.5f),new Vector3(.5f,0,-.5f)};
     m.uv=new[]{new Vector2(left+inset,bottom+inset),new Vector2(left+inset,top-inset),new Vector2(right-inset,top-inset),new Vector2(right-inset,bottom+inset)};
     m.triangles=new[]{0,1,2,0,2,3};
    }else{
     const int segments=40;var vertices=new Vector3[segments+1];var uv=new Vector2[segments+1];var triangles=new int[segments*3];
     float u=(left+right)*.5f,v=(top+bottom)*.5f;uv[0]=new Vector2(u,v);
     // Sample only the central inner membrane, excluding the reference atlas's outer border.
     for(int i=0;i<segments;i++){
      float angle=-i*Mathf.PI*2f/segments,x=Mathf.Cos(angle),z=Mathf.Sin(angle);
      vertices[i+1]=new Vector3(x*.5f,0,z*.5f);uv[i+1]=new Vector2(u+x*.068f,v+z*.136f);
      triangles[i*3]=0;triangles[i*3+1]=i+1;triangles[i*3+2]=(i+1)%segments+1;
     }
     m.vertices=vertices;m.uv=uv;m.triangles=triangles;
    }
    m.RecalculateNormals();m.RecalculateBounds();frames[frame]=m;
   }
   return frames;
  }
  private static Mesh Interior(){
   const int segments=40;var vertices=new Vector3[segments+1];var uv=new Vector2[segments+1];var triangles=new int[segments*3];uv[0]=new Vector2(.5f,.5f);
   for(int i=0;i<segments;i++){
    float a=-i*Mathf.PI*2f/segments;float x=Mathf.Cos(a)*.34f,z=Mathf.Sin(a)*.44f;
    vertices[i+1]=new Vector3(x,0,z);uv[i+1]=new Vector2(x+.5f,z+.5f);triangles[i*3]=0;triangles[i*3+1]=i+1;triangles[i*3+2]=(i+1)%segments+1;
   }
   var m=new Mesh{name="SamokCocoonBackInterior"};m.vertices=vertices;m.uv=uv;m.triangles=triangles;m.RecalculateNormals();m.RecalculateBounds();return m;
  }
 }
}

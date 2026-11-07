using UnityEngine;
using Verse;
namespace YR_RJW {
 internal sealed partial class CoccyxTentacleDrawing {
  static Vector3 RemoteDisplayPoint(HediffComp_CoccyxCombat comp,PawnDrawParms parms,Vector3 root,CoccyxLimb limb,int index,float t,float progress,float scale){var rest=root+Rest(comp,parms.facing,index,t)*scale;var path=HediffComp_CoccyxCombat.RemotePath(root,limb,t*progress);return Vector3.Lerp(rest,path,Smooth(progress/.2f));}
  Material jetCore,jetTail;
  const int JetSegments=26;
  readonly Mesh[] jetBodies=new Mesh[6],jetHighlights=new Mesh[6];
  readonly Vector3[][] jetBodyPoints=new Vector3[6][],jetHighlightPoints=new Vector3[6][];
  static Mesh CreateJetMesh(out Vector3[] points){
   var mesh=new Mesh();mesh.MarkDynamic();points=new Vector3[(JetSegments+1)*2];
   var indices=new int[JetSegments*6];
   for(int i=0;i<JetSegments;i++){
    int v=i*2,k=i*6;
    indices[k]=v;indices[k+1]=v+1;indices[k+2]=v+2;
    indices[k+3]=v+1;indices[k+4]=v+3;indices[k+5]=v+2;
   }
   mesh.vertices=points;mesh.triangles=indices;return mesh;
  }
  void DrawContinuousJet(int index,Vector3 start,Vector3 end,Quaternion rotation,float age,float pressure){
   if(jetBodies[index]==null){
    jetBodies[index]=CreateJetMesh(out jetBodyPoints[index]);
    jetHighlights[index]=CreateJetMesh(out jetHighlightPoints[index]);
   }
   var axis=end-start;
   var side=Vector3.Cross(axis,Vector3.up).normalized;
   float phase=age*.52f+index*2.14f;
   Vector3 Point(float t)=>start+axis*t+side*((.062f*Mathf.Sin(10f*t-phase)+.027f*Mathf.Sin(27f*t-phase*1.55f))*4*t*(1-t));
   for(int step=0;step<=JetSegments;step++){
    float t=step/(float)JetSegments;
    var center=Point(t);
    var tangent=Point(Mathf.Min(1,t+.012f))-Point(Mathf.Max(0,t-.012f));
    var normal=Vector3.Cross(tangent,Vector3.up).normalized;
    float pulse=.86f+.14f*Mathf.Sin(t*31-phase*1.25f);
    float radius=Mathf.Lerp(.074f,.167f,Mathf.Sqrt(t))*pulse*pressure;
    float highlightOffset=Mathf.Sin(t*13-phase)*radius*.24f;
    jetBodyPoints[index][step*2]=center-normal*radius;
    jetBodyPoints[index][step*2+1]=center+normal*radius;
    var sheen=center+normal*highlightOffset+Vector3.up*.0015f;
    jetHighlightPoints[index][step*2]=sheen-normal*radius*.47f;
    jetHighlightPoints[index][step*2+1]=sheen+normal*radius*.47f;
   }
   jetBodies[index].vertices=jetBodyPoints[index];jetBodies[index].RecalculateBounds();
   jetHighlights[index].vertices=jetHighlightPoints[index];jetHighlights[index].RecalculateBounds();
   Graphics.DrawMesh(jetBodies[index],Vector3.zero,Quaternion.identity,jetTail,0);
   Graphics.DrawMesh(jetHighlights[index],Vector3.zero,Quaternion.identity,jetCore,0);
   for(int droplet=0;droplet<4;droplet++){
    float t=Mathf.Repeat(age*.105f+droplet*.25f+index*.13f,1f);
    var glint=Point(t)+side*(Mathf.Sin(t*18f+phase)*.061f);
    Graphics.DrawMesh(MeshPool.plane10,Matrix4x4.TRS(glint+Vector3.up*.002f,rotation,new Vector3(.13f*pressure,1,.31f*pressure)),spray,0);
   }
   float splash=.42f+Mathf.Sin(phase*1.3f)*.09f;
   Graphics.DrawMesh(MeshPool.plane10,Matrix4x4.TRS(end+Vector3.up*.003f,rotation,new Vector3(splash*pressure,1,.45f*pressure)),spray,0);
  }
  void DisposeJetMeshes(){
   foreach(var mesh in jetBodies)if(mesh!=null)UnityEngine.Object.Destroy(mesh);
   foreach(var mesh in jetHighlights)if(mesh!=null)UnityEngine.Object.Destroy(mesh);
  }
  void DrawRemoteSprays(HediffComp_CoccyxCombat comp,Matrix4x4 matrix){
   int now=Find.TickManager.TicksGame;
   for(int index=0;index<comp.limbs.Count;index++){
    var limb=comp.limbs[index];
    int age=now-limb.shotTick;
    bool streaming=limb.continuous&&limb.streamStart>=0&&(limb.streamStop<0||now-limb.streamStop<6);
    if(!limb.remoteFluid||limb.shotTick<0||age<0||(!streaming&&age>=limb.shotDuration))continue;
    spray??=MaterialPool.MatFrom("Yuran/Implants/CoccyxTentacles/SlimeSpray",ShaderDatabase.Transparent);
    jetCore??=SolidColorMaterials.SimpleSolidColorMaterial(new Color(.98f,.97f,1f,.88f),false);
    jetTail??=SolidColorMaterials.SimpleSolidColorMaterial(new Color(.91f,.91f,1f,.47f),false);
    var end=limb.target?.Spawned==true?limb.target.DrawPos:limb.shotEnd;
    var start=limb.shotOrigin;
    start.y=end.y=matrix.MultiplyPoint3x4(Vector3.zero).y+.009f;
    var direction=end-start;
    if(direction.sqrMagnitude<.001f)continue;
    var rotation=Quaternion.AngleAxis(direction.AngleFlat(),Vector3.up);
    float progress=Mathf.Clamp01((age+.5f)/limb.shotDuration);
    var head=Vector3.Lerp(start,end,progress);
    if(streaming){
     float ending=limb.streamStop<0?0:Mathf.Clamp01((now-limb.streamStop)/6f);
     float pressure=Mathf.Lerp(.35f,1f,Mathf.Clamp01(age/6f))*(1-ending);
     DrawContinuousJet(index,Vector3.Lerp(start,head,ending),head,rotation,age,pressure);
     continue;
    }
    var tail=Vector3.Lerp(start,end,Mathf.Max(0,progress-5f/limb.shotDuration));
    var mid=Vector3.Lerp(tail,head,.44f);
    GenDraw.DrawLineBetween(tail,mid,jetTail,.075f);
    GenDraw.DrawLineBetween(mid,head,jetCore,.135f);
    Graphics.DrawMesh(MeshPool.plane10,Matrix4x4.TRS(head,rotation,new Vector3(.24f,1,.48f)),spray,0);
    if(progress<.6f)continue;
    var side=Vector3.Cross(Vector3.up,direction).normalized;
    float spread=(progress-.6f)*.6f;
    for(int i=-1;i<=1;i+=2){
     var droplet=Vector3.Lerp(mid,head,.62f)+side*(spread*i);
     Graphics.DrawMesh(MeshPool.plane10,Matrix4x4.TRS(droplet,rotation,new Vector3(.10f,1,.18f)),spray,0);
    }
   }
  }
 }
}

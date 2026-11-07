using System;
using UnityEngine;
using Verse;

namespace YR_RJW {
 internal sealed partial class CoccyxTentacleDrawing {
  const int SplitSegments=40;
  readonly Mesh[] splitMeshes=new Mesh[1];
  readonly Vector3[][] splitPoints=new Vector3[1][];
  Material[] splitFrames;
  Mesh[,] splitPanels;

  // Follow the top edge of the front lip in the three 112 px source frames.
  static readonly int[] PanelColumns={0,20,29,37,46,55,64,73,82,92,112};
  static readonly int[][] FrontLip={
   new[]{57,57,55,49,46,45,46,49,55,57,57},
   new[]{55,55,52,47,44,43,44,47,52,55,55},
   new[]{49,49,46,38,27,22,23,29,40,49,49}
  };

  void DrawSplitStrip(int index,Func<float,Vector3> left,Func<float,Vector3> right,Material material){
   if(splitMeshes[index]==null){
    var points=new Vector3[(SplitSegments+1)*2];
    var uv=new Vector2[points.Length];
    var normals=new Vector3[points.Length];
    var triangles=new int[SplitSegments*6];
    for(int segment=0;segment<=SplitSegments;segment++){
     int vertex=segment*2;
     uv[vertex]=new Vector2(segment/(float)SplitSegments,0);
     uv[vertex+1]=new Vector2(segment/(float)SplitSegments,1);
     normals[vertex]=normals[vertex+1]=Vector3.up;
     if(segment==SplitSegments)continue;
     int triangle=segment*6;
     triangles[triangle]=vertex;triangles[triangle+1]=vertex+1;triangles[triangle+2]=vertex+2;
     triangles[triangle+3]=vertex+1;triangles[triangle+4]=vertex+3;triangles[triangle+5]=vertex+2;

    }
    var mesh=new Mesh();mesh.MarkDynamic();mesh.vertices=points;mesh.uv=uv;mesh.normals=normals;mesh.triangles=triangles;
    splitMeshes[index]=mesh;splitPoints[index]=points;
   }
   for(int segment=0;segment<=SplitSegments;segment++){
    float t=segment/(float)SplitSegments;
    splitPoints[index][segment*2]=left(t);
    splitPoints[index][segment*2+1]=right(t);
   }
   splitMeshes[index].vertices=splitPoints[index];splitMeshes[index].RecalculateBounds();
   Graphics.DrawMesh(splitMeshes[index],Vector3.zero,Quaternion.identity,material,0);
  }

  void DrawSplitEscort(HediffComp_CoccyxCombat comp,PawnDrawParms parms,Vector3 root,float scale){
   var target=comp.EscortTarget;
   float size=Mathf.Clamp(Mathf.Sqrt(target.BodySize),.72f,1.5f);
   float progress=comp.EscortProgress;
   float reach=Smooth(progress/.48f);
   float opening=Smooth((progress-.28f)/.24f);
   float lift=Hediff_CoccyxEscort.Get(target)?.Lift??0;
   var bottom=target.DrawPos+new Vector3(0,0,(-.91f+.22f*lift)*size);
   var restingTip=root+Rest(comp,parms.facing,0,1)*scale;
   var contact=Vector3.Lerp(restingTip,bottom,reach);
   float unfold=Mathf.Lerp(.45f,1,opening);
   var shellSize=new Vector3(1.40f*size*comp.splitOuterWidth*unfold,1,1.20f*size*comp.splitOuterHeight*unfold);
   var outer=contact+new Vector3(comp.splitOuterX*size,0,(.50f+comp.splitOuterZ)*size);
   float phase=2*Mathf.Clamp01((progress-comp.splitOuterStart)/Mathf.Max(.05f,1-comp.splitOuterStart));
   // The stem ends inside the opaque neck. Its width follows the neck when the shell is resized.
   var neck=outer+new Vector3((Mathf.Lerp(55f,57f,phase*.5f)/112f-.5f)*shellSize.x,0,(.5f-101f/112f)*shellSize.z);
   var end=Vector3.Lerp(contact,neck,opening);
   var delta=end-root;
   var bend=Vector3.Cross(Vector3.up,delta.WithY(0).normalized)*(.24f*Mathf.Sin(progress*Mathf.PI));
   Vector3 Stem(float t)=>Bezier(root,root+delta*.34f+bend,end-new Vector3(0,0,.27f*size),end,t);
   Vector3 StemEdge(float t,float sign){
    var tangent=Stem(Mathf.Min(1,t+.01f))-Stem(Mathf.Max(0,t-.01f));
    var side=Vector3.Cross(tangent,Vector3.up).normalized;
    float width=Mathf.Lerp(.065f*scale,.58f*size,Smooth(progress/.52f))*Mathf.Lerp(.64f,1,t);
    float neckWidth=shellSize.x*(20f/112f);
    width=Mathf.Lerp(width,neckWidth,Smooth((t-.42f)/.58f)*opening);
    var point=Stem(t)+side*(width*.5f*sign);point.y=root.y;return point;
   }
   DrawSplitStrip(0,t=>StemEdge(t,-1),t=>StemEdge(t,1),shaft);
   if(opening<=0)return;
   float front=AltitudeLayer.MoteOverhead.AltitudeFor()+.005f;
   var pawnLayer=target.Downed?AltitudeLayer.LayingPawn:AltitudeLayer.Pawn;
   float back=Mathf.Lerp(pawnLayer.AltitudeFor()-.009f,front-.041f,lift);
   DrawSplitPanels(phase,outer,shellSize,comp.SplitColor,back,front);
   DrawSplitBubble(comp,opening);
  }

  static Mesh MakeSplitPanel(int frame,bool front){
   int count=PanelColumns.Length;
   var points=new Vector3[count*2];
   var uv=new Vector2[points.Length];
   var indices=new int[(count-1)*6];
   for(int column=0;column<count;column++){
    float x=PanelColumns[column]/112f;
    float lip=1-FrontLip[frame][column]/112f;
    float bottom=front?0:lip,top=front?lip:1;
    int v=column*2;
    points[v]=new Vector3(x-.5f,0,bottom-.5f);
    points[v+1]=new Vector3(x-.5f,0,top-.5f);
    uv[v]=new Vector2(x,bottom);uv[v+1]=new Vector2(x,top);
    if(column==count-1)continue;
    int i=column*6;
    indices[i]=v;indices[i+1]=v+1;indices[i+2]=v+2;
    indices[i+3]=v+1;indices[i+4]=v+3;indices[i+5]=v+2;
   }
   var mesh=new Mesh{vertices=points,uv=uv,triangles=indices};
   mesh.RecalculateBounds();return mesh;
  }

  void DrawSplitPanels(float phase,Vector3 position,Vector3 scale,Color tint,float back,float front){
   if(splitFrames==null){
    splitFrames=new Material[3];
    splitPanels=new Mesh[3,2];
    for(int i=0;i<3;i++){
     splitFrames[i]=new Material(MaterialPool.MatFrom("Yuran/Implants/CoccyxTentacles/SplitMouth/Outer"+i,ShaderDatabase.Transparent));
     splitPanels[i,0]=MakeSplitPanel(i,false);
     splitPanels[i,1]=MakeSplitPanel(i,true);
    }
   }
   int current=Mathf.Min(2,Mathf.FloorToInt(phase));
   float blend=Smooth(phase-current);
   void Draw(int frame,float opacity,float depth){
    var color=tint;color.a=opacity;splitFrames[frame].color=color;
    position.y=back+depth;
    Graphics.DrawMesh(splitPanels[frame,0],Matrix4x4.TRS(position,Quaternion.identity,scale),splitFrames[frame],0);
    position.y=front+depth;
    Graphics.DrawMesh(splitPanels[frame,1],Matrix4x4.TRS(position,Quaternion.identity,scale),splitFrames[frame],0);
   }
   Draw(current,1-blend,0);
   if(current<2&&blend>0)Draw(current+1,blend,.0001f);
  }

  void DisposeSplitMeshes(){
   ReleaseSplitBubble();
   foreach(var mesh in splitWrapMeshes)if(mesh!=null)UnityEngine.Object.Destroy(mesh);
   if(splitFrames!=null)foreach(var material in splitFrames)if(material!=null)UnityEngine.Object.Destroy(material);
   if(splitPanels!=null)foreach(var mesh in splitPanels)if(mesh!=null)UnityEngine.Object.Destroy(mesh);
   foreach(var mesh in splitMeshes)if(mesh!=null)UnityEngine.Object.Destroy(mesh);
  }
 }
}

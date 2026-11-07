using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using YR_Hentai_Prime_AnimationBed;

namespace YR_RJW {
 internal sealed partial class CoccyxTentacleDrawing {
  static PortraitSetting splitPortraitSetting;
  static Graphic splitBubbleBack,splitBubbleFront;

  static void LoadFacilityBubble(){
   if(splitPortraitSetting!=null)return;
   var props=DefDatabase<ThingDef>.GetNamed("YR_Building_GloryHole").comps.OfType<CompProperties_AnimationSetting>().First();
   splitPortraitSetting=BubblePortraitV57.Slots(props).First().Value;
   splitBubbleBack=DefDatabase<BedAnimationDef>.GetNamed("YR_GloryHole_FellatioBackGround_BackGround").bedAnimationSettings[0].graphicData.Graphic;
   splitBubbleFront=DefDatabase<BedAnimationDef>.GetNamed("YR_GloryHole_FellatioBackGround_Front").bedAnimationSettings[0].graphicData.Graphic;
  }
  const int FluidColumns=40,FluidBands=4;
  Mesh splitFluidMesh;
  readonly Vector3[] splitFluidVertices=new Vector3[(FluidColumns+1)*FluidBands];
  readonly Color[] splitFluidColors=new Color[(FluidColumns+1)*FluidBands];
  Material splitFluidMaterial,splitBubbleBrace;
  const int BraceColumns=12,BraceRows=40;
  Mesh splitBraceMesh;
  readonly Vector3[] splitBraceVertices=new Vector3[(BraceColumns+1)*(BraceRows+1)];
  readonly Vector2[] splitBraceUvs=new Vector2[(BraceColumns+1)*(BraceRows+1)];
  readonly float[] splitBraceRowX=new float[BraceColumns+1];
  // Uneven timing keeps the lower crossing fixed while the sides settle in turn.
  static readonly int[] BracePoseTicks={0,7,18,30,47,63,81};
  static readonly Vector2[] BracePoses={
   new Vector2(.08f,-.09f),new Vector2(-.26f,.13f),new Vector2(.04f,-.24f),
   new Vector2(.24f,-.11f),new Vector2(-.12f,.24f),new Vector2(.17f,.07f),
   new Vector2(.08f,-.09f)
  };
  Pawn splitBubblePawn;
  Material splitBubbleMaterial;
  Texture2D splitNativeBodyFrame;
  int splitPreparedFacing;
  Vector3 splitPreparedLegOffset;
  float splitPreparedScale;
  Color splitPreparedColor;
  Graphic splitNativeBody;
  PawnRenderNode splitPreparedBodyNode;

  void DrawSplitBubble(HediffComp_CoccyxCombat comp,float opening){
   var target=comp.EscortTarget;
   if(!comp.HasEscortGrip||!comp.splitShowBubble||target?.Spawned!=true||!AdultEligibility.IsAllowed(target)){ReleaseSplitBodyFrames();return;}
   // The prepared body node includes installed body mods, but no equipment or apparel nodes.
   var tree=target.Drawer.renderer.renderTree;
   tree.TryGetNodeByTag(PawnRenderNodeTagDefOf.Body,out var node);
   if(node==null)return;
   LoadFacilityBubble();
   var color=node.ColorFor(target);
   var legOffset=new Vector3(comp.splitLegX,0,comp.splitLegZ);
   if(splitBubblePawn!=target||splitPreparedFacing!=comp.splitBodyFacing||
      splitPreparedLegOffset!=legOffset||splitPreparedScale!=comp.splitBodyZoom||splitPreparedColor!=color||
      splitNativeBody!=node.PrimaryGraphic||splitPreparedBodyNode!=node){
    ReleaseSplitBodyFrames();splitBubblePawn=target;
    splitPreparedFacing=comp.splitBodyFacing;
    splitPreparedLegOffset=legOffset;splitPreparedScale=comp.splitBodyZoom;splitPreparedColor=color;
    splitNativeBody=node.PrimaryGraphic;splitPreparedBodyNode=node;
    var facing=SplitBodyFacing(comp.splitBodyFacing);
    var native=ResolveNativeBubbleGraphic(target,splitNativeBody,color,facing);
    Vector2? meshSize=null;
    if(native!=null){
     var mesh=node.GetMesh(new PawnDrawParms{pawn=target,facing=facing});
     if(mesh!=null){
      var extent=mesh.bounds.size;
      if(extent.x>0&&extent.z>0)meshSize=new Vector2(extent.x,extent.z);
     }
    }
    var mask=ContentFinder<Texture2D>.Get(splitPortraitSetting.maskPath);
    float zoom=splitPortraitSetting.cameraZoom*comp.splitBodyZoom/1.4f;
    // Use the intact body and its active SA0 body parts. No clothes or equipment are sampled.
    var position=legOffset-splitPortraitSetting.cameraOffset;
    splitNativeBodyFrame=FacilityPortrait.FrameTexture(null,position,zoom,mask,native,facing,sharedMeshSize:meshSize,
     bodyLayers:NativeBubbleBodyLayers(target,node,facing));
    splitBubbleMaterial=BedAnimationUtility.CreatePortraitMaterial(splitPortraitSetting);
    splitBubbleMaterial.mainTexture=splitNativeBodyFrame;
   }
   var center=target.DrawPos+new Vector3(comp.splitBubbleX,0,comp.splitBubbleZ);
   center.y=AltitudeLayer.MetaOverlays.AltitudeFor();
   float scale=comp.splitBubbleScale;
   void DrawBubbleArt(Graphic art,Vector3 pos){
    Graphics.DrawMesh(art.MeshAt(Rot4.South),Matrix4x4.TRS(pos,Quaternion.identity,new Vector3(scale,1,scale)),art.MatSingle,0);
   }
   DrawBubbleArt(splitBubbleBack,center);
   var portraitSize=splitPortraitSetting.drawSize*scale;
   var portrait=Matrix4x4.TRS(center+Vector3.up*.001f,Quaternion.AngleAxis(splitPortraitSetting.angle,Vector3.up),
    new Vector3(portraitSize.x,1,portraitSize.y));
   Graphics.DrawMesh(splitPortraitSetting.portraitMeshGraphicData.Graphic.MeshAt(Rot4.South),portrait,splitBubbleMaterial,0);
   DrawBubbleTendrils(comp,center,opening,scale);
   DrawBubbleFluid(center,scale,comp.ExtractionFill);
   DrawBubbleArt(splitBubbleFront,center+Vector3.up*.002f);
  }

  void DrawBubbleFluid(Vector3 center,float bubbleScale,float fill){
   if(fill<=0)return;
   if(splitFluidMesh==null){
    var triangles=new int[FluidColumns*(FluidBands-1)*6];
    int index=0;
    for(int x=0;x<FluidColumns;x++)for(int band=0;band<FluidBands-1;band++){
     int left=x*FluidBands+band,right=left+FluidBands;
     triangles[index++]=left;triangles[index++]=left+1;triangles[index++]=right;
     triangles[index++]=left+1;triangles[index++]=right+1;triangles[index++]=right;
    }
    splitFluidMesh=new Mesh();splitFluidMesh.MarkDynamic();
    splitFluidMesh.vertices=splitFluidVertices;splitFluidMesh.triangles=triangles;
    splitFluidMaterial=new Material(ShaderDatabase.Transparent){mainTexture=BaseContent.WhiteTex,color=Color.white};
   }
   float radius=BubbleOpeningRadius(bubbleScale);
   float level=Mathf.Lerp(-1f,.99f,fill);
   float time=Find.TickManager.TicksGame*.032f;
   float waveStrength=.08f*Mathf.Sin(Mathf.PI*fill);
   for(int col=0;col<=FluidColumns;col++){
    float x=Mathf.Lerp(-1,1,col/(float)FluidColumns);
    float circle=Mathf.Sqrt(Mathf.Max(0,1-x*x));
    float curve=level+waveStrength*(Mathf.Sin(4.4f*x-time)+.34f*Mathf.Sin(8f*x+time*.62f))+.065f*x*x;
    float surface=Mathf.Clamp(curve,-circle,circle);
    float broad=Mathf.Max(-circle,surface-.24f);
    float edge=Mathf.Max(broad,surface-.044f);
    float shine=Mathf.Clamp01((Mathf.Sin(5.4f*x-time*.75f)+.18f)*1.25f);
    if(curve>circle)shine=0;
    int v=col*FluidBands;
    splitFluidVertices[v]=new Vector3(x*radius,0,-circle*radius);
    splitFluidVertices[v+1]=new Vector3(x*radius,0,broad*radius);
    splitFluidVertices[v+2]=new Vector3(x*radius,0,edge*radius);
    splitFluidVertices[v+3]=new Vector3(x*radius,0,surface*radius);
    splitFluidColors[v]=new Color(.65f,.68f,.81f,.89f);
    splitFluidColors[v+1]=new Color(.87f,.89f,.98f,.83f);
    splitFluidColors[v+2]=Color.Lerp(new Color(.91f,.92f,.98f,.86f),Color.white,shine*.54f);
    splitFluidColors[v+3]=Color.Lerp(new Color(.70f,.73f,.86f,.85f),Color.white,shine);
   }
   splitFluidMesh.vertices=splitFluidVertices;splitFluidMesh.colors=splitFluidColors;splitFluidMesh.RecalculateBounds();
   Graphics.DrawMesh(splitFluidMesh,center+Vector3.up*.0018f,Quaternion.identity,splitFluidMaterial,0);
  }

  static Rot4 SplitBodyFacing(int setting)=>setting==2?Rot4.North:Rot4.South;

  static List<FacilityPortrait.BodyGraphicLayer> NativeBubbleBodyLayers(Pawn pawn,PawnRenderNode body,Rot4 facing){
   if(body.children==null)return null;
   List<FacilityPortrait.BodyGraphicLayer> layers=null;
   var parms=new PawnDrawParms{pawn=pawn,facing=facing,matrix=Matrix4x4.identity,posture=PawnPosture.Standing};
   float baseDepth=body.Worker.AltitudeFor(body,parms);
   foreach(var child in body.children){
    if(child.apparel!=null||(!(child is PawnRenderNode_Fur)&&
       child.GetType().FullName!="SizedApparel.SizedApparelRenderNode"))continue;
    var graphic=child.PrimaryGraphic;
    if(!HasBubbleTexture(graphic,facing)||!child.Worker.CanDrawNow(child,parms))continue;
    child.GetTransform(parms,out var position,out var pivot,out var rotation,out var scale);
    var mesh=child.GetMesh(parms);
    if(mesh==null)continue;
    var extent=mesh.bounds.size;
    var size=new Vector2(Mathf.Abs(extent.x*scale.x),Mathf.Abs(extent.z*scale.z));
    if(size.x<=0||size.y<=0)continue;
    position+=pivot-rotation*Vector3.Scale(pivot,scale);
    if(layers==null)layers=new List<FacilityPortrait.BodyGraphicLayer>();
    layers.Add(new FacilityPortrait.BodyGraphicLayer(graphic,position,size,child.Worker.AltitudeFor(child,parms)-baseDepth));
   }
   layers?.Sort((a,b)=>a.depth.CompareTo(b.depth));
   return layers;
  }

  static bool HasBubbleTexture(Graphic graphic,Rot4 facing){
   return graphic?.MatAt(facing)?.mainTexture is Texture2D texture&&texture!=BaseContent.BadTex;
  }

  static Graphic ResolveNativeBubbleGraphic(Pawn target,Graphic preferred,Color color,Rot4 facing){
   string suffix=facing==Rot4.North?"_north":"_south";
   string opposite=facing==Rot4.North?"_south":"_north";
   var bodyType=target.story?.bodyType;
   if(HasBubbleTexture(preferred,facing)){
    var mat=preferred.MatAt(facing);
    var texture=mat.mainTexture as Texture2D;
    string path=preferred.path??"";
    string textureName=texture.name??"";
    var otherSex=bodyType==BodyTypeDefOf.Female?BodyTypeDefOf.Male:
     bodyType==BodyTypeDefOf.Male?BodyTypeDefOf.Female:null;
    string wrongSexPath=otherSex?.bodyNakedGraphicPath;
    bool wrongSex=wrongSexPath!=null&&(path.Equals(wrongSexPath,StringComparison.OrdinalIgnoreCase)||
     path.Equals(wrongSexPath+suffix,StringComparison.OrdinalIgnoreCase)||
     path.Equals(wrongSexPath+opposite,StringComparison.OrdinalIgnoreCase));
    bool wrongFacing=textureName.EndsWith(opposite,StringComparison.OrdinalIgnoreCase)||
     path.EndsWith(opposite,StringComparison.OrdinalIgnoreCase);
    if(!wrongFacing&&preferred is Graphic_Multi&&path.Length!=0){
     var oppositeTexture=ContentFinder<Texture2D>.Get(path+opposite,false);
     wrongFacing=oppositeTexture==texture&&ContentFinder<Texture2D>.Get(path+suffix,false)==null;
    }
    if(!wrongSex&&!wrongFacing)return preferred;
    if(!wrongSex&&path.Length!=0){
     if(path.EndsWith(opposite,StringComparison.OrdinalIgnoreCase))path=path.Substring(0,path.Length-opposite.Length);
     var directional=path+suffix;
     if(ContentFinder<Texture2D>.Get(directional,false)!=null)
      return GraphicDatabase.Get<Graphic_Single>(directional,mat.shader,preferred.drawSize,preferred.color,preferred.colorTwo,preferred.data,preferred.maskPath);
    }
   }
   Graphic LoadBody(BodyTypeDef type){
    string path=type?.bodyNakedGraphicPath;
    if(string.IsNullOrEmpty(path))return null;
    string directional=path+suffix;
    if(ContentFinder<Texture2D>.Get(directional,false)==null)return null;
    return GraphicDatabase.Get<Graphic_Single>(directional,ShaderDatabase.CutoutSkin,Vector2.one,color);
   }
   var body=LoadBody(bodyType);
   if(body!=null||target.def!=ThingDefOf.Human)return body;
   return LoadBody(target.gender==Gender.Female?BodyTypeDefOf.Female:BodyTypeDefOf.Male);
  }

  float BubbleOpeningRadius(float scale){
   var size=splitBubbleBack.drawSize;
   return Mathf.Min(size.x,size.y)*.308f*scale;
  }

  void DrawBubbleTendrils(HediffComp_CoccyxCombat comp,Vector3 center,float opening,float bubbleScale){
   if(splitBubbleBrace==null)
    splitBubbleBrace=MaterialPool.MatFrom("Yuran/Implants/CoccyxTentacles/SplitMouth/BubbleBrace",ShaderDatabase.Cutout);
   if(splitBraceMesh==null)splitBraceMesh=MakeBraceMesh();
   float radius=BubbleOpeningRadius(bubbleScale);
   var shift=new Vector2(comp.splitBubbleTendrilX,comp.splitBubbleTendrilZ);
   float requested=comp.splitBubbleTendrilScale/HediffComp_CoccyxCombat.DefaultBubbleTendrilScale;
   // Draw larger than the opening and clip the excess without squeezing the art.
   float extent=radius*1.38f*requested*Mathf.Lerp(.85f,1f,opening)/.475f;
   float bottom=shift.y-extent*.5f;
   float from=Mathf.Max(-radius,bottom),to=Mathf.Min(radius,bottom+extent);
   if(to<=from)return;

   int now=Find.TickManager.TicksGame;
   int start=Hediff_CoccyxEscort.Get(comp.EscortTarget)?.liftStart??now;
   int elapsed=Mathf.Max(0,now-start);
   int tick=elapsed%BracePoseTicks[BracePoseTicks.Length-1];
   int next=1;
   while(tick>=BracePoseTicks[next])next++;
   float blend=Smooth((tick-BracePoseTicks[next-1])/(float)(BracePoseTicks[next]-BracePoseTicks[next-1]));
   var pose=Vector2.Lerp(BracePoses[next-1],BracePoses[next],blend);
   for(int row=0;row<=BraceRows;row++){
    float z=Mathf.Lerp(from,to,row/(float)BraceRows);
    float texV=(z-bottom)/extent;
    float v=texV-.5f;
    float free=Smooth((v+.26f)/.70f);
    float middle=Smooth((v+.36f)/.19f)*(1f-Smooth((v-.20f)/.27f));
    for(int col=0;col<=BraceColumns;col++){
     float x=col/(float)BraceColumns-.5f;
     float magnitude=Mathf.Abs(x);
     float inward=Smooth(magnitude/.16f)*(1f-Smooth((magnitude-.13f)/.27f));
     float flex=x<0?pose.x:pose.y;
     float curl=.086f*inward*middle;
     splitBraceRowX[col]=shift.x+(x-Mathf.Sign(x)*curl)*(1f+flex*free)*extent;
    }
    float half=Mathf.Sqrt(Mathf.Max(0,radius*radius-z*z));
    float leftU=BraceUvAt(-half),rightU=BraceUvAt(half);
    for(int col=0;col<=BraceColumns;col++){
     float x=splitBraceRowX[col];
     int vertex=row*(BraceColumns+1)+col;
     splitBraceVertices[vertex]=new Vector3(Mathf.Clamp(x,-half,half),0,z);
     float u=x<-half?leftU:x>half?rightU:col/(float)BraceColumns;
     float bend=u<.5f?pose.x:pose.y;
     float flexV=bend*.19f*free*Smooth(Mathf.Abs(u-.5f)*2f);
     splitBraceUvs[vertex]=new Vector2(u,Mathf.Clamp01(texV-flexV));
    }
   }
   splitBraceMesh.vertices=splitBraceVertices;splitBraceMesh.uv=splitBraceUvs;splitBraceMesh.RecalculateBounds();
   Graphics.DrawMesh(splitBraceMesh,center+Vector3.up*.0015f,Quaternion.identity,splitBubbleBrace,0);
  }

  float BraceUvAt(float x){
   if(x<=splitBraceRowX[0])return 0;
   for(int col=1;col<=BraceColumns;col++){
    if(x<=splitBraceRowX[col])
     return (col-1+Mathf.InverseLerp(splitBraceRowX[col-1],splitBraceRowX[col],x))/BraceColumns;
   }
   return 1;
  }

  Mesh MakeBraceMesh(){
   var triangles=new int[BraceColumns*BraceRows*6];
   int index=0;
   for(int row=0;row<BraceRows;row++)for(int col=0;col<BraceColumns;col++){
    int v=row*(BraceColumns+1)+col,above=v+BraceColumns+1;
    triangles[index++]=v;triangles[index++]=above;triangles[index++]=v+1;
    triangles[index++]=v+1;triangles[index++]=above;triangles[index++]=above+1;
   }
   var mesh=new Mesh();mesh.MarkDynamic();mesh.vertices=splitBraceVertices;mesh.triangles=triangles;
   return mesh;
  }

  internal void ReleaseSplitBubble()=>ReleaseSplitBodyFrames();
  void ReleaseSplitBodyFrames(){
   if(splitNativeBodyFrame!=null){UnityEngine.Object.Destroy(splitNativeBodyFrame);splitNativeBodyFrame=null;}
   if(splitBubbleMaterial!=null){UnityEngine.Object.Destroy(splitBubbleMaterial);splitBubbleMaterial=null;}
   splitBubblePawn=null;splitNativeBody=null;splitPreparedBodyNode=null;
  }
 }
}

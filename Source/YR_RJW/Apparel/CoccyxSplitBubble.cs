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
  const int WrapRows=20;
  readonly Mesh[] splitWrapMeshes=new Mesh[2];
  readonly Vector3[] wrapVertices=new Vector3[(WrapRows+1)*2];
  readonly Vector2[] wrapUvs=new Vector2[(WrapRows+1)*2];
  Material splitBubbleWrap;
  Pawn splitBubblePawn;
  Material splitBubbleMaterial;
  BedAnimationDef splitBodySource;
  readonly List<BedAnimationSetting> splitBodyFrames=new List<BedAnimationSetting>();
  readonly Dictionary<BedAnimationSetting,Texture2D> splitFrameTextures=new Dictionary<BedAnimationSetting,Texture2D>();
  string splitPreparedBody;
  Vector3 splitPreparedOffset;
  float splitPreparedScale;
  Color splitPreparedColor;
  Graphic splitNativeBody;
  Vector3 splitAuthoredOffset;

  void DrawSplitBubble(HediffComp_CoccyxCombat comp,float opening){
   var target=comp.EscortTarget;
   if(!comp.splitShowBubble||target?.Spawned!=true||!AdultEligibility.IsAllowed(target)){ReleaseSplitBubble();return;}
   // Read the already prepared body; do not initialize, capture or dirty the world pawn.
   target.Drawer.renderer.renderTree.TryGetNodeByTag(PawnRenderNodeTagDefOf.Body,out var node);
   if(node?.PrimaryGraphic==null)return;
   LoadFacilityBubble();
   var color=node.ColorFor(target);
   var offset=new Vector3(comp.splitBodyX,0,comp.splitBodyZ);
   var source=FacilityBodyCatalogV47.DetachedChoice(target,comp.splitBodyChoice,out var authoredOffset);
   if(splitBubblePawn!=target||splitPreparedBody!=comp.splitBodyChoice||splitBodySource!=source||
      splitPreparedOffset!=offset||splitPreparedScale!=comp.splitBodyZoom||splitPreparedColor!=color||
      splitNativeBody!=node.PrimaryGraphic||splitAuthoredOffset!=authoredOffset){
    ReleaseSplitBubble();splitBubblePawn=target;splitPreparedBody=comp.splitBodyChoice;
    splitPreparedOffset=offset;splitPreparedScale=comp.splitBodyZoom;splitPreparedColor=color;
    splitBodySource=source;splitNativeBody=node.PrimaryGraphic;splitAuthoredOffset=authoredOffset;
    if(source!=null)splitBodyFrames.AddRange(BubblePortraitV57.PrepareFrames(target,source,bodyColor:color));
    if(splitBodyFrames.Count==0)splitBodyFrames.Add(new BedAnimationSetting{graphic=null});
    var mask=ContentFinder<Texture2D>.Get(splitPortraitSetting.maskPath);
    var frameOffset=source==null?offset-splitPortraitSetting.cameraOffset:offset+authoredOffset+source.offset;
    // The saved default zoom represents the facility's original framing.
    float zoom=splitPortraitSetting.cameraZoom*comp.splitBodyZoom/1.4f;
    foreach(var frame in splitBodyFrames)
     splitFrameTextures[frame]=FacilityPortrait.FrameTexture(frame,frameOffset,zoom,mask,
      source==null||source.bodyOverlay||frame.graphic==null?splitNativeBody:null,splitPortraitSetting.rotation);
    splitBubbleMaterial=BedAnimationUtility.CreatePortraitMaterial(splitPortraitSetting);
   }
   int duration=splitBodySource?.autoDurationTicksSetting==true?60:splitBodySource?.durationTicks??60;
   int tick=comp.splitPreview>=0?Mathf.FloorToInt(comp.splitPreview*duration):Find.TickManager.TicksGame;
   var current=splitBodySource==null?splitBodyFrames[0]:BubblePortraitV57.FrameAtTick(splitBodyFrames,splitBodySource,tick);
   splitBubbleMaterial.mainTexture=splitFrameTextures[current];
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
   DrawBubbleWrap(comp,center,opening,scale);
   DrawBubbleArt(splitBubbleFront,center+Vector3.up*.002f);
  }

  void DrawBubbleWrap(HediffComp_CoccyxCombat comp,Vector3 center,float opening,float bubbleScale){
   if(opening<=.1f)return;
   if(splitBubbleWrap==null)splitBubbleWrap=CompSamokSleepCocoon.WrapMaterial();
   var size=splitBubbleBack.drawSize;
   // The facility's round opening occupies about 63% of the source square.
   float radius=Mathf.Min(size.x,size.y)*.308f*bubbleScale;
   float breath=CompSamokSleepCocoon.AnimatedScale(Find.TickManager.TicksGame,comp.Pawn.thingIDNumber);
   float scale=radius*breath*comp.splitBubbleTendrilScale*Mathf.Lerp(.72f,1f,opening);
   float width=.575f*scale,height=1.863f*scale;
   float bottom=center.z+comp.splitBubbleTendrilZ-.07f*scale-height*.5f;
   float from=Mathf.Max(bottom,center.z-radius);
   float to=Mathf.Min(bottom+height,center.z+radius);
   if(to<=from)return;
   for(int side=0;side<2;side++){
    float x=center.x+comp.splitBubbleTendrilX+(side==0?-.63f:.63f)*scale;
    float left=x-width*.5f,right=x+width*.5f;
    // Sample the original side curls; leave the central crossbars out of the portrait.
    float u0=side==0?.13f:.63f;
    for(int row=0;row<=WrapRows;row++){
     float z=Mathf.Lerp(from,to,row/(float)WrapRows);
     float dz=z-center.z;
     float half=Mathf.Sqrt(Mathf.Max(0,radius*radius-dz*dz));
     float x0=Mathf.Clamp(left,center.x-half,center.x+half);
     float x1=Mathf.Clamp(right,center.x-half,center.x+half);
     int v=row*2;
     float texV=Mathf.Lerp(.07f,.81f,(z-bottom)/height);
     wrapVertices[v]=new Vector3(x0,center.y+.0015f,z);
     wrapVertices[v+1]=new Vector3(x1,center.y+.0015f,z);
     wrapUvs[v]=new Vector2(u0+.24f*Mathf.Clamp01((x0-left)/width),texV);
     wrapUvs[v+1]=new Vector2(u0+.24f*Mathf.Clamp01((x1-left)/width),texV);
    }
    var mesh=splitWrapMeshes[side];
    if(mesh==null){
     var indices=new int[WrapRows*6];
     for(int row=0;row<WrapRows;row++){
      int v=row*2,k=row*6;
      indices[k]=v;indices[k+1]=v+2;indices[k+2]=v+1;
      indices[k+3]=v+1;indices[k+4]=v+2;indices[k+5]=v+3;
     }
     mesh=new Mesh();mesh.MarkDynamic();mesh.vertices=wrapVertices;mesh.triangles=indices;
     splitWrapMeshes[side]=mesh;
    }
    mesh.vertices=wrapVertices;mesh.uv=wrapUvs;mesh.RecalculateBounds();
    Graphics.DrawMesh(mesh,Vector3.zero,Quaternion.identity,splitBubbleWrap,0);
   }
  }

  internal void ReleaseSplitBubble(){
   foreach(var texture in splitFrameTextures.Values)UnityEngine.Object.Destroy(texture);
   splitFrameTextures.Clear();splitBodyFrames.Clear();
   if(splitBubbleMaterial!=null){UnityEngine.Object.Destroy(splitBubbleMaterial);splitBubbleMaterial=null;}
   splitBubblePawn=null;splitPreparedBody=null;splitBodySource=null;splitNativeBody=null;
  }
 }
}

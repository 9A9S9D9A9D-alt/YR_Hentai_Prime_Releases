using System;
using System.Linq;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using YR_Hentai_Prime_AnimationBed;
namespace YR_RJW {
 public class CompProperties_ServiceSacInterior : CompProperties {
  public CompProperties_ServiceSacInterior(){compClass=typeof(CompServiceSacInterior);}
 }
 // The cutaway renders a separate scene into the existing masked portrait target.
 public class CompServiceSacInterior : ThingComp,IAnimationPositionSource {
  [ThreadStatic] internal static CompServiceSacInterior Capturing;
  public bool showInterior;
  private PortraitIngredient portrait;
  private Graphic[] tendrils;
  private readonly List<BedAnimationSetting> bodyFrames=new List<BedAnimationSetting>();
  private BedAnimationDef bodySource;
  private string preparedChoice;
  private Material background,bed,waterMaterial,waterSurfaceMaterial,interiorBackdrop,residueMaterial;
  private Mesh liquidMesh,surfaceMesh;
  private Vector3[] liquidVertices,surfaceVertices;
  private Vector3 captureCamera;
  private float captureZoom,liquidLevel;
  private int currentFrame,sceneHash;
  private const string Bubble="Yuran/Things/Building/Furniture/GloryHole/Body/FellatioBackGround/";
  private CompAnimationPosition Adjust => parent.TryGetComp<CompAnimationPosition>();
  public Pawn PositionPawn => (parent as Building_ServiceSac)?.InteriorPawn;
  public IEnumerable<KeyValuePair<string,string>> PositionLayers {
   get {yield return new KeyValuePair<string,string>("interior","내부 말풍선 위치·크기");
    yield return new KeyValuePair<string,string>("interiorContent","말풍선 안쪽 장면");
    yield return new KeyValuePair<string,string>("interiorBed","내부 사목나무 침대");
    yield return new KeyValuePair<string,string>("interiorTentacles","내부 촉수 애니메이션");}
  }
  public override void PostExposeData(){base.PostExposeData();Scribe_Values.Look(ref showInterior,"serviceSacShowInterior");}
  public override IEnumerable<Gizmo> CompGetGizmosExtra(){
   yield return new Command_Toggle {defaultLabel="내부 보기",defaultDesc="사목나무 침대 바탕·대상·촉수 애니메이션을 말풍선 안에만 표시합니다.",
    icon=ContentFinder<Texture2D>.Get("Yuran/Things/Building/ServiceSac/Closed"),isActive=()=>showInterior,
    toggleAction=()=>{showInterior=!showInterior;if(!showInterior)Release();}};
   if(PositionPawn!=null)yield return new Command_Action {defaultLabel="내부 몸체 선택",defaultDesc="내부 영상에 표시할 기본 몸체 또는 종족별 몸통을 선택합니다. 실제 종족이나 체형은 바꾸지 않습니다.",
    icon=ContentFinder<Texture2D>.Get("UI/Icons/GDAV3/Up"),action=()=>{
     Pawn pawn=PositionPawn;if(pawn==null || Adjust==null)return;
     var list=new List<FloatMenuOption>{new FloatMenuOption("원래 몸체",()=>Adjust.SetBodyChoice(pawn,null))};
     foreach(var source in FacilityBodyCatalogV47.Choices()){var choice=source;list.Add(new FloatMenuOption(FacilityBodyCatalogV47.Label(choice.defName),()=>Adjust.SetBodyChoice(pawn,choice.defName)));}
     Find.WindowStack.Add(new FloatMenu(list));
    }};
  }
  private void Release(){
   if(liquidMesh!=null){UnityEngine.Object.Destroy(liquidMesh);liquidMesh=null;}
   if(surfaceMesh!=null){UnityEngine.Object.Destroy(surfaceMesh);surfaceMesh=null;}
   if(portrait!=null){FacilityPortrait.Release(portrait);portrait=null;}bodyFrames.Clear();preparedChoice=null;bodySource=null;}
  public override void PostDeSpawn(Map map,DestroyMode mode=DestroyMode.Vanish){Release();base.PostDeSpawn(map,mode);}
  public override void PostDestroy(DestroyMode mode,Map previousMap){Release();base.PostDestroy(mode,previousMap);}
  public override void PostDraw(){
   var b=parent as Building_ServiceSac;Pawn pawn=PositionPawn;
   if(!showInterior || b==null || !b.InteriorVisible || pawn==null){Release();return;}
   if(!parent.Spawned || parent.Position.Fogged(parent.Map))return;
   if(portrait?.pawn!=pawn){Release();Prepare(pawn);}
   if(portrait==null)return;
   string choiceName=Adjust?.BodyChoice(pawn);
   if(preparedChoice!=choiceName)PrepareBody(choiceName,pawn);
   currentFrame=(Find.TickManager.TicksGame/6)%tendrils.Length;
   Graphic body=null;
   if(bodyFrames.Count>0){
    int duration=Math.Max(bodySource.durationTicks,bodyFrames.Max(f=>f.tick)+1);
    int tick=Find.TickManager.TicksGame%Math.Max(1,duration);
    body=(bodyFrames.LastOrDefault(f=>f.tick<=tick)??bodyFrames[0]).graphic;
   }
   portrait.bubbleFrames[0].graphic=body;
   portrait.bubbleSource.bodyOverlay=bodySource?.bodyOverlay??false;
   Vector3 camera=new Vector3(0,0,-0.4f)+Offset("interiorContent");camera.y=0;
   captureCamera=camera;captureZoom=3.5f*Scale("interiorContent");liquidLevel=b.InteriorLiquidLevel;
   int hash=unchecked(currentFrame*31+Mathf.RoundToInt(liquidLevel*48f));
   foreach(string key in new[]{"interiorContent","interiorBed","body","interiorTentacles"}){
    hash=unchecked(hash*31+Offset(key).GetHashCode());hash=unchecked(hash*31+Scale(key).GetHashCode());
   }
   if(hash!=sceneHash){portrait.lastBubbleZoom=-1;sceneHash=hash;}
   Texture texture;var previous=Capturing;
   try{Capturing=this;texture=FacilityPortrait.GetTexture(portrait,camera,captureZoom,Rot4.South);}
   finally{Capturing=previous;}
   if(texture==null)return;
   portrait.iconMat.mainTexture=texture;
   Vector3 loc=parent.DrawPos+new Vector3(1.9f,2f,0.6f)+Offset("all")+Offset("interior");
   float scale=1.6f*(Adjust?.RenderScale(pawn,"interior")??1f);
   var matrix=Matrix4x4.TRS(loc,Quaternion.identity,new Vector3(scale,1f,scale));
   Graphics.DrawMesh(MeshPool.plane10,matrix,background,0);matrix.m13+=0.01f;
   Graphics.DrawMesh(MeshPool.plane10,matrix,portrait.iconMat,0);
  }
  private Vector3 Offset(string key)=>Adjust?.Offset(PositionPawn,key)??Vector3.zero;
  private float Scale(string key)=>Adjust?.DisplayScale(PositionPawn,key)??1f;
  internal void DrawBed(){
   DrawScenePlane(interiorBackdrop,captureCamera,2f/captureZoom,-0.2f);
   DrawScenePlane(bed,Offset("interiorBed"),2.25f*Scale("interiorBed"),-0.1f);
  }
  internal void DrawTentacles(){
   DrawScenePlane(tendrils[currentFrame].MatSingle,Offset("interiorTentacles"),2.25f*Scale("interiorTentacles"),0.5f);
   var sac=parent as Building_ServiceSac;
   if(sac?.HasInteriorResidue==true){
    if(residueMaterial==null)residueMaterial=MaterialPool.MatFrom("Yuran/Things/Building/ServiceSac/FluidOverlay",ShaderDatabase.Cutout);
    var offset=Offset("interiorTentacles")+new Vector3(0,0,-.12f);offset.y=.65f;
    float scale=.8f*Scale("interiorTentacles");
    GenDraw.DrawMeshNowOrLater(ServiceSacFluidMeshes.ForStage(sac.InteriorResidueStage),Matrix4x4.TRS(offset,Quaternion.identity,new Vector3(scale,1,scale)),residueMaterial,true);
   }
   DrawLiquid();
  }
  private void DrawLiquid(){
   if(liquidLevel<=0f)return;
   const int segments=8;
   if(liquidMesh==null){
    liquidVertices=new Vector3[(segments+1)*2];surfaceVertices=new Vector3[liquidVertices.Length];
    var triangles=new int[segments*6];var uv=new Vector2[liquidVertices.Length];
    for(int i=0;i<=segments;i++){uv[i*2]=new Vector2(i/(float)segments,0);uv[i*2+1]=new Vector2(i/(float)segments,1);}
    for(int i=0;i<segments;i++){int n=i*2,t=i*6;triangles[t]=n;triangles[t+1]=n+1;triangles[t+2]=n+3;triangles[t+3]=n;triangles[t+4]=n+3;triangles[t+5]=n+2;}
    liquidMesh=new Mesh{name="ServiceSacInteriorLiquid"};liquidMesh.vertices=liquidVertices;liquidMesh.uv=uv;liquidMesh.triangles=triangles;
    surfaceMesh=new Mesh{name="ServiceSacInteriorLiquidSurface"};surfaceMesh.vertices=surfaceVertices;surfaceMesh.uv=uv;surfaceMesh.triangles=triangles;
   }
   float half=1f/captureZoom,bottom=captureCamera.z-half;
   float level=bottom+2f*half*liquidLevel;
   for(int i=0;i<=segments;i++){
    float x=captureCamera.x-half+2f*half*i/segments;
    float z=level+half*0.025f*Mathf.Sin(i*1.3f+currentFrame*Mathf.PI/3f);
    liquidVertices[i*2]=new Vector3(x,0,bottom);liquidVertices[i*2+1]=new Vector3(x,0,z);
    surfaceVertices[i*2]=new Vector3(x,0,z-half*0.025f);surfaceVertices[i*2+1]=new Vector3(x,0,z);
   }
   liquidMesh.vertices=liquidVertices;liquidMesh.RecalculateBounds();liquidMesh.RecalculateNormals();surfaceMesh.vertices=surfaceVertices;surfaceMesh.RecalculateBounds();surfaceMesh.RecalculateNormals();
   GenDraw.DrawMeshNowOrLater(liquidMesh,Matrix4x4.Translate(new Vector3(0,0.7f,0)),waterMaterial,true);
   GenDraw.DrawMeshNowOrLater(surfaceMesh,Matrix4x4.Translate(new Vector3(0,0.71f,0)),waterSurfaceMaterial,true);
  }
  private static void DrawScenePlane(Material mat,Vector3 offset,float scale,float depth){
   offset.y=depth;GenDraw.DrawMeshNowOrLater(MeshPool.plane10,Matrix4x4.TRS(offset,Quaternion.identity,new Vector3(scale,1,scale)),mat,true);
  }
  internal Matrix4x4 BodyTransform(){
   var offset=Offset("body");offset.y=0;offset.z+=0.004f*Mathf.Sin(currentFrame*Mathf.PI/3f);
   float s=Scale("body");return Matrix4x4.TRS(offset,Quaternion.identity,new Vector3(s,1,s));
  }
  private void Prepare(Pawn pawn){
   if(tendrils==null){
    tendrils=new Graphic[6];
    for(int i=0;i<tendrils.Length;i++)tendrils[i]=GraphicDatabase.Get<Graphic_Single>(
      "Yuran/Things/Building/Furniture/Tentacle/Front/"+(i+1),ShaderDatabase.Cutout,Vector2.one,Color.white);
   }
   if(waterMaterial==null){
    waterMaterial=MaterialPool.MatFrom(new MaterialRequest{mainTex=BaseContent.WhiteTex,shader=ShaderDatabase.Transparent,color=new Color(1f,0.98f,0.91f,0.35f)});
    waterSurfaceMaterial=MaterialPool.MatFrom(new MaterialRequest{mainTex=BaseContent.WhiteTex,shader=ShaderDatabase.Transparent,color=new Color(1f,1f,0.97f,0.65f)});
    interiorBackdrop=MaterialPool.MatFrom(new MaterialRequest{mainTex=BaseContent.WhiteTex,shader=ShaderDatabase.Cutout,color=new Color(0.12f,0.08f,0.17f,1f)});
   }
   if(background==null)background=MaterialPool.MatFrom(Bubble+"BackGround",ShaderDatabase.Cutout);
   if(bed==null)bed=MaterialPool.MatFrom("Yuran/Things/Building/Furniture/Tentacle/Bed/Texture_south",ShaderDatabase.Cutout);
   portrait=new PortraitIngredient {pawn=pawn,slot=0,
    portraitSetting=new PortraitSetting {maskPath=Bubble+"Mask",rotation=Rot4.South,renderClothes=false,renderHeadgear=false,pawnRenderNodeTagDef=PawnRenderNodeTagDefOf.Body},
    bubbleSource=new BedAnimationDef {durationTicks=36}};
   // A null replacement keeps the native body, but still uses the scoped fresh target.
   portrait.bubbleFrames.Add(new BedAnimationSetting {tick=0,graphic=null});
   portrait.iconMat=new Material(MaterialPool.MatFrom(new MaterialRequest {
    mainTex=Texture2D.blackTexture,color=Color.white,shader=ShaderDatabase.CutoutComplex,
    maskTex=ContentFinder<Texture2D>.Get(Bubble+"Mask")}));
   sceneHash=int.MinValue;
  }
  private void PrepareBody(string name,Pawn pawn){
   preparedChoice=name;bodyFrames.Clear();bodySource=DefDatabase<BedAnimationDef>.GetNamedSilentFail(name??"");
   if(bodySource==null || !FacilityBodyCatalogV47.Choices().Contains(bodySource))return;
   foreach(var f in bodySource.bedAnimationSettings.OrderBy(f=>f.tick)){
    if(!YR_Hentai_Prime_AnimationBed.Condition.Match(pawn,null,f.condition,out _))continue;
    var data=f.graphicData;
    foreach(var choice in f.conditionGraphicDatas)
     if(YR_Hentai_Prime_AnimationBed.Condition.Match(pawn,null,choice.condition,out bool stop)){data=choice.graphicData;if(stop)break;}
    if(data==null || ContentFinder<Texture2D>.Get(data.texPath,false)==null)continue;
    var node=bodySource.setPawnColor?FacilityRenderNodes.Find(pawn,PawnRenderNodeTagDefOf.Body):null;
    var color=bodySource.setPawnColor?(node?.ColorFor(pawn)??Color.white):data.color;
    var frame=f.Copy();frame.graphic=GraphicDatabase.Get<Graphic_Single>(data.texPath,data.shaderType?.Shader??ShaderDatabase.Cutout,data.drawSize,color,data.colorTwo,data,data.maskPath);
    bodyFrames.Add(frame);
   }
   portrait.lastBubbleZoom=-1;
  }
 }
 // These callbacks draw only during this component's private portrait capture.
 [HarmonyPatch(typeof(PawnCacheRenderer),nameof(PawnCacheRenderer.OnPostRender))]
 internal static class ServiceSacCaptureLayers {
  static void Prefix(){CompServiceSacInterior.Capturing?.DrawBed();}
  static void Postfix(){CompServiceSacInterior.Capturing?.DrawTentacles();}
 }
 [HarmonyPatch(typeof(PawnRenderTree),nameof(PawnRenderTree.Draw))]
 internal static class ServiceSacCaptureBody {
  private static readonly AccessTools.FieldRef<PawnRenderTree,List<PawnGraphicDrawRequest>> Requests=AccessTools.FieldRefAccess<PawnRenderTree,List<PawnGraphicDrawRequest>>("drawRequests");
  static void Prefix(PawnRenderTree __instance,PawnDrawParms parms,out Matrix4x4[] __state){
   __state=null;var capture=CompServiceSacInterior.Capturing;
   if(capture==null || !parms.Portrait || parms.pawn!=capture.PositionPawn)return;
   var transform=capture.BodyTransform();var list=Requests(__instance);
   __state=new Matrix4x4[list.Count];
   for(int i=0;i<list.Count;i++){var r=list[i];__state[i]=r.preDrawnComputedMatrix;r.preDrawnComputedMatrix=transform*r.preDrawnComputedMatrix;list[i]=r;}
  }
  // Restore even on a failed capture. Never compound transforms in native cached requests.
  static void Finalizer(PawnRenderTree __instance,Matrix4x4[] __state){
   if(__state==null)return;var list=Requests(__instance);
   for(int i=0;i<Math.Min(list.Count,__state.Length);i++){var r=list[i];r.preDrawnComputedMatrix=__state[i];list[i]=r;}
  }
 }
}

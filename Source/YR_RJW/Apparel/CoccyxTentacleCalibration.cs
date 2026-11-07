using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
namespace YR_RJW {
 public sealed partial class HediffComp_CoccyxCombat {
  public List<float> offsetX=new List<float>{0,0,0,0},offsetZ=new List<float>{0,0,0,0};
  public float visualScale=1,visualWidth=1,poseSpread=1,poseHeight=1,curveFactor=1,easeFactor=1;
  void ExposeCalibration(){Scribe_Collections.Look(ref offsetX,"coccyxOffsetX",LookMode.Value);Scribe_Collections.Look(ref offsetZ,"coccyxOffsetZ",LookMode.Value);Scribe_Values.Look(ref visualScale,"coccyxVisualScale",1);Scribe_Values.Look(ref visualWidth,"coccyxVisualWidth",1);Scribe_Values.Look(ref poseSpread,"coccyxPoseSpread",1);Scribe_Values.Look(ref poseHeight,"coccyxPoseHeight",1);Scribe_Values.Look(ref curveFactor,"coccyxCurveFactor",1);Scribe_Values.Look(ref easeFactor,"coccyxEaseFactor",1);if(Scribe.mode==LoadSaveMode.PostLoadInit){if(offsetX==null||offsetX.Count!=4)offsetX=new List<float>{0,0,0,0};if(offsetZ==null||offsetZ.Count!=4)offsetZ=new List<float>{0,0,0,0};}}
  Gizmo CalibrationCommand()=>new Command_Action{defaultLabel="애니메이션 위치 조정",defaultDesc="방향별 부착 위치와 외형·공격 곡선을 보정합니다. 피해량·사거리·타격 시점에는 영향을 주지 않습니다.",icon=ContentFinder<Texture2D>.Get("UI/Icons/GDAV3/Up"),action=()=>Find.WindowStack.Add(new CoccyxCalibrationWindow(this))};
 }
 public sealed class CoccyxCalibrationWindow:YR_Hentai_Prime_AnimationBed.DialogAnimationPosition {
  public CoccyxCalibrationWindow(HediffComp_CoccyxCombat c):base(new CoccyxPositionAdapter(c),"2"){}
 }
 public sealed class CoccyxPositionAdapter:YR_Hentai_Prime_AnimationBed.CompAnimationPosition {
  readonly HediffComp_CoccyxCombat comp;
  public CoccyxPositionAdapter(HediffComp_CoccyxCombat c){comp=c;}
  public override Pawn Pawn=>comp.Pawn;
  public override bool SupportsBodyChoice=>false;
  public override List<KeyValuePair<string,string>> Layers()=>new List<KeyValuePair<string,string>>{
   new KeyValuePair<string,string>("0","북쪽 부착 위치"),new KeyValuePair<string,string>("1","동쪽 부착 위치"),new KeyValuePair<string,string>("2","남쪽 부착 위치"),new KeyValuePair<string,string>("3","서쪽 부착 위치"),
   new KeyValuePair<string,string>("width","촉수 굵기"),new KeyValuePair<string,string>("spread","자세 펼침"),new KeyValuePair<string,string>("height","자세 높이"),new KeyValuePair<string,string>("curve","공격 휘어짐"),new KeyValuePair<string,string>("ease","신축 완급")};
  static int Facing(string layer)=>int.TryParse(layer,out int n)?n:2;
  public override Vector3 Offset(Pawn pawn,string layer){int i=Facing(layer);return new Vector3(comp.offsetX[i],0,comp.offsetZ[i]);}
  public override float DisplayScale(Pawn pawn,string layer)=>layer=="width"?comp.visualWidth:layer=="spread"?comp.poseSpread:layer=="height"?comp.poseHeight:layer=="curve"?comp.curveFactor:layer=="ease"?comp.easeFactor:comp.visualScale;
  public override void Move(Pawn pawn,string layer,Vector3 delta,bool reset=false){
   if(layer.StartsWith("scale:")){
    string key=layer.Substring(6);float value=reset?1:Mathf.Clamp(DisplayScale(pawn,key)+delta.x,.2f,3f);
    if(key=="width")comp.visualWidth=value;else if(key=="spread")comp.poseSpread=value;else if(key=="height")comp.poseHeight=value;else if(key=="curve")comp.curveFactor=value;else if(key=="ease")comp.easeFactor=value;else comp.visualScale=value;
   }else{int i=Facing(layer);comp.offsetX[i]=reset?0:comp.offsetX[i]+delta.x;comp.offsetZ[i]=reset?0:comp.offsetZ[i]+delta.z;}
   pawn.Drawer.renderer.SetAllGraphicsDirty();
  }
 }
}

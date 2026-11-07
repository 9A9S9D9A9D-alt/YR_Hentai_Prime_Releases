using System.Collections.Generic;
using RimWorld;
using YR_Hentai_Prime_AnimationBed;
using UnityEngine;
using Verse;

namespace YR_RJW {
 public sealed partial class HediffComp_CoccyxCombat {
  public string splitBodyChoice;
  public int splitDuration=72;
  public float splitOuterWidth=1,splitOuterHeight=1;
  public float splitOuterX,splitOuterZ;
  public float splitOuterStart=.28f;
  public float splitRed=1,splitGreen=1,splitBlue=1;
  public bool splitShowBubble=true;
  public float splitBubbleX=1.5f,splitBubbleZ=.6f,splitBubbleScale=1,splitBodyX,splitBodyZ,splitBodyZoom=1.4f;
  public float splitBubbleTendrilX,splitBubbleTendrilZ,splitBubbleTendrilScale=1;
  public float splitPreview=-1;
  public Color SplitColor=>new Color(splitRed,splitGreen,splitBlue);
  public int EscortDuration=>splitMouthEscort?splitDuration:24;
  void ExposeSplitAnimation(){
   Scribe_Values.Look(ref splitBodyChoice,"splitBodyChoice");
   Scribe_Values.Look(ref splitOuterStart,"splitOuterStart",.28f);
   Scribe_Values.Look(ref splitShowBubble,"splitShowBubble",true);
   Scribe_Values.Look(ref splitBubbleX,"splitBubbleX",1.5f);Scribe_Values.Look(ref splitBubbleZ,"splitBubbleZ",.6f);Scribe_Values.Look(ref splitBubbleScale,"splitBubbleScale",1);
   Scribe_Values.Look(ref splitBodyX,"splitBodyX");Scribe_Values.Look(ref splitBodyZ,"splitBodyZ");Scribe_Values.Look(ref splitBodyZoom,"splitBodyZoom",1.4f);
   Scribe_Values.Look(ref splitBubbleTendrilX,"splitBubbleTendrilX");
   Scribe_Values.Look(ref splitBubbleTendrilZ,"splitBubbleTendrilZ");
   Scribe_Values.Look(ref splitBubbleTendrilScale,"splitBubbleTendrilScale",1);
   Scribe_Values.Look(ref splitDuration,"splitDuration",72);
   Scribe_Values.Look(ref splitOuterWidth,"splitOuterWidth",1);Scribe_Values.Look(ref splitOuterHeight,"splitOuterHeight",1);
   Scribe_Values.Look(ref splitOuterX,"splitOuterX");Scribe_Values.Look(ref splitOuterZ,"splitOuterZ");
   Scribe_Values.Look(ref splitRed,"splitRed",1);Scribe_Values.Look(ref splitGreen,"splitGreen",1);Scribe_Values.Look(ref splitBlue,"splitBlue",1);
  }
  Gizmo SplitAnimationCommand()=>new Command_Action{
   defaultLabel="자진입대 위치 조정",defaultDesc="기존 시설의 위치 조정 창에서 몸체·말풍선·외피를 이동하고 배율을 조절합니다. 선택값은 저장됩니다.",
   icon=ContentFinder<Texture2D>.Get("UI/Icons/GDAV3/Up"),action=()=>Find.WindowStack.Add(new SplitAnimationEditor(this))
  };
 }

 public sealed class SplitAnimationEditor:DialogAnimationPosition {
  readonly HediffComp_CoccyxCombat escort;
  public SplitAnimationEditor(HediffComp_CoccyxCombat comp):base(new SplitPositionAdapter(comp),"body"){
   escort=comp;
   preventCameraMotion=false;
  }
  public override void DoWindowContents(Rect rect){
   if(!escort.HasEscortGrip){Close(false);return;}
   base.DoWindowContents(rect);
  }
  protected override void SetInitialSizeAndPosition(){base.SetInitialSizeAndPosition();windowRect.x=16;windowRect.y=250;}
 }
 public sealed class SplitPositionAdapter:CompAnimationPosition {
  readonly HediffComp_CoccyxCombat comp;
  public SplitPositionAdapter(HediffComp_CoccyxCombat value){comp=value;}
  public override Pawn Pawn=>comp.EscortTarget??comp.Pawn;
  public override bool SupportsBodyChoice=>true;
  public override string BodyChoice(Pawn pawn)=>comp.splitBodyChoice;
  public override void SetBodyChoice(Pawn pawn,string name){comp.splitBodyChoice=name;}
  public override List<KeyValuePair<string,string>> Layers()=>new List<KeyValuePair<string,string>>{
   new KeyValuePair<string,string>("body","말풍선 안쪽 몸체"),
   new KeyValuePair<string,string>("bubble","말풍선 위치·크기"),
   new KeyValuePair<string,string>("shell","촉수 외피"),
   new KeyValuePair<string,string>("bubbleTendrils","말풍선 안쪽 촉수")
  };
  public override Vector3 Offset(Pawn pawn,string layer)=>layer=="bubbleTendrils"?new Vector3(comp.splitBubbleTendrilX,0,comp.splitBubbleTendrilZ):layer=="bubble"?new Vector3(comp.splitBubbleX,0,comp.splitBubbleZ):layer=="shell"?new Vector3(comp.splitOuterX,0,comp.splitOuterZ):new Vector3(comp.splitBodyX,0,comp.splitBodyZ);
  public override float DisplayScale(Pawn pawn,string layer)=>layer=="bubbleTendrils"?comp.splitBubbleTendrilScale:layer=="bubble"?comp.splitBubbleScale:layer=="shell"?comp.splitOuterWidth:comp.splitBodyZoom;
  public override void Move(Pawn pawn,string layer,Vector3 delta,bool reset=false){
   if(layer.StartsWith("scale:")){
    string key=layer.Substring(6);
    float value=reset?1:Mathf.Clamp(DisplayScale(pawn,key)+delta.x,.2f,3f);
    if(key=="bubble")comp.splitBubbleScale=value;
    else if(key=="bubbleTendrils")comp.splitBubbleTendrilScale=value;
    else if(key=="shell"){comp.splitOuterWidth=value;comp.splitOuterHeight=value;}
    else comp.splitBodyZoom=value;
    return;
   }
   var position=reset?Vector3.zero:Offset(pawn,layer)+delta;
   if(layer=="bubble"){comp.splitBubbleX=position.x;comp.splitBubbleZ=position.z;}
   else if(layer=="bubbleTendrils"){comp.splitBubbleTendrilX=position.x;comp.splitBubbleTendrilZ=position.z;}
   else if(layer=="shell"){comp.splitOuterX=position.x;comp.splitOuterZ=position.z;}
   else{comp.splitBodyX=position.x;comp.splitBodyZ=position.z;}
  }
 }
}

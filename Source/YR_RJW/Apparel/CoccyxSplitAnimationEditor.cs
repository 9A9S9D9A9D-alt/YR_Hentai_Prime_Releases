using System.Collections.Generic;
using RimWorld;
using YR_Hentai_Prime_AnimationBed;
using UnityEngine;
using Verse;

namespace YR_RJW {
 public sealed partial class HediffComp_CoccyxCombat {
  public string splitBodyChoice;
  public int splitBodyFacing;
  public int splitDuration=72;
  public float splitOuterWidth=1,splitOuterHeight=1;
  public float splitOuterX,splitOuterZ;
  public float splitOuterStart=.28f;
  public float splitRed=1,splitGreen=1,splitBlue=1;
  public bool splitShowBubble=true;
  public float splitBubbleX=1.5f,splitBubbleZ=.6f,splitBubbleScale=1,splitBodyX,splitBodyZ,splitBodyZoom=1.4f;
  public const float DefaultBubbleTendrilScale=1.20f;
  public float splitBubbleTendrilX,splitBubbleTendrilZ,splitBubbleTendrilScale=DefaultBubbleTendrilScale;
  public float splitHeadX,splitHeadZ,splitLegX,splitLegZ;
  public float splitPreview=-1;
  string splitTargetKey;
  Dictionary<string,SplitTargetSettings> splitTargetSettings=new Dictionary<string,SplitTargetSettings>();
  List<string> splitTargetKeys;
  List<SplitTargetSettings> splitTargetValues;

  static string SplitTargetKey(Pawn target)=>target.def.defName+"/"+(target.genes?.Xenotype?.defName??"none")+
   "/"+(target.story?.bodyType?.defName??"none")+"/"+target.gender;
  void SelectSplitTargetSettings(Pawn target){
   string key=SplitTargetKey(target);
   if(key==splitTargetKey)return;
   if(splitTargetKey!=null)splitTargetSettings[splitTargetKey]=new SplitTargetSettings{
    choice=splitBodyChoice,facing=splitBodyFacing,
    body=new Vector3(splitBodyX,splitBodyZoom,splitBodyZ),
    bubble=new Vector3(splitBubbleX,splitBubbleScale,splitBubbleZ),
    shell=new Vector3(splitOuterX,splitOuterWidth,splitOuterZ),shellHeight=splitOuterHeight,
    tendrils=new Vector3(splitBubbleTendrilX,splitBubbleTendrilScale,splitBubbleTendrilZ),
    head=new Vector2(splitHeadX,splitHeadZ),legs=new Vector2(splitLegX,splitLegZ)
   };
   if(!splitTargetSettings.TryGetValue(key,out var profile))profile=new SplitTargetSettings();
   splitTargetKey=key;
   splitBodyChoice=profile.choice;splitBodyFacing=profile.facing;
   splitBodyX=profile.body.x;splitBodyZoom=profile.body.y;splitBodyZ=profile.body.z;
   splitBubbleX=profile.bubble.x;splitBubbleScale=profile.bubble.y;splitBubbleZ=profile.bubble.z;
   splitOuterX=profile.shell.x;splitOuterWidth=profile.shell.y;splitOuterZ=profile.shell.z;splitOuterHeight=profile.shellHeight;
   splitBubbleTendrilX=profile.tendrils.x;splitBubbleTendrilScale=profile.tendrils.y;splitBubbleTendrilZ=profile.tendrils.z;
   splitHeadX=profile.head.x;splitHeadZ=profile.head.y;splitLegX=profile.legs.x;splitLegZ=profile.legs.y;
  }

  public sealed class SplitTargetSettings:IExposable {
   public string choice;
   public int facing;
   public Vector3 body=new Vector3(0,1.4f,0),bubble=new Vector3(1.5f,1f,.6f);
   public Vector3 shell=new Vector3(0,1f,0),tendrils=new Vector3(0,DefaultBubbleTendrilScale,0);
   public float shellHeight=1;
   public Vector2 head,legs;
   public void ExposeData(){
    Scribe_Values.Look(ref choice,"choice");Scribe_Values.Look(ref facing,"facing");
    Scribe_Values.Look(ref body,"body",new Vector3(0,1.4f,0));
    Scribe_Values.Look(ref bubble,"bubble",new Vector3(1.5f,1f,.6f));
    Scribe_Values.Look(ref shell,"shell",new Vector3(0,1f,0));
    Scribe_Values.Look(ref tendrils,"tendrils",new Vector3(0,DefaultBubbleTendrilScale,0));
    Scribe_Values.Look(ref shellHeight,"shellHeight",1);
    Scribe_Values.Look(ref head,"head");Scribe_Values.Look(ref legs,"legs");
   }
  }
  public Color SplitColor=>new Color(splitRed,splitGreen,splitBlue);
  public int EscortDuration=>splitMouthEscort?splitDuration:24;
  void ExposeSplitAnimation(){
   Scribe_Values.Look(ref splitTargetKey,"splitTargetKey");
   Scribe_Collections.Look(ref splitTargetSettings,"splitTargetSettings",LookMode.Value,LookMode.Deep,ref splitTargetKeys,ref splitTargetValues);
   if(Scribe.mode==LoadSaveMode.PostLoadInit)splitTargetSettings??=new Dictionary<string,SplitTargetSettings>();
   Scribe_Values.Look(ref splitBodyChoice,"splitBodyChoice");
   Scribe_Values.Look(ref splitBodyFacing,"splitBodyFacing");
   Scribe_Values.Look(ref splitOuterStart,"splitOuterStart",.28f);
   Scribe_Values.Look(ref splitShowBubble,"splitShowBubble",true);
   Scribe_Values.Look(ref splitBubbleX,"splitBubbleX",1.5f);Scribe_Values.Look(ref splitBubbleZ,"splitBubbleZ",.6f);Scribe_Values.Look(ref splitBubbleScale,"splitBubbleScale",1);
   Scribe_Values.Look(ref splitBodyX,"splitBodyX");Scribe_Values.Look(ref splitBodyZ,"splitBodyZ");Scribe_Values.Look(ref splitBodyZoom,"splitBodyZoom",1.4f);
   Scribe_Values.Look(ref splitBubbleTendrilX,"splitBubbleTendrilX");
   Scribe_Values.Look(ref splitBubbleTendrilZ,"splitBubbleTendrilZ");
   Scribe_Values.Look(ref splitBubbleTendrilScale,"splitBubbleTendrilScale",DefaultBubbleTendrilScale);
   Scribe_Values.Look(ref splitHeadX,"splitHeadX");
   Scribe_Values.Look(ref splitHeadZ,"splitHeadZ");
   Scribe_Values.Look(ref splitLegX,"splitLegX");
   Scribe_Values.Look(ref splitLegZ,"splitLegZ");
   Scribe_Values.Look(ref splitDuration,"splitDuration",72);
   Scribe_Values.Look(ref splitOuterWidth,"splitOuterWidth",1);Scribe_Values.Look(ref splitOuterHeight,"splitOuterHeight",1);
   Scribe_Values.Look(ref splitOuterX,"splitOuterX");Scribe_Values.Look(ref splitOuterZ,"splitOuterZ");
   Scribe_Values.Look(ref splitRed,"splitRed",1);Scribe_Values.Look(ref splitGreen,"splitGreen",1);Scribe_Values.Look(ref splitBlue,"splitBlue",1);
  }
  Gizmo SplitAnimationCommand()=>new Command_Action{
   defaultLabel="포획 위치 조정",defaultDesc="기존 시설의 위치 조정 창에서 몸체·말풍선·외피를 이동하고 배율을 조절합니다. 선택값은 저장됩니다.",
   icon=ContentFinder<Texture2D>.Get("UI/Icons/GDAV3/Up"),action=()=>Find.WindowStack.Add(new SplitAnimationEditor(this))
  };
 }

 public sealed class SplitAnimationEditor:DialogAnimationPosition {
  readonly HediffComp_CoccyxCombat escort;
  public SplitAnimationEditor(HediffComp_CoccyxCombat comp):base(new SplitPositionAdapter(comp),"legs"){
   escort=comp;
   preventCameraMotion=false;
  }
  public override void DoWindowContents(Rect rect){
   if(!escort.HasEscortGrip){Close(false);return;}
   base.DoWindowContents(rect);
   string facing=escort.splitBodyFacing==1?"앞":escort.splitBodyFacing==2?"뒤":"자동";
   if(Widgets.ButtonText(new Rect(0,378,330,28),"몸통 방향: "+facing))
    escort.splitBodyFacing=(escort.splitBodyFacing+1)%3;
  }
  protected override void SetInitialSizeAndPosition(){base.SetInitialSizeAndPosition();windowRect.x=16;windowRect.y=250;}
 }
 public sealed class SplitPositionAdapter:CompAnimationPosition {
  readonly HediffComp_CoccyxCombat comp;
  public SplitPositionAdapter(HediffComp_CoccyxCombat value){comp=value;}
  public override Pawn Pawn=>comp.EscortTarget??comp.Pawn;
  public override bool SupportsBodyChoice=>false;
  public override string BodyChoice(Pawn pawn)=>comp.splitBodyChoice;
  public override void SetBodyChoice(Pawn pawn,string name){comp.splitBodyChoice=name;}
  public override List<KeyValuePair<string,string>> Layers()=>new List<KeyValuePair<string,string>>{
   new KeyValuePair<string,string>("legs","말풍선 안쪽 몸체"),
   new KeyValuePair<string,string>("bubble","말풍선 위치·크기"),
   new KeyValuePair<string,string>("shell","촉수 외피"),
   new KeyValuePair<string,string>("bubbleTendrils","말풍선 안쪽 촉수")
  };
  public override Vector3 Offset(Pawn pawn,string layer)=>layer=="legs"?new Vector3(comp.splitLegX,0,comp.splitLegZ):layer=="head"?new Vector3(comp.splitHeadX,0,comp.splitHeadZ):layer=="bubbleTendrils"?new Vector3(comp.splitBubbleTendrilX,0,comp.splitBubbleTendrilZ):layer=="bubble"?new Vector3(comp.splitBubbleX,0,comp.splitBubbleZ):layer=="shell"?new Vector3(comp.splitOuterX,0,comp.splitOuterZ):new Vector3(comp.splitBodyX,0,comp.splitBodyZ);
  public override float DisplayScale(Pawn pawn,string layer)=>layer=="bubbleTendrils"?comp.splitBubbleTendrilScale:layer=="bubble"?comp.splitBubbleScale:layer=="shell"?comp.splitOuterWidth:comp.splitBodyZoom;
  public override void Move(Pawn pawn,string layer,Vector3 delta,bool reset=false){
   if(layer.StartsWith("scale:")){
    string key=layer.Substring(6);
    float defaultScale=key=="bubbleTendrils"?HediffComp_CoccyxCombat.DefaultBubbleTendrilScale:key=="legs"?1.4f:1f;
    float value=reset?defaultScale:Mathf.Clamp(DisplayScale(pawn,key)+delta.x,.2f,3f);
    if(key=="bubble")comp.splitBubbleScale=value;
    else if(key=="bubbleTendrils")comp.splitBubbleTendrilScale=value;
    else if(key=="shell"){comp.splitOuterWidth=value;comp.splitOuterHeight=value;}
    else comp.splitBodyZoom=value;
    return;
   }
   var position=reset?Vector3.zero:Offset(pawn,layer)+delta;
   if(layer=="bubble"){comp.splitBubbleX=position.x;comp.splitBubbleZ=position.z;}
   else if(layer=="head"){comp.splitHeadX=position.x;comp.splitHeadZ=position.z;}
   else if(layer=="legs"){comp.splitLegX=position.x;comp.splitLegZ=position.z;}
   else if(layer=="bubbleTendrils"){comp.splitBubbleTendrilX=position.x;comp.splitBubbleTendrilZ=position.z;}
   else if(layer=="shell"){comp.splitOuterX=position.x;comp.splitOuterZ=position.z;}
   else{comp.splitBodyX=position.x;comp.splitBodyZ=position.z;}
  }
 }
}

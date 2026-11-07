using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace YR_Hentai_Prime_AnimationBed
{
    public interface IAnimationPositionSource
    {
        Pawn PositionPawn { get; }
        IEnumerable<KeyValuePair<string,string>> PositionLayers { get; }
    }
    [StaticConstructorOnStartup]
    public static class AnimationPositionSetup
    {
        static AnimationPositionSetup()
        {
            foreach(var def in DefDatabase<ThingDef>.AllDefs)
                if(def.comps != null && def.comps.Any(c=>c.compClass!=null && typeof(IAnimationPositionSource).IsAssignableFrom(c.compClass)) &&
                    !def.comps.Any(c=>c.compClass==typeof(CompAnimationPosition)))
                    def.comps.Add(new CompProperties { compClass=typeof(CompAnimationPosition) });
        }
    }
    public class CompAnimationPosition : ThingComp
    {
        // Published dictionaries are never mutated while draw workers may read them.
        private Dictionary<string,Vector3> offsets=new Dictionary<string,Vector3>();
        private Dictionary<string,string> bodyChoices=new Dictionary<string,string>();
        private Dictionary<string,string> bubbleChoices=new Dictionary<string,string>();
        private bool sleepNativePositionV63;
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if(parent.def.defName!="YR_SamokSleepBed"||sleepNativePositionV63)return;
            // Drop prior compensation coordinates once. Future deliberate adjustments
            // remain available; preserve wrap offsets and all saved size controls.
            offsets=offsets.Where(entry=>!entry.Key.EndsWith("/all",StringComparison.Ordinal) &&
                !entry.Key.EndsWith("/body",StringComparison.Ordinal) && !entry.Key.EndsWith("/head",StringComparison.Ordinal))
                .ToDictionary(entry=>entry.Key,entry=>entry.Value);
            sleepNativePositionV63=true;
        }
        public bool BubbleBodyAllowed => parent.def.defName.StartsWith("YR_Building_GloryHole",StringComparison.Ordinal);
        public string BubbleChoice(Pawn pawn,int slot) => bubbleChoices.TryGetValue(Key(pawn,"bubble:"+slot),out var name)?name:null;
        public virtual float DisplayScale(Pawn pawn,string layer) => Mathf.Clamp(1f+Offset(pawn,"scale:"+layer).x,.2f,3f);
        public float RenderScale(Pawn pawn,string layer) => DisplayScale(pawn,"all")*(layer=="all"?1f:DisplayScale(pawn,layer));
        public void OpenBubbleChoices(int slot)
        {
            var pawn=Pawn;if(pawn==null||!BubbleBodyAllowed)return;
            var choices=new List<FloatMenuOption>{new FloatMenuOption("원래 말풍선 몸체",()=>SetBubbleChoice(pawn,slot,null))};
            foreach(var source in FacilityBodyCatalogV47.Choices()){
                var choice=source;
                choices.Add(new FloatMenuOption(FacilityBodyCatalogV47.Label(choice.defName),()=>SetBubbleChoice(pawn,slot,choice.defName)));
            }
            Find.WindowStack.Add(new FloatMenu(choices));
        }
        private void SetBubbleChoice(Pawn pawn,int slot,string name)
        {
            var copy=new Dictionary<string,string>(bubbleChoices);string key=Key(pawn,"bubble:"+slot);
            if(name==null)copy.Remove(key);else copy[key]=name;bubbleChoices=copy;
            if(parent is Building_AnimationBed bed){BedAnimationUtility.MakePortrait(bed);bed.makePortrait=true;}
        }
        public virtual bool SupportsBodyChoice => parent.def.defName=="YR_Building_MilkingMachine" || parent.def.defName=="YR_Building_EggMachine";
        public virtual string BodyChoice(Pawn pawn) => bodyChoices.TryGetValue(Context(pawn),out var name)?name:null;
        public virtual void SetBodyChoice(Pawn pawn,string name)
        {
            var copy=new Dictionary<string,string>(bodyChoices);
            if(string.IsNullOrEmpty(name))copy.Remove(Context(pawn));else copy[Context(pawn)]=name;
            bodyChoices=copy;
            var animation=parent.TryGetComp<CompAnimationSetting>();
            if(animation!=null)animation.needMakeGraphics=true;
            pawn?.Drawer.renderer.SetAllGraphicsDirty();
            if(parent is Building_AnimationBed facility && facility.HeldPawn!=null)BedAnimationUtility.MakeBedAnimation(facility);
            foreach(var refresh in parent.AllComps.OfType<IBodyChoiceRefreshV47>())refresh.RefreshBodyChoice();
        }
        private IAnimationPositionSource Source => parent.AllComps.OfType<IAnimationPositionSource>().FirstOrDefault();
        public virtual Pawn Pawn => Source?.PositionPawn;
        private string Context(Pawn pawn) => (pawn?.def.defName??"empty")+"/"+(pawn?.story?.bodyType?.defName??"none")+"/"+pawn?.gender;
        private string Key(Pawn pawn,string layer) => Context(pawn)+"/"+layer;
        public virtual Vector3 Offset(Pawn pawn,string layer) => offsets.TryGetValue(Key(pawn,layer),out var value)?value:Vector3.zero;
        public virtual void Move(Pawn pawn,string layer,Vector3 delta,bool reset=false)
        {
            var next=new Dictionary<string,Vector3>(offsets);string key=Key(pawn,layer);
            if(reset)next.Remove(key);else next[key]=Offset(pawn,layer)+delta;
            offsets=next;
            if(parent is Building_AnimationBed bed)bed.makePortrait=true;
            pawn?.Drawer.renderer.SetAllGraphicsDirty();
        }
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref sleepNativePositionV63,"samokSleepNativePositionV63",false);
            Scribe_Collections.Look(ref offsets,"animationPositionV38",LookMode.Value,LookMode.Value);
            Scribe_Collections.Look(ref bodyChoices,"facilityBodyChoiceV47",LookMode.Value,LookMode.Value);
            Scribe_Collections.Look(ref bubbleChoices,"bubbleBodyChoiceV57",LookMode.Value,LookMode.Value);
            if(Scribe.mode==LoadSaveMode.PostLoadInit){
                if(offsets==null)offsets=new Dictionary<string,Vector3>();
                if(bodyChoices==null)bodyChoices=new Dictionary<string,string>();
                if(bubbleChoices==null)bubbleChoices=new Dictionary<string,string>();
            }
        }
        public virtual List<KeyValuePair<string,string>> Layers()
        {
            var result=new List<KeyValuePair<string,string>> {
                new KeyValuePair<string,string>("all","전체 애니메이션"),
                new KeyValuePair<string,string>("body","기본 몸체·다리"),
                new KeyValuePair<string,string>("head","머리") };
            if(Source!=null)foreach(var entry in Source.PositionLayers)
                if(!result.Any(x=>x.Key==entry.Key))result.Add(entry);
            return result;
        }
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if(SupportsBodyChoice)yield return new Command_Action {
                defaultLabel="몸통 선택",
                defaultDesc="자동 또는 출력할 몸통 파츠를 선택합니다. 실제 종족·성별·체형은 변경하지 않습니다.",
                icon=ContentFinder<Texture2D>.Get("UI/Icons/GDAV3/Up"),
                action=()=>OpenBodyChoices() };
            yield return new Command_Action {
                defaultLabel="애니메이션 위치 조정",
                defaultDesc="몸체·머리·효과를 선택해 화살표로 이동합니다. 건물별·종족별·체형별·성별 좌표가 저장됩니다.",
                icon=ContentFinder<Texture2D>.Get("UI/Icons/GDAV3/Up"),
                action=()=>Find.WindowStack.Add(new DialogAnimationPosition(this)) };
        }
        public virtual void OpenBodyChoices()
        {
            var pawn=Pawn;
            if(pawn==null){Messages.Message("장치에 대상을 배치한 뒤 몸통을 선택하세요.",MessageTypeDefOf.RejectInput,false);return;}
            var choices=new List<FloatMenuOption> {
                new FloatMenuOption("자동 (종족·성별 기본값)",()=>SetBodyChoice(pawn,null)),
                new FloatMenuOption("기본 몸체만 사용",()=>SetBodyChoice(pawn,"native")) };
            foreach(var def in FacilityBodyCatalogV47.Choices()){
                var choice=def;
                choices.Add(new FloatMenuOption(FacilityBodyCatalogV47.Label(choice.defName),()=>SetBodyChoice(pawn,choice.defName)));
            }
            Find.WindowStack.Add(new FloatMenu(choices));
        }
        public static CompAnimationPosition For(Pawn pawn)
        {
            var holder=pawn?.ParentHolder;
            var thing=holder is Corpse ? holder.ParentHolder as ThingWithComps :
                holder as ThingWithComps ?? holder?.ParentHolder as ThingWithComps;
            return thing?.TryGetComp<CompAnimationPosition>() ?? pawn?.CurrentBed()?.TryGetComp<CompAnimationPosition>();
        }
    }
    public class DialogAnimationPosition : Window
    {
        private readonly CompAnimationPosition comp;
        private string selected="body";
        private float step=.05f;
        public override Vector2 InitialSize => new Vector2(370,475);
        public DialogAnimationPosition(CompAnimationPosition comp,string initialLayer="body")
        { this.comp=comp;selected=initialLayer;doCloseX=true;draggable=true;closeOnClickedOutside=false; }
        public override void DoWindowContents(Rect rect)
        {
            Widgets.Label(new Rect(0,0,330,27),"애니메이션 위치 조정");
            var pawn=comp.Pawn;
            if(pawn==null){Widgets.Label(new Rect(0,40,330,60),"장치에 대상을 배치한 뒤 조정하세요.");return;}
            Widgets.Label(new Rect(0,29,330,38),pawn.LabelShort+" · "+pawn.def.label+" · "+pawn.story?.bodyType?.label);
            var layers=comp.Layers();var label=layers.FirstOrDefault(x=>x.Key==selected).Value??selected;
            if(Widgets.ButtonText(new Rect(0,70,330,28),label))Find.WindowStack.Add(new FloatMenu(layers.Select(x=>new FloatMenuOption(x.Value,()=>selected=x.Key)).ToList()));
            float[] steps={.01f,.05f,.1f,.5f};
            for(int i=0;i<steps.Length;i++)if(Widgets.ButtonText(new Rect(i*83,105,79,26),(step==steps[i]?"● ":"")+steps[i].ToString("0.00")))step=steps[i];
            if(Widgets.ButtonText(new Rect(128,139,70,34),"↑"))comp.Move(pawn,selected,new Vector3(0,0,step));
            if(Widgets.ButtonText(new Rect(50,179,70,34),"←"))comp.Move(pawn,selected,new Vector3(-step,0,0));
            if(Widgets.ButtonText(new Rect(128,179,70,34),"초기화"))comp.Move(pawn,selected,Vector3.zero,true);
            if(Widgets.ButtonText(new Rect(206,179,70,34),"→"))comp.Move(pawn,selected,new Vector3(step,0,0));
            if(Widgets.ButtonText(new Rect(128,219,70,34),"↓"))comp.Move(pawn,selected,new Vector3(0,0,-step));
            var offset=comp.Offset(pawn,selected);
            bool bubbleContent=selected.StartsWith("bubbleContent:",StringComparison.Ordinal);
            bool bubble=comp.Layers().Any(l=>l.Key==selected&&l.Value.StartsWith("말풍선",StringComparison.Ordinal));
            {
                Widgets.Label(new Rect(0,343,140,26),"배율 "+comp.DisplayScale(pawn,selected).ToString("0.00"));
                if(Widgets.ButtonText(new Rect(140,343,58,26),"−")){
                    float target=Mathf.Max(.2f,comp.DisplayScale(pawn,selected)-.1f);
                    comp.Move(pawn,"scale:"+selected,new Vector3(target-comp.DisplayScale(pawn,selected),0,0));
                }
                if(Widgets.ButtonText(new Rect(202,343,58,26),"+")){
                    float target=Mathf.Min(3f,comp.DisplayScale(pawn,selected)+.1f);
                    comp.Move(pawn,"scale:"+selected,new Vector3(target-comp.DisplayScale(pawn,selected),0,0));
                }
                if(Widgets.ButtonText(new Rect(264,343,66,26),"초기화"))comp.Move(pawn,"scale:"+selected,Vector3.zero,true);
                if(bubbleContent&&comp.BubbleBodyAllowed&&int.TryParse(selected.Substring("bubbleContent:".Length),out int slot)){
                    if(Widgets.ButtonText(new Rect(0,378,330,28),"말풍선 몸통: "+(comp.BubbleChoice(pawn,slot)==null?"원본":FacilityBodyCatalogV47.Label(comp.BubbleChoice(pawn,slot)))))comp.OpenBubbleChoices(slot);
                }
            }
            if(comp.SupportsBodyChoice && Widgets.ButtonText(new Rect(0,308,330,28),"몸통: "+FacilityBodyCatalogV47.Label(comp.BodyChoice(pawn))))comp.OpenBodyChoices();
            Widgets.Label(new Rect(0,264,330,42),"가로 "+offset.x.ToString("0.00")+" / 세로 "+offset.z.ToString("0.00")+" · 단위: 타일\n게임 저장 시 함께 저장됩니다.");
        }
    }
    [HarmonyPatch(typeof(PawnRenderTree),nameof(PawnRenderTree.ParallelPreDraw))]
    internal static class AnimationPositionNative
    {
        private static readonly AccessTools.FieldRef<PawnRenderTree,List<PawnGraphicDrawRequest>> Requests=
            AccessTools.FieldRefAccess<PawnRenderTree,List<PawnGraphicDrawRequest>>("drawRequests");
        private struct Applied { public PawnRenderNode node; public Matrix4x4 matrix,original; }
        // Overlay geometry uses the native animation size, independently of base-body sizing.
        internal static Matrix4x4 WithoutUserScale(PawnRenderTree tree,PawnRenderNode node,Matrix4x4 matrix)
        {
            if(AppliedByTree.TryGetValue(tree,out var entries))foreach(var entry in entries)
                if(entry.node==node){
                    for(int r=0;r<3;r++)for(int c=0;c<3;c++)matrix[r,c]=entry.original[r,c];
                    break;
                }
            return matrix;
        }
        private static readonly ConditionalWeakTable<PawnRenderTree,List<Applied>> AppliedByTree=new ConditionalWeakTable<PawnRenderTree,List<Applied>>();
        private static void Prefix(PawnRenderTree __instance)
        {
            if(!AppliedByTree.TryGetValue(__instance,out var previous))return;
            var list=Requests(__instance);
            for(int i=0;i<previous.Count && i<list.Count;i++){
                var r=list[i];var old=previous[i];
                if(r.node!=old.node || r.preDrawnComputedMatrix!=old.matrix)continue;
                var matrix=old.original;
                r.preDrawnComputedMatrix=matrix;list[i]=r;
            }
            previous.Clear();
        }
        private static bool Head(PawnRenderNode node)
        {
            for(var n=node;n!=null;n=n.parent)
                if(n is PawnRenderNode_Head || n is PawnRenderNode_Hair || n.Props?.tagDef==PawnRenderNodeTagDefOf.Head)return true;
            return node is AlienRace.AlienPawnRenderNode_BodyAddon && AlienRace.AlienPawnRenderNodeWorker_BodyAddon.AddonFromNode(node).alignWithHead;
        }
        private static void Postfix(PawnRenderTree __instance,PawnDrawParms parms)
        {
            if(parms.Portrait || FacilityPortrait.HeadOnlyFor(parms.pawn))return;
            var comp=CompAnimationPosition.For(parms.pawn);if(comp==null)return;
            var all=comp.Offset(parms.pawn,"all");var body=comp.Offset(parms.pawn,"body");var head=comp.Offset(parms.pawn,"head");
            float bodyScale=comp.RenderScale(parms.pawn,"body"),headScale=comp.RenderScale(parms.pawn,"head");
            if(all==Vector3.zero && body==Vector3.zero && head==Vector3.zero && bodyScale==1f && headScale==1f)return;
            var list=Requests(__instance);
            var applied=AppliedByTree.GetValue(__instance,_=>new List<Applied>());
            applied.Clear();
            for(int i=0;i<list.Count;i++){
                var r=list[i];bool isHead=Head(r.node);var delta=all+(isHead?head:body);var original=r.preDrawnComputedMatrix;
                float scale=isHead?headScale:bodyScale;
                var matrix=original*Matrix4x4.Scale(new Vector3(scale,1f,scale));
                matrix.m03+=delta.x;matrix.m23+=delta.z;r.preDrawnComputedMatrix=matrix;list[i]=r;
                applied.Add(new Applied { node=r.node,matrix=matrix,original=original });
            }
        }
    }
}

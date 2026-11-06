using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace YR_RJW
{
    [StaticConstructorOnStartup]
    internal static class ApparelScaleSetupV45
    {
        static ApparelScaleSetupV45()
        {
            foreach(var def in DefDatabase<ThingDef>.AllDefs)
                if(def.IsApparel && string.Equals(def.modContentPack?.PackageIdPlayerFacing,"SutSutMan.YRHentaiPrime",StringComparison.OrdinalIgnoreCase)){
                    if(def.comps==null)def.comps=new List<CompProperties>();
                    if(!def.comps.Any(c=>c.compClass==typeof(CompApparelScaleV45)))
                        def.comps.Add(new CompProperties { compClass=typeof(CompApparelScaleV45) });
                }
        }
    }
    public class CompApparelScaleV45 : ThingComp
    {
        private Dictionary<string,Vector3> scales=new Dictionary<string,Vector3>();
        private Dictionary<string,Vector3> positions=new Dictionary<string,Vector3>();
        private Apparel Item => parent as Apparel;
        private string Key => (Item?.Wearer?.def.defName??"none")+"/"+(Item?.Wearer?.story?.bodyType?.defName??"none")+"/"+Item?.Wearer?.gender;
        public Vector3 Scale => scales.TryGetValue(Key,out var value)?value:Vector3.one;
        public Vector3 Position => positions.TryGetValue(Key,out var value)?value:Vector3.zero;
        public void Move(float x,float z,bool reset=false)
        {
            var value=Position+new Vector3(x,0,z);
            var copy=new Dictionary<string,Vector3>(positions);
            if(reset)copy.Remove(Key);else copy[Key]=value;
            positions=copy;
            var wearer=Item?.Wearer;wearer?.Drawer.renderer.SetAllGraphicsDirty();
            if(wearer!=null)PortraitsCache.SetDirty(wearer);
        }
        public override void PostExposeData()
        {
            base.PostExposeData();Scribe_Collections.Look(ref scales,"wornApparelScaleV45",LookMode.Value,LookMode.Value);
            Scribe_Collections.Look(ref positions,"wornApparelPositionV45",LookMode.Value,LookMode.Value);
            if(Scribe.mode==LoadSaveMode.PostLoadInit){
                if(scales==null)scales=new Dictionary<string,Vector3>();
                if(positions==null)positions=new Dictionary<string,Vector3>();
            }
        }
        public void Change(float x,float z,bool reset=false)
        {
            var value=Scale;
            value=new Vector3(Mathf.Clamp(value.x+x,.2f,3f),1f,Mathf.Clamp(value.z+z,.2f,3f));
            var copy=new Dictionary<string,Vector3>(scales);
            if(reset)copy.Remove(Key);else copy[Key]=value;
            scales=copy;
            var wearer=Item?.Wearer;
            wearer?.Drawer.renderer.SetAllGraphicsDirty();
            if(wearer!=null)PortraitsCache.SetDirty(wearer);
        }
        public override IEnumerable<Gizmo> CompGetWornGizmosExtra()
        {
            if(Item?.Wearer==null||Find.Selector.SelectedObjectsListForReading.OfType<Pawn>().Take(2).Count()>1)yield break;
            yield return new Command_Action {
                defaultLabel="의상 조정",
                defaultDesc=parent.LabelCap+"\n\n착용 중인 의상의 크기·위치를 조정합니다. 이 의상에 종족·체형·성별로 저장됩니다.",
                icon=ApparelCommandIcons.For(parent.def),
                iconDrawScale=.85f,
                groupable=false,
                action=()=>Find.WindowStack.Add(new DialogApparelScaleV45(this))
            };
        }
    }
    internal static class ApparelCommandIcons
    {
        internal static Texture2D For(ThingDef def)
        {
            string name=def.defName switch {
                "YR_AP_Tentacle" => "SamokArmor",
                "YR_AP_Tentacle_Rooted" => "SamokRooted",
                "YR_AP_Tentacle_Headgear" => "SamokHeadgear",
                "YR_AP_Tentacle_Headgear_Mask" => "SamokMask",
                _ => null
            };
            return name==null ? def.uiIcon : ContentFinder<Texture2D>.Get("UI/Commands/"+name);
        }
    }
    public class DialogApparelScaleV45 : Window
    {
        private readonly CompApparelScaleV45 comp;
        private float step=.05f;
        private bool positionMode;
        public override Vector2 InitialSize => new Vector2(360,345);
        public DialogApparelScaleV45(CompApparelScaleV45 comp){this.comp=comp;doCloseX=true;draggable=true;}
        public override void DoWindowContents(Rect rect)
        {
            var titleRect=new Rect(0,0,rect.width-24f,28);
            string title="의상 조정 · "+comp.parent.LabelCap;
            Widgets.Label(titleRect,title.Truncate(titleRect.width,null));
            TooltipHandler.TipRegion(titleRect,title);
            if(Widgets.ButtonText(new Rect(0,30,156,26),(positionMode?"":"● ")+"크기"))positionMode=false;
            if(Widgets.ButtonText(new Rect(164,30,156,26),(positionMode?"● ":"")+"위치"))positionMode=true;
            if(positionMode){DrawPosition();return;}
            float[] steps={.01f,.05f,.1f};
            for(int i=0;i<steps.Length;i++)if(Widgets.ButtonText(new Rect(i*106,66,101,26),(step==steps[i]?"● ":"")+(steps[i]*100).ToString("0")+"%"))step=steps[i];
            Row(103,"전체",()=>comp.Change(-step,-step),()=>comp.Change(step,step));
            Row(142,"가로",()=>comp.Change(-step,0),()=>comp.Change(step,0));
            Row(181,"세로",()=>comp.Change(0,-step),()=>comp.Change(0,step));
            if(Widgets.ButtonText(new Rect(0,221,100,28),"100% 초기화"))comp.Change(0,0,true);
            var s=comp.Scale;Widgets.Label(new Rect(110,221,210,35),"가로 "+(s.x*100).ToString("0")+"% · 세로 "+(s.z*100).ToString("0")+"%");
            Widgets.Label(new Rect(0,264,320,24),"범위 20~300% · 게임 저장에 함께 저장");
        }
        private void DrawPosition()
        {
            float[] steps={.01f,.05f,.1f};
            for(int i=0;i<steps.Length;i++)if(Widgets.ButtonText(new Rect(i*106,66,101,26),(step==steps[i]?"● ":"")+steps[i].ToString("0.00")+" 타일"))step=steps[i];
            if(Widgets.ButtonText(new Rect(124,104,72,34),"↑"))comp.Move(0,step);
            if(Widgets.ButtonText(new Rect(44,145,72,34),"←"))comp.Move(-step,0);
            if(Widgets.ButtonText(new Rect(124,145,72,34),"초기화"))comp.Move(0,0,true);
            if(Widgets.ButtonText(new Rect(204,145,72,34),"→"))comp.Move(step,0);
            if(Widgets.ButtonText(new Rect(124,186,72,34),"↓"))comp.Move(0,-step);
            var value=comp.Position;
            Widgets.Label(new Rect(0,237,320,28),"가로 "+value.x.ToString("0.00")+" · 세로 "+value.z.ToString("0.00")+" 타일");
            Widgets.Label(new Rect(0,270,320,28),"크기와 독립 저장 · 착용 이미지에만 적용");
        }
        private void Row(float y,string label,Action minus,Action plus)
        {
            Widgets.Label(new Rect(0,y,100,28),label);
            if(Widgets.ButtonText(new Rect(110,y,96,28),"−"))minus();
            if(Widgets.ButtonText(new Rect(216,y,96,28),"+"))plus();
        }
    }
    [HarmonyPatch(typeof(PawnRenderNodeWorker),nameof(PawnRenderNodeWorker.ScaleFor))]
    internal static class ApparelScaleWorkerV45
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(PawnRenderNode node,ref Vector3 __result)
        {
            var comp=node?.apparel?.TryGetComp<CompApparelScaleV45>();
            if(comp==null||node.parent?.apparel==node.apparel)return;
            var scale=comp.Scale;__result.x*=scale.x;__result.z*=scale.z;
        }
    }
}

namespace YR_RJW
{
    [HarmonyPatch(typeof(PawnRenderNodeWorker),nameof(PawnRenderNodeWorker.OffsetFor))]
    internal static class ApparelPositionWorkerV45
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(PawnRenderNode node,ref Vector3 __result)
        {
            var comp=node?.apparel?.TryGetComp<CompApparelScaleV45>();
            if(comp==null||node.parent?.apparel==node.apparel)return;
            __result+=comp.Position;
        }
    }
}

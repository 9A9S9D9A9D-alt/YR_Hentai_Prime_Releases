using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace YR_RJW {
 // Recipes and acquisition costs remain unspecified; this implant is not randomly generated.
 public sealed class PawnRenderNode_CoccyxTentacles : PawnRenderNode {
  Graphic idle,rallied,core;
  public PawnRenderNode_CoccyxTentacles(Pawn pawn,PawnRenderNodeProperties props,PawnRenderTree tree):base(pawn,props,tree) { }
  Graphic Load(string state)=>GraphicDatabase.Get<Graphic_Multi>("Yuran/Implants/CoccyxTentacles/"+state,ShaderDatabase.Cutout,Props.drawSize,Color.white);
  public override Graphic GraphicFor(Pawn pawn)=>idle??(idle=Load("Idle"));
  protected override IEnumerable<Graphic> GraphicsFor(Pawn pawn) {
   yield return GraphicFor(pawn);
   yield return rallied??(rallied=Load("Rallied"));
   yield return core??(core=GraphicDatabase.Get<Graphic_Single>("Yuran/Implants/CoccyxTentacles/Core",ShaderDatabase.Cutout,Props.drawSize,Color.white));
  }
  public Graphic ForState(bool drafted,bool mesh=false)=>mesh?core:(drafted?rallied:idle);
 }
 public sealed class PawnRenderNodeWorker_CoccyxTentacles : PawnRenderNodeWorker {
  protected override Graphic GetGraphic(PawnRenderNode node,PawnDrawParms parms)=>((PawnRenderNode_CoccyxTentacles)node).ForState(parms.pawn.Drafted,node.hediff?.TryGetComp<HediffComp_CoccyxCombat>() is HediffComp_CoccyxCombat c&&!parms.Portrait&&CoccyxTentacleDrawing.UsesMesh(c))??node.PrimaryGraphic;
  public override void PostDraw(PawnRenderNode node,PawnDrawParms parms,Mesh mesh,Matrix4x4 matrix){base.PostDraw(node,parms,mesh,matrix);var comp=node.hediff?.TryGetComp<HediffComp_CoccyxCombat>();if(comp!=null){comp.drawing.Draw(comp,parms,matrix);if(!parms.Portrait)comp.drawing.DrawSpray(comp,matrix);} }
  public override bool CanDrawNow(PawnRenderNode node,PawnDrawParms parms)=>parms.pawn.RaceProps.Humanlike&&base.CanDrawNow(node,parms);
  public override Vector3 ScaleFor(PawnRenderNode node,PawnDrawParms parms){var result=base.ScaleFor(node,parms);var c=node.hediff?.TryGetComp<HediffComp_CoccyxCombat>();if(c!=null){result*=Mathf.Clamp(Mathf.Sqrt(c.Pawn.BodySize),.65f,1.5f)*c.visualScale;if(!parms.Portrait){result.x*=.5f;result.z*=.5f;}}return result;}
  public override float LayerFor(PawnRenderNode node,PawnDrawParms parms)=>parms.facing==Rot4.North?30f:-10f;
  public override Vector3 OffsetFor(PawnRenderNode node,PawnDrawParms parms,out Vector3 pivot) {
   var offset=base.OffsetFor(node,parms,out pivot);
   var calibration=node.hediff?.TryGetComp<HediffComp_CoccyxCombat>();if(calibration!=null){offset.x+=calibration.offsetX[parms.facing.AsInt];offset.z+=calibration.offsetZ[parms.facing.AsInt];}
   offset.z-=.28f;
   if(parms.facing==Rot4.East)offset.x-=.1f;
   if(parms.facing==Rot4.West)offset.x+=.1f;
   return offset;
  }
 }

 // The owner can be offscreen while its active mesh still crosses the camera view.
 [HarmonyPatch(typeof(DynamicDrawManager),"ComputeCulledThings")]
 static class CoccyxActionDrawBounds {
  static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
   var vanilla=AccessTools.Method(typeof(GenAdj),nameof(GenAdj.OccupiedDrawRect));
   var extended=AccessTools.Method(typeof(CoccyxActionDrawBounds),nameof(DrawBounds));
   foreach(var instruction in instructions) {
    if(instruction.Calls(vanilla))instruction.operand=extended;
    yield return instruction;
   }
  }

  internal static CellRect DrawBounds(Thing thing) {
   var bounds=thing.OccupiedDrawRect();
   if(!(thing is Pawn pawn)||!pawn.Spawned||pawn.Dead)return bounds;
   var implant=DefDatabase<HediffDef>.GetNamedSilentFail("YR_CoccyxTentacleImplant");
   var comp=implant==null?null:pawn.health?.hediffSet.GetFirstHediffOfDef(implant)?.TryGetComp<HediffComp_CoccyxCombat>();
   if(comp==null)return bounds;
   void Include(Vector3 point,int padding=1) =>
    bounds=bounds.Encapsulate(CellRect.CenteredOn(point.ToIntVec3(),padding));

   int now=Find.TickManager.TicksGame;
   foreach(var limb in comp.limbs) {
    if(limb.Active) {
     int padding=Mathf.CeilToInt(Mathf.Max(Mathf.Abs(limb.strikeWidth),Mathf.Abs(limb.strikeBend))*Mathf.Abs(comp.curveFactor))+1;
     Include(limb.endpoint,padding);
     Include(pawn.DrawPos,padding);
    }
    bool streaming=limb.continuous&&limb.streamStart>=0&&(limb.streamStop<0||now-limb.streamStop<6);
    if(limb.remoteFluid&&limb.shotTick>=0&&now>=limb.shotTick&&(streaming||now-limb.shotTick<limb.shotDuration)) {
     Include(limb.shotOrigin);
     Include(limb.target?.Spawned==true?limb.target.DrawPos:limb.shotEnd);
    }
   }
   if(comp.FrenzyActive)
    Include(pawn.DrawPos,Mathf.CeilToInt(4.8f*Mathf.Clamp(Mathf.Sqrt(pawn.BodySize),.65f,1.5f)*comp.visualScale)+1);
   if(now>=comp.lastSprayTick&&now-comp.lastSprayTick<15) {
    Include(comp.lastSprayOrigin,2);
    Include(comp.lastSprayOrigin+comp.lastSprayDirection*6f,2);
   }
   if(comp.EscortTarget?.Spawned==true) {
    var target=comp.EscortTarget.DrawPos;
    Include(target,2);
    if(comp.splitMouthEscort) {
     Include(target+new Vector3(comp.splitOuterX,0,comp.splitOuterZ)*1.5f,
      Mathf.CeilToInt(Mathf.Max(comp.splitOuterWidth,comp.splitOuterHeight))+1);
     if(comp.splitShowBubble)
      Include(target+new Vector3(comp.splitBubbleX,0,comp.splitBubbleZ),Mathf.CeilToInt(comp.splitBubbleScale)+1);
    }
   }
   return bounds;
  }
 }
}

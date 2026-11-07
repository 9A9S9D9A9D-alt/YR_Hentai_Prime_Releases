using System.Collections.Generic;
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
}

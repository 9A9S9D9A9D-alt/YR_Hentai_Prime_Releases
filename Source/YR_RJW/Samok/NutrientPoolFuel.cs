using RimWorld;
using UnityEngine;
using Verse;
namespace YR_RJW {
 [StaticConstructorOnStartup]
 internal static class NutrientPoolGraphics {
  internal static readonly Graphic Empty=GraphicDatabase.Get<Graphic_Single>("Yuran/Things/Building/Furniture/Tentacle/SwimmingPool/Empty",ShaderDatabase.Cutout,new Vector2(6,6),Color.white);
 }
 public partial class Building_SamokSwimmingPool {
  public bool HasNutrition => this.TryGetComp<CompRefuelable>()?.HasFuel==true;
  public override Graphic Graphic => HasNutrition?base.Graphic:NutrientPoolGraphics.Empty;
 }
}

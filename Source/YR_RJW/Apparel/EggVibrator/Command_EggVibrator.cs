using UnityEngine;
using Verse;

namespace YR_RJW
{
    [StaticConstructorOnStartup]
    public class Command_EggVibrator : Command_VerbTarget
    {
        public Command_EggVibrator(Comp_EggVibrator comp)
        {
            this.comp = comp;
        }
        public override Color IconDrawColor
        {
            get
            {
                Color? color = overrideColor;
                return color == null ? base.IconDrawColor : color.GetValueOrDefault();
            }
        }

        public override bool GroupsWith(Gizmo other)
        {
            return base.GroupsWith(other)
&& other is Command_EggVibrator command_EggVibrator && comp.parent.def == command_EggVibrator.comp.parent.def;
        }

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            Rect rect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), 75f);
            GizmoResult result = base.GizmoOnGUI(topLeft, maxWidth, parms);
            if (comp.cooldownTicks > 0)
            {
                float num = Mathf.InverseLerp(comp.Props.cooldownTicks, 0f, comp.cooldownTicks);
                Widgets.FillableBar(rect, Mathf.Clamp01(num), cooldownBarTex, null, false);
                if (comp.cooldownTicks > 0)
                {
                    Text.Font = GameFont.Tiny;
                    Text.Anchor = TextAnchor.UpperCenter;
                    Widgets.Label(rect, num.ToStringPercent("F0"));
                    Text.Anchor = TextAnchor.UpperLeft;
                }
            }
            return result.State == GizmoState.Interacted && comp.cooldownTicks <= 0 ? result : new GizmoResult(result.State);
        }

        private readonly Comp_EggVibrator comp;

        public Color? overrideColor;
        private static readonly Texture2D cooldownBarTex = SolidColorMaterials.NewSolidColorTexture(new Color32(9, 203, 4, 64));
    }
}

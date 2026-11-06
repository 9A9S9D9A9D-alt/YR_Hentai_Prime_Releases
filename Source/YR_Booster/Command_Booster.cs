using UnityEngine;
using Verse;

namespace YR_Booster
{
    [StaticConstructorOnStartup]
    public class Command_Booster : Command_VerbTarget
    {
        public Command_Booster(CompBooster comp)
        {
            this.comp = comp;
        }
        public override Color IconDrawColor
        {
            get
            {
                Color? color = overrideColor;
                if (color == null)
                {
                    return base.IconDrawColor;
                }
                return color.GetValueOrDefault();
            }
        }

        public override bool GroupsWith(Gizmo other)
        {
            if (!base.GroupsWith(other))
            {
                return false;
            }
            Command_Booster command_EggVibrator = other as Command_Booster;
            return command_EggVibrator != null && comp.parent.def == command_EggVibrator.comp.parent.def;
        }

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            Rect rect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), 75f);
            GizmoResult result = base.GizmoOnGUI(topLeft, maxWidth, parms);
            if (comp.cooldownTicks > 0)
            {
                float num = Mathf.InverseLerp((float)comp.Props.cooldownTicks, 0f, (float)comp.cooldownTicks);
                Widgets.FillableBar(rect, Mathf.Clamp01(num), cooldownBarTex, null, false);
                if (comp.cooldownTicks > 0)
                {
                    Text.Font = GameFont.Tiny;
                    Text.Anchor = TextAnchor.UpperCenter;
                    Widgets.Label(rect, num.ToStringPercent("F0"));
                    Text.Anchor = TextAnchor.UpperLeft;
                }
            }
            if (result.State == GizmoState.Interacted && comp.cooldownTicks <= 0)
            {
                return result;
            }
            return new GizmoResult(result.State);
        }
        public override void GizmoUpdateOnMouseover()
        {
            this.verb.DrawHighlight(LocalTargetInfo.Invalid);
        }

        private readonly CompBooster comp;

        public Color? overrideColor;
        private static readonly Texture2D cooldownBarTex = SolidColorMaterials.NewSolidColorTexture(new Color32(9, 203, 4, 64));
    }
}

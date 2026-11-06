using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace YR_RJW
{
    // Addon22's three static appearances; no use-item job or additional interaction.
    public class CompProperties_ChangeTextureGizmo : CompProperties
    {
        public List<GraphicData> graphicDatas;
        public CompProperties_ChangeTextureGizmo() { compClass = typeof(CompChangeTextureGizmo); }
    }

    public class CompChangeTextureGizmo : ThingComp
    {
        public CompProperties_ChangeTextureGizmo Props => (CompProperties_ChangeTextureGizmo)props;
        public int indexNum;
        public bool changeTexture = true;
        public GraphicData GraphicData => Props.graphicDatas[indexNum];
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref indexNum, "indexNum", 0);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (indexNum < 0 || indexNum >= Props.graphicDatas.Count) indexNum = 0;
                changeTexture = true;
            }
        }
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            yield return new Command_Action
            {
                defaultLabel = "YR_ChangeTexture_Label".Translate(),
                defaultDesc = "YR_ChangeTexture_Desc".Translate(),
                icon = GraphicData.Graphic.MatSingle.mainTexture as Texture2D,
                action = delegate
                {
                    indexNum = (indexNum + 1) % Props.graphicDatas.Count;
                    changeTexture = true;
                }
            };
        }
    }
    public class Building_ChangeTextureGizmo : Building
    {
        public Graphic graphicInt;
        public override Graphic Graphic
        {
            get
            {
                var comp = this.TryGetComp<CompChangeTextureGizmo>();
                if (comp == null) return base.Graphic;
                if (graphicInt == null || comp.changeTexture)
                {
                    graphicInt = comp.GraphicData.GraphicColoredFor(this);
                    comp.changeTexture = false;
                }
                return graphicInt;
            }
        }
    }
}

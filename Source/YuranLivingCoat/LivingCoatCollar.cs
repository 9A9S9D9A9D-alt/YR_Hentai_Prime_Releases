using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace YuranLivingCoat
{
    public sealed class CollarNode : PawnRenderNode_Apparel
    {
        public CollarNode(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree)
            : base(pawn, props, tree) { }

        protected override IEnumerable<Graphic> GraphicsFor(Pawn pawn)
        {
            yield return GraphicDatabase.Get<Graphic_Multi>(
                "YuranLivingCoat/Finished/Collar", ShaderDatabase.Cutout, Vector2.one, apparel.DrawColor);
        }
    }
}

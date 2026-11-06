using System.Collections.Generic;
using UnityEngine;
using Verse;
using Object = UnityEngine.Object;

namespace YR_RJW
{
    public static class PortraitIconMaterialCache
    {
        public static Material MatFrom(Pawn pawn, Color color, Vector3 cameraOffset, float cameraZoom)
        {
            if (!UnityData.IsInMainThread)
            {
                Log.Error("Tried to get a material from a different thread.");
                return null;
            }
            KeyValuePair<Pawn, Color> key = new KeyValuePair<Pawn, Color>(pawn, color);
            if (!PortraitIconMaterialCache._matDict.TryGetValue(key, out Material material))
            {
                if (!PortraitIconMaterialCache._texDict.TryGetValue(pawn, out Texture texture))
                {
                    texture = new RenderTexture(256, 256, 0)
                    {
                        filterMode = (FilterMode)1
                    };
                    Find.PawnCacheRenderer.RenderPawn(pawn, (RenderTexture)texture, cameraOffset, cameraZoom, 0f, Rot4.South, true, true, true, true, default, null, null, true);
                    PortraitIconMaterialCache._texDict.Add(pawn, texture);
                }
                material = new Material(ShaderDatabase.DefaultShader);
                Object @object = material;
                Name name = pawn.Name;
                @object.name = (name?.ToString()) + "_PortraitIcon";
                material.mainTexture = texture;
                material.color = color;
                PortraitIconMaterialCache._matDict.Add(key, material);
            }
            return material;
        }
        private static readonly Dictionary<KeyValuePair<Pawn, Color>, Material> _matDict = new Dictionary<KeyValuePair<Pawn, Color>, Material>();

        private static readonly Dictionary<Pawn, Texture> _texDict = new Dictionary<Pawn, Texture>();
    }
}

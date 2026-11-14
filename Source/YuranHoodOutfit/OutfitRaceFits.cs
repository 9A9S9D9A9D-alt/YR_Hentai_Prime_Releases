using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace YuranHoodOutfit
{
    public sealed class OutfitFit : IExposable
    {
        internal Vector3 Position;
        internal Vector2 Scale = Vector2.one;
        internal float Angle;
        internal float Layer;

        public void ExposeData()
        {
            Scribe_Values.Look(ref Position, "position");
            Scribe_Values.Look(ref Scale, "scale", Vector2.one);
            Scribe_Values.Look(ref Angle, "angle");
            Scribe_Values.Look(ref Layer, "layer");
        }
    }

    public sealed class OutfitRaceFits : GameComponent
    {
        private Dictionary<string, OutfitFit> defaults = new Dictionary<string, OutfitFit>();

        public OutfitRaceFits(Game game) { }

        internal OutfitFit Get(string key) => defaults.TryGetValue(key, out var fit) ? fit : null;
        internal void Set(string key, OutfitFit fit) =>
            defaults = new Dictionary<string, OutfitFit>(defaults) { [key] = fit };

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref defaults, "hoodOutfitRaceFits", LookMode.Value, LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                defaults ??= new Dictionary<string, OutfitFit>();
        }

        // Ratkin adult Thin body and RK_HeadType textures. Keys use Rot4.AsInt.
        internal static readonly Dictionary<string, OutfitFit> Ratkin = new Dictionary<string, OutfitFit>
        {
            ["Hood:0"] = Fit(0, -.052f, 1.06f, 1.05f),
            ["Collar:0"] = Fit(0, .047f, .77f, .83f),
            ["ShoulderLeft:0"] = Fit(.05f, .03f, .84f, .86f, 4),
            ["ShoulderRight:0"] = Fit(-.05f, .03f, .84f, .86f, -4),
            ["LegWrap:0"] = Fit(0, 0, .79f, .71f),
            ["CapeLeft:0"] = Fit(.016f, .019f, .82f, .78f, 3),
            ["CapeRight:0"] = Fit(-.016f, .019f, .82f, .78f, -3),

            ["Hood:1"] = Fit(.042f, -.058f, 1.07f, 1.03f, -4),
            ["Collar:1"] = Fit(.013f, .051f, .85f, .88f, -3),
            ["ShoulderRight:1"] = Fit(.008f, .034f, .85f, .81f, -4),
            ["ChestRight:1"] = Fit(.011f, .037f, 1.02f, .69f, -9),
            ["Lower:1"] = Fit(-.015f, .004f, .93f, .8f, 9),
            ["LegWrap:1"] = Fit(-.022f, .002f, 1.04f, .69f, -6),
            ["CapeLeft:1"] = Fit(.078f, .022f, .9f, .82f, -3),
            ["CapeRight:1"] = Fit(.022f, .021f, .91f, .78f, -3),

            ["Hood:2"] = Fit(0, -.054f, 1.03f, 1.04f),
            ["Collar:2"] = Fit(0, .054f, .75f, .80f),
            ["ShoulderLeft:2"] = Fit(.056f, .041f, .85f, .79f, 4),
            ["ShoulderRight:2"] = Fit(-.056f, .041f, .85f, .79f, -4),
            ["ChestLeft:2"] = Fit(.006f, .037f, .88f, .77f, -4),
            ["ChestRight:2"] = Fit(-.006f, .037f, .88f, .77f, 4),
            ["Lower:2"] = Fit(0, .009f, .74f, .78f),
            ["LegWrap:2"] = Fit(0, -.003f, .78f, .72f),
            ["CapeLeft:2"] = Fit(.027f, .023f, .83f, .78f, 3),
            ["CapeRight:2"] = Fit(-.027f, .023f, .83f, .78f, -3),

            ["Hood:3"] = Fit(-.042f, -.058f, 1.07f, 1.03f, 4),
            ["Collar:3"] = Fit(-.013f, .051f, .85f, .88f, 3),
            ["ShoulderLeft:3"] = Fit(-.008f, .034f, .85f, .81f, 4),
            ["ChestLeft:3"] = Fit(-.011f, .037f, 1.02f, .69f, 9),
            ["Lower:3"] = Fit(.015f, .004f, .93f, .8f, -9),
            ["LegWrap:3"] = Fit(.022f, .002f, 1.04f, .69f, 6),
            ["CapeLeft:3"] = Fit(-.022f, .021f, .91f, .78f, 3),
            ["CapeRight:3"] = Fit(-.078f, .022f, .9f, .82f, 3)
        };

        internal static readonly Dictionary<string, OutfitFit> Dragonian = new Dictionary<string, OutfitFit>
        {
            // West settings from Dragonian_Race3056 in 외부인들1. East mirrors X and rotation.
            ["ShoulderLeft:3"] = Fit(-.06f, 0f, 1f, 1f),
            ["ShoulderRight:3"] = Fit(-.04f, -.02f, 1f, 1f),
            ["ChestLeft:3"] = Fit(-.05f, -.05f, 1.07f, .66f, -13f),
            ["Lower:3"] = Fit(-.06f, -.16f, .93f, .76f, -15f),
            ["LegWrap:3"] = Fit(0f, -.20f, .86f, .69f, 16f),
            ["Collar:3"] = Fit(.05f, -.04f, 1f, 1f),

            ["ShoulderRight:1"] = Fit(.06f, 0f, 1f, 1f),
            ["ShoulderLeft:1"] = Fit(.04f, -.02f, 1f, 1f),
            ["ChestRight:1"] = Fit(.05f, -.05f, 1.07f, .66f, 13f),
            ["Lower:1"] = Fit(.06f, -.16f, .93f, .76f, 15f),
            ["LegWrap:1"] = Fit(0f, -.20f, .86f, .69f, -16f),
            ["Collar:1"] = Fit(-.05f, -.04f, 1f, 1f)
        };

        private static OutfitFit Fit(float x, float z, float width, float height, float angle = 0f) =>
            new OutfitFit { Position = new Vector3(x, 0, z), Scale = new Vector2(width, height), Angle = angle };
    }
}

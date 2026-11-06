using RimWorld;
using System;
using UnityEngine;
using Verse;

namespace YR_Booster
{
    //Player flyer MinchoTest_CastSkipper
    public class PawnSkipper : PawnFlyer
    {
        private static float Linermotion(float x) { return 0; }
        private static readonly Func<float, float> FlightSpeed;
        private static readonly Func<float, float> FlightCurveHeight = new Func<float, float>(Linermotion);
        private int positionLastComputedTick = -1;
        private Vector3 groundPos;
        private Vector3 effectivePos;
        private float effectiveHeight;

        private Material ShadowMaterial => def.pawnFlyer.ShadowMaterial;

        static PawnSkipper()
        {
            AnimationCurve animationCurve = new AnimationCurve();
            animationCurve.AddKey(0f, 0f);
            animationCurve.AddKey(0.1f, 0.15f);
            animationCurve.AddKey(1f, 1f);
            FlightSpeed = new Func<float, float>(animationCurve.Evaluate);
        }
        public override Vector3 DrawPos
        {
            get
            {
                RecomputePosition();
                return effectivePos;
            }
        }



        private void RecomputePosition()
        {
            if (positionLastComputedTick == ticksFlying)
            {
                return;
            }
            positionLastComputedTick = ticksFlying;
            float arg = ticksFlying / (float)ticksFlightTime;
            float num = FlightSpeed(arg);
            effectiveHeight = FlightCurveHeight(num);
            groundPos = Vector3.Lerp(startVec, DestinationPos, num);
            Vector3 a = new Vector3(0f, 0f, 2f);
            Vector3 b = Altitudes.AltIncVect * effectiveHeight;
            Vector3 b2 = a * effectiveHeight;
            effectivePos = groundPos + b + b2;
        }
        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            RecomputePosition();
            DrawShadow(groundPos, effectiveHeight);
            FlyingPawn.Drawer.renderer.RenderPawnAt(effectivePos);
            if (CarriedThing != null)
            {
                PawnRenderUtility.DrawCarriedThing(FlyingPawn, effectivePos, CarriedThing);
            }
        }
        private void DrawShadow(Vector3 drawLoc, float height)
        {
            Material shadowMaterial = ShadowMaterial;
            if (shadowMaterial == null)
            {
                return;
            }
            float num = Mathf.Lerp(1f, 0.6f, height);
            Vector3 s = new Vector3(num, 1f, num);
            Matrix4x4 matrix = default(Matrix4x4);
            matrix.SetTRS(drawLoc, Quaternion.identity, s);
            Graphics.DrawMesh(MeshPool.plane10, matrix, shadowMaterial, 0);
        }
    }
}

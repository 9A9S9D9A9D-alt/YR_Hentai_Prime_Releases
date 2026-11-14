using UnityEngine;
using Verse;

namespace YuranLivingCoat
{
    [StaticConstructorOnStartup]
    sealed class PenanceEntrance
    {
        internal const int OpeningTicks = 27;
        const int GripTick = 38;
        const int ClosedTick = 76;
        const int EndTick = 86;
        const int FrameCount = 18;
        const float IntakeDepth = .22f;
        const float SpriteHeightOffset = .31f;
        const string TextureRoot = "YuranLivingCoat/Rift/PenanceEntrance/";
        static readonly Material[] Rear = new Material[FrameCount];
        static readonly Material[] Front = new Material[FrameCount];

        internal readonly FixedModeComp Mode;
        internal readonly Pawn Pawn;
        readonly int start;
        Vector3 anchor;
        readonly bool exiting;

        internal PenanceEntrance(Pawn pawn, Vector3 position)
        {
            Pawn = pawn;
            anchor = position;
            start = Find.TickManager.TicksGame - OpeningTicks;
            exiting = true;
        }

        internal PenanceEntrance(FixedModeComp mode, Pawn pawn)
        {
            Mode = mode;
            Pawn = pawn;
            start = Find.TickManager.TicksGame;
            anchor = pawn.DrawPos;
        }

        internal void TrackBeforeGrip()
        {
            if (Age <= GripTick) anchor = Pawn.DrawPos;
        }
        int Age => Find.TickManager.TicksGame - start;
        internal bool Ready => Age >= EndTick;
        static float Ease(float value) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(value));

        internal Vector3 PullOffset()
        {
            if (exiting)
                return Vector3.forward * (IntakeDepth * (1f - Ease((Age - OpeningTicks) / 25f)));

            Vector3 offset = anchor - Pawn.DrawPos;
            offset.z += IntakeDepth;
            offset.y = 0f;
            return offset * Ease((Age - GripTick) / (float)(ClosedTick - GripTick));
        }

        internal void Draw()
        {
            int age = Age;
            float opening = Ease(age / (float)OpeningTicks);
            float grip = Ease((age - GripTick) / (float)(ClosedTick - GripTick));
            int frame = Mathf.Clamp(Mathf.RoundToInt((FrameCount - 1) * opening * (1f - grip)), 0, FrameCount - 1);
            float emergence = Ease((age + 3f) / 12f) * (1f - .94f * Ease((age - ClosedTick) / (float)(EndTick - ClosedTick)));
            DrawLayers(anchor, frame, emergence, exiting || age > GripTick);
        }

        internal static void DrawExitOpening(Vector3 position, int age)
        {
            float opening = Ease(age / (float)OpeningTicks);
            DrawLayers(position, Mathf.RoundToInt((FrameCount - 1) * opening),
                Ease((age + 3f) / 12f), true);
        }

        static void DrawLayers(Vector3 anchor, int frame, float emergence, bool showFront)
        {
            var rear = Rear[frame] ??= MaterialPool.MatFrom(TextureRoot + "Rear" + frame.ToString("00"), ShaderDatabase.Transparent);
            var position = anchor;
            position.z += SpriteHeightOffset;
            position.y = AltitudeLayer.Pawn.AltitudeFor() - .035f;
            RiftStrike.DrawQuad(position, Quaternion.identity, 2.45f * emergence, 2.65f * emergence, rear);
            if (!showFront) return;
            var front = Front[frame] ??= MaterialPool.MatFrom(TextureRoot + "Front" + frame.ToString("00"), ShaderDatabase.Transparent);
            position.y = AltitudeLayer.MoteOverhead.AltitudeFor() + .02f;
            RiftStrike.DrawQuad(position, Quaternion.identity, 2.45f * emergence, 2.65f * emergence, front);
        }
    }
}

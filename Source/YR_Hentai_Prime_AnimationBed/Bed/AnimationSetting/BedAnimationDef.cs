using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace YR_Hentai_Prime_AnimationBed
{
    public class BedAnimationDef : Def
    {
        public List<BedAnimationSetting> bedAnimationSettings = new List<BedAnimationSetting>();
        public int durationTicks = 10;
        public bool autoDurationTicksSetting = false;
        public bool isPawnTextureReplace = false;
        // Addon25 south-only body frames must keep their authored facing.
        public bool fixedFacingBody = false;
        public bool bodyOverlay = false;
        public bool setPawnColor = false;
        public PawnRenderNodeTagDef pawnRenderNodeTagDef;
        public bool animationSynchro;
        public bool animationSynchrotoDummyForJoyAnimation;
        public bool isPortrait = false;
        public Vector3 offset;

        public bool logCurrentTexture = false;
        public bool logCurrentTick = false;
        public bool logCurrentOffset = false;
    }
}
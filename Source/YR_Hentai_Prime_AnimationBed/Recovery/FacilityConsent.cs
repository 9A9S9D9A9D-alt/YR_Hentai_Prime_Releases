using System.Collections.Generic;
using Verse;

namespace YR_Hentai_Prime_AnimationBed
{
    // Compatibility reader for saves made with v3/v4. No gameplay checks or UI.
    public class FacilityConsent : GameComponent
    {
        private List<string> oldRecords;
        public FacilityConsent(Game game) { }
        public override void ExposeData()
        {
            Scribe_Collections.Look(ref oldRecords, "yrpFacilityConsent", LookMode.Value);
        }
    }
}

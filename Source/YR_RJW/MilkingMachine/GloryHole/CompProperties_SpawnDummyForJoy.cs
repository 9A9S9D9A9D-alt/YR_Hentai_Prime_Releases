using RimWorld;
using System.Collections.Generic;
using Verse;

namespace YR_RJW
{
    public class CompProperties_SpawnDummyForJoy : CompProperties
    {
        public CompProperties_SpawnDummyForJoy()
        {
            compClass = typeof(CompSpawnDummyForJoy);
        }

        public ThingDef spawnDummy;
        public SoundDef soundAmbient;
        public int voiceProbability;
        public int climaxListNumForVoice = -10;

        public EroVoiceDef femaleEroVoice;
        public EroVoiceDef noneEroVoice;
        public EroVoiceDef maleEroVoice;

        public EroVoiceDef climaxSound;

        public EroVoiceDef finishSound;

        public List<NeedIncrease> joyPawnNeedIncrease = new List<NeedIncrease>();
        public List<NeedIncrease> occupantPawnNeedIncrease = new List<NeedIncrease>();

        public List<PawnHediffSetting> joyPawnHediffSettings = new List<PawnHediffSetting>();
        public List<PawnHediffSetting> occupantPawnHediffSettings = new List<PawnHediffSetting>();


        public List<PawnHediffSetting> joyPawnFinishHediffSettings = new List<PawnHediffSetting>();
        public List<PawnHediffSetting> occupantPawnFinishHediffSettings = new List<PawnHediffSetting>();

        public int needTicks = 50;

        public List<ThingDef> neverVoiceRaces = new List<ThingDef>();

        public ReduceResistanceAndWillSetting reduceResistanceAndWillSetting = new ReduceResistanceAndWillSetting();

        public ApparelSetting apparelSetting = new ApparelSetting();
    }

    public class ApparelSetting
    {
        public bool hideHat = true;
        public bool hideApparel = true;
        public List<string> neverHideTags = new List<string>();
    }

    public class ReduceResistanceAndWillSetting
    {
        public int ticks = 500;
        public float reduceResistance = 0;
        public float reduceWill = 0;
    }

    public class PawnHediffSetting
    {
        public List<HediffDef> hediffDefs = new List<HediffDef>();
        public List<RaceBodyPart> raceBodyParts = new List<RaceBodyPart>();
    }
    public class RaceBodyPart
    {
        public ThingDef race;
        public BodyPartDef bodyPart;
    }

    public class NeedIncrease
    {
        public NeedDef needDef;
        public float increase = 0.001f;
    }
    public class CompSpawnDummyForJoy : ThingComp
    {
        public CompProperties_SpawnDummyForJoy Props => (CompProperties_SpawnDummyForJoy)props;
    }
}
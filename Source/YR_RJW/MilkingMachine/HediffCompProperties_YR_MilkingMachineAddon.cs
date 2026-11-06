using RimWorld;
using System.Linq;
using Verse;

namespace YR_RJW
{
    public class HediffCompProperties_YR_MilkingMachineAddon : HediffCompProperties
    {
        public HediffCompProperties_YR_MilkingMachineAddon()
        {
            compClass = typeof(HediffComp_YR_MilkingMachineAddon);
        }

        public int ticks = 50;

        public float severity = 0.1f;

        public float minSeverity = 0.1f;
        public float maxSeverity = 0.9f;

        public bool backToMinSeverity = false;
    }


    public class HediffComp_YR_MilkingMachineAddon : HediffComp
    {
        private bool plusMinus = true;
        private int ticks = 1;
        private HediffCompProperties_YR_MilkingMachineAddon Props => (HediffCompProperties_YR_MilkingMachineAddon)props;
        public CompChangeBedTexture compChangeBedTexture = null;

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            Building_BaseBondageBed BBB = (Building_BaseBondageBed)Pawn.CurrentBed();
            if (BBB != null)
            {
                compChangeBedTexture = BBB.TryGetComp<CompChangeBedTexture>();
            }
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            if (Pawn.GetPosture() == PawnPosture.LayingInBed)
            {
                if (compChangeBedTexture != null)
                {
                    if (compChangeBedTexture.AnimationWork)
                    {
                        ticks--;
                    }
                    if (ticks <= 0)
                    {
                        YR_RJW_Setting.SetTicksByCurTimeSpeed(ref ticks, Props.ticks);
                        if (Props.backToMinSeverity)
                        {
                            if (parent.Severity < Props.maxSeverity)
                            {
                                parent.Severity += Props.severity;
                            }
                            else if (Props.maxSeverity <= parent.Severity)
                            {
                                parent.Severity = Props.minSeverity;
                            }
                        }
                        else
                        {
                            if (plusMinus && parent.Severity < Props.maxSeverity)
                            {
                                parent.Severity += Props.severity;
                            }
                            else
                            {
                                plusMinus = false;
                            }

                            if (!plusMinus && parent.Severity > Props.minSeverity)
                            {
                                float nonRemove = parent.Severity - Props.severity;
                                if (nonRemove > 0)
                                {
                                    parent.Severity -= Props.severity;
                                }
                                else
                                {
                                    plusMinus = true;
                                    parent.Severity += Props.severity;
                                }
                            }
                            else
                            {
                                plusMinus = true;
                            }
                        }
                        parent.pawn.health.Notify_HediffChanged(parent);
                    }
                }
                else
                {
                    Building_BaseBondageBed BBB = (Building_BaseBondageBed)Pawn.CurrentBed();
                    if (BBB != null)
                    {
                        compChangeBedTexture = BBB.TryGetComp<CompChangeBedTexture>();
                    }
                }
            }
            else
            {
                HediffRemover.RemoveBondadgeHediff(Pawn);

                HediffDef hediffBed = parent.def;


                using System.Collections.Generic.List<Hediff>.Enumerator enumerator = Pawn.health.hediffSet.hediffs.Where(x => x.def == hediffBed).ToList().GetEnumerator();
                while (enumerator.MoveNext())
                {
                    Hediff current = enumerator.Current;
                    Pawn.health.RemoveHediff(current);
                }

                return;
            }
        }
    }
}

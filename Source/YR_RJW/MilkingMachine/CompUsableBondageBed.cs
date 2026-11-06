using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace YR_RJW
{
    public class CompUsableBondageBed : CompUsable
    {
        public new CompProperties_Usable_BondageBed Props => (CompProperties_Usable_BondageBed)props;
        protected override string FloatMenuOptionLabel(Pawn pawn)
        {
            return "YR_CantUse".Translate();
        }

        public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn pawn)
        {
            if (!(parent is Building_BaseBondageBed) || pawn.Map == null || pawn.Map != Find.CurrentMap)
            {
                yield break;
            }

            if (parent is Building_BaseBondageBed BBB)
            {
                if (!pawn.CanReach((LocalTargetInfo)parent, PathEndMode.Touch, Danger.Deadly))
                {
                    yield return new FloatMenuOption($"{FloatMenuOptionLabel(pawn)} (" + "NoPath".Translate() + ")", null,
                        MenuOptionPriority.DisabledOption);
                    yield break;
                }

                if (!pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
                {
                    yield return new FloatMenuOption($"{FloatMenuOptionLabel(pawn)} (" + "Incapable".Translate() + ")", null,
                        MenuOptionPriority.DisabledOption);
                    yield break;
                }

                if (pawn.WorkTagIsDisabled(WorkTypeDefOf.Warden.workTags))
                {
                    yield return new FloatMenuOption($"{FloatMenuOptionLabel(pawn)} (" + "YR_Forbid".Translate() + ")", null,
                        MenuOptionPriority.DisabledOption);
                    yield break;
                }

                if (BBB.occupant != null)
                {
                    if (!pawn.CanReserve(BBB.occupant))
                    {
                        yield return new FloatMenuOption(
                            $"{FloatMenuOptionLabel(BBB.occupant)} (" + "YR_Reserved".Translate() + ")", null,
                            MenuOptionPriority.DisabledOption);
                        yield break;
                    }

                    void Action2()
                    {
                        TryReleasePrisoner(pawn, BBB.occupant);
                    }

                    string str2 = "YR_Release_BondageBed".Translate(BBB.occupant.Label);
                    yield return new FloatMenuOption(str2, Action2, MenuOptionPriority.DisabledOption);
                    yield break;
                }

                bool hasPrisoner = false;
                foreach (Pawn prisoner in pawn.Map.mapPawns.AllPawns)
                {
                    if (prisoner == pawn || !prisoner.Spawned || !LegacyEligibility.Allows(prisoner))
                    {
                        yield break;
                    }

                    hasPrisoner = true;
                    if (!pawn.CanReserve(prisoner))
                    {
                        yield return new FloatMenuOption(
                            $"{FloatMenuOptionLabel(prisoner)} (" + "YR_Reserved".Translate(prisoner.Label) + ")", null,
                            MenuOptionPriority.DisabledOption);
                        yield break;
                    }

                    void Action()
                    {
                        TryStartUseJob(pawn, prisoner);
                    }

                    string str = "YR_BondageBed".Translate(pawn.Named(pawn.Name.ToString()),
                        prisoner.Named(prisoner.Name.ToString()));
                    yield return new FloatMenuOption(str, Action, MenuOptionPriority.DisabledOption);
                }

                if (!hasPrisoner)
                {
                    yield return new FloatMenuOption($"{FloatMenuOptionLabel(pawn)} (" + "YR_NoPrisoner".Translate() + ")",
                        null, MenuOptionPriority.DisabledOption);
                }
            }
        }
        public virtual void TryReleasePrisoner(Pawn pawn, LocalTargetInfo extraTarget)
        {
            if (!pawn.CanReach((LocalTargetInfo)parent, PathEndMode.Touch, Danger.Some) ||
                !pawn.CanReserveAndReach(extraTarget, PathEndMode.Touch, Danger.Some))
            {
                return;
            }

            Job job = extraTarget.IsValid
                ? JobMaker.MakeJob(YR_RJW_DefOf.YR_Job_ReleaseBed, parent, extraTarget)
                : JobMaker.MakeJob(YR_RJW_DefOf.YR_Job_ReleaseBed, parent);
            job.count = 1;
            pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
        }

        public override void TryStartUseJob(Pawn pawn, LocalTargetInfo extraTarget, bool forced = false)
        {
            if (!LegacyEligibility.Allows(pawn) || !LegacyEligibility.Allows(extraTarget.Pawn)) return;
            if (!pawn.CanReserveAndReach(parent, PathEndMode.Touch, Danger.Some) ||
                !pawn.CanReserveAndReach(extraTarget, PathEndMode.Touch, Danger.Some))
            {
                return;
            }

            Job job = extraTarget.IsValid
                ? JobMaker.MakeJob(Props.useJob, parent, extraTarget)
                : JobMaker.MakeJob(Props.useJob, parent);
            job.count = 1;
            pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
        }
    }
}
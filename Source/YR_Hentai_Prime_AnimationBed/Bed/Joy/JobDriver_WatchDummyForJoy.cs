using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace YR_Hentai_Prime_AnimationBed
{
    public class JobDriver_WatchDummyForJoy : JobDriver_WatchBuilding
    {
        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => !AdultEligibility.IsAllowed(pawn) ||
                !AdultEligibility.IsAllowed(Building_AnimationBed?.HeldPawn));
            this.EndOnDespawnedOrNull(TargetIndex.A);
            Toil watch;
            if (base.TargetC.HasThing && base.TargetC.Thing is Building_Bed)
            {
                this.KeepLyingDown(TargetIndex.C);
                yield return Toils_Bed.ClaimBedIfNonMedical(TargetIndex.C);
                yield return Toils_Bed.GotoBed(TargetIndex.C);
                watch = Toils_LayDown.LayDown(TargetIndex.C, hasBed: true, lookForOtherJobs: false);
                watch.AddFailCondition(() => !watch.actor.Awake());
            }
            else
            {
                yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);
                watch = ToilMaker.MakeToil("MakeNewToils");
            }

            watch.AddPreTickIntervalAction(delegate(int delta)
            {
                WatchTickAction(delta);
            });

            // JoyUtility와 FinishAction 호출
            watch.AddFinishAction(delegate
            {
                JoyUtility.TryGainRecRoomThought(pawn);
                FinishAction();

                CompProperties_SpawnDummyForJoy props = CompSpawnDummyForJoy?.Props;
                if (props != null && !start && AdultEligibility.IsAllowed(Building_AnimationBed?.HeldPawn))
                {
                    Building_AnimationBed.PlaySoundSettings(Building_AnimationBed.HeldPawn, Building_AnimationBed, props.makeSound.heldPawnSound.waitAfterJoySoundSettings);
                    Building_AnimationBed.PlaySoundSettings(pawn, Building_AnimationBed, props.makeSound.joyPawnSound.waitAfterJoySoundSettings);
                }
            });

            watch.defaultCompleteMode = ToilCompleteMode.Delay;
            watch.defaultDuration = job.def.joyDuration;
            watch.handlingFacing = true;
            if (base.TargetA.Thing.def.building != null && base.TargetA.Thing.def.building.effectWatching != null)
            {
                watch.WithEffect(() => base.TargetA.Thing.def.building.effectWatching, EffectTargetGetter);
            }

            yield return watch;

            LocalTargetInfo EffectTargetGetter()
            {
                return base.TargetA.Thing.OccupiedRect().RandomCell + IntVec3.North.RotatedBy(base.TargetA.Thing.Rotation);
            }
        }


        public CompDummyForJoy CompDummyForJoy => job?.targetA.Thing?.TryGetComp<CompDummyForJoy>();

        public Building_AnimationBed Building_AnimationBed => CompDummyForJoy?.building_AnimationBed;
        public CompSpawnDummyForJoy CompSpawnDummyForJoy => Building_AnimationBed?.TryGetComp<CompSpawnDummyForJoy>();
        public CompAnimationSetting CompAnimationSetting => Building_AnimationBed?.TryGetComp<CompAnimationSetting>();

        bool stop = false;

        bool start = true;

        public bool watchNow = false;
        protected override void WatchTickAction(int delta)
        {
            if(job==null || pawn?.jobs?.curDriver!=this)return;
            if (!AdultEligibility.IsAllowed(pawn) || !AdultEligibility.IsAllowed(Building_AnimationBed?.HeldPawn))
            {
                EndJobWith(JobCondition.Incompletable);
                return;
            }
            pawn.rotationTracker.FaceCell(base.TargetA.Cell);
            pawn.GainComfortFromCellIfPossible(delta);
            // JoyTickCheckEnd can end/replace this job synchronously, including on
            // the first tick before start effects have run. Its no-joy branch even
            // ends the job while returning false, so also verify job ownership.
            var watchingJob=job;
            if(JoyUtility.JoyTickCheckEnd(pawn, delta, JoyTickFullJoyAction.EndJob, 1f, Building_AnimationBed) ||
                pawn.CurJob!=watchingJob || pawn.jobs?.curDriver!=this)return;

            if (stop)
            {
                return;
            }

            watchNow = true;

            CompProperties_SpawnDummyForJoy props = CompSpawnDummyForJoy?.Props;

            if (props != null)
            {
                Pawn heldPawn = Building_AnimationBed?.HeldPawn;
                if (pawn.Drawer.renderer.CurAnimation != props.animationDef && heldPawn != null)
                {
                    //여기에 뭔가 원인이 있음
                    pawn.Drawer.renderer.SetAnimation(props.animationDef);
                    Building_AnimationBed.dummyForJoyIsActive = true;
                    Building_AnimationBed.dummyForJoyPawn = pawn;
                    Building_AnimationBed.setAnimation = true;
                    CompAnimationSetting.needMakeGraphics = true;

                    if (start)
                    {
                        AddHediff(heldPawn, Building_AnimationBed, props.makeHediff.heldPawnHediffSetting.startHediffSettings);
                        AddHediff(pawn, Building_AnimationBed, props.makeHediff.joyPawnHediffSetting.startHediffSettings);

                        Building_AnimationBed.PlaySoundSettings(heldPawn, Building_AnimationBed, props.makeSound.heldPawnSound.startSoundSettings);
                        Building_AnimationBed.PlaySoundSettings(pawn, Building_AnimationBed, props.makeSound.joyPawnSound.startSoundSettings);

                        start = false;
                    }

                    Building_AnimationBed.PlaySoundSettings(heldPawn, Building_AnimationBed, props.makeSound.heldPawnSound.randomSoundSettings);
                    Building_AnimationBed.PlaySoundSettings(pawn, Building_AnimationBed, props.makeSound.joyPawnSound.randomSoundSettings);
                }
            }
        }


        private static void AddHediff(Pawn pawn, Building_AnimationBed building_AnimationBed, List<HediffSetting> hediffSettings)
        {
            if (!AdultEligibility.IsAllowed(pawn) || hediffSettings == null) return;
            foreach (var hediffSetting in hediffSettings)
            {
                var hediffDef = hediffSetting.hediffDef;

                foreach (var conditionHediffDef in hediffSetting.conditionHediffDefs)
                {
                    void action() => hediffDef = conditionHediffDef.hediffDef;

                    if (Condition.ExecuteActionIfConditionMatches(pawn, building_AnimationBed, conditionHediffDef.condition, action))
                    {
                        break;
                    }

                }
                pawn.health.AddHediff(hediffDef);
            }
        }

        protected void FinishAction()
        {
            watchNow=false;
            stop=true;
            if (start) return;
            pawn.Drawer.renderer.SetAnimation(null);
            watchNow = false;
            stop = true;
            if (Building_AnimationBed == null || CompAnimationSetting == null || CompSpawnDummyForJoy == null)
                return;
            Building_AnimationBed.dummyForJoyIsActive = false;
            Building_AnimationBed.dummyForJoyPawn = null;
            Building_AnimationBed.setAnimation = true;
            CompAnimationSetting.needMakeGraphics = true;


            Pawn heldPawn = Building_AnimationBed.HeldPawn;

            CompProperties_SpawnDummyForJoy props = CompSpawnDummyForJoy.Props;
            if (!AdultEligibility.IsAllowed(pawn) || !AdultEligibility.IsAllowed(heldPawn))
            {
                RemoveStartHediff(heldPawn, props.makeHediff.heldPawnHediffSetting.startHediffSettings);
                RemoveStartHediff(pawn, props.makeHediff.joyPawnHediffSetting.startHediffSettings);
                return;
            }
            AddHediff(heldPawn, Building_AnimationBed, props.makeHediff.heldPawnHediffSetting.finishHediffSettings);
            AddHediff(pawn, Building_AnimationBed, props.makeHediff.joyPawnHediffSetting.finishHediffSettings);
            CompAnimationSetting.RecordResidualEffect();

            RemoveStartHediff(heldPawn, props.makeHediff.heldPawnHediffSetting.startHediffSettings);
            RemoveStartHediff(pawn, props.makeHediff.joyPawnHediffSetting.startHediffSettings);

            Building_AnimationBed.PlaySoundSettings(heldPawn, Building_AnimationBed, props.makeSound.heldPawnSound.finishSoundSettings);
            Building_AnimationBed.PlaySoundSettings(pawn, Building_AnimationBed, props.makeSound.joyPawnSound.finishSoundSettings);

            //이걸로 멈춰야 에러 안남
            stop = true;

            watchNow = false;
        }

        private static void RemoveStartHediff(Pawn pawn, List<HediffSetting> hediffSettings)
        {
            if (pawn?.health == null || hediffSettings == null) return;
            var hediffsToRemove = pawn.health.hediffSet.hediffs
                .Where(hediff => hediffSettings
                    .Any(startHediffSetting => startHediffSetting.removeWhenFinish &&
                        (hediff.def == startHediffSetting.hediffDef ||
                        startHediffSetting.conditionHediffDefs.Any(conditionHediffDef => hediff.def == conditionHediffDef.hediffDef))))
                .ToList();

            foreach (var hediff in hediffsToRemove)
            {
                pawn.health.RemoveHediff(hediff);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();


            Scribe_Values.Look(ref start, "start", true);
        }
    }
}

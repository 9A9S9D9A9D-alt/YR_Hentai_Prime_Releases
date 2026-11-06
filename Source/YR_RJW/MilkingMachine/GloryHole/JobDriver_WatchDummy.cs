using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace YR_RJW
{
    public class JobDriver_WatchDummy : JobDriver_WatchBuilding
    {
        public bool startAni = false;
        public Sustainer sustainerAmbient;
        private CompChangeBedTexture compChangeBedTexture;
        private CompSpawnDummyForJoy compSpawnDummyForJoy;
        private Vector3 DrawPos;
        private bool dontMove = false;
        private bool onlyOnce = true;
        private bool neverVoice = false;
        public DummyForJoy Dummy => TargetThingA as DummyForJoy;

        protected override IEnumerable<Toil> MakeNewToils()
        {
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

            watch.AddPreTickAction(delegate
            {
                WatchTickAction(1);
            });
            watch.AddFinishAction(delegate
            {
                JoyUtility.TryGainRecRoomThought(pawn);
                startAni = false;

                Dummy.startAni = startAni;

                if (sustainerAmbient != null)
                {
                    sustainerAmbient.End();
                    sustainerAmbient = null;
                }
                if (Dummy.parent is Building_BaseBondageBed bbb)
                {
                    compChangeBedTexture ??= bbb.TryGetComp<CompChangeBedTexture>();
                    compSpawnDummyForJoy ??= bbb.TryGetComp<CompSpawnDummyForJoy>();
                    CompProperties_SpawnDummyForJoy compPropsSpawnDummyForJoy = compSpawnDummyForJoy.Props;

                    compChangeBedTexture.joyPawnOffsetNum = 0;

                    if (compSpawnDummyForJoy != null)
                    {
                        List<SoundDef> soundDefs = compSpawnDummyForJoy.Props.finishSound?.soundDefs;
                        if (!soundDefs.NullOrEmpty())
                        {
                            System.Random rand = new System.Random();
                            int randNum = rand.Next(0, soundDefs.Count);
                            soundDefs[randNum].PlayOneShot(new TargetInfo(Dummy.occupant.Position, Dummy.occupant.Map, false));
                        }
                    }

                    foreach (PawnHediffSetting joyPawnHediffSetting in compPropsSpawnDummyForJoy.joyPawnHediffSettings)
                    {
                        foreach (HediffDef hediffDef in joyPawnHediffSetting.hediffDefs)
                        {
                            foreach (Hediff hediff in pawn.health.hediffSet.hediffs.ToList())
                            {
                                if (hediff.def == hediffDef)
                                {
                                    pawn.health.RemoveHediff(hediff);
                                }
                            }
                        }
                    }

                    //끝나고 헤디프 추가
                    AddHediff(pawn, compPropsSpawnDummyForJoy.joyPawnFinishHediffSettings);

                    if (Dummy.occupant != null)
                    {
                        foreach (PawnHediffSetting joyPawnHediffSetting in compPropsSpawnDummyForJoy.joyPawnHediffSettings)
                        {
                            foreach (HediffDef hediffDef in joyPawnHediffSetting.hediffDefs)
                            {
                                foreach (Hediff hediff in Dummy.occupant.health.hediffSet.hediffs.ToList())
                                {
                                    if (hediff.def == hediffDef)
                                    {
                                        Dummy.occupant.health.RemoveHediff(hediff);
                                    }
                                }
                            }
                        }

                        //끝나고 헤디프 추가
                        AddHediff(Dummy.occupant, compPropsSpawnDummyForJoy.occupantPawnFinishHediffSettings);
                    }
                }
                Dummy.user = null;

                Dummy.iconReset = true;

                if (compChangeBedTexture.Props.joyPawnSetting.startBedAniWhenJoying)
                {
                    compChangeBedTexture.needMakeGrapgicList = true;
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

        private Building_BaseBondageBed BBB = null;
        private bool movingOffsetISNullOrEmpty;
        private int tickFormovingOffsetISNullOrEmpty = 1;
        public int tick = 100;
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref tick, "tick");
        }
        protected override void WatchTickAction(int delta)
        {
            if (!LegacyEligibility.Allows(pawn) || !LegacyEligibility.Allows(Dummy?.occupant))
            { pawn.jobs.EndCurrentJob(JobCondition.Incompletable); return; }
            if (pawn == null)
            {
                return;
            }
            Stopwatch stopwatch = new Stopwatch(); //객체 선언
            stopwatch.Start();// 시간측정 시작
            startAni = true;

            pawn.rotationTracker.FaceCell(base.TargetA.Cell);
            pawn.GainComfortFromCellIfPossible(delta, false);

            GlobalTextureAtlasManager.TryMarkPawnFrameSetDirty(pawn);
            if (BBB == null)
            {
                if (Dummy.parent is Building_BaseBondageBed bbb)
                { BBB = bbb; }
            }

            if (BBB != null)
            {
                compChangeBedTexture ??= BBB.TryGetComp<CompChangeBedTexture>();
                compSpawnDummyForJoy ??= BBB.TryGetComp<CompSpawnDummyForJoy>();
                CompProperties_SpawnDummyForJoy compPropsSpawnDummyForJoy = compSpawnDummyForJoy.Props;
                CompProperties_ChangeBedTexture.JoyPawnSetting joyPawnSetting = compChangeBedTexture.Props.joyPawnSetting;
                Pawn occupant = Dummy.occupant;

                if (onlyOnce)
                {
                    //더미쪽 설정 해주기
                    Dummy.user = pawn;
                    Dummy.startAni = startAni;
                    Dummy.drawFlags = PawnRenderFlags.Cache;
                    if (!compPropsSpawnDummyForJoy.apparelSetting.hideHat)
                    {
                        Dummy.drawFlags |= PawnRenderFlags.Headgear;
                    }
                    if (!compPropsSpawnDummyForJoy.apparelSetting.hideApparel)
                    {
                        Dummy.drawFlags |= PawnRenderFlags.Clothes;
                    }

                    foreach (Apparel apparel in pawn.apparel.WornApparel)
                    {
                        foreach (var neverHideTag in compPropsSpawnDummyForJoy.apparelSetting.neverHideTags)
                        {
                            if (apparel.def.apparel.tags.Contains(neverHideTag))
                            {
                                Dummy.drawFlags |= PawnRenderFlags.Headgear;
                                Dummy.drawFlags |= PawnRenderFlags.Clothes;
                                break;
                            }
                        }
                    }

                    //위치 지정해주기
                    DrawPos = Dummy.Position.ToVector3();
                    DrawPos.y = pawn.Drawer.DrawPos.y;
                    foreach (OffsetCondition offsetCondition in joyPawnSetting.offsetConditions)
                    {
                        if (CompChangeBedTexture.ConditionMatch(pawn, offsetCondition.condition))
                        {
                            DrawPos = Dummy.Position.ToVector3();
                            DrawPos.y = pawn.Drawer.DrawPos.y;

                            DrawPos.x += offsetCondition.offset.x;
                            DrawPos.z += offsetCondition.offset.y;

                            dontMove = offsetCondition.dontMove;

                            if (offsetCondition.Break)
                            {
                                break;
                            }
                        }
                    }
                    //목소리를 내는 종족인지 체크
                    foreach (ThingDef race in compPropsSpawnDummyForJoy.neverVoiceRaces)
                    {
                        if (race == pawn.def)
                        {
                            neverVoice = true;
                            break;
                        }
                    }

                    //movingOffset null 체크
                    movingOffsetISNullOrEmpty = compChangeBedTexture.Props.joyPawnSetting.movingOffset.NullOrEmpty();

                    //movingOffset 없을때 틱 설정
                    YR_RJW_Setting.SetTicksByCurTimeSpeed(ref tickFormovingOffsetISNullOrEmpty, 50);
                    //더미 아이콘 리셋, 폰 그래픽 재설정
                    Dummy.iconReset = true;
                    pawn.Drawer.renderer.SetAllGraphicsDirty();
                }

                //요거는 계속 반복해서 계산해줘야 함
                Dummy.offset = dontMove ? DrawPos : DrawPos + compChangeBedTexture.joyPawnMovingOffset;
                Dummy.offset.x += 0.5f;

                //소리 반복
                if (compPropsSpawnDummyForJoy.soundAmbient != null)
                {
                    if (sustainerAmbient == null || sustainerAmbient.Ended)
                    {
                        sustainerAmbient = compPropsSpawnDummyForJoy.soundAmbient.TrySpawnSustainer(SoundInfo.InMap(pawn, MaintenanceType.PerTick));
                    }
                    sustainerAmbient.Maintain();
                }
                //노는 폰 관련
                if (pawn.IsHashIntervalTick(compPropsSpawnDummyForJoy.needTicks))
                {
                    //만족도 추가
                    if (!compPropsSpawnDummyForJoy.joyPawnNeedIncrease.NullOrEmpty())
                    {
                        foreach (Need need in pawn.needs?.AllNeeds)
                        {
                            foreach (NeedIncrease NeedIncrease in compPropsSpawnDummyForJoy.joyPawnNeedIncrease)
                            {
                                if (need.def == NeedIncrease.needDef)
                                {
                                    need.CurLevel += NeedIncrease.increase;
                                }
                            }
                        }
                    }

                    //노는 폰 헤디프 주기
                    AddHediff(pawn, compPropsSpawnDummyForJoy.joyPawnHediffSettings);
                }
                //속박 폰 관련
                if (occupant != null)
                {
                    if (occupant.IsHashIntervalTick(compPropsSpawnDummyForJoy.needTicks))
                    {
                        if (!compPropsSpawnDummyForJoy.occupantPawnNeedIncrease.NullOrEmpty())
                        {
                            foreach (Need need in occupant?.needs?.AllNeeds)
                            {
                                foreach (NeedIncrease NeedIncrease in compPropsSpawnDummyForJoy.occupantPawnNeedIncrease)
                                {
                                    if (need.def == NeedIncrease.needDef)
                                    {
                                        need.CurLevel += NeedIncrease.increase;
                                    }
                                }
                            }
                        }

                        //신음 발사
                        {
                            float voiceProbability = compPropsSpawnDummyForJoy.voiceProbability;

                            TickManager tickManager = Find.TickManager;

                            if (tickManager.CurTimeSpeed == TimeSpeed.Fast)
                            {
                                voiceProbability *= 0.6f;
                            }
                            else if (tickManager.CurTimeSpeed == TimeSpeed.Superfast)
                            {
                                voiceProbability *= 0.3f;
                            }
                            else if (tickManager.CurTimeSpeed == TimeSpeed.Ultrafast)
                            {
                                voiceProbability *= 0.15f;
                            }

                            if (!neverVoice)
                            {
                                if (compPropsSpawnDummyForJoy.climaxListNumForVoice < 0)
                                {
                                    ConvenientTool.ShotSexSound(occupant, compPropsSpawnDummyForJoy.femaleEroVoice, compPropsSpawnDummyForJoy.maleEroVoice, compPropsSpawnDummyForJoy.noneEroVoice, (int)voiceProbability);
                                }
                            }
                        }

                        //묶인 폰 헤디프 주기
                        AddHediff(occupant, compPropsSpawnDummyForJoy.occupantPawnHediffSettings);
                    }

                    //저항도 감소
                    if (occupant.IsPrisoner)
                    {
                        ReduceResistanceAndWillSetting reduceResistanceAndWillSetting = compPropsSpawnDummyForJoy.reduceResistanceAndWillSetting;
                        if (Dummy.resetTick)
                        {
                            Dummy.tickForPrisoner = reduceResistanceAndWillSetting.ticks;
                            Dummy.resetTick = false;
                        }
                        Dummy.tickForPrisoner--;
                        if (Dummy.tickForPrisoner <= 0)
                        {
                            occupant.guest.resistance -= reduceResistanceAndWillSetting.reduceResistance;
                            occupant.guest.will -= reduceResistanceAndWillSetting.reduceWill;
                        }
                    }
                    else
                    {
                        Dummy.resetTick = true;
                    }

                    //요거는 틱 영향 x
                    //신음 발사
                    {
                        float voiceProbability = compPropsSpawnDummyForJoy.voiceProbability;

                        TickManager tickManager = Find.TickManager;

                        if (tickManager.CurTimeSpeed == TimeSpeed.Fast)
                        {
                            voiceProbability *= 0.6f;
                        }
                        else if (tickManager.CurTimeSpeed == TimeSpeed.Superfast)
                        {
                            voiceProbability *= 0.3f;
                        }
                        else if (tickManager.CurTimeSpeed == TimeSpeed.Ultrafast)
                        {
                            voiceProbability *= 0.15f;
                        }

                        if (!neverVoice)
                        {
                            if (compPropsSpawnDummyForJoy.climaxListNumForVoice >= 0)
                            {
                                if (compPropsSpawnDummyForJoy.climaxListNumForVoice == compChangeBedTexture.joyPawnOffsetNum && soundOnce)
                                {
                                    ConvenientTool.ShotSexSound(occupant, compPropsSpawnDummyForJoy.femaleEroVoice, compPropsSpawnDummyForJoy.maleEroVoice, compPropsSpawnDummyForJoy.noneEroVoice, (int)voiceProbability);
                                    soundOnce = false;
                                }
                                if (compPropsSpawnDummyForJoy.climaxListNumForVoice != compChangeBedTexture.joyPawnOffsetNum)
                                {
                                    soundOnce = true;
                                }
                            }
                        }
                    }
                }

                //클라이맥스 소리
                if (compPropsSpawnDummyForJoy.climaxListNumForVoice >= 0)
                {
                    if (compPropsSpawnDummyForJoy.climaxListNumForVoice == compChangeBedTexture.joyPawnOffsetNum && climaxSoundOnce)
                    {
                        ConvenientTool.ShotClimaxSound(pawn, compPropsSpawnDummyForJoy.climaxSound, (int)200);
                        climaxSoundOnce = false;
                    }
                    if (compPropsSpawnDummyForJoy.climaxListNumForVoice != compChangeBedTexture.joyPawnOffsetNum)
                    {
                        climaxSoundOnce = true;
                    }
                }

                if (movingOffsetISNullOrEmpty)
                {
                    if (pawn.IsHashIntervalTick(tickFormovingOffsetISNullOrEmpty))
                    {
                        Traverse.Create(pawn.Drawer).Field<JitterHandler>("jitterer").Value.AddOffset(1f, 1f);
                    }
                }
            }
            JoyUtility.JoyTickCheckEnd(pawn, 1, JoyTickFullJoyAction.EndJob, 1f, Dummy.parent);

            onlyOnce = false;
        }
        bool soundOnce = true;
        bool climaxSoundOnce = true;
        static void AddHediff(Pawn pawn, List<PawnHediffSetting> pawnHediffSettings)
        {
            foreach (PawnHediffSetting joyPawnHediffSetting in pawnHediffSettings)
            {
                bool addHediff = false;
                foreach (RaceBodyPart raceBodyPart in joyPawnHediffSetting.raceBodyParts)
                {
                    if (raceBodyPart.race == pawn.def)
                    {
                        foreach (BodyPartRecord part in pawn.RaceProps.body.AllParts)
                        {
                            if (raceBodyPart.bodyPart == part.def)
                            {
                                foreach (HediffDef hediffDef in joyPawnHediffSetting.hediffDefs)
                                {
                                    pawn.health.AddHediff(hediffDef, part);
                                    addHediff = true;
                                }
                            }
                        }
                    }
                }
                if (!addHediff)
                {
                    foreach (HediffDef hediffDef in joyPawnHediffSetting.hediffDefs)
                    {
                        pawn.health.AddHediff(hediffDef);
                    }
                }
            }
        }
    }
}

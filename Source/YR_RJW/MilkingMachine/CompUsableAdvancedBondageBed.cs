using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace YR_RJW
{
    public class CompUsableAdvancedBondageBed : CompUsableBondageBed
    {
        private bool UsesWardenEscort => parent.def.defName is
            "YR_Building_BellyPunch" or "YR_Building_EggMachine" or "YR_Building_Mimic";

        public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn pawn){
            if(CanSelfUse(pawn))yield return new FloatMenuOption("스스로 들어가기",()=>TryStartUseJob(pawn,pawn));
            foreach(var option in OptionsForTarget(pawn,null))yield return option;
        }
        public virtual bool AllowsTarget(Pawn target){
            if(target==null || !LegacyEligibility.Allows(target) || !YR_Hentai_Prime_AnimationBed.UnsupportedRacesV8.OrdinaryHumanlike(target) || !TargetConditionAllows(target))return false;
            if(target.Faction!=Faction.OfPlayer && !target.IsPrisonerOfColony)return false;
            if(!Props.races.NullOrEmpty()&&!Props.races.Contains(target.def))return false;
            if(Props.forbiddenHediffDefs.Any(h=>target.health.hediffSet.HasHediff(h)))return false;
            bool exception=Props.settingException.genderExceptionRaces.Contains(target.def);
            if(!exception && ((Props.settingException.femaleOnly&&target.gender!=Gender.Female)||(Props.settingException.maleOnly&&target.gender!=Gender.Male)))return false;
            return !target.health.hediffSet.HasHediff(YR_RJW_DefOf.YR_MilkingMachineAddon_Hediff);
        }
        public bool CanSelfUse(Pawn target)=>parent.Spawned && parent is Building_BaseBondageBed bed && bed.occupant==null && bed.CoffinCorpse==null &&
            target?.Spawned==true && target.Map==parent.Map && target.Faction==Faction.OfPlayer && !target.IsPrisonerOfColony && !target.Downed && !target.InMentalState &&
            AllowsTarget(target) && parent.Position.InAllowedArea(target) && target.CanReserveAndReach(parent,PathEndMode.Touch,Danger.Some);
        public override void TryStartUseJob(Pawn actor,LocalTargetInfo target,bool forced=false){
            if(!AllowsTarget(target.Pawn) || !(parent is Building_BaseBondageBed bed) || bed.occupant!=null || bed.CoffinCorpse!=null)return;
            if(actor!=target.Pawn){base.TryStartUseJob(actor,target,forced);return;}
            if(!CanSelfUse(actor))return;
            var job=JobMaker.MakeJob(Props.selfUseJob??DefDatabase<JobDef>.GetNamed("YR_Job_SelfBondageBed"),parent);job.count=1;
            actor.jobs.TryTakeOrderedJob(job,JobTag.Misc);
        }
        public override IEnumerable<Gizmo> CompGetGizmosExtra(){
            foreach(var g in base.CompGetGizmosExtra())yield return g;
            if(!parent.Spawned || !(parent is Building_BaseBondageBed bed))yield break;
            var command=new Command_Action{defaultLabel="이용 대상 지정",defaultDesc=UsesWardenEscort ? "정착민은 직접 입장하고 식민지 포로는 교도관 업무가 활성화된 정착민이 자동으로 호송합니다." : "정착민은 직접 입장하고 식민지 포로는 정착민이 호송합니다.",icon=ContentFinder<UnityEngine.Texture2D>.Get("UI/Commands/AssignEscort"),iconDrawScale=.85f,action=()=>{
                var options=new List<FloatMenuOption>();
                foreach(var candidate in parent.Map.mapPawns.AllPawnsSpawned.Where(AllowsTarget).OrderBy(p=>p.LabelShort)){
                    var target=candidate;options.Add(new FloatMenuOption(target.LabelShortCap,()=>{
                        if(CanSelfUse(target)){TryStartUseJob(target,target);return;}
                        foreach(var actor in parent.Map.mapPawns.FreeColonistsSpawned.Where(p=>p!=target&&!p.Downed&&!p.Drafted&&!p.InMentalState &&
                            (!UsesWardenEscort || (p.workSettings?.WorkIsActive(WorkTypeDefOf.Warden) ?? false)))){
                            var use=OptionsForTarget(actor,target).FirstOrDefault(o=>!o.Disabled&&o.action!=null);
                            if(use!=null){use.action();return;}
                        }
                        Messages.Message(UsesWardenEscort ? "교도관 업무가 활성화된 호송 가능 정착민과 접근 경로가 필요합니다." : "호송 가능한 정착민 또는 접근 경로가 필요합니다.",MessageTypeDefOf.RejectInput,false);
                    }));
                }
                if(options.Count==0)options.Add(new FloatMenuOption("현재 이용 가능한 대상이 없습니다.",null));
                Find.WindowStack.Add(new FloatMenu(options));
            }};
            if(bed.occupant!=null || bed.CoffinCorpse!=null)command.Disable("현재 사용 중입니다.");
            yield return command;
            if(bed.occupant!=null && !Props.neverRelease)yield return new Command_Action{defaultLabel="이용 종료",icon=TexCommand.Install,action=()=>bed.GetComp<CompRemoveEffectBondageBed>()?.DoEffect(bed.occupant)};
        }

        protected virtual bool TargetConditionAllows(Pawn target) => true;

        public IEnumerable<FloatMenuOption> OptionsForTarget(Pawn pawn, Pawn requestedTarget)
        {
            //침대가 구속 침대가 아니고, 사용할려는 폰이 맵에 없을때
            if (!(parent is Building_BaseBondageBed) || pawn.Map == null || pawn.Map != Find.CurrentMap)
            {
                yield break;
            }
            if (parent is Building_BaseBondageBed BBB)
            {
                //사용자가 침대에 도달할 수 없을때
                if (!pawn.CanReach((LocalTargetInfo)parent, PathEndMode.Touch, Danger.Deadly))
                {
                    yield return new FloatMenuOption($"{FloatMenuOptionLabel(pawn)} (" + "NoPath".Translate() + ")", null,
                        MenuOptionPriority.DisabledOption);
                    yield break;
                }

                //구속자가 있을 경우
                if (BBB.occupant != null)
                {
                    //절대 풀 수 없음
                    if (Props.neverRelease)
                    {
                        yield return new FloatMenuOption(
                            $"{FloatMenuOptionLabel(BBB.occupant)} (" + "YR_NeverRelease".Translate() + ")", null,
                            MenuOptionPriority.DisabledOption);
                        yield break;
                    }

                    //구속자가 딴 사람에게 예약됨
                    if (!pawn.CanReserve(BBB.occupant))
                    {
                        yield return new FloatMenuOption(
                            $"{FloatMenuOptionLabel(BBB.occupant)} (" + "YR_Reserved".Translate() + ")", null,
                            MenuOptionPriority.DisabledOption);
                        yield break;
                    }

                    //폰 해방 시도
                    CompRemoveEffectBondageBed comp = ThingCompUtility.TryGetComp<CompRemoveEffectBondageBed>(parent);

                    if (comp != null)
                    {
                        void Action2()
                        {
                            TryReleasePrisoner(pawn, BBB.occupant);
                        }

                        string str2 = "YR_Release_BondageBed".Translate(BBB.occupant.Label);
                        yield return new FloatMenuOption(str2, Action2, MenuOptionPriority.DisabledOption);
                        yield break;
                    }
                }

                //사용자가 움직일 수 없을때
                if (!pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
                {
                    yield return new FloatMenuOption($"{FloatMenuOptionLabel(pawn)} (" + "Incapable".Translate() + ")", null,
                        MenuOptionPriority.DisabledOption);
                    yield break;
                }


                //사용자가 해당 작업이 비활성화
                if (pawn.WorkTagIsDisabled(WorkTypeDefOf.Warden.workTags))
                {
                    if (!CheckCanWorkHediff(pawn, out HediffDef canWorkHediffDefResult))
                    {
                        if (canWorkHediffDefResult != null)
                        {
                            yield return new FloatMenuOption($"YR_Forbid".Translate(canWorkHediffDefResult.label), null,
                            MenuOptionPriority.DisabledOption);
                        }
                        yield break;
                    }
                }

                //구속자가 없을 경우
                bool hasTarget = false;
                IEnumerable<Pawn> candidates = requestedTarget == null ? pawn.Map.mapPawns.AllPawns : new[] { requestedTarget };
                foreach (Pawn target in candidates)
                {
                    //타겟이 본인이거나, 타겟이 스폰되지 않았거나, 타겟이 이미 다른 가구에 묶여 구속 헤디프가 있는 경우
                    if (target == pawn || !target.Spawned || !AllowsTarget(target))
                    {
                        //if (Prefs.DevMode)
                        //{
                        //    yield return new FloatMenuOption($"Dev : {FloatMenuOptionLabel(target)} (" + $"{target.Label} is this pawn" + ")", null,MenuOptionPriority.DisabledOption);
                        //}
                        continue;
                    }

                    //타겟이 딴 침대에 묶여 움직일 수 없음
                    if (target.health.hediffSet.HasHediff(YR_RJW_DefOf.YR_MilkingMachineAddon_Hediff))
                    {
                        //if (Prefs.DevMode)
                        //{
                        //    yield return new FloatMenuOption($"Dev : {FloatMenuOptionLabel(target)} (" + $"{target.Label} is already bound" + ")", null, MenuOptionPriority.DisabledOption);
                        //}
                        continue;
                    }

                    //종족 리스트가 있을 경우, 목표 종족외엔 묶을 수 없음
                    if (!Props.races.NullOrEmpty() && !Props.races.Contains(target.def))
                    {
                        //if (Prefs.DevMode)
                        //{
                        //    yield return new FloatMenuOption($"Dev : {FloatMenuOptionLabel(target)} (" + $"{target.Label} is fobidden race" + ")", null, MenuOptionPriority.DisabledOption);
                        //}
                        continue;
                    }

                    //허가되지 않은 성별일 경우
                    if (Props.settingException.maleOnly)
                    {
                        if (target.gender != Gender.Male)
                        {
                            if (!Props.settingException.genderExceptionRaces.Contains(target.def))
                            {
                                continue;
                            }
                        }
                    }

                    if (Props.settingException.femaleOnly)
                    {
                        if (target.gender != Gender.Female)
                        {
                            if (!Props.settingException.genderExceptionRaces.Contains(target.def))
                            {
                                continue;
                            }
                        }
                    }

                    //폰이 비허가된 헤디프가 있을 경우
                    if (CheckForbiddenHediff(target, out HediffDef forbidfenHediffDef))
                    {
                        yield return new FloatMenuOption($"" + "YR_HasForbiddenHediffDef".Translate(target.Label, forbidfenHediffDef?.label ?? "??"), null,
                            MenuOptionPriority.DisabledOption);
                        continue;
                    }

                    hasTarget = true;

                    //목표가 침대에 도달할 수 없을때
                    if (!pawn.CanReserveAndReach(target, PathEndMode.Touch, Danger.Some) || !pawn.CanReserveAndReach(parent, PathEndMode.Touch, Danger.Some))
                    {
                        yield return new FloatMenuOption($"{FloatMenuOptionLabel(target)} (" + target.Label + " : " + "NoPath".Translate() + ")", null,
                            MenuOptionPriority.DisabledOption);
                        continue;
                    }

                    //타겟이 딴 사람에게 예약됨
                    if (!pawn.CanReserve(target))
                    {
                        yield return new FloatMenuOption(
                            $"{FloatMenuOptionLabel(target)} (" + "YR_Reserved".Translate(target.Label) + ")", null,
                            MenuOptionPriority.DisabledOption);
                    }

                    //타겟이 어른이 아님
                    //이 위로 넣기
                    //만약 타겟을 묶을 수 있는 경우
                    void Action()
                    {
                        TryStartUseJob(pawn, target);
                    }

                    string str = "YR_BondageBed".Translate(pawn.Named(pawn.Name.ToString()), target.Named(target.Name.ToString()));
                    yield return new FloatMenuOption(str, Action, MenuOptionPriority.DisabledOption);
                }

                //타겟이 아예 없는 경우
                if (!hasTarget)
                {
                    yield return new FloatMenuOption($"{FloatMenuOptionLabel(pawn)} (" + "YR_NoTarget".Translate() + ")", null,
                        MenuOptionPriority.DisabledOption);
                }

                //로컬 함수
                bool CheckForbiddenHediff(Pawn pawn, out HediffDef forbiddenHediffDefResult)
                {
                    foreach (HediffDef forbiddenHediffDef in from pawnHediff in pawn.health.hediffSet.hediffs
                                                             from forbiddenHediffDef in Props.forbiddenHediffDefs
                                                             where pawnHediff.def == forbiddenHediffDef
                                                             select forbiddenHediffDef)
                    {
                        forbiddenHediffDefResult = forbiddenHediffDef;
                        return true;
                    }
                    forbiddenHediffDefResult = null;
                    return false;
                }

                bool CheckCanWorkHediff(Pawn pawn, out HediffDef canWorkHediffDefResult)
                {
                    foreach (HediffDef canWorkHediffDef in from pawnHediff in pawn.health.hediffSet.hediffs
                                                           from canWorkHediffDef in Props.canWorkHediffDefs
                                                           where pawnHediff.def == canWorkHediffDef
                                                           select canWorkHediffDef)
                    {
                        canWorkHediffDefResult = canWorkHediffDef;
                        return true;
                    }
                    canWorkHediffDefResult = !Props.canWorkHediffDefs.NullOrEmpty() ? Props.canWorkHediffDefs[0] : null;
                    return false;
                }
            }
        }
    }
}

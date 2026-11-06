using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse.AI;
using Verse;

namespace YR_Hentai_Prime_AnimationBed
{
    public class CompAnimationBed : ThingComp
    {
        public float ContainmentStrength => parent.GetStatValue(StatDefOf.ContainmentStrength);

        public CompProperties_AnimationBed Props => (CompProperties_AnimationBed)props;

        protected Building_AnimationBed AnimationBed => (Building_AnimationBed)parent;

        public bool Available
        {
            get
            {
                return !AnimationBed.Occupied;
            }
        }

        public Pawn HeldPawn
        {
            get
            {
                return AnimationBed.HeldPawn;
            }
        }

        public ThingOwner Container
        {
            get
            {
                return AnimationBed.innerContainer;
            }
        }

        public void EjectContents()
        {
            AnimationBed.EjectContents();
        }

        private bool UsesWardenEscort => parent.def.defName is
            "YR_Building_Cumdump" or "YR_Building_GeneratorPod" or
            "YR_Building_GloryHole" or "YR_Building_GloryHole_Cask" or
            "YR_Building_GloryHole_Face" or "YR_Building_MilkingMachine";

        public bool AllowsEntry(Pawn target)=>parent.Spawned && target?.Spawned==true && target.Map==parent.Map &&
            AdultEligibility.IsAllowed(target) && UnsupportedRacesV8.OrdinaryHumanlike(target) && (target.Faction==Faction.OfPlayer || target.IsPrisonerOfColony) &&
            target.TryGetComp<CompAnimationBedTarget>()!=null && !target.TryGetComp<CompAnimationBedTarget>().CurrentlyHeldOnPlatform &&
            Condition.Match(target,AnimationBed,Props.condition,out _);
        public bool CanEnterSelf(Pawn target)=>Available && AllowsEntry(target) && target.Faction==Faction.OfPlayer && !target.IsPrisonerOfColony &&
            !target.Downed && !target.InMentalState && parent.Position.InAllowedArea(target) && target.CanReserveAndReach(parent,PathEndMode.Touch,Danger.Some);
        private void RequestEntry(Pawn target){
            if(!Available || !AllowsEntry(target))return;
            var assignment=target.TryGetComp<CompAnimationBedTarget>();
            if(assignment.targetHolder!=null && assignment.targetHolder!=parent){Messages.Message("이미 다른 시설에 지정되어 있습니다.",MessageTypeDefOf.RejectInput,false);return;}
            if(CanEnterSelf(target)){
                assignment.targetHolder=parent;
                var enter=JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("YR_EnterAnimationFacility"),parent);enter.count=1;
                if(!target.jobs.TryTakeOrderedJob(enter,JobTag.Misc))assignment.targetHolder=null;
                return;
            }
            var actor=parent.Map.mapPawns.FreeColonistsSpawned.FirstOrDefault(p=>p!=target && AdultEligibility.IsAllowed(p) && !p.Downed && !p.Drafted && !p.InMentalState &&
                p.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation) && !p.WorkTagIsDisabled(WorkTypeDefOf.Warden.workTags) &&
                (!UsesWardenEscort || (p.workSettings?.WorkIsActive(WorkTypeDefOf.Warden) ?? false)) &&
                p.CanReserveAndReach(target,PathEndMode.Touch,Danger.Some) && p.CanReserveAndReach(parent,PathEndMode.Touch,Danger.Some));
            if(actor==null){Messages.Message(UsesWardenEscort ? "교도관 업무가 활성화된 호송 가능 정착민과 접근 경로가 필요합니다." : "호송 가능한 정착민 또는 접근 경로가 필요합니다.",MessageTypeDefOf.RejectInput,false);return;}
            assignment.targetHolder=parent;
            var transport=JobMaker.MakeJob(YR_H_P_DefOf.YR_CarryToAnimationBed,parent,target);transport.count=1;
            if(!actor.jobs.TryTakeOrderedJob(transport,JobTag.Misc))assignment.targetHolder=null;
        }
        public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn pawn){
            foreach(var option in base.CompFloatMenuOptions(pawn))yield return option;
            if(CanEnterSelf(pawn))yield return new FloatMenuOption("스스로 들어가기",()=>RequestEntry(pawn));
        }
        public override IEnumerable<Gizmo> CompGetGizmosExtra(){
            foreach(var gizmo in base.CompGetGizmosExtra())yield return gizmo;
            if(!parent.Spawned)yield break;
            var command=new Command_Action{defaultLabel="이용 대상 지정",defaultDesc=UsesWardenEscort ? "정착민은 직접 입장하고 식민지 포로는 교도관 업무가 활성화된 정착민이 자동으로 호송합니다." : "정착민은 직접 입장하고 식민지 포로는 정착민이 호송합니다.",icon=ContentFinder<UnityEngine.Texture2D>.Get("UI/Commands/AssignEscort"),iconDrawScale=.85f,action=()=>{
                var options=new List<FloatMenuOption>();
                foreach(var candidate in parent.Map.mapPawns.AllPawnsSpawned.Where(AllowsEntry).OrderBy(p=>p.LabelShort)){
                    var target=candidate;options.Add(new FloatMenuOption(target.LabelShortCap,()=>RequestEntry(target)));
                }
                if(options.Count==0)options.Add(new FloatMenuOption("현재 이용 가능한 대상이 없습니다.",null));
                Find.WindowStack.Add(new FloatMenu(options));
            }};
            if(!Available)command.Disable("현재 사용 중입니다.");
            yield return command;
        }
    }
    public class JobDriver_EnterAnimationFacility : JobDriver {
        private CompAnimationBed Facility=>job.targetA.Thing?.TryGetComp<CompAnimationBed>();
        public override bool TryMakePreToilReservations(bool errorOnFailed)=>Facility?.CanEnterSelf(pawn)==true && pawn.Reserve(job.targetA,job,1,-1,null,errorOnFailed);
        protected override IEnumerable<Toil> MakeNewToils(){
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOn(()=>Facility==null || !Facility.CanEnterSelf(pawn) || pawn.TryGetComp<CompAnimationBedTarget>()?.targetHolder!=job.targetA.Thing);
            AddFinishAction(condition=>{var target=pawn.TryGetComp<CompAnimationBedTarget>();if(target!=null && pawn.Spawned && target.targetHolder==job.targetA.Thing)target.targetHolder=null;});
            yield return Toils_Goto.GotoThing(TargetIndex.A,PathEndMode.Touch);
            foreach(var toil in JobDriver_CarryToAnimationBed.ChainTakeeToPlatformToils(pawn,pawn,Facility,TargetIndex.A))yield return toil;
        }
    }
}

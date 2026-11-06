using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace YR_RJW
{
    public class SamokSpaPatient : IExposable
    {
        public Pawn patient;
        public IntVec3 cell;
        public int admittedAt;
        public int placementVersion;
        public void ExposeData()
        {
            Scribe_References.Look(ref patient, "patient");
            Scribe_Values.Look(ref cell, "cell");
            Scribe_Values.Look(ref admittedAt, "admittedAt");
            Scribe_Values.Look(ref placementVersion, "placementVersion", 0);
        }
    }

    public partial class Building_SamokSwimmingPool
    {
        public const int BathTicks = 5000;
        public CellRect PatientCells => WaterCells.ContractedBy(1);
        private List<SamokSpaPatient> patients = new List<SamokSpaPatient>();
        public IEnumerable<Pawn> Patients => patients.Where(r => r.patient != null).Select(r => r.patient);
        public IEnumerable<Pawn> ActiveUsers => Spawned && HasNutrition ? Map.mapPawns.AllPawnsSpawned.Where(IsUsing) : Enumerable.Empty<Pawn>();
        public bool IsUsing(Pawn p)
            => p != null && Spawned && HasNutrition && p.Spawned && p.Map == Map && WaterCells.Contains(p.Position) && Adult(p) &&
               ((p.jobs?.curDriver is JobDriver_SamokSwimming swim && swim.Pool == this && swim.IsSwimming) ||
                (p.jobs?.curDriver is JobDriver_SamokBath bath && bath.Pool == this && bath.IsBathing) ||
                patients.Any(r => r.patient == p && r.cell == p.Position && p.Downed));
        public static bool Adult(Pawn p) => p != null && !p.Dead && p.RaceProps.Humanlike && LegacyEligibility.Allows(p) &&
            p.ageTracker.AgeBiologicalYearsFloat >= 18f && p.def.defName.IndexOf("maggot", System.StringComparison.OrdinalIgnoreCase) < 0;
        public bool HasPlace(Pawn user)
            => Spawned && patients.Count(r => r.patient != user) + Map.mapPawns.AllPawnsSpawned.Count(p => p != user &&
                p.CurJob?.targetA.Thing == this && (p.CurJobDef.defName == "YR_SamokSwim" || p.CurJobDef.defName == "YR_SamokBath" ||
                (p.CurJobDef.defName == "YR_SamokCarryToSpa" && p.CurJob.targetB.Pawn != user && !Patients.Contains(p.CurJob.targetB.Pawn)))) < Capacity;
        public bool ReservedWaterCell(IntVec3 c, Pawn user)
            => patients.Any(r => r.patient != user && r.cell == c) || Map.mapPawns.AllPawnsSpawned.Any(p => p != user &&
                p.CurJob?.targetA.Thing == this && (p.CurJobDef.defName == "YR_SamokCarryToSpa" ? p.CurJob.targetC.Cell : p.CurJob.targetB.Cell) == c);
        public Job BathJob(Pawn p)
        {
            if (!CanSwim(p)) return null;
            IntVec3 cell = NextCell(p);
            return cell.IsValid ? JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("YR_SamokBath"), this, cell) : null;
        }
        public bool CanTransport(Pawn actor, Pawn patient)
            => Spawned && actor != null && actor.IsColonistPlayerControlled && !actor.Downed && !actor.Drafted && actor.Spawned && actor.Map == Map &&
               Adult(actor) && !actor.WorkTypeIsDisabled(WorkTypeDefOf.Hauling) && actor.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation) &&
               Adult(patient) && patient.Downed && patient.Spawned && patient.Map == Map && patient.Faction == Faction.OfPlayer &&
               !patient.IsForbidden(actor) && !this.IsForbidden(actor) && actor.CanReserveAndReach(patient, PathEndMode.ClosestTouch, Danger.Some);
        public Job CarryJob(Pawn actor, Pawn patient, bool release = false)
        {
            if (!CanTransport(actor, patient) || (!release && (!HasNutrition || Patients.Contains(patient) || !HasPlace(actor)))) return null;
            IntVec3 cell = release ? ExitCell(actor) : PatientCells.Cells.Where(c => SafeCell(actor, c) && actor.CanReserve(c) && actor.CanReach(c, PathEndMode.OnCell, Danger.None)).DefaultIfEmpty(IntVec3.Invalid).First();
            if (!cell.IsValid) return null;
            Job transport = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed(release ? "YR_SamokRetrievePatient" : "YR_SamokCarryToSpa"), this, patient, cell);
            transport.count = 1;
            return transport;
        }
        public bool AdmitPatient(Pawn patient)
        {
            if (!Spawned || !HasNutrition || !Adult(patient) || !patient.Spawned || patient.Map != Map || !patient.Downed || !WaterCells.Contains(patient.Position) || !HasPlace(patient)) return false;
            patients.RemoveAll(r => r.patient == patient);
            patients.Add(new SamokSpaPatient { patient = patient, cell = patient.Position, admittedAt = Find.TickManager.TicksGame, placementVersion = 1 });
            return true;
        }
        // Old saves used the first corner water cell. Move only an assigned, still-downed patient
        // into an available central cell of this same pool; retry safely if all four are occupied.
        public void MigratePatientPlacement()
        {
            if (!Spawned) return;
            foreach (SamokSpaPatient record in patients)
            {
                if (record.placementVersion >= 1) continue;
                Pawn p = record.patient;
                if (!Adult(p) || !p.Downed || !p.Spawned || p.Map != Map || p.Position != record.cell || !WaterCells.Contains(p.Position)) continue;
                if (PatientCells.Contains(record.cell)) { record.placementVersion = 1; continue; }
                IntVec3 central = PatientCells.Cells.Where(c => SafeCell(p, c) &&
                    Map.reachability.CanReach(p.Position, c, PathEndMode.OnCell, TraverseParms.For(p, Danger.None)))
                    .DefaultIfEmpty(IntVec3.Invalid).First();
                if (!central.IsValid) continue;
                p.Position = central;
                p.Notify_Teleported(endCurrentJob: false);
                record.cell = central;
                record.placementVersion = 1;
            }
        }
        public void ForgetPatient(Pawn patient) => patients.RemoveAll(r => r.patient == patient);
        public IntVec3 ExitCell(Pawn p)
            => GenRadial.RadialCellsAround(Position, 12f, true).Where(c => c.InBounds(Map) && !this.OccupiedRect().Contains(c) &&
                c.Standable(Map) && !c.IsForbidden(p) && !c.GetThingList(Map).OfType<Pawn>().Any() && p.CanReach(c, PathEndMode.OnCell, Danger.Some))
                .DefaultIfEmpty(IntVec3.Invalid).First();
        public bool ReleasePatient(Pawn p)
        {
            if (p == null || !p.Spawned || p.Map != Map) { ForgetPatient(p); return true; }
            IntVec3 exit = ExitCell(p);
            if (!exit.IsValid) return false;
            p.pather?.StopDead();
            p.Position = exit;
            p.Notify_Teleported();
            ForgetPatient(p);
            return true;
        }
        protected override void Tick()
        {
            base.Tick();
            if (!Spawned) return;
            if(!HasNutrition){
                if(this.IsHashIntervalTick(60))foreach(Pawn p in Patients.ToList())ReleasePatient(p);
                return;
            }
            MigratePatientPlacement();
            foreach (SamokSpaPatient record in patients.ToList())
            {
                Pawn p = record.patient;
                if (p == null || !p.Spawned || p.Map != Map || p.Dead || p.Position != record.cell) { patients.Remove(record); continue; }
                if (p.needs?.food != null && p.needs.food.CurLevelPercentage < 0.10f) { ForgetPatient(p); Messages.Message(p.LabelShort + ": 식사가 필요하여 영양액 요양을 중단합니다.", p, MessageTypeDefOf.CautionInput); continue; }
                if (!Adult(p) || !p.Downed)
                    ReleasePatient(p);
            }
        }
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref patients, "samokSpaPatients", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && patients == null) patients = new List<SamokSpaPatient>();
        }
        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            foreach (Pawn p in Patients.ToList()) ReleasePatient(p);
            patients.Clear(); // Pawns remain independently spawned even if every dry exit is blocked.
            base.DeSpawn(mode);
        }
        public IEnumerable<FloatMenuOption> SpaOptions(Pawn actor)
        {
            if (CanSwim(actor)) yield return new FloatMenuOption("영양액에 몸 담그기", () => { Job j = BathJob(actor); if (j != null) actor.jobs.TryTakeOrderedJob(j, JobTag.Misc); });
            foreach (Pawn p in Map.mapPawns.AllPawnsSpawned.Where(p => CanTransport(actor, p)).ToList())
            {
                Pawn patient = p;
                bool release = Patients.Contains(patient);
                Job j = CarryJob(actor, patient, release);
                if (j != null) yield return new FloatMenuOption(patient.LabelShort + (release ? ": 영양액 웅덩이에서 데려오기" : ": 영양액 웅덩이로 옮기기"), () => {
                    Job current = CarryJob(actor, patient, release);
                    if (current != null) actor.jobs.TryTakeOrderedJob(current, JobTag.Misc);
                });
            }
        }
    }

    public class JobDriver_SamokBath : JobDriver
    {
        private bool bathing;
        private int ticks;
        public Building_SamokSwimmingPool Pool => job.targetA.Thing as Building_SamokSwimmingPool;
        public bool IsBathing => bathing && !ended && Pool != null && Pool.HasNutrition && pawn.Spawned && pawn.Map == Pool.Map && Pool.WaterCells.Contains(pawn.Position) && Building_SamokSwimmingPool.Eligible(pawn);
        public override void ExposeData() { base.ExposeData(); Scribe_Values.Look(ref bathing, "bathing"); Scribe_Values.Look(ref ticks, "bathTicks"); }
        public override bool TryMakePreToilReservations(bool errorOnFailed) => Pool != null && Pool.CanSwim(pawn) &&
            pawn.Reserve(Pool, job, 4, 0, null, errorOnFailed) && pawn.Reserve(job.targetB, job, 1, -1, null, errorOnFailed);
        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOn(() => Pool==null || !Pool.HasNutrition || !Building_SamokSwimmingPool.Eligible(pawn));
            AddFinishAction(_ => { bathing = false; pawn.pather?.StopDead(); });
            yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);
            Toil bath = ToilMaker.MakeToil("SamokBath");
            bath.defaultCompleteMode = ToilCompleteMode.Never;
            bath.initAction = () => { bathing = true; pawn.pather.StopDead(); };
            bath.tickAction = () => { if (!IsBathing) EndJobWith(JobCondition.Incompletable); else if (++ticks >= Building_SamokSwimmingPool.BathTicks) EndJobWith(JobCondition.Succeeded); };
            yield return bath;
        }
    }
    public class JobDriver_SamokCarryPatient : JobDriver
    {
        public Building_SamokSwimmingPool Pool => job.targetA.Thing as Building_SamokSwimmingPool;
        public Pawn Patient => job.targetB.Pawn;
        private bool Release => job.def.defName == "YR_SamokRetrievePatient";
        public override bool TryMakePreToilReservations(bool errorOnFailed) => Pool != null && Pool.CanTransport(pawn, Patient) &&
            (Release || (Pool.HasNutrition && Pool.HasPlace(pawn))) && pawn.Reserve(Patient, job, 1, -1, null, errorOnFailed) &&
            pawn.Reserve(job.targetC, job, 1, -1, null, errorOnFailed) && (Release || pawn.Reserve(Pool, job, 4, 0, null, errorOnFailed));
        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOnDestroyedOrNull(TargetIndex.B);
            this.FailOn(() => Pool==null || (!Release && !Pool.HasNutrition) || !Building_SamokSwimmingPool.Adult(Patient) || !Patient.Downed || pawn.Downed);
            AddFinishAction(_ => {
                if (pawn.carryTracker.CarriedThing == Patient)
                    pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out var dropped);
            });
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch).FailOnDespawnedNullOrForbidden(TargetIndex.B);
            yield return Toils_Haul.StartCarryThing(TargetIndex.B);
            yield return Toils_Goto.GotoCell(TargetIndex.C, PathEndMode.OnCell).FailOn(() => pawn.carryTracker.CarriedThing != Patient);
            yield return Toils_General.Do(() => {
                IntVec3 cell = job.targetC.Cell;
                if ((Release ? !cell.Standable(pawn.Map) : !Pool.SafeCell(pawn, cell)) ||
                    !pawn.carryTracker.TryDropCarriedThing(cell, ThingPlaceMode.Direct, out var dropped)) { EndJobWith(JobCondition.Incompletable); return; }
                if (Release) Pool.ForgetPatient(Patient);
                else if (!Pool.AdmitPatient(Patient)) Pool.ReleasePatient(Patient);
            });
        }
    }
}







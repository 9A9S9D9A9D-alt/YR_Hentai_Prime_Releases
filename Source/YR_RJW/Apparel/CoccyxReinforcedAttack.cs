using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace YR_RJW
{
    public sealed partial class HediffComp_CoccyxCombat
    {
        internal const int ReinforcedLimbCount = 4;
        internal const int ReinforcedWarmup = 12;
        internal const int ReinforcedPulseTicks = 10;
        internal const int ReinforcedImpact = 4;
        internal const int BarrageRounds = 6;
        private const int BarrageStrikeSpacing = 5;
        private const int BarrageRoundSpacing = ReinforcedLimbCount * BarrageStrikeSpacing;
        private static readonly int[] SalvoOffsets = { 0, 3, 1, 4 };
        private static readonly int[] BarrageOrder = { 0, 2, 1, 3 };
        private const int ReinforcedCycleTicks = CycleTicks * 13 / 10;
        private const float ReinforcedDamage = 10f;
        private Pawn reinforcedTarget;
        private int reinforcedHits;
        private bool reinforcedSequential;
        private List<Pawn> reinforcedHitTargets = new List<Pawn>();

        internal bool Type2Active => Pawn.apparel?.WornApparel.Any(item => item.def.defName == "YR_AP_YuranBioArmor") == true;

        internal float Type2AttackRange => sequentialAttack ? Type2SequentialRange : Type2Range;
        internal int ReinforcedRepeatGap => reinforcedSequential ? BarrageRoundSpacing : 0;

        private void ExposeReinforcedAttack()
        {
            Scribe_References.Look(ref reinforcedTarget, "reinforcedAttackTarget");
            Scribe_Values.Look(ref reinforcedHits, "reinforcedAttackHits");
            Scribe_Values.Look(ref reinforcedSequential, "reinforcedAttackSequential");
            Scribe_Collections.Look(ref reinforcedHitTargets, "reinforcedAttackHitTargets", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                reinforcedHitTargets ??= new List<Pawn>();
                if (reinforcedHits > 0 && reinforcedTarget != null && !reinforcedHitTargets.Contains(reinforcedTarget))
                    reinforcedHitTargets.Add(reinforcedTarget);
                reinforcedTarget = null;
                reinforcedHits = 0;
            }
        }

        private void TryStartReinforcedAttack(int now)
        {
            int occupied = ThrowOccupiedLimbs;
            if (occupied >= ReinforcedLimbCount || (sequentialAttack && occupied > 0)
                || limbs.Any(limb => limb.Active || now < limb.nextReady))
                return;

            nextSearch = now + 15;
            var targets = Viable(focus, true) ? new[] { focus } : Pawn.Map.mapPawns.AllPawnsSpawned
                .Where(pawn => Viable(pawn, false))
                .OrderBy(pawn => pawn.Position.DistanceToSquared(Pawn.Position))
                .Take(sequentialAttack ? 1 : ReinforcedLimbCount - occupied)
                .ToArray();
            if (targets.Length == 0)
                return;

            reinforcedSequential = sequentialAttack;
            reinforcedHitTargets.Clear();
            for (int index = occupied; index < ReinforcedLimbCount; index++)
            {
                var limb = limbs[index];
                var target = targets[(index - occupied) % targets.Length];
                limb.target = target;
                limb.endpoint = target.DrawPos;
                limb.started = now + (reinforcedSequential ? BarrageOrder[index] * BarrageStrikeSpacing : SalvoOffsets[index]);
                limb.nextReady = now + ReinforcedCycleTicks;
                limb.reinforced = true;
                limb.reinforcedPulses = 0;
                limb.remoteFluid = false;
                limb.continuous = false;
                limb.impacted = false;
                limb.swung = false;
                limb.electric = ElectricActive;
                limb.strikeWidth = 0f;
                limb.strikeBend = 0f;
                limb.trajectoryPattern = 0;
            }
        }

        private void TickReinforcedAttack(int now)
        {
            if (reinforcedHitTargets.Count == 0 && !limbs.Any(limb => limb.reinforced))
                return;

            float range = reinforcedSequential ? Type2SequentialRange : Type2Range;
            int pulseCount = reinforcedSequential ? BarrageRounds : 1;
            foreach (var limb in limbs)
            {
                if (!limb.reinforced)
                    continue;

                var target = limb.target;
                int elapsed = now - limb.started - ReinforcedWarmup;
                bool available = target?.Spawned == true && !target.Dead && target.Map == Pawn.Map;
                if (available)
                    limb.endpoint = target.DrawPos;
                while (limb.reinforcedPulses < pulseCount)
                {
                    int pulseElapsed = elapsed - limb.reinforcedPulses * BarrageRoundSpacing;
                    if (pulseElapsed < 0)
                        break;
                    if (reinforcedSequential && !limb.swung)
                    {
                        limb.swung = true;
                        CoccyxAttackSound.Play(Pawn, "BarrageSwing");
                    }
                    if (pulseElapsed < ReinforcedImpact)
                        break;

                    limb.reinforcedPulses++;
                    limb.swung = false;
                    if (!available || target.Dead || !target.Spawned)
                        continue;

                    int distanceSquared = target.Position.DistanceToSquared(Pawn.Position);
                    if (distanceSquared > range * range || !GenSight.LineOfSight(Pawn.Position, target.Position, Pawn.Map))
                        continue;

                    bool finishing = reinforcedSequential && limb.reinforcedPulses == BarrageRounds
                        && limb == limbs[ReinforcedLimbCount - 1];
                    float damage = finishing
                        ? distanceSquared switch { <= 25 => 10f, <= 36 => 9f, <= 49 => 8f, _ => 7f }
                        : reinforcedSequential
                        ? distanceSquared switch
                        {
                            <= 25 => 2f,
                            <= 36 => 1.75f,
                            <= 49 => 1.5f,
                            _ => 1.25f
                        }
                        : ReinforcedDamage;
                    var hitPosition = target.DrawPos;
                    string impactCue = finishing ? "BarrageFinish" : reinforcedSequential ? "BarrageImpact" : "Type2Impact";
                    Attempts++;
                    if (strike.Resolve(Pawn, target, damage, limb.electric, reinforcedSequential ? 0.55f : 0.50f, impactCue))
                    {
                        Hits++;
                        if (reinforcedSequential)
                            ShowBarrageImpact(hitPosition, finishing);
                        if (!reinforcedHitTargets.Contains(target))
                            reinforcedHitTargets.Add(target);
                    }
                }

                if ((!available && !reinforcedSequential) || elapsed >= (pulseCount - 1) * BarrageRoundSpacing + ReinforcedPulseTicks)
                {
                    limb.reinforced = false;
                    limb.started = -1;
                    limb.target = null;
                }
            }

            if (limbs.Any(limb => limb.reinforced))
                return;

            foreach (var target in reinforcedHitTargets)
                if (target != null)
                    CoccyxReinforcedFlyer.Launch(Pawn, target, 4);
            reinforcedHitTargets.Clear();
        }

        private void ShowBarrageImpact(Vector3 hitPosition, bool finishing)
        {
            var map = Pawn.Map;
            var direction = (hitPosition - Pawn.DrawPos).WithY(0f).normalized;
            float angle = direction.AngleFlat();
            var contact = hitPosition - direction * 0.13f;
            var flash = FleckMaker.GetDataStatic(contact, map, DefDatabase<FleckDef>.GetNamed("BodyImpact"), finishing ? 1.25f : 0.66f);
            flash.rotation = angle;
            flash.instanceColor = new Color(1f, 0.52f, 0.9f, 1f);
            map.flecks.CreateFleck(flash);

            var burst = FleckMaker.GetDataStatic(hitPosition, map, FleckDefOf.AirPuff, finishing ? 1.15f : 0.5f);
            burst.rotation = angle;
            burst.velocityAngle = angle;
            burst.velocitySpeed = finishing ? 3.5f : 2.5f;
            burst.instanceColor = new Color(0.88f, 0.57f, 0.85f, 0.86f);
            map.flecks.CreateFleck(burst);
            if (finishing)
                FleckMaker.ThrowDustPuff(hitPosition, map, 1.1f);
        }
    }
    public sealed class CoccyxReinforcedFlyer : PawnFlyer
    {
        private const float CollisionDamage = 30f;
        private const float ThrowFlightSpeed = 30f;
        private float thrownDamage;
        private float splashRadius;
        private IntVec3 obstacle = IntVec3.Invalid;
        private IntVec3 checkedCell = IntVec3.Invalid;
        private IntVec3 lastSafeCell = IntVec3.Invalid;
        private Pawn attacker;
        private Pawn struckPawn;

        public override Vector3 DrawPos
        {
            get
            {
                if (thrownDamage <= 0f)
                    return base.DrawPos;
                var cargo = FlyingThing;
                if (cargo == null)
                    return Position.ToVector3Shifted();
                var ground = ThrowGroundPosition(cargo, ticksFlying, out float height);
                return ground + Altitudes.AltIncVect * height + Vector3.forward * (0.12f * height);
            }
        }

        private Vector3 ThrowGroundPosition(Thing cargo, int tick, out float height)
        {
            float progress = Mathf.Clamp01(tick / (float)Mathf.Max(1, ticksFlightTime));
            height = 4f * progress * (1f - progress);
            var destination = GenThing.TrueCenter(FlightFields.Destination(this), cargo.Rotation, cargo.def.size, cargo.def.Altitude);
            return Vector3.Lerp(FlightFields.Start(this), destination, progress);
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            if (thrownDamage > 0f && !respawningAfterLoad)
                ticksFlightTime = Mathf.Max(1, (FlightFields.Distance(this) / ThrowFlightSpeed).SecondsToTicks());
        }

        public override void DynamicDrawPhaseAt(DrawPhase phase, Vector3 drawLoc, bool flip = false)
        {
            if (thrownDamage <= 0f)
            {
                base.DynamicDrawPhaseAt(phase, drawLoc, flip);
                return;
            }

            var cargo = FlyingThing;
            if (cargo is Pawn || cargo is Corpse { InnerPawn: not null })
                cargo.DynamicDrawPhaseAt(phase, drawLoc, flip);
            else if (phase == DrawPhase.Draw && cargo is not null and not Corpse)
                cargo.Graphic?.Draw(drawLoc, cargo.Rotation, cargo);
        }

        private static class FlightFields
        {
            internal static readonly AccessTools.FieldRef<PawnFlyer, Vector3> Start =
                AccessTools.FieldRefAccess<PawnFlyer, Vector3>("startVec");
            internal static readonly AccessTools.FieldRef<PawnFlyer, IntVec3> Destination =
                AccessTools.FieldRefAccess<PawnFlyer, IntVec3>("destCell");
            internal static readonly AccessTools.FieldRef<PawnFlyer, float> Distance =
                AccessTools.FieldRefAccess<PawnFlyer, float>("flightDistance");
        }

        internal static void Launch(Pawn source, Pawn target, int distance)
        {
            if (source?.Spawned != true || target?.Spawned != true || target.Dead
                || target.Map != source.Map || Hediff_CoccyxEscort.IsHeld(target))
                return;

            var map = source.Map;
            var start = target.Position;
            var direction = (start - source.Position).ToVector3().normalized;
            if (direction.sqrMagnitude < 0.01f)
                direction = source.Rotation.FacingCell.ToVector3();
            var end = (start.ToVector3Shifted() + direction * distance).ToIntVec3();
            var landing = start;
            var obstacle = IntVec3.Invalid;
            foreach (var cell in GenSight.PointsOnLineOfSight(start, end))
            {
                if (cell == start)
                    continue;
                if (!cell.InBounds(map))
                    break;
                if (CollisionBuildingAt(cell, map, false) != null)
                {
                    obstacle = cell;
                    break;
                }
                if (!cell.Walkable(map))
                    break;
                landing = cell;
            }

            if (landing == start && !obstacle.IsValid)
                return;

            var flyer = (CoccyxReinforcedFlyer)MakeFlyer(ThingDef.Named("YR_CoccyxReinforcedFlyer"), target, landing, null, null);
            flyer.obstacle = obstacle;
            flyer.attacker = source;
            GenSpawn.Spawn(flyer, start, map);
        }

        internal static bool Throw(Pawn source, Thing cargo, IntVec3 end, System.Action releaseGrip)
        {
            if (source?.Spawned != true || cargo == null || cargo.Destroyed || !end.InBounds(source.Map))
                return false;

            var map = source.Map;
            var start = source.Position;
            if (cargo is Pawn pawn)
            {
                if (!pawn.Spawned || pawn.Dead || pawn.Map != map || Hediff_CoccyxEscort.Get(pawn)?.escort != source
                    || releaseGrip == null || !pawn.Position.Walkable(map))
                    return false;
                start = pawn.Position;
            }
            else if (cargo.holdingOwner == null || cargo.holdingOwner != source.inventory?.innerContainer)
            {
                return false;
            }

            if (start == end)
                return false;

            float mass = HediffComp_CoccyxCombat.ThrowMass(cargo);
            var def = ThingDef.Named("YR_CoccyxReinforcedFlyer");
            CoccyxReinforcedFlyer flyer;
            if (cargo is Pawn pawnCargo)
            {
                releaseGrip();
                flyer = (CoccyxReinforcedFlyer)MakeFlyer(def, pawnCargo, end, null, null);
            }
            else
            {
                flyer = (CoccyxReinforcedFlyer)ThingMaker.MakeThing(def);
                FlightFields.Start(flyer) = GenThing.TrueCenter(start, cargo.Rotation, cargo.def.size, cargo.def.Altitude);
                FlightFields.Destination(flyer) = end;
                FlightFields.Distance(flyer) = start.DistanceTo(end);
                flyer.Rotation = cargo.Rotation;
                if (!cargo.holdingOwner.TryTransferToContainer(cargo, flyer.GetDirectlyHeldThings(), false))
                {
                    flyer.Destroy();
                    return false;
                }
            }

            flyer.thrownDamage = Mathf.Clamp(Mathf.Round(mass * 0.3f), 5f, 40f);
            flyer.splashRadius = mass < 20f ? 0f : mass < 60f ? 1f : mass < 110f ? 2f : 3f;
            flyer.checkedCell = start;
            flyer.lastSafeCell = start;
            flyer.attacker = source;
            GenSpawn.Spawn(flyer, start, map);
            PlayThrowSound(DefDatabase<SoundDef>.GetNamed("YR_Coccyx_ThrowRelease"), start, map, 1f);
            return true;
        }

        private static Building CollisionBuildingAt(IntVec3 cell, Map map, bool includeTurrets)
        {
            if (includeTurrets)
            {
                var turret = cell.GetThingList(map).OfType<Building_Turret>().FirstOrDefault();
                if (turret != null)
                    return turret;
            }

            var building = cell.GetEdifice(map);
            if (building is Building_Door door)
                return door.Open ? null : door;
            return building?.def.passability == Traversability.Impassable ? building : null;
        }

        protected override void TickInterval(int delta)
        {
            if (thrownDamage <= 0f)
            {
                base.TickInterval(delta);
                return;
            }

            var cargo = FlyingThing;
            if (cargo == null)
            {
                Destroy();
                return;
            }

            var map = Map;
            var end = FlightFields.Destination(this);
            int nextTick = Mathf.Min(ticksFlying + delta, ticksFlightTime);
            var nextCell = ThrowGroundPosition(cargo, nextTick, out _).ToIntVec3();
            foreach (var cell in GenSight.PointsOnLineOfSight(checkedCell, nextCell))
            {
                if (cell == checkedCell)
                    continue;
                if (!cell.InBounds(map))
                {
                    FinishThrow();
                    return;
                }
                if (CollisionBuildingAt(cell, map, true) != null)
                {
                    obstacle = cell;
                    FinishThrow();
                    return;
                }
                if (!cell.Walkable(map))
                {
                    FinishThrow();
                    return;
                }

                var pawns = cell.GetThingList(map).OfType<Pawn>();
                struckPawn = pawns.FirstOrDefault(pawn => pawn != attacker && !pawn.Dead
                    && attacker != null && pawn.HostileTo(attacker));
                if (struckPawn != null)
                {
                    obstacle = cell;
                    FinishThrow();
                    return;
                }

                if (!pawns.Any())
                    lastSafeCell = cell;
            }

            checkedCell = nextCell;
            ticksFlying = nextTick;
            if (nextTick >= ticksFlightTime || checkedCell == end)
                FinishThrow();
            else
                Position = nextCell;
        }

        private void FinishThrow()
        {
            FlightFields.Destination(this) = lastSafeCell;
            RespawnPawn();
            Destroy();
        }

        protected override void RespawnPawn()
        {
            var cargo = FlyingThing;
            if (cargo == null)
                return;

            var target = FlyingPawn;
            var map = Map;
            var landing = DestinationPos;
            var landingCell = landing.ToIntVec3();
            if (target != null)
                base.RespawnPawn();
            else if (GetDirectlyHeldThings().TryDrop(cargo, landingCell, map, ThingPlaceMode.Near,
                         out var placed, playDropSound: false) && placed?.Spawned == true)
                landingCell = placed.Position;
            if (GetDirectlyHeldThings().Count > 0)
                GetDirectlyHeldThings().TryDropAll(landingCell, map, ThingPlaceMode.Near, playDropSound: false);

            if (thrownDamage > 0f)
            {
                var landingSound = cargo is Pawn
                    ? DefDatabase<SoundDef>.GetNamed(IsMetalCargo(cargo) ? "ChunkSlag_Drop" : "Corpse_Drop")
                    : cargo.def.soundDrop;
                PlayThrowSound(landingSound, landingCell, map, obstacle.IsValid ? 1.2f : 1.9f);
                if (!obstacle.IsValid && target == null)
                    FleckMaker.ThrowDustPuff(landingCell.ToVector3Shifted(), map, 0.75f);
            }

            if (struckPawn?.Spawned == true && !struckPawn.Dead && struckPawn.Map == map && struckPawn.Position == obstacle)
            {
                ShowImpact(obstacle.ToVector3Shifted(), map, cargo);
                struckPawn.TakeDamage(new DamageInfo(DamageDefOf.Blunt, thrownDamage, 0.5f, -1f, attacker));
                if (target?.Spawned == true && !target.Dead)
                    target.TakeDamage(new DamageInfo(DamageDefOf.Blunt, thrownDamage, 0.5f, -1f, attacker));
                ApplySplash(map, target);
            }
            else if (obstacle.IsValid && CollisionBuildingAt(obstacle, map, thrownDamage > 0f) is Building building)
            {
                var contact = obstacle.ToVector3Shifted();
                ShowImpact(thrownDamage > 0f ? contact : Vector3.Lerp(landing, contact, 0.5f), map, cargo);
                if (target?.Spawned == true && !target.Dead)
                    target.TakeDamage(new DamageInfo(DamageDefOf.Blunt,
                        thrownDamage > 0f ? thrownDamage * 1.5f : CollisionDamage, 0.75f, -1f, attacker));
                if (thrownDamage > 0f && building.Spawned)
                    building.TakeDamage(new DamageInfo(DamageDefOf.Blunt, thrownDamage * 3f, 0f, -1f, attacker));
            }
        }

        private void ApplySplash(Map map, Pawn thrownPawn)
        {
            if (splashRadius <= 0f || attacker == null)
                return;

            var nearby = map.mapPawns.AllPawnsSpawned.Where(pawn => pawn != struckPawn && pawn != thrownPawn
                && !pawn.Dead && pawn.HostileTo(attacker)
                && obstacle.DistanceToSquared(pawn.Position) <= splashRadius * splashRadius
                && GenSight.LineOfSight(obstacle, pawn.Position, map)).ToArray();
            foreach (var pawn in nearby)
            {
                float falloff = obstacle.DistanceTo(pawn.Position) / splashRadius;
                float damage = Mathf.Max(1f, Mathf.Round(thrownDamage * Mathf.Lerp(0.5f, 0.25f, falloff)));
                pawn.TakeDamage(new DamageInfo(DamageDefOf.Blunt, damage, 0.5f, -1f, attacker));
            }
        }

        private static bool IsMetalCargo(Thing cargo) => cargo.def.soundDrop?.defName is "ChunkSlag_Drop" or "Metal_Drop"
            || (cargo as Pawn ?? (cargo as Corpse)?.InnerPawn)?.RaceProps.IsMechanoid == true;

        private static void PlayThrowSound(SoundDef sound, IntVec3 cell, Map map, float volume)
        {
            var info = SoundInfo.InMap(new TargetInfo(cell, map));
            info.volumeFactor = volume;
            sound?.PlayOneShot(info);
        }

        private void ShowImpact(Vector3 contact, Map map, Thing cargo)
        {
            FleckMaker.ThrowDustPuff(contact, map, 1.8f);
            if (thrownDamage > 0f)
            {
                string soundName = IsMetalCargo(cargo) ? "MeleeHit_Metal_Blunt"
                    : cargo is Pawn or Corpse ? "Pawn_Melee_Punch_HitPawn" : "MeleeHit_Stone";
                PlayThrowSound(DefDatabase<SoundDef>.GetNamed(soundName), obstacle, map, 1.5f);
            }
            else if (attacker?.Spawned == true && attacker.Map == map)
                CoccyxAttackSound.Play(attacker, "Impact");
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref obstacle, "reinforcedCollisionCell", IntVec3.Invalid);
            Scribe_Values.Look(ref checkedCell, "reinforcedThrowCheckedCell", IntVec3.Invalid);
            Scribe_Values.Look(ref lastSafeCell, "reinforcedThrowSafeCell", IntVec3.Invalid);
            Scribe_References.Look(ref attacker, "reinforcedAttacker");
            Scribe_References.Look(ref struckPawn, "reinforcedThrownStruckPawn");
            Scribe_Values.Look(ref thrownDamage, "reinforcedThrownDamage");
            Scribe_Values.Look(ref splashRadius, "reinforcedThrowSplashRadius");
        }
    }

    internal sealed partial class CoccyxTentacleDrawing
    {
        private const string ReinforcedTexture = "Yuran/Implants/CoccyxTentacles/ReinforcedShaft";
        private const int BarrageWindup = 4;
        private const int BarrageRecoilTicks = 9;
        internal const int BarrageRecoilDrawPadding = 6;
        private Material reinforcedShaft;
        private Material reinforcedAfterimage;
        private static readonly int[] ReinforcedSharedPoseIndices = { 0, 2, 3, 5 };
        // Distances behind and across the attack direction, in map cells.
        private static readonly Vector2[,] BarrageRecoilOffsets =
        {
            { new Vector2(2.9f, 3.1f), new Vector2(3.9f, 0.85f) },
            { new Vector2(3.8f, -0.8f), new Vector2(2.9f, -3f) },
            { new Vector2(1.9f, 0.97f), new Vector2(2.9f, 3.1f) },
            { new Vector2(1.95f, -0.96f), new Vector2(1.25f, -3.75f) }
        };

        private Material ReinforcedMaterial => reinforcedShaft ??= MaterialPool.MatFrom(ReinforcedTexture, ShaderDatabase.Cutout);

        private static float ReinforcedExtension(int elapsed, int repeatGap)
        {
            int duration = HediffComp_CoccyxCombat.ReinforcedPulseTicks;
            int impact = HediffComp_CoccyxCombat.ReinforcedImpact;
            if (elapsed < 0)
                return 0f;
            if (repeatGap > 0)
            {
                int lastRound = HediffComp_CoccyxCombat.BarrageRounds - 1;
                int round = elapsed / repeatGap;
                if (round > lastRound)
                    return 0f;
                int pulseTick = elapsed - round * repeatGap;
                if (pulseTick <= impact)
                    return Smooth(pulseTick / (float)impact);
                return round == lastRound ? 1f : 1f - Smooth((pulseTick - impact) / (float)BarrageRecoilTicks);
            }
            if (elapsed >= duration)
                return 0f;

            int phase = elapsed % duration;
            return phase <= impact ? Smooth(phase / (float)impact)
                : 1f - Smooth((phase - impact) / (float)(duration - impact));
        }

        internal static Vector3 BarrageRecoilPosition(Vector3 root, Vector3 direction, int index, int round)
        {
            bool finisher = index == HediffComp_CoccyxCombat.ReinforcedLimbCount - 1
                && round == HediffComp_CoccyxCombat.BarrageRounds - 1;
            var offset = finisher ? BarrageRecoilOffsets[1, 0] : BarrageRecoilOffsets[index, round % 2];
            return root - direction * offset.x + Vector3.Cross(direction, Vector3.up) * offset.y;
        }

        private static Vector3 BarrageStrikePoint(Vector3 origin, Vector3 tip, Vector3 facing, float scale, float along)
        {
            const float bendStart = 0.64f;
            const float bendEnd = 0.78f;
            float foldLength = 0.72f * scale;
            var corner = tip - facing * foldLength;
            var stemDirection = (corner - origin).normalized;
            float rounding = Mathf.Min(0.14f * scale, Vector3.Distance(origin, corner) * 0.3f);
            var beforeCorner = corner - stemDirection * rounding;
            var afterCorner = corner + facing * rounding;
            if (along < bendStart)
                return Vector3.Lerp(origin, beforeCorner, along / bendStart);
            if (along >= bendEnd)
                return Vector3.Lerp(afterCorner, tip, (along - bendEnd) / (1f - bendEnd));

            float t = (along - bendStart) / (bendEnd - bendStart);
            return Vector3.Lerp(Vector3.Lerp(beforeCorner, corner, t), Vector3.Lerp(corner, afterCorner, t), t);
        }

        private void DrawReinforced(HediffComp_CoccyxCombat comp, PawnDrawParms parms, int index,
            CoccyxLimb limb, Vector3 root, float scale, float width, int now)
        {
            int elapsed = now - limb.started - HediffComp_CoccyxCombat.ReinforcedWarmup;
            int repeatGap = comp.ReinforcedRepeatGap;
            bool lastLimb = index == HediffComp_CoccyxCombat.ReinforcedLimbCount - 1;
            float extension = limb.reinforced ? ReinforcedExtension(elapsed, repeatGap) : 0f;
            float previousExtension = limb.reinforced ? ReinforcedExtension(elapsed - 2, repeatGap) : 0f;

            float flank = index < 2 ? -1f : 1f;
            bool upper = index % 2 == 0;
            float spread = comp.poseSpread * scale;
            float height = comp.poseHeight * scale;
            var rotation = Quaternion.AngleAxis(parms.facing.AsAngle, Vector3.up);
            Vector3 Local(float x, float z) => root + rotation * new Vector3(flank * x * spread, 0f, z * height);
            bool rear = parms.facing == Rot4.North;
            bool frontal = parms.facing == Rot4.South;
            float anchorX = rear || frontal ? 0.10f : 0.20f;
            float anchorZ = rear ? (upper ? 0.115f : 0.080f)
                : frontal ? (upper ? 0.025f : -0.045f)
                : (upper ? 0.10f : -0.04f);
            var origin = root + rotation * new Vector3(flank * anchorX * scale, 0f, anchorZ * scale);
            float rootDepth = rear ? 0.009f : 0f;
            var bend1 = Local(0.45f, upper ? 0.22f : -0.16f);
            var bend2 = Local(0.78f, upper ? 0.02f : -0.62f);
            var restingEnd = Local(upper ? 0.76f : 0.52f, upper ? 0.37f : -0.54f);
            var coilTarget = comp.ThrowCoilTarget(index, out var holding);
            if (coilTarget != null)
            {
                bool live = coilTarget is Pawn;
                bool large = live || coilTarget is Corpse;
                var center = holding && !live
                    ? comp.ThrowCarryCenter(coilTarget, large ? Local(0f, -0.43f) : restingEnd)
                    : coilTarget.DrawPos;
                float tightness = holding ? comp.ThrowGripTightness(coilTarget) : 1.22f;
                float progress = holding ? 1f : live && comp.ThrowHoldingPawn ? comp.EscortProgress : comp.ThrowGrabProgress;
                Vector3 RestingPoint(float along) => Bezier(origin, bend1, bend2, restingEnd, along);
                if (!large)
                {
                    if (holding)
                        center.y = root.y + 0.004f;
                    DrawDebrisCoil(index, origin, center, scale, width, rootDepth, progress, tightness, RestingPoint);
                    if (holding && comp.ThrowDebrisVisible(coilTarget))
                        coilTarget.DrawNowAt(center);
                    return;
                }

                DrawThrowCoil(index, origin, center, scale * tightness, width, rootDepth, progress, RestingPoint);
                if (holding && !live)
                    coilTarget.DrawNowAt(center.WithY(root.y + 0.004f));
                return;
            }
            bool sideWalking = !limb.Active && parms.facing.IsHorizontal && comp.Pawn.pather?.Moving == true;
            int walkPoseIndex = ReinforcedSharedPoseIndices[index];
            float heightOffset = (upper ? 0.09f : -0.09f) + 0.04f * flank;
            var aim = limb.endpoint.WithY(origin.y) + Vector3.forward * heightOffset;
            var strikeDirection = (aim - root).WithY(0f).normalized;
            if (strikeDirection.sqrMagnitude < 0.01f)
                strikeDirection = rotation * Vector3.forward;
            bool barrage = limb.reinforced && repeatGap > 0;
            Vector3 BarrageEnd(int tick, float progress)
            {
                int round = Mathf.Max(0, tick / repeatGap);
                if (tick % repeatGap > HediffComp_CoccyxCombat.ReinforcedImpact)
                    round = Mathf.Min(round + 1, HediffComp_CoccyxCombat.BarrageRounds - 1);
                return Vector3.Lerp(BarrageRecoilPosition(root, strikeDirection, index, round), aim, progress);
            }
            float straighten = Smooth(extension / 0.22f);
            var strikeEnd = Vector3.Lerp(origin, aim, extension);
            if (barrage)
            {
                int lastImpact = (HediffComp_CoccyxCombat.BarrageRounds - 1) * repeatGap + HediffComp_CoccyxCombat.ReinforcedImpact;
                float returnTicks = HediffComp_CoccyxCombat.ReinforcedPulseTicks - HediffComp_CoccyxCombat.ReinforcedImpact;
                straighten = Smooth((elapsed + BarrageWindup) / (float)BarrageWindup) * (1f - Smooth((elapsed - lastImpact) / returnTicks));
                strikeEnd = BarrageEnd(elapsed, extension);
            }
            Vector3 TipFacing(Vector3 tip)
            {
                var direction = (aim - tip).WithY(0f);
                return direction.sqrMagnitude > 0.0001f ? direction.normalized : strikeDirection;
            }
            var barrageTip = Vector3.Lerp(restingEnd, strikeEnd, straighten);
            var barrageFacing = TipFacing(barrageTip);
            Vector3 CurrentPoint(float along)
            {
                if (sideWalking)
                    return Vector3.Lerp(origin, root, along) + Rest(comp, parms.facing, walkPoseIndex, along) * scale;

                var resting = Bezier(origin, bend1, bend2, restingEnd, along);
                if (barrage && straighten > 0f)
                {
                    var path = BarrageStrikePoint(origin, barrageTip, barrageFacing, scale, along);
                    resting += (barrageTip - restingEnd) * along;
                    float blend = Mathf.Lerp(straighten, 1f, Smooth(along / 0.64f));
                    return Vector3.Lerp(resting, path, blend);
                }
                return Vector3.Lerp(resting, Vector3.Lerp(origin, strikeEnd, along), straighten);
            }
            Ensure(index, Segments);
            bool finishingStrike = barrage && lastLimb && elapsed >= (HediffComp_CoccyxCombat.BarrageRounds - 1) * repeatGap;
            float strikeWidth = finishingStrike ? width * (1f + 0.24f * Mathf.Max(0f, extension)) : width;
            DrawReinforcedMesh(meshes[index], vertices[index], CurrentPoint, strikeWidth, index * 0.00004f, rootDepth, ReinforcedMaterial);

            bool advancing = barrage && extension > 0.35f && extension > previousExtension + 0.12f;
            if (advancing || (previousExtension > 0.10f && previousExtension > extension + 0.10f))
            {
                float trailLength = advancing ? extension * 0.84f : previousExtension;
                var previousEnd = barrage ? BarrageEnd(advancing ? elapsed : elapsed - 2, trailLength)
                    : Vector3.Lerp(origin, aim, trailLength);
                var previousFacing = TipFacing(previousEnd);
                reinforcedAfterimage ??= MaterialPool.MatFrom(ReinforcedTexture, ShaderDatabase.Transparent, new Color(1f, 0.78f, 0.9f, 0.34f));
                DrawReinforcedMesh(glowMeshes[index], glowVertices[index],
                    along => barrage ? BarrageStrikePoint(origin, previousEnd, previousFacing, scale, along)
                        : Vector3.Lerp(origin, previousEnd, along),
                    strikeWidth * 0.85f, index * 0.00004f - 0.0002f, rootDepth, reinforcedAfterimage);
            }
        }

        private void DrawDebrisCoil(int index, Vector3 origin, Vector3 center, float scale, float width,
            float rootDepth, float progress, float tightness, System.Func<float, Vector3> restingPoint)
        {
            const float reachEnd = 0.44f;
            bool startsLeft = origin.x <= center.x;
            float entryAngle = startsLeft ? Mathf.PI : 0f;
            float direction = startsLeft ? -1f : 1f;
            float reachBlend = Smooth(progress / 0.24f);

            Vector3 Orbit(float along)
            {
                float angle = entryAngle + direction * (6f * Mathf.PI * along);
                float rear = Mathf.Sin(angle);
                float radius = Mathf.Lerp(0.40f, 0.24f, along) * scale * tightness;
                float rise = Mathf.Lerp(-0.17f, 0.23f, along) * scale;
                return center + new Vector3(Mathf.Cos(angle) * radius, -0.008f * rear + 0.001f * along,
                    rise + rear * radius * 0.28f);
            }

            var entry = Orbit(0f);
            var approach = entry - origin;
            var bend1 = origin + approach * 0.36f;
            var bend2 = origin + approach * 0.78f - Vector3.forward * (0.12f * scale);
            Vector3 Point(float along)
            {
                float step = along * progress;
                var path = step < reachEnd ? Bezier(origin, bend1, bend2, entry, step / reachEnd)
                    : Orbit((step - reachEnd) / (1f - reachEnd));
                return Vector3.Lerp(restingPoint(along), path, reachBlend);
            }

            Ensure(index, EscortSegments);
            DrawReinforcedMesh(meshes[index], vertices[index], Point, Mathf.Max(width, 0.083f * scale),
                index * 0.0001f, rootDepth, ReinforcedMaterial);
        }

        private void DrawThrowCoil(int index, Vector3 origin, Vector3 center, float scale, float width,
            float rootDepth, float progress, System.Func<float, Vector3> restingPoint)
        {
            center.y = origin.y;
            float radius = 0.31f * scale;
            float angle = Mathf.Atan2(origin.z - center.z, origin.x - center.x);
            float turn = index % 2 == 0 ? 1f : -1f;
            float reachBlend = Smooth(progress / 0.28f);

            Vector3 Orbit(float along)
            {
                float phase = angle + along * 2.65f * Mathf.PI * turn;
                float taper = 1f - 0.23f * along;
                return center + new Vector3(Mathf.Cos(phase) * radius * taper, 0f,
                    Mathf.Sin(phase) * radius * 0.58f * taper);
            }

            Vector3 Point(float along)
            {
                float step = along * progress;
                var entry = Orbit(0f);
                var reach = step < 0.52f ? Vector3.Lerp(origin, entry, Smooth(step / 0.52f))
                    : Orbit((step - 0.52f) / 0.48f);
                return reachBlend < 1f ? Vector3.Lerp(restingPoint(along), reach, reachBlend) : reach;
            }

            Ensure(index, Segments);
            DrawReinforcedMesh(meshes[index], vertices[index], Point, width, 0.006f + index * 0.0001f,
                rootDepth, ReinforcedMaterial);
        }
        private static void DrawReinforcedMesh(Mesh mesh, Vector3[] points, System.Func<float, Vector3> sample,
            float width, float altitude, float rootDepth, Material material)
        {
            int segmentCount = points.Length / 2 - 1;
            for (int segment = 0; segment <= segmentCount; segment++)
            {
                float along = segment / (float)segmentCount;
                var direction = sample(Mathf.Min(1f, along + 0.01f)) - sample(Mathf.Max(0f, along - 0.01f));
                direction.y = 0f;
                direction.Normalize();
                var perpendicular = Vector3.Cross(direction, Vector3.up);
                float thickness = width * Mathf.Lerp(2.35f, 1.65f, along);
                var center = sample(along);
                center.y += altitude - rootDepth * (1f - Smooth(along / 0.40f));
                var half = perpendicular * thickness * 0.5f;
                points[segment * 2] = center - half;
                points[segment * 2 + 1] = center + half;
            }

            mesh.vertices = points;
            mesh.RecalculateBounds();
            Graphics.DrawMesh(mesh, Vector3.zero, Quaternion.identity, material, 0);
        }
    }
}

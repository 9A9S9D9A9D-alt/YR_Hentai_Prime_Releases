using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace YR_RJW
{
    public sealed partial class HediffComp_CoccyxCombat
    {
        private const float ThrowGrabRange = 13f;
        private const int ThrowGrabTicks = 24;
        private const int ThrowGripTicks = 12;
        private Thing throwGrabTarget;
        private Thing lastThrowGrip;
        private int lastThrowGripTick;
        private Vector3 throwGripContact;
        private int throwGrabStarted = -1;
        private bool throwHoldingPawn;
        private List<Thing> throwCargo = new List<Thing>();

        internal bool ThrowHoldingPawn => throwHoldingPawn && EscortTarget != null;
        internal Thing ThrowGrabTarget => throwGrabTarget;
        internal float ThrowGrabProgress => throwGrabStarted < 0 ? 0f
            : Mathf.Clamp01((Find.TickManager.TicksGame - throwGrabStarted) / (float)ThrowGrabTicks);
        private float ThrowGripProgress(Thing target) => target == lastThrowGrip
            ? Mathf.Clamp01((Find.TickManager.TicksGame - lastThrowGripTick) / (float)ThrowGripTicks) : 1f;

        internal float ThrowGripTightness(Thing target)
        {
            float progress = ThrowGripProgress(target);
            return progress < 0.5f ? Mathf.Lerp(1.22f, 0.88f, progress * 2f)
                : Mathf.Lerp(0.88f, 1f, (progress - 0.5f) * 2f);
        }

        internal Vector3 ThrowCarryCenter(Thing target, Vector3 rest)
            => Vector3.Lerp(throwGripContact.WithY(rest.y), rest, Mathf.SmoothStep(0f, 1f, ThrowGripProgress(target)));

        internal bool ThrowDebrisVisible(Thing target) => ThrowGripProgress(target) < 0.5f;

        private void FinishThrowGrip(Thing target, Vector3 contact, IntVec3 cell)
        {
            lastThrowGrip = target;
            lastThrowGripTick = Find.TickManager.TicksGame;
            throwGripContact = contact;
            CoccyxAttackSound.Play(Pawn, "Grip", cell);

            for (int i = 0; i < 3; i++)
            {
                var fleck = FleckMaker.GetDataStatic(contact, Pawn.Map, FleckDefOf.DustPuff, 0.27f);
                fleck.instanceColor = new Color(0.75f, 0.32f, 0.73f, 0.7f);
                fleck.velocityAngle = 30f + i * 120f;
                fleck.velocitySpeed = 0.7f;
                Pawn.Map.flecks.CreateFleck(fleck);
            }
        }
        private ThingOwner ThrowInventory => Pawn.inventory?.innerContainer;
        private int ThrowCargoCount => throwCargo.Count(IsHeldThrowCargo);
        private int ThrowOccupiedLimbs => throwCargo.Any(item => item is Corpse && IsHeldThrowCargo(item))
            ? ReinforcedLimbCount : ThrowCargoCount;

        private bool IsHeldThrowCargo(Thing item) => item != null && !item.Destroyed
            && ThrowInventory != null && item.holdingOwner == ThrowInventory;

        internal Thing ThrowCargoForLimb(int limbIndex)
        {
            int slot = 0;
            foreach (var item in throwCargo)
                if (IsHeldThrowCargo(item) && slot++ == limbIndex)
                    return item;
            return null;
        }

        internal Thing ThrowCoilTarget(int limbIndex, out bool held)
        {
            if (ThrowHoldingPawn)
            {
                held = HasEscortGrip;
                return limbIndex == 0 ? EscortTarget : null;
            }

            var cargo = ThrowCargoForLimb(limbIndex);
            held = cargo != null;
            if (held)
                return cargo;
            return limbIndex == ThrowCargoCount ? throwGrabTarget : null;
        }
        private void ExposeThrowCarry()
        {
            Scribe_References.Look(ref throwGrabTarget, "type2ThrowGrabTarget");
            Scribe_Values.Look(ref throwGrabStarted, "type2ThrowGrabStarted", -1);
            Scribe_Values.Look(ref throwHoldingPawn, "type2ThrowHoldingPawn");
            Scribe_Collections.Look(ref throwCargo, "type2ThrowCargo", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                throwCargo ??= new List<Thing>();
        }

        private bool CanOperateThrow => Type2Active && Pawn.Spawned && Pawn.Drafted && !Pawn.Downed
            && !Pawn.Dead && !Hediff_CoccyxEscort.IsHeld(Pawn);

        private bool CanGrabForThrow(Thing target)
        {
            if (!CanOperateThrow || EscortTarget != null || target?.Spawned != true || target.Map != Pawn.Map
                || target.Position.DistanceToSquared(Pawn.Position) > ThrowGrabRange * ThrowGrabRange
                || !GenSight.LineOfSight(Pawn.Position, target.Position, Pawn.Map))
                return false;

            if (target is Pawn pawn)
                return ThrowCargoCount == 0 && IsThrowablePawn(pawn) && CanEscort(pawn, ThrowGrabRange);

            if (ThrowInventory == null || ThrowOccupiedLimbs >= ReinforcedLimbCount)
                return false;

            return target is Corpse ? ThrowCargoCount == 0
                : target.def.category == ThingCategory.Item && target.def.IsWithinCategory(ThingCategoryDefOf.Chunks);
        }

        private bool IsThrowablePawn(Pawn target) => target.HostileTo(Pawn)
            || ((target.RaceProps.Animal || target.RaceProps.IsMechanoid) && target.Faction != Pawn.Faction
                && (target.Faction == null || target.Faction.RelationKindWith(Pawn.Faction) == FactionRelationKind.Neutral));

        internal static float ThrowMass(Thing cargo) => Mathf.Max(1f, cargo.GetStatValue(StatDefOf.Mass) * cargo.stackCount);

        private static int ThrowRange(Thing cargo) => Mathf.Clamp(32 - Mathf.FloorToInt(ThrowMass(cargo) / 20f), 24, 32);
        private void BeginThrowGrab(Thing target)
        {
            if (throwGrabTarget != null || PowerPending || !CanGrabForThrow(target))
                return;

            CancelTentacleAttacks();
            throwGrabTarget = target;
            throwGrabStarted = Find.TickManager.TicksGame;
            CoccyxAttackSound.Play(Pawn, "Swing");
        }

        private bool TickThrowCarry()
        {
            if (throwGrabTarget == null && !throwHoldingPawn && throwCargo.Count == 0)
                return false;

            throwCargo.RemoveAll(item => !IsHeldThrowCargo(item));
            if (!CanOperateThrow)
            {
                ReleaseThrowCarry();
                return false;
            }

            if (ThrowHoldingPawn && !HasEscortGrip && EscortTarget.Position.DistanceToSquared(Pawn.Position) > ThrowGrabRange * ThrowGrabRange)
                EndEscort();

            var target = throwGrabTarget;
            if (target == null)
                return false;
            if (!CanGrabForThrow(target))
            {
                throwGrabTarget = null;
                throwGrabStarted = -1;
                return false;
            }
            if (ThrowGrabProgress < 1f)
                return true;

            throwGrabTarget = null;
            throwGrabStarted = -1;
            if (target is Pawn pawn)
            {
                escortTarget = pawn;
                throwHoldingPawn = true;
                LockEscort(pawn);
                return false;
            }

            var map = target.Map;
            var position = target.Position;
            var contact = target.DrawPos;
            target.DeSpawn(DestroyMode.WillReplace);
            if (ThrowInventory.TryAdd(target, false))
            {
                throwCargo.Add(target);
                FinishThrowGrip(target, contact, position);
            }
            else
                GenSpawn.Spawn(target, position, map);
            return false;
        }

        private void ReleaseThrowCarry()
        {
            throwGrabTarget = null;
            throwGrabStarted = -1;
            if (ThrowHoldingPawn)
                EndEscort();
            throwHoldingPawn = false;

            if (Pawn.Spawned && ThrowInventory != null)
            {
                foreach (var item in throwCargo.Where(IsHeldThrowCargo).ToArray())
                    ThrowInventory.TryDrop(item, Pawn.Position, Pawn.Map, ThingPlaceMode.Near, out _);
            }
            throwCargo.Clear();
        }

        private IEnumerable<Gizmo> ThrowGizmos()
        {
            if (!Type2Active)
                yield break;

            var icon = ContentFinder<Texture2D>.Get("Yuran/UI/Commands/TentacleFocus");
            var grab = new CoccyxRangeTarget
            {
                origin = Pawn,
                radius = ThrowGrabRange,
                defaultLabel = "촉수 집기",
                defaultDesc = $"{ThrowGrabRange:0}칸 안의 적·중립 동물·메카노이드 또는 시체는 하나, 돌덩이와 금속 잔해는 최대 네 개까지 감아 들고 이동합니다.",
                icon = icon,
                targetingParams = new TargetingParameters
                {
                    canTargetPawns = true,
                    canTargetHumans = true,
                    canTargetAnimals = true,
                    canTargetMechs = true,
                    canTargetItems = true,
                    canTargetCorpses = true,
                    canTargetBuildings = false,
                    mapObjectTargetsMustBeAutoAttackable = false,
                    validator = target => CanGrabForThrow(target.Thing)
                },
                action = target => BeginThrowGrab(target.Thing)
            };
            if (!CanOperateThrow)
                grab.Disable("집결 상태에서 사용 가능한 촉수가 필요합니다.");
            else if (EscortTarget != null || ThrowOccupiedLimbs >= ReinforcedLimbCount)
                grab.Disable("촉수가 대상을 들고 있습니다. 먼저 내려놓거나 던지세요.");
            else if (throwGrabTarget != null || PowerPending)
                grab.Disable("현재 동작이 끝나야 집을 수 있습니다.");
            yield return grab;

            var cargo = ThrowHoldingPawn && HasEscortGrip
                ? new Thing[] { EscortTarget }
                : throwCargo.Where(IsHeldThrowCargo).ToArray();
            if (cargo.Length > 0)
            {
                var heldNames = string.Join("\n", cargo.Select((item, index) => $"{index + 1}. {item.LabelCap}"));
                var throwCommand = new Command_Action
                {
                    defaultLabel = $"촉수 던지기 ({cargo.Length})",
                    defaultDesc = $"들고 있는 대상\n{heldNames}\n\n대상 하나를 골라 던집니다. 무거울수록 사거리(24~32칸)는 짧아지고 충돌 피해는 커집니다. 폰끼리 부딪히면 양쪽이 피해를 받고, 무거운 투척물은 주변 적에게도 피해를 줍니다. 구조물에 맞으면 직격 피해의 3배가 들어갑니다.",
                    icon = icon,
                    action = () => Find.WindowStack.Add(new FloatMenu(cargo.Select((item, index) =>
                        new FloatMenuOption($"{index + 1}. {item.LabelCap}", () => AimThrow(item))).ToList()))
                };
                if (!CanOperateThrow || throwGrabTarget != null)
                    throwCommand.Disable("집기가 끝나면 던질 수 있습니다.");
                yield return throwCommand;
            }

            if (ThrowHoldingPawn || throwGrabTarget != null || cargo.Length > 0)
                yield return new Command_Action
                {
                    defaultLabel = "촉수 내려놓기",
                    defaultDesc = "들고 있는 대상을 내려놓습니다.",
                    icon = icon,
                    action = ReleaseThrowCarry
                };
        }

        private bool CanThrowCargo(Thing cargo) => CanOperateThrow && throwGrabTarget == null
            && (cargo is Pawn pawn ? ThrowHoldingPawn && HasEscortGrip && pawn == EscortTarget : IsHeldThrowCargo(cargo));

        private IntVec3 ThrowOrigin(Thing cargo) => cargo is Pawn ? cargo.Position : Pawn.Position;

        private void AimThrow(Thing cargo)
        {
            if (!CanThrowCargo(cargo))
                return;

            int range = ThrowRange(cargo);

            bool ValidAim(LocalTargetInfo aim)
            {
                if (!CanThrowCargo(cargo) || !aim.Cell.InBounds(Pawn.Map))
                    return false;
                var origin = ThrowOrigin(cargo);
                return aim.Cell != origin && origin.DistanceToSquared(aim.Cell) <= range * range;
            }

            Find.Targeter.BeginTargeting(new TargetingParameters
            {
                canTargetLocations = true,
                canTargetPawns = true,
                canTargetBuildings = true,
                canTargetItems = true
            }, aim =>
            {
                if (!ValidAim(aim))
                    return;
                if (CoccyxReinforcedFlyer.Throw(Pawn, cargo, aim.Cell, cargo is Pawn ? EndEscort : null))
                    throwCargo.Remove(cargo);
            }, null, ValidAim, Pawn, onUpdateAction: aim =>
            {
                if (CanThrowCargo(cargo))
                    GenDraw.DrawRadiusRing(ThrowOrigin(cargo), range);
            });
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace YuranLivingCoat
{
    public sealed partial class FixedModeComp : IThingHolder
    {
        const float RiftRange = 15f;
        const int CartridgeFluidCost = 10;
        const int CartridgeCapacity = 15;
        const int AmpouleCartridgeCost = 5;
        const int CombatGasCartridgeCost = 3;
        const int ErosionGasCartridgeCost = 3;
        const int AmpouleCooldownTicks = 1250;
        const int CartridgeSupplyCooldownTicks = (2500 * 20 + 59) / 60;
        const string CartridgeDef = "YR_BaekyunCartridge";
        enum RiftMode { None, Attack, Support, Isolation }
        RiftMode riftMode;
        Pawn focus;
        int nextSupply;
        int nextAmpoule;
        int rootedFluidSince = -1;
        int loadedCartridges;
        ThingOwner<Thing> cartridgeHolder;
        ThingOwner<Thing> Cartridges => cartridgeHolder ??= new ThingOwner<Thing>(this);
        int CartridgeCount => cartridgeHolder?.TotalStackCount ?? 0;

        public ThingOwner GetDirectlyHeldThings() => Cartridges;
        public void GetChildHolders(List<IThingHolder> children) =>
            ThingOwnerUtility.AppendThingHoldersFromThings(children, Cartridges);
        internal long PenanceFluidTicks;
        internal bool CanUseRift => Active && Wearer is { Spawned: true };
        RiftChambers Chambers => Wearer?.MapHeld?.GetComponent<RiftChambers>();

        void ExposeRift()
        {
            Scribe_Values.Look(ref riftMode, "riftMode");
            Scribe_References.Look(ref focus, "riftFocus");
            Scribe_Values.Look(ref nextSupply, "riftNextSupply");
            Scribe_Values.Look(ref nextAmpoule, "riftNextAmpoule");
            if (Scribe.mode == LoadSaveMode.Saving) BankRootedFluid(Find.TickManager.TicksGame);
            Scribe_Values.Look(ref rootedFluidSince, "rootedFluidSince", -1);
            Scribe_Deep.Look(ref cartridgeHolder, "riftCartridgeHolder", this);
            if (Scribe.mode == LoadSaveMode.LoadingVars)
                Scribe_Values.Look(ref loadedCartridges, "riftCartridges");
            if (Scribe.mode == LoadSaveMode.PostLoadInit && loadedCartridges > 0)
            {
                var def = DefDatabase<ThingDef>.GetNamed(CartridgeDef);
                while (loadedCartridges > 0)
                {
                    var stack = ThingMaker.MakeThing(def);
                    stack.stackCount = Mathf.Min(loadedCartridges, def.stackLimit);
                    loadedCartridges -= stack.stackCount;
                    Cartridges.TryAdd(stack);
                }
            }
            Scribe_Values.Look(ref PenanceFluidTicks, "penanceFluidTicks");
        }

        void ResetRift()
        {
            BankRootedFluid(Find.TickManager.TicksGame);
            rootedFluidSince = -1;
            Chambers?.ReleaseFor(this);
            riftMode = RiftMode.None;
            focus = null;
        }

        int RootedFluidBatches(int now) => rootedFluidSince < 0 ? 0 :
            Mathf.Max(0, now - rootedFluidSince) / RiftChambers.RootedFluidInterval;

        internal long PendingRootedFluidTicks(int now) => (long)RootedFluidBatches(now) *
            RiftChambers.RootedFluidPerInterval * RiftChambers.FluidTicksPerItem;

        internal void BankRootedFluid(int now)
        {
            int batches = RootedFluidBatches(now);
            PenanceFluidTicks += (long)batches * RiftChambers.RootedFluidPerInterval * RiftChambers.FluidTicksPerItem;
            rootedFluidSince += batches * RiftChambers.RootedFluidInterval;
        }

        internal bool InRiftRange(IntVec3 cell, Map map) =>
            CanUseRift && map == Wearer.Map && cell.InBounds(map) && !cell.Fogged(map) &&
            (cell - Wearer.Position).LengthHorizontalSquared <= RiftRange * RiftRange;

        bool IsEnemy(Pawn pawn) => pawn is { Spawned: true, Dead: false, Downed: false } &&
            pawn.HostileTo(Wearer) && InRiftRange(pawn.Position, pawn.Map);

        internal Pawn RiftTarget()
        {
            if (riftMode != RiftMode.Attack || !CanUseRift) return null;
            if (IsEnemy(focus)) return focus;
            focus = null;
            Pawn closest = null;
            int distance = int.MaxValue;
            foreach (Pawn pawn in Wearer.Map.mapPawns.AllPawnsSpawned)
            {
                if (!IsEnemy(pawn)) continue;
                int candidate = (pawn.Position - Wearer.Position).LengthHorizontalSquared;
                if (candidate >= distance) continue;
                closest = pawn;
                distance = candidate;
            }
            return closest;
        }

        IEnumerable<Gizmo> RiftGizmos(Texture2D icon)
        {
            if (!CanUseRift) yield break;
            Texture2D attackIcon = ContentFinder<Texture2D>.Get("YuranLivingCoat/Rift/AttackIcon", false) ?? icon;
            Texture2D rescueIcon = ContentFinder<Texture2D>.Get("YuranLivingCoat/Rift/RescueIcon", false) ?? icon;
            Texture2D isolationIcon = ContentFinder<Texture2D>.Get("YuranLivingCoat/Rift/IsolationIcon", false) ?? icon;
            string label = riftMode switch
            {
                RiftMode.Attack => "공격 모드",
                RiftMode.Support => "지원 모드",
                RiftMode.Isolation => "공생 조율",
                _ => "모드 선택"
            };
            yield return new RiftModePicker(ModeOptions)
            {
                defaultLabel = label,
                defaultDesc = "공격 모드, 지원 모드, 공생 조율 중 하나를 선택합니다.",
                icon = riftMode == RiftMode.Attack ? attackIcon : riftMode == RiftMode.Support ? rescueIcon :
                    riftMode == RiftMode.Isolation ? isolationIcon : icon,
                groupable = false
            };
            if (riftMode == RiftMode.Attack)
            {
                yield return Action("최음침식가스",
                    $"카트리지 {ErosionGasCartridgeCost}개를 사용해 밝혀진 15칸 안의 지면이나 벽에 분사구를 매설하거나, 적에게 기생시킵니다. 벽을 지정한 뒤에는 가스를 뿜을 쪽을 선택합니다. 노출된 적은 이동속도 50%, 시야 30% 감소, 받는 피해 10% 증가가 적용되며 조작 능력은 노출에 따라 최대 60% 감소합니다. 10초 연속 노출부터 5초마다 25% 확률로 광란에 빠집니다.",
                    SelectErosionGas, SkillIcon("CombatGasIcon"));
                yield return Action("집중 대상", "공격할 적 한 명을 지정합니다.", () => SelectPawn(IsEnemy, pawn => focus = pawn));
                if (focus != null)
                    yield return Action("자동 표적", "가장 가까운 적을 공격합니다.", () => focus = null);
            }

            var chamber = Chambers.RescueFor(this);
            if (chamber != null)
            {
                yield return Action("차원 배출", "밝혀진 15칸 안에 구조한 아군을 내보냅니다.", () =>
                {
                    Find.Targeter.BeginTargeting(new TargetingParameters
                    {
                        canTargetLocations = true,
                        canTargetPawns = false,
                        canTargetBuildings = false,
                        validator = target => chamber.Treated && InRiftRange(target.Cell, target.Map) && target.Cell.Standable(target.Map) && target.Cell.GetFirstPawn(target.Map) == null
                    }, target =>
                    {
                        if (chamber.Treated && InRiftRange(target.Cell, Wearer?.MapHeld))
                            Chambers.Release(chamber, target.Cell);
                    }, Wearer);
                }, rescueIcon);
            }
            else if (riftMode == RiftMode.Support)
            {
                yield return Action("긴급구조", "카트리지 1개로 아군 한 명을 구조합니다. 치료가 필요하면 일반 약품 수준으로 치료합니다.",
                    SelectRescue, rescueIcon);
            }

            if (riftMode == RiftMode.Isolation)
            {
                yield return Action("태동실 입장", "최대 5명까지 입실할 수 있습니다. 맵에서 Shift+클릭으로 추가하고 일반 클릭으로 확정합니다.",
                    () => Chambers.SelectIsolationGroup(this), SkillIcon("PenanceRoomIcon"));

                bool occupied = Chambers.FirstIsolation(parent) != null;
                if (occupied)
                    yield return Action("태동실 보기", "함께 격리 중인 정착민과 방의 진행 상황을 봅니다.",
                        () => Chambers.OpenPenanceRoom(parent), SkillIcon("PenanceRoomIcon"));
                long fluid = Chambers.PenanceFluidCount(this);
                if (occupied || fluid > 0)
                    yield return Action($"백윤액 사출 ({fluid}개)", "뿌리내림 중 입실자 없이도 30분당 10개가 쌓입니다. 입실자 생산량은 게임 시간당 1명 25개, 2명 35개, 3명 40개, 4명 43개, 5명 46개가 쌓입니다. 사출하면 방 연출이 1단계부터 다시 시작합니다.",
                        () => Chambers?.DischargePenanceRoom(this), SkillIcon("FluidDischargeIcon"));
                var production = Action($"카트리지 생산 (백윤액 {fluid}개)",
                    "태동실 내부 백윤액 10개로 카트리지 1개를 만듭니다. 외투에 최대 15개까지 저장하고, 초과분은 소지품에 넣습니다.",
                    ProduceCartridge, SkillIcon("CartridgeProductionIcon"));
                if (fluid < CartridgeFluidCost) production.Disable("태동실 내부 백윤액이 10개 필요합니다.");
                yield return production;
            }

            if (riftMode == RiftMode.Support)
            {
                yield return Action($"카트리지 보급 ({CartridgeCount}개)", "외투 홀더에 장전한 카트리지 1개를 밝혀진 15칸 안의 아군에게 보냅니다. 대상의 외투를 먼저 채우고, 가득 찼거나 외투가 없으면 소지품에 넣습니다. 재사용 대기시간은 게임 시간 20분입니다.",
                    SelectSupplyRecipient, SkillIcon("CartridgeSupplyIcon"));
                yield return Action("초재생 급속 앰플 투여",
                    "카트리지 5개로 15칸 안의 아군의 일반 상처는 손상의 70%, 영구 흉터는 각각 0.5만큼 즉시 회복합니다. 3시간 동안 감염 면역 획득과 상처 회복 속도가 4배가 되며, 투여 후 1일 동안 행복진동이 간헐적으로 발동합니다. 시전자는 30분 뒤 다시 사용할 수 있고, 같은 대상에게는 3일 뒤 다시 투여할 수 있습니다.",
                    SelectRegenerationAmpoule, SkillIcon("RegenerationAmpouleIcon"));
                yield return Action("정제된 전투용 최음가스 투여",
                    $"카트리지 {CombatGasCartridgeCost}개로 15칸 안의 아군에게 사목의 마스크와 같은 전투용 가스를 7.2시간 동안 투여합니다. 효과가 끝나면 약 1일간 후유증이 생기고, 2일간 허기와 수면이 30% 빠르게 줄어듭니다.",
                    SelectCombatGas, SkillIcon("CombatGasIcon"));
            }

            IEnumerable<FloatMenuOption> ModeOptions()
            {
                yield return new FloatMenuOption("공격 모드", () => SetRiftMode(RiftMode.Attack));
                yield return new FloatMenuOption("지원 모드", () => SetRiftMode(RiftMode.Support));
                yield return new FloatMenuOption("공생 조율", () => SetRiftMode(RiftMode.Isolation));
                if (riftMode != RiftMode.None)
                    yield return new FloatMenuOption("모드 해제", () => SetRiftMode(RiftMode.None));
            }

            Texture2D SkillIcon(string name) => ContentFinder<Texture2D>.Get("YuranLivingCoat/Rift/" + name);

            Command_Action Action(string label, string description, System.Action action, Texture2D skillIcon = null) => new()
            {
                defaultLabel = label, defaultDesc = description, icon = skillIcon ?? attackIcon, action = action
            };
        }

        void SetRiftMode(RiftMode mode)
        {
            if (!CanUseRift || riftMode == mode) return;
            riftMode = mode;
            focus = null;
            if (mode != RiftMode.Support) Chambers.CancelRescue(this);
            if (mode != RiftMode.Isolation) Chambers.CancelIsolation(this);
            if (mode == RiftMode.Attack) Chambers.RegisterAttack(this);
            else Chambers.CancelAttack(this);
        }

        sealed class RiftModePicker : Command_Action
        {
            readonly System.Func<IEnumerable<FloatMenuOption>> options;

            internal RiftModePicker(System.Func<IEnumerable<FloatMenuOption>> options)
            {
                this.options = options;
                action = () => Find.WindowStack.Add(new FloatMenu(options().ToList()));
            }

            public override IEnumerable<FloatMenuOption> RightClickFloatMenuOptions => options();
        }

        void SelectPawn(System.Predicate<Pawn> allowed, System.Action<Pawn> action)
        {
            bool Valid(Pawn pawn) => pawn != Wearer && pawn is { Dead: false, Spawned: true } &&
                InRiftRange(pawn.Position, pawn.Map) && allowed(pawn);
            var parameters = TargetingParameters.ForPawns();
            Find.Targeter.BeginTargeting(parameters, target => action(target.Pawn), null,
                target => Valid(target.Pawn), Wearer, onUpdateAction: _ => DrawRiftRange());
        }

        IEnumerable<Gizmo> CartridgeHolderGizmos(Texture2D fallbackIcon)
        {
            var icon = DefDatabase<ThingDef>.GetNamedSilentFail(CartridgeDef)?.uiIcon ?? fallbackIcon;
            var load = new Command_Action
            {
                defaultLabel = $"카트리지 장전 ({CartridgeCount}/{CartridgeCapacity})",
                defaultDesc = "소지품의 카트리지를 공생외투 홀더에 장전합니다.",
                icon = icon,
                action = LoadCartridges
            };
            if (CartridgeCount >= CartridgeCapacity) load.Disable("카트리지 홀더가 가득 찼습니다.");
            yield return load;

            if (CartridgeCount > 0)
                yield return new Command_Action
                {
                    defaultLabel = "카트리지 꺼내기",
                    defaultDesc = "홀더의 카트리지를 소지품으로 옮깁니다.",
                    icon = icon,
                    action = UnloadCartridges
                };
        }

        void LoadCartridges()
        {
            var inventory = Wearer?.inventory?.innerContainer;
            if (inventory == null) return;
            foreach (var stack in inventory.Where(item => item.def.defName == CartridgeDef).ToList())
            {
                int space = CartridgeCapacity - CartridgeCount;
                if (space <= 0) return;
                Cartridges.TryAddOrTransfer(stack, Mathf.Min(space, stack.stackCount));
            }
        }

        void UnloadCartridges()
        {
            var inventory = Wearer?.inventory?.innerContainer;
            if (inventory == null) return;
            foreach (var stack in Cartridges.ToList())
                Cartridges.TryTransferToContainer(stack, inventory);
        }

        void SelectRescue()
        {
            if (!CheckCartridge("긴급구조에는 카트리지 1개가 필요합니다.")) return;
            SelectPawn(CanRescue, pawn =>
            {
                if (CheckCartridge("긴급구조에는 카트리지 1개가 필요합니다."))
                    Chambers.BeginRescue(this, pawn);
            });
        }

        internal bool CanRescue(Pawn pawn) => riftMode == RiftMode.Support && pawn is { Spawned: true, Dead: false } &&
            pawn.Faction == Wearer?.Faction && !pawn.InMentalState && InRiftRange(pawn.Position, pawn.Map);

        internal void FinishRescue(Pawn pawn)
        {
            if (!CanRescue(pawn) || !CheckCartridge("긴급구조에는 카트리지 1개가 필요합니다.")) return;
            bool needsTreatment = pawn.health.HasHediffsNeedingTend();
            if (Chambers.Admit(this, pawn, isolation: false, advanced: needsTreatment))
                ConsumeCartridge();
        }
        void SelectErosionGas()
        {
            const string missing = "최음침식가스에는 카트리지 3개가 필요합니다.";
            if (!IsAttacking || !CheckCartridge(missing, ErosionGasCartridgeCost)) return;
            var map = Wearer.Map;
            bool ValidCell(IntVec3 cell) => IsAttacking && InRiftRange(cell, map);
            bool ValidTarget(LocalTargetInfo target)
            {
                if (!ValidCell(target.Cell)) return false;
                if (target.Pawn is { } pawn)
                    return !pawn.Dead && pawn.Spawned && pawn.Map == map && pawn.HostileTo(Wearer) &&
                        !Chambers.HasErosionGasHost(pawn);
                return target.Thing is Building building
                    ? building.def.Fillage == FillCategory.Full
                    : target.Cell.Walkable(map);
            }

            void Deploy(IntVec3 cell, Pawn host = null, Building mount = null)
            {
                if (!CheckCartridge(missing, ErosionGasCartridgeCost)) return;
                Chambers.DeployErosionGas(Wearer, cell, host, mount);
                ConsumeCartridge(ErosionGasCartridgeCost);
            }

            void Preview(IntVec3 center)
            {
                GenDraw.DrawRadiusRing(center, RiftErosionGas.Radius, new Color(.9f, .36f, .62f),
                    cell => RiftErosionGas.Reaches(center, cell, map));
            }

            void SelectWallSide(Building wall)
            {
                bool ValidOutlet(LocalTargetInfo outlet) => wall.Spawned && wall.Map == map &&
                    ValidCell(wall.Position) && outlet.Cell.InBounds(map) && !outlet.Cell.Fogged(map) &&
                    outlet.Cell.Walkable(map) && (outlet.Cell - wall.Position).LengthHorizontalSquared == 1;
                Messages.Message("벽에 인접한 칸을 찍어 분사 방향을 정하세요.", wall,
                    MessageTypeDefOf.NeutralEvent, false);
                Find.Targeter.BeginTargeting(TargetingParameters.ForCell(),
                    outlet => Deploy(outlet.Cell, mount: wall), null, ValidOutlet, Wearer,
                    onUpdateAction: outlet =>
                    {
                        if (ValidOutlet(outlet)) Preview(outlet.Cell);
                    });
            }

            var parameters = new TargetingParameters
            {
                canTargetLocations = true,
                canTargetBuildings = true,
                canTargetPawns = true
            };
            Find.Targeter.BeginTargeting(parameters, target =>
            {
                if (target.Thing is Building wall) SelectWallSide(wall);
                else Deploy(target.Cell, target.Pawn);
            }, null, ValidTarget, Wearer, onUpdateAction: target =>
            {
                if (ValidTarget(target) && target.Thing is not Building) Preview(target.Cell);
            });
        }

        void SelectCombatGas()
        {
            string missing = $"정제된 전투용 최음가스 투여에는 카트리지 {CombatGasCartridgeCost}개가 필요합니다.";
            if (riftMode != RiftMode.Support || !CanUseRift || !CheckCartridge(missing, CombatGasCartridgeCost)) return;
            var duration = DefDatabase<HediffDef>.GetNamed("YR_LivingCoatCombatGasDuration");
            var gas = CombatGasDuration.Gas;
            var aftereffect = CombatGasDuration.Aftereffect;
            if (gas == null || aftereffect == null)
            {
                Messages.Message("사목의 마스크 효과를 찾을 수 없습니다.", Wearer, MessageTypeDefOf.RejectInput, false);
                return;
            }

            var eligibility = AccessTools.Method("YR_RJW.LegacyEligibility:Allows");
            var allows = eligibility == null ? null :
                (System.Func<Pawn, bool>)System.Delegate.CreateDelegate(typeof(System.Func<Pawn, bool>), eligibility);
            SelectPawn(pawn => riftMode == RiftMode.Support && pawn.Faction == Wearer.Faction &&
                pawn.DevelopmentalStage == DevelopmentalStage.Adult && pawn.ageTracker.AgeBiologicalYearsFloat >= 18f &&
                (allows?.Invoke(pawn) ?? pawn.RaceProps.Humanlike) &&
                !pawn.health.hediffSet.HasHediff(gas) && !pawn.health.hediffSet.HasHediff(aftereffect) &&
                !pawn.health.hediffSet.HasHediff(CombatGasDuration.MaskAftereffect) &&
                !pawn.health.hediffSet.HasHediff(duration), pawn =>
            {
                if (!CheckCartridge(missing, CombatGasCartridgeCost)) return;
                pawn.health.AddHediff(gas);
                if (!pawn.health.hediffSet.HasHediff(gas)) return;
                pawn.health.AddHediff(duration);
                ConsumeCartridge(CombatGasCartridgeCost);
            });
        }

        void SelectRegenerationAmpoule()
        {
            if (!CanUseRift || riftMode != RiftMode.Support || !CanUseAmpoule()) return;

            var cooldown = DefDatabase<HediffDef>.GetNamed("YR_LivingCoatAmpouleCooldown");
            var regeneration = DefDatabase<HediffDef>.GetNamed("YR_LivingCoatAmpouleRegeneration");
            var vibration = DefDatabase<HediffDef>.GetNamed("YR_LivingCoatAmpouleHappyVibration");
            SelectPawn(pawn => riftMode == RiftMode.Support && pawn.Faction == Wearer.Faction &&
                !pawn.health.hediffSet.HasHediff(cooldown), pawn =>
            {
                if (!CanUseAmpoule()) return;
                pawn.health.AddHediff(cooldown);
                pawn.health.AddHediff(regeneration);
                pawn.health.AddHediff(vibration);
                var injuries = pawn.health.hediffSet.hediffs.OfType<Hediff_Injury>().ToList();
                foreach (var injury in injuries)
                    injury.Heal(injury.IsPermanent() ? Mathf.Min(.5f, injury.Severity) : injury.Severity * .70f);
                ConsumeCartridge(AmpouleCartridgeCost);
                nextAmpoule = Find.TickManager.TicksGame + AmpouleCooldownTicks;
            });
        }

        bool CanUseAmpoule()
        {
            if (Find.TickManager.TicksGame >= nextAmpoule)
                return CheckCartridge($"초재생 급속 앰플 투여에는 카트리지 {AmpouleCartridgeCost}개가 필요합니다.",
                    AmpouleCartridgeCost);

            Messages.Message("초재생 급속 앰플이 준비 중입니다.", Wearer, MessageTypeDefOf.RejectInput, false);
            return false;
        }

        void SelectSupplyRecipient()
        {
            if (!CanUseRift || riftMode != RiftMode.Support) return;
            if (Find.TickManager.TicksGame < nextSupply)
            {
                Messages.Message("카트리지 보급이 준비 중입니다.", Wearer, MessageTypeDefOf.RejectInput, false);
                return;
            }
            if (!CheckCartridge("보급할 카트리지가 없습니다.")) return;
            SelectPawn(pawn => riftMode == RiftMode.Support && pawn.Faction == Wearer.Faction,
                SupplyCartridge);
        }

        void ProduceCartridge()
        {
            if (!IsIsolating) return;
            if (Chambers.PenanceFluidCount(this) < CartridgeFluidCost)
            {
                Messages.Message($"태동실 내부 백윤액 {CartridgeFluidCost}개가 필요합니다.", Wearer,
                    MessageTypeDefOf.RejectInput, false);
                return;
            }
            if (TryStoreCartridge(Wearer, this))
                Chambers.SpendPenanceFluid(this, CartridgeFluidCost);
        }

        bool CheckCartridge(string missingMessage, int count = 1)
        {
            if (CartridgeCount >= count) return true;
            Messages.Message(missingMessage, Wearer, MessageTypeDefOf.RejectInput, false);
            return false;
        }

        void SupplyCartridge(Pawn recipient)
        {
            if (Find.TickManager.TicksGame < nextSupply || !CheckCartridge("보급할 카트리지가 없습니다.")) return;
            var coat = recipient.apparel?.WornApparel
                .Select(item => item.TryGetComp<FixedModeComp>()).FirstOrDefault(comp => comp != null);
            if (!TryStoreCartridge(recipient, coat)) return;

            ConsumeCartridge();
            nextSupply = Find.TickManager.TicksGame + CartridgeSupplyCooldownTicks;
        }

        void ConsumeCartridge(int count = 1)
        {
            while (count > 0)
            {
                var stack = Cartridges[0];
                int taken = Mathf.Min(count, stack.stackCount);
                stack.SplitOff(taken).Destroy();
                count -= taken;
            }
        }

        static bool TryStoreCartridge(Pawn recipient, FixedModeComp coat)
        {
            var def = DefDatabase<ThingDef>.GetNamedSilentFail(CartridgeDef);
            if (def == null) return false;
            var item = ThingMaker.MakeThing(def);
            if (coat != null && coat.CartridgeCount < CartridgeCapacity && coat.Cartridges.TryAdd(item))
                return true;
            if (recipient.inventory?.innerContainer.TryAdd(item) == true) return true;
            item.Destroy();
            Messages.Message("소지품에 카트리지를 넣을 수 없습니다.", recipient, MessageTypeDefOf.RejectInput, false);
            return false;
        }

        internal bool IsIsolating => riftMode == RiftMode.Isolation && CanUseRift;
        internal bool IsAttacking => riftMode == RiftMode.Attack && CanUseRift;
        internal void DrawRiftRange()
        {
            if (!CanUseRift || Wearer.Faction != Faction.OfPlayer) return;
            var map = Wearer.Map;
            GenDraw.DrawRadiusRing(Wearer.Position, RiftRange, Color.white, cell => InRiftRange(cell, map));
        }

        internal bool CanStrike(Pawn pawn) => IsAttacking && IsEnemy(pawn);
    }

    public sealed class RiftChambers : MapComponent, IThingHolder
    {
        ThingOwner<Pawn> occupants;
        List<RiftStay> stays = new();
        readonly Dictionary<FixedModeComp, RiftStrike> attacks = new();
        readonly List<RiftRescue> rescues = new();
        List<RiftErosionGas> erosionGas = new();
        readonly List<PenanceEntrance> entrances = new();
        readonly List<PenanceEntrance> exits = new();
        Dictionary<Pawn, PawnKindDef> ranWild = new();
        List<Pawn> wildKeys;
        List<PawnKindDef> wildKinds;
        readonly List<FixedModeComp> attackTick = new();
        public RiftChambers(Map map) : base(map) => occupants = new ThingOwner<Pawn>(this);
        public IThingHolder ParentHolder => map;
        public ThingOwner GetDirectlyHeldThings() => occupants;
        public void GetChildHolders(List<IThingHolder> children) =>
            ThingOwnerUtility.AppendThingHoldersFromThings(children, occupants);

        public override void ExposeData()
        {
            Scribe_Deep.Look(ref occupants, "livingCoatRiftOccupants", this);
            Scribe_Collections.Look(ref erosionGas, "livingCoatErosionGas", LookMode.Deep);
            erosionGas ??= new List<RiftErosionGas>();
            Scribe_Collections.Look(ref stays, "livingCoatRiftStays", LookMode.Deep);
            occupants ??= new ThingOwner<Pawn>(this);
            stays ??= new List<RiftStay>();
            Scribe_Collections.Look(ref ranWild, "livingCoatRanWild", LookMode.Reference, LookMode.Def,
                ref wildKeys, ref wildKinds);
            ranWild ??= new Dictionary<Pawn, PawnKindDef>();
        }

        public override void FinalizeInit()
        {
            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
            {
                var mode = FixedHelmet.ModeFor(pawn);
                if (mode?.IsAttacking == true) RegisterAttack(mode);
            }
        }

        internal RiftStay RescueFor(FixedModeComp mode) =>
            stays.Find(stay => stay.Coat == mode.parent && !stay.Isolation);

        internal RiftStay FirstIsolation(Thing coat) =>
            stays.Find(stay => stay.Coat == coat && stay.Isolation);

        const int TicksPerHour = 2500;
        // Keep the saved balance in its original units so existing stock retains its value.
        internal const int FluidTicksPerItem = 100;
        internal const int RootedFluidInterval = TicksPerHour / 2;
        internal const int RootedFluidPerInterval = 10;

        static int HourlyFluidForSlot(int slot) => slot switch
        {
            0 => 25,
            1 => 10,
            2 => 5,
            _ => 3
        };

        internal long PenanceFluidCount(FixedModeComp mode)
        {
            int now = Find.TickManager.TicksGame;
            long ticks = mode.PenanceFluidTicks + mode.PendingRootedFluidTicks(now);
            int slot = 0;
            foreach (var stay in stays)
                if (stay.Coat == mode.parent && stay.Isolation)
                    ticks += CompletedFluidHours(stay, now) * HourlyFluidForSlot(slot++) * FluidTicksPerItem;
            return ticks / FluidTicksPerItem;
        }

        internal void SpendPenanceFluid(FixedModeComp mode, int count)
        {
            int now = Find.TickManager.TicksGame;
            mode.BankRootedFluid(now);
            BankRoomFluid(mode, now);
            mode.PenanceFluidTicks -= (long)count * FluidTicksPerItem;
        }

        internal void DischargePenanceRoom(FixedModeComp mode)
        {
            if (!mode.CanUseRift || mode.Wearer.Map != map) return;
            var def = DefDatabase<ThingDef>.GetNamedSilentFail("YR_BaekyunFluid");
            if (def == null) return;
            int now = Find.TickManager.TicksGame;
            mode.BankRootedFluid(now);
            BankRoomFluid(mode, now);
            foreach (var stay in stays)
                if (stay.Coat == mode.parent && stay.Isolation)
                    stay.PenanceRoomStart = now;
            while (mode.PenanceFluidTicks >= FluidTicksPerItem)
            {
                var fluid = ThingMaker.MakeThing(def);
                int count = (int)System.Math.Min(def.stackLimit, mode.PenanceFluidTicks / FluidTicksPerItem);
                fluid.stackCount = count;
                if (!GenPlace.TryPlaceThing(fluid, mode.Wearer.Position, map, ThingPlaceMode.Near))
                {
                    fluid.Destroy();
                    Messages.Message("백윤액을 놓을 공간이 부족합니다.", mode.Wearer, MessageTypeDefOf.RejectInput, false);
                    break;
                }
                mode.PenanceFluidTicks -= count * FluidTicksPerItem;
            }
        }

        static int CompletedFluidHours(RiftStay stay, int now) =>
            Mathf.Max(0, now - stay.FluidStart) / TicksPerHour;

        void BankRoomFluid(FixedModeComp mode, int now)
        {
            int slot = 0;
            foreach (var stay in stays)
            {
                if (stay.Coat != mode.parent || !stay.Isolation) continue;
                int hours = CompletedFluidHours(stay, now);
                mode.PenanceFluidTicks += hours * HourlyFluidForSlot(slot++) * FluidTicksPerItem;
                stay.PenanceFluidSince = stay.FluidStart + hours * TicksPerHour;
            }
        }

        internal void CollectIsolation(Thing coat, List<RiftStay> result)
        {
            result.Clear();
            foreach (var stay in stays)
                if (stay.Coat == coat && stay.Isolation && stay.Pawn is { Destroyed: false, Spawned: false })
                    result.Add(stay);
        }

        internal void OpenPenanceRoom(Thing coat)
        {
            if (Find.WindowStack.WindowOfType<PenanceWindow>() is { } open)
            {
                if (open.Coat == coat) return;
                open.Close(false);
            }
            Find.WindowStack.Add(new PenanceWindow(this, coat));
        }

        const int PenanceCapacity = 5;

        int FreePenanceSlots(FixedModeComp mode) => PenanceCapacity -
            stays.Count(stay => stay.Isolation && stay.Coat == mode.parent) -
            entrances.Count(entrance => entrance.Mode == mode);

        internal void SelectIsolationGroup(FixedModeComp mode)
        {
            if (FreePenanceSlots(mode) <= 0)
            {
                Messages.Message("태동실에는 최대 5명까지 입실할 수 있습니다.", mode.Wearer,
                    MessageTypeDefOf.RejectInput, false);
                return;
            }

            var selected = new HashSet<Pawn>();
            var parameters = TargetingParameters.ForPawns();
            bool Valid(LocalTargetInfo target) => target.Pawn is Pawn pawn &&
                CanEnterIsolation(mode, pawn) && !IntakePending(pawn);

            void PickTarget()
            {
                Find.Targeter.BeginTargeting(parameters, target =>
                {
                    if (!Valid(target)) return;
                    selected.RemoveWhere(member => !CanEnterIsolation(mode, member) || IntakePending(member));
                    var pawn = target.Pawn;
                    if (!selected.Contains(pawn) && selected.Count >= FreePenanceSlots(mode))
                    {
                        Messages.Message("남은 입실 자리를 모두 선택했습니다. 선택한 정착민을 클릭해 확정하세요.",
                            mode.Wearer, MessageTypeDefOf.RejectInput, false);
                        PickTarget();
                        return;
                    }

                    if (Event.current.shift)
                    {
                        if (!selected.Add(pawn)) selected.Remove(pawn);
                        PickTarget();
                        return;
                    }

                    selected.Add(pawn);
                    foreach (var member in selected)
                        BeginIsolation(mode, member);
                }, null, Valid, mode.Wearer, onUpdateAction: _ =>
                {
                    foreach (var pawn in selected)
                        if (pawn.Spawned && pawn.Map == map)
                            GenDraw.DrawTargetHighlight(pawn);
                });
            }

            PickTarget();
        }

        internal void TrackWild(Pawn pawn, PawnKindDef previousKind) => ranWild[pawn] = previousKind;

        bool CanIsolate(Pawn pawn) => pawn.IsColonist ||
            (pawn.Faction == null && pawn.kindDef == PawnKindDefOf.WildMan && ranWild.ContainsKey(pawn));

        internal bool Admit(FixedModeComp mode, Pawn pawn, bool isolation, bool advanced)
        {
            if ((!isolation && RescueFor(mode) != null) || !pawn.Spawned || pawn.Map != map || pawn.CarriedBy != null ||
                FixedHelmet.ModeFor(pawn) != null) return false;
            int now = Find.TickManager.TicksGame;
            int duration = isolation ? RiftStay.IsolationTicks : 180;
            var first = isolation ? FirstIsolation(mode.parent) : null;
            var stay = new RiftStay
            {
                Coat = mode.parent, Pawn = pawn, Entry = pawn.Position, Isolation = isolation, Advanced = advanced,
                Ends = now + duration, Duration = duration,
                PenanceRoomStart = first?.RoomStart ?? now, PenanceFluidSince = isolation ? now : -1
            };
            pawn.jobs?.StopAll();
            pawn.DeSpawn();
            if (!occupants.TryAdd(pawn))
            {
                GenSpawn.Spawn(pawn, stay.Entry, map);
                return false;
            }
            if (isolation && !StowIsolationGear(pawn))
            {
                occupants.Remove(pawn);
                GenSpawn.Spawn(pawn, stay.Entry, map);
                Messages.Message("장비를 인벤토리로 옮길 수 없어 격리를 취소했습니다.", pawn,
                    MessageTypeDefOf.RejectInput, false);
                return false;
            }
            stays.Add(stay);
            return true;
        }

        static bool StowIsolationGear(Pawn pawn)
        {
            var gear = new List<(Thing item, ThingOwner owner, bool locked, bool forced)>();
            if (pawn.apparel != null)
                foreach (var item in pawn.apparel.WornApparel)
                    gear.Add((item, item.holdingOwner, pawn.apparel.IsLocked(item),
                        pawn.outfits?.forcedHandler?.IsForced(item) == true));

            if (pawn.equipment != null)
                foreach (var item in pawn.equipment.AllEquipmentListForReading)
                    gear.Add((item, item.holdingOwner, false, false));

            if (gear.Count == 0) return true;
            var inventory = pawn.inventory?.innerContainer;
            if (inventory == null) return false;

            int moved = 0;
            for (; moved < gear.Count; moved++)
                if (!inventory.TryAddOrTransfer(gear[moved].item, canMergeWithExistingStacks: false))
                    break;
            if (moved == gear.Count) return true;

            for (int i = moved - 1; i >= 0; i--)
            {
                var (item, owner, locked, forced) = gear[i];
                if (!owner.TryAddOrTransfer(item, canMergeWithExistingStacks: false) || item is not Apparel apparel)
                    continue;
                if (locked) pawn.apparel.Lock(apparel);
                if (forced) pawn.outfits?.forcedHandler?.SetForced(apparel, true);
            }
            return false;
        }

        internal void ReleaseFor(FixedModeComp mode)
        {
            CancelAttack(mode);
            CancelRescue(mode);
            CancelIsolation(mode);
            for (int i = stays.Count - 1; i >= 0; i--)
                if (stays[i].Coat == mode.parent)
                    Release(stays[i], stays[i].Entry);
        }

        internal void Release(RiftStay stay, IntVec3 cell, bool completedIsolation = false)
        {
            var pawn = stay.Pawn;
            if (pawn == null || pawn.Destroyed || !occupants.Contains(pawn))
            {
                RemoveStay(stay);
                return;
            }
            if (!cell.InBounds(map) || !cell.Standable(map) || cell.GetFirstPawn(map) != null)
                cell = CellFinder.RandomClosewalkCellNear(stay.Entry, map, 5,
                    candidate => candidate.GetFirstPawn(map) == null);
            if (!cell.InBounds(map) || !cell.Standable(map)) return;
            occupants.Remove(pawn);
            GenSpawn.Spawn(pawn, cell, map);
            RemoveStay(stay);
            if (completedIsolation)
            {
                pawn.MentalState?.RecoverFromState();
                if (pawn.Faction == null && pawn.kindDef == PawnKindDefOf.WildMan &&
                    ranWild.Remove(pawn, out var previousKind))
                {
                    pawn.ChangeKind(previousKind);
                    pawn.SetFaction(Faction.OfPlayer);
                }
                if (DefDatabase<HediffDef>.GetNamedSilentFail("Anesthetic") is { } anesthesia)
                {
                    var sedation = pawn.health.hediffSet.GetFirstHediffOfDef(anesthesia);
                    if (sedation == null)
                    {
                        sedation = pawn.health.AddHediff(anesthesia);
                        if (sedation.TryGetComp<HediffComp_Disappears>() is { } timeout)
                            timeout.ticksToDisappear = 2500;
                    }
                    sedation.Severity = Mathf.Max(1f, sedation.Severity);
                }
                var coating = pawn.health.hediffSet.GetFirstHediffOfDef(PenanceCoating.Def) ??
                    pawn.health.AddHediff(PenanceCoating.Def);
                if (coating.TryGetComp<HediffComp_Disappears>() is { } expiry)
                    expiry.ticksToDisappear = 600;
                exits.Add(new PenanceEntrance(pawn, pawn.DrawPos));
            }

            if (stay.Treated && stay.Advanced &&
                DefDatabase<HediffDef>.GetNamedSilentFail("YR_CoccyxSlimeCoat") is { } slime)
            {
                var coating = pawn.health.hediffSet.GetFirstHediffOfDef(slime) ?? pawn.health.AddHediff(slime);
                if (coating.TryGetComp<HediffComp_Disappears>() is { } expiry)
                    expiry.ticksToDisappear = 600;
            }
        }

        void RemoveStay(RiftStay stay)
        {
            if (stay.Isolation && stay.Coat?.TryGetComp<FixedModeComp>() is { } mode)
                BankRoomFluid(mode, Find.TickManager.TicksGame);
            stays.Remove(stay);
        }

        public override void MapRemoved()
        {
            // Map removal also removes its chambers. Preserve occupants in the world pawn pool.
            for (int i = occupants.Count - 1; i >= 0; i--)
            {
                Pawn pawn = occupants[i];
                occupants.Remove(pawn);
                Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.KeepForever);
            }
            stays.Clear();
            attacks.Clear();
            erosionGas.Clear();
            entrances.Clear();
            exits.Clear();
        }

        internal void BeginRescue(FixedModeComp mode, Pawn pawn)
        {
            if (RescueFor(mode) != null || !mode.CanRescue(pawn) || IntakePending(pawn) ||
                rescues.Exists(rescue => rescue.Mode == mode)) return;
            rescues.Add(new RiftRescue(mode, pawn));
        }

        internal void CancelRescue(FixedModeComp mode) => rescues.RemoveAll(rescue => rescue.Mode == mode);

        bool IntakePending(Pawn pawn) =>
            rescues.Exists(rescue => rescue.Pawn == pawn) || entrances.Exists(entrance => entrance.Pawn == pawn);

        internal void BeginIsolation(FixedModeComp mode, Pawn pawn)
        {
            if (FreePenanceSlots(mode) <= 0 || IntakePending(pawn) || !CanEnterIsolation(mode, pawn)) return;
            entrances.Add(new PenanceEntrance(mode, pawn));
        }

        internal void CancelIsolation(FixedModeComp mode) => entrances.RemoveAll(entrance => entrance.Mode == mode);

        bool CanEnterIsolation(FixedModeComp mode, Pawn pawn) =>
            mode.IsIsolating && mode.Wearer.Map == map && pawn != mode.Wearer && pawn is { Spawned: true, Dead: false } &&
            pawn.Map == map && mode.InRiftRange(pawn.Position, map) && CanIsolate(pawn) &&
            pawn.CarriedBy == null && FixedHelmet.ModeFor(pawn) == null;

        internal Vector3 IntakeOffset(Pawn pawn)
        {
            var entrance = entrances.Find(entry => entry.Pawn == pawn) ?? exits.Find(entry => entry.Pawn == pawn);
            if (entrance != null) return entrance.PullOffset();
            var rescue = rescues.Find(entry => entry.Pawn == pawn);
            return rescue == null ? Vector3.zero : rescue.PullOffset();
        }

        internal bool HasErosionGasHost(Pawn host) => erosionGas.Any(cloud => cloud.Host == host);

        internal void DeployErosionGas(Pawn caster, IntVec3 cell, Pawn host, Building mount)
        {
            var cloud = new RiftErosionGas(caster.Faction, cell, Find.TickManager.TicksGame, host, mount);
            cloud.Tick(map, Find.TickManager.TicksGame);
            erosionGas.Add(cloud);
            DefDatabase<SoundDef>.GetNamedSilentFail("GasPack_Deploy")?.PlayOneShot(new TargetInfo(cell, map));
        }

        public override void MapComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            for (int i = erosionGas.Count - 1; i >= 0; i--)
                if (!erosionGas[i].Tick(map, now))
                    erosionGas.RemoveAt(i);
            exits.RemoveAll(exit => exit.Ready || !exit.Pawn.Spawned || exit.Pawn.Map != map);
            for (int i = entrances.Count - 1; i >= 0; i--)
            {
                var entrance = entrances[i];
                if (!CanEnterIsolation(entrance.Mode, entrance.Pawn))
                {
                    entrances.RemoveAt(i);
                    continue;
                }
                entrance.TrackBeforeGrip();
                if (!entrance.Ready) continue;
                entrances.RemoveAt(i);
                if (Admit(entrance.Mode, entrance.Pawn, isolation: true, advanced: false) && Find.CurrentMap == map)
                    OpenPenanceRoom(entrance.Mode.parent);
            }
            for (int i = rescues.Count - 1; i >= 0; i--)
            {
                var rescue = rescues[i];
                if (!rescue.Mode.CanRescue(rescue.Pawn))
                {
                    rescues.RemoveAt(i);
                    continue;
                }
                if (!rescue.Ready) continue;
                rescues.RemoveAt(i);
                rescue.Mode.FinishRescue(rescue.Pawn);
            }

            if (now % 30 == 0)
            {
                for (int i = stays.Count - 1; i >= 0; i--)
                {
                    var stay = stays[i];
                    var mode = stay.Coat?.TryGetComp<FixedModeComp>();
                    if (mode?.CanUseRift != true || mode.Wearer.Map != map)
                    {
                        Release(stay, stay.Entry);
                        continue;
                    }
                    if (now < stay.Ends) continue;
                    if (stay.Isolation)
                    {
                        if (!stay.ExitCell.IsValid)
                        {
                            stay.ExitCell = stay.Entry;
                            if (!stay.ExitCell.InBounds(map) || !stay.ExitCell.Standable(map) ||
                                stay.ExitCell.GetFirstPawn(map) != null)
                                stay.ExitCell = CellFinder.RandomClosewalkCellNear(stay.Entry, map, 5,
                                    candidate => candidate.GetFirstPawn(map) == null);
                            stay.ExitStarted = now;
                        }
                        if (now >= stay.ExitStarted + PenanceEntrance.OpeningTicks)
                            Release(stay, stay.ExitCell, completedIsolation: true);
                    }
                    else if (!stay.Treated)
                    {
                        TendInChamber(stay, mode.Wearer);
                        stay.Treated = true;
                    }
                }
            }

            attackTick.Clear();
            attackTick.AddRange(attacks.Keys);
            foreach (var mode in attackTick)
            {
                if (!mode.IsAttacking || mode.Wearer.Map != map) attacks.Remove(mode);
                else if (attacks.TryGetValue(mode, out var strike)) strike.Tick(mode, now);
            }
        }

        static void TendInChamber(RiftStay stay, Pawn doctor)
        {
            var patient = stay.Pawn;
            ThingDef medicine = stay.Advanced ? ThingDefOf.MedicineIndustrial : ThingDefOf.MedicineHerbal;
            float quality = TendUtility.CalculateBaseTendQuality(doctor, patient, medicine);
            float limit = medicine.GetStatValueAbstract(StatDefOf.MedicalQualityMax);
            var batch = new List<Hediff>();
            while (patient.health.HasHediffsNeedingTend())
            {
                TendUtility.GetOptimalHediffsToTendWithSingleTreatment(patient, true, batch);
                if (batch.Count == 0) break;
                for (int i = 0; i < batch.Count; i++) batch[i].Tended(quality, limit, i);
                if (batch.Any(hediff => hediff.TendableNow())) break;
                batch.Clear();
            }
        }

        internal void RegisterAttack(FixedModeComp mode)
        {
            if (!attacks.ContainsKey(mode)) attacks.Add(mode, new RiftStrike());
        }

        internal void CancelAttack(FixedModeComp mode) => attacks.Remove(mode);

        public override void MapComponentDraw()
        {
            foreach (var rescue in rescues) rescue.Draw();
            foreach (var entrance in entrances) entrance.Draw();
            foreach (var exit in exits) exit.Draw();
            foreach (var stay in stays)
                if (stay.ExitCell.IsValid)
                    PenanceEntrance.DrawExitOpening(stay.ExitCell.ToVector3Shifted(),
                        Find.TickManager.TicksGame - stay.ExitStarted);
            foreach (var pair in attacks)
                if (pair.Key.IsAttacking)
                {
                    pair.Key.DrawRiftRange();
                    pair.Value.Draw();
                }
        }
    }

    public sealed class CombatGasDuration : HediffWithComps
    {
        const string GasName = "YR_Tentacle_Combat_Aphrodisiac_Gas";
        internal static HediffDef Gas => DefDatabase<HediffDef>.GetNamedSilentFail(GasName);
        internal static HediffDef Aftereffect => DefDatabase<HediffDef>.GetNamed("YR_LivingCoatRefinedGasAftereffect");
        internal static HediffDef MaskAftereffect => DefDatabase<HediffDef>.GetNamedSilentFail(GasName + "_Aftereffect");

        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);
            DrawGas();
        }

        public override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            if (pawn.IsHashIntervalTick(200, delta)) DrawGas();
        }

        void DrawGas()
        {
            if (!pawn.Spawned || Gas is not { } gas || !pawn.health.hediffSet.HasHediff(gas)) return;
            var mote = DefDatabase<ThingDef>.GetNamedSilentFail(GasName);
            if (mote == null) return;
            for (int i = 0; i < 3; i++)
                MoteMaker.MakeAttachedOverlay(pawn, mote, Vector3.zero, 1f, -1f);
        }

        public override void PostRemoved()
        {
            base.PostRemoved();
            if (pawn.Dead || Gas is not { } gas ||
                pawn.health.hediffSet.GetFirstHediffOfDef(gas) is not { } effect) return;
            pawn.health.RemoveHediff(effect);
            if (Aftereffect is { } aftereffect && !pawn.health.hediffSet.HasHediff(aftereffect))
                pawn.health.AddHediff(aftereffect);
            var fatigue = DefDatabase<HediffDef>.GetNamed("YR_LivingCoatRefinedGasFatigue");
            var fatigueEffect = pawn.health.hediffSet.GetFirstHediffOfDef(fatigue) ?? pawn.health.AddHediff(fatigue);
            fatigueEffect.TryGetComp<HediffComp_Disappears>().ticksToDisappear = 120000;
        }
    }

    public sealed class AmpouleHappyVibration : HediffComp
    {
        const int CheckInterval = 1000;
        int nextCheck = -1;

        public override void CompPostPostAdd(DamageInfo? dinfo) =>
            nextCheck = Find.TickManager.TicksGame + CheckInterval;

        public override void CompExposeData() =>
            Scribe_Values.Look(ref nextCheck, "nextVibrationCheck", -1);

        public override void CompPostTick(ref float severityAdjustment) => CheckForVibration();

        public override void CompPostTickInterval(ref float severityAdjustment, int delta) => CheckForVibration();

        void CheckForVibration()
        {
            int now = Find.TickManager.TicksGame;
            if (nextCheck < 0) nextCheck = now + CheckInterval;
            if (now < nextCheck) return;
            nextCheck = now + CheckInterval;
            if (!Pawn.Spawned || parent.TryGetComp<HediffComp_Disappears>().ticksToDisappear < 400 ||
                Rand.Range(0, 101) > 30) return;

            var vibration = DefDatabase<HediffDef>.GetNamedSilentFail("YR_TentacleWiggle_Hediff");
            if (vibration != null) Pawn.health.AddHediff(vibration);
        }
    }

    public sealed class PenanceCoating : PawnRenderNode
    {
        internal static HediffDef Def => DefDatabase<HediffDef>.GetNamed("YR_LivingCoatPenanceCoating");

        public PenanceCoating(Pawn pawn, PawnRenderNodeProperties properties, PawnRenderTree tree)
            : base(pawn, properties, tree) { }

        public override Graphic GraphicFor(Pawn pawn) => GraphicDatabase.Get<Graphic_Single>(
            Props.texPath, ShaderDatabase.Transparent, Vector2.one, new Color(1f, 1f, 1f, .60f));
    }

    public sealed class PenanceCoatingWorker : PawnRenderNodeWorker
    {
        public override bool CanDrawNow(PawnRenderNode node, PawnDrawParms parms) =>
            !parms.Portrait && !parms.pawn.Dead &&
            (node.Props.parentTagDef != PawnRenderNodeTagDefOf.Head || !parms.flags.FlagSet(PawnRenderFlags.HeadStump)) &&
            base.CanDrawNow(node, parms);

        public override Vector3 OffsetFor(PawnRenderNode node, PawnDrawParms parms, out Vector3 pivot)
        {
            var offset = base.OffsetFor(node, parms, out pivot);
            if (node.Props.parentTagDef == PawnRenderNodeTagDefOf.Body) offset.z -= .21f;
            return offset;
        }

        public override Vector3 ScaleFor(PawnRenderNode node, PawnDrawParms parms)
        {
            var scale = base.ScaleFor(node, parms);
            if (parms.facing.IsHorizontal) scale.x *= .8f;
            return scale;
        }
    }

    public sealed class ErosionGasExposure : HediffWithComps
    {
        const int BerserkExposureTicks = 600;
        const int BerserkRollInterval = 300;
        const float BerserkChance = .25f;
        int firstExposureTick;
        int lastExposureTick = -1;
        bool triggeredBerserk;
        int nextBerserkRollElapsed = BerserkExposureTicks;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref firstExposureTick, "firstExposureTick");
            Scribe_Values.Look(ref lastExposureTick, "lastExposureTick", -1);
            Scribe_Values.Look(ref triggeredBerserk, "triggeredBerserk");
            Scribe_Values.Look(ref nextBerserkRollElapsed, "nextBerserkRollElapsed", BerserkExposureTicks);
        }

        internal void RefreshExposure(int now, int pulseInterval, int exposureTicks)
        {
            if (lastExposureTick == now) return;
            if (lastExposureTick < 0 || now - lastExposureTick > pulseInterval)
            {
                firstExposureTick = now;
                triggeredBerserk = false;
                nextBerserkRollElapsed = BerserkExposureTicks;
            }
            lastExposureTick = now;
            int elapsed = now - firstExposureTick;
            Severity = Mathf.Clamp(elapsed / 60f, 1f, def.maxSeverity);
            this.TryGetComp<HediffComp_Disappears>().ticksToDisappear = exposureTicks;

            if (elapsed < nextBerserkRollElapsed || triggeredBerserk || !pawn.RaceProps.Humanlike) return;
            nextBerserkRollElapsed = elapsed + BerserkRollInterval;
            if (pawn.Downed || pawn.InMentalState || !Rand.Chance(BerserkChance)) return;
            var berserk = DefDatabase<MentalStateDef>.GetNamed("Berserk");
            pawn.mindState?.mentalStateHandler.TryStartMentalState(berserk, forced: true, forceWake: true);
            triggeredBerserk = pawn.MentalStateDef == berserk;
        }
    }

    sealed class RiftErosionGas : IExposable
    {
        internal const float Radius = 3.8f;
        const int ReleaseTicks = 765;
        const int PulseInterval = 60;
        const int ExposureTicks = 150;
        static readonly Color GasColor = new(.96f, .40f, .66f, .78f);
        Faction faction;
        Pawn host;
        Building mount;
        IntVec3 center;

        internal Pawn Host => host;
        int started;
        int nextPulse;

        public RiftErosionGas() { }

        internal RiftErosionGas(Faction faction, IntVec3 center, int now, Pawn host, Building mount)
        {
            this.faction = faction;
            this.center = center;
            this.host = host;
            this.mount = mount;
            started = nextPulse = now;
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref faction, "sourceFaction");
            Scribe_References.Look(ref host, "host");
            Scribe_References.Look(ref mount, "mount");
            Scribe_Values.Look(ref center, "center");
            Scribe_Values.Look(ref started, "started");
            Scribe_Values.Look(ref nextPulse, "nextPulse");
        }

        internal static bool Reaches(IntVec3 center, IntVec3 cell, Map map) =>
            cell.InBounds(map) && cell.Walkable(map) && !cell.Fogged(map) && GenSight.LineOfSight(center, cell, map);

        internal bool Tick(Map map, int now)
        {
            int age = now - started;
            if (age >= ReleaseTicks || (host != null && (!host.Spawned || host.Map != map || host.Dead)) ||
                (mount != null && (!mount.Spawned || mount.Map != map))) return false;
            if (now < nextPulse) return true;
            if (host != null) center = host.Position;
            nextPulse = now + PulseInterval;

            float spread = Mathf.Lerp(1f, Radius, Mathf.Clamp01(age / 240f));
            var effect = DefDatabase<HediffDef>.GetNamed("YR_LivingCoatErosionGasExposure");
            foreach (var cell in GenRadial.RadialCellsAround(center, spread, true))
            {
                if (!Reaches(center, cell, map)) continue;
                foreach (var thing in cell.GetThingList(map))
                    if (thing is Pawn { Dead: false } pawn && faction != null && pawn.HostileTo(faction))
                    {
                        var exposure = pawn.health.hediffSet.GetFirstHediffOfDef(effect) ?? pawn.health.AddHediff(effect);
                        if (exposure is ErosionGasExposure erosion)
                            erosion.RefreshExposure(now, PulseInterval, ExposureTicks);
                    }

                if (cell != center && Rand.Value > .38f) continue;
                var smoke = FleckMaker.GetDataStatic(cell.ToVector3Shifted(), map, FleckDefOf.Smoke, Rand.Range(1.35f, 2.1f));
                smoke.instanceColor = GasColor;
                smoke.rotationRate = Rand.Range(-22f, 22f);
                smoke.velocityAngle = Rand.Range(0f, 360f);
                smoke.velocitySpeed = Rand.Range(.22f, .45f);
                map.flecks.CreateFleck(smoke);
            }
            return true;
        }
    }

    public sealed class RiftStay : IExposable
    {
        internal const int IsolationTicks = 7 * 2500;
        public Thing Coat;
        public Pawn Pawn;
        public IntVec3 Entry;
        public IntVec3 ExitCell = IntVec3.Invalid;
        public int ExitStarted;
        public int Ends;
        public int Duration;
        public bool Isolation, Advanced, Treated;
        public int PenanceWetStage;
        public int PenanceRoomStart = -1;
        internal int RoomStart => PenanceRoomStart >= 0 ? PenanceRoomStart : Ends - IsolationTicks;
        public int PenanceFluidSince = -1;
        internal int FluidStart => PenanceFluidSince >= 0 ? PenanceFluidSince : RoomStart;

        public void ExposeData()
        {
            Scribe_References.Look(ref Coat, "coat");
            Scribe_References.Look(ref Pawn, "pawn");
            Scribe_Values.Look(ref Entry, "entry");
            Scribe_Values.Look(ref ExitCell, "exitCell", IntVec3.Invalid);
            Scribe_Values.Look(ref ExitStarted, "exitStarted");
            Scribe_Values.Look(ref Ends, "ends");
            Scribe_Values.Look(ref Duration, "duration");
            Scribe_Values.Look(ref Isolation, "isolation");
            Scribe_Values.Look(ref Advanced, "advanced");
            Scribe_Values.Look(ref Treated, "treated");
            bool oldWet = false;
            if (Scribe.mode == LoadSaveMode.LoadingVars)
                Scribe_Values.Look(ref oldWet, "penanceWallWet");
            Scribe_Values.Look(ref PenanceRoomStart, "penanceRoomStart", -1);
            Scribe_Values.Look(ref PenanceFluidSince, "penanceFluidSince", -1);
            Scribe_Values.Look(ref PenanceWetStage, "penanceWetStage", oldWet ? 1 : 0);
        }
    }

    [StaticConstructorOnStartup]
    static class RiftHolderRegistration
    {
        static RiftHolderRegistration()
        {
            var harmony = new Harmony("local.yuran.livingcoat.riftholder");
            harmony.Patch(AccessTools.Method(typeof(PawnRenderer), nameof(PawnRenderer.RenderPawnAt)),
                prefix: new HarmonyMethod(typeof(RiftHolderRegistration), nameof(PullPawn)));
            harmony.Patch(
                AccessTools.Method(typeof(Map), nameof(Map.GetChildHolders)),
                postfix: new HarmonyMethod(typeof(RiftHolderRegistration), nameof(AddChambers)));
            harmony.Patch(AccessTools.DeclaredMethod(typeof(MentalBreakWorker_RunWild), nameof(MentalBreakWorker_RunWild.TryStart)),
                prefix: new HarmonyMethod(typeof(RiftHolderRegistration), nameof(RememberKind)),
                postfix: new HarmonyMethod(typeof(RiftHolderRegistration), nameof(RecordRunWild)));
        }

        static void PullPawn(Pawn ___pawn, ref Vector3 drawLoc)
        {
            var chamber = ___pawn.Map?.GetComponent<RiftChambers>();
            if (chamber != null) drawLoc += chamber.IntakeOffset(___pawn);
        }

        static void RememberKind(Pawn pawn, out PawnKindDef __state) =>
            __state = pawn.IsColonist ? pawn.kindDef : null;

        static void RecordRunWild(Pawn pawn, bool __result, PawnKindDef __state)
        {
            if (__result && __state != null && pawn.kindDef == PawnKindDefOf.WildMan)
                pawn.Map?.GetComponent<RiftChambers>()?.TrackWild(pawn, __state);
        }

        static void AddChambers(Map __instance, List<IThingHolder> outChildren)
        {
            var chamber = __instance.GetComponent<RiftChambers>();
            if (chamber?.GetDirectlyHeldThings().Count > 0) outChildren.Add(chamber);
        }
    }

    sealed class RiftStrike
    {
        const int HitAt = 15, VisualEnd = 35;
        static Thing rifle;
        static Material shaft, rim;
        Pawn target;
        int opened = -1000;
        int next;
        float lastAngle;
        Vector3 portal, impact;
        float width;
        bool pending;

        internal void Tick(FixedModeComp mode, int now)
        {
            var wearer = mode.Wearer;
            if (pending && now >= opened + HitAt)
            {
                pending = false;
                if (mode.CanStrike(target))
                {
                    impact = target.DrawPos;
                    impact.y = AltitudeLayer.MoteOverhead.AltitudeFor();
                    var projectile = ThingDef.Named("Bullet_AssaultRifle").projectile;
                    target.TakeDamage(new DamageInfo(DamageDefOf.Stab, projectile.GetDamageAmount(MasterworkRifle()),
                        projectile.GetArmorPenetration(MasterworkRifle()), (impact - portal).AngleFlat(), wearer));
                }
            }
            if (now < next) return;
            target = mode.RiftTarget();
            if (target == null)
            {
                next = now + 20;
                return;
            }
            var gun = MasterworkRifle();
            var verb = gun.def.Verbs[0];
            float cycle = (verb.warmupTime + gun.GetStatValue(StatDefOf.RangedWeapon_Cooldown)) * 60f +
                (verb.burstShotCount - 1) * verb.ticksBetweenBurstShots;
            next = now + Mathf.RoundToInt(cycle / verb.burstShotCount);
            opened = now;
            pending = true;
            lastAngle += Rand.Range(65f, 295f);
            float angle = lastAngle * Mathf.Deg2Rad;
            impact = target.DrawPos;
            impact.y = AltitudeLayer.MoteOverhead.AltitudeFor();
            portal = FindPortal(wearer.Map, impact, angle, Rand.Range(2f, 4f));
            width = Rand.Range(.19f, .29f);
        }

        internal static Vector3 FindPortal(Map map, Vector3 center, float angle, float distance)
        {
            for (int attempt = 0; attempt < 8; attempt++, angle += Mathf.PI / 4f)
            {
                var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                var position = center + direction * distance;
                var cell = position.ToIntVec3();
                if (cell.InBounds(map) && !cell.Fogged(map)) return position;
                position = center + direction * 2f;
                cell = position.ToIntVec3();
                if (cell.InBounds(map) && !cell.Fogged(map)) return position;
            }
            return center;
        }

        static Thing MasterworkRifle()
        {
            if (rifle != null) return rifle;
            rifle = ThingMaker.MakeThing(ThingDef.Named("Gun_AssaultRifle"));
            rifle.TryGetComp<CompQuality>().SetQuality(QualityCategory.Masterwork, ArtGenerationContext.Colony);
            return rifle;
        }

        internal void Draw()
        {
            int age = Find.TickManager.TicksGame - opened;
            if (age < 0 || age >= VisualEnd) return;
            float aperture = Mathf.Clamp01(age / 7f) * Mathf.Clamp01((VisualEnd - age) / 8f);
            float reach = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((age - 5) / 10f)) *
                (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((age - HitAt) / 16f)));
            Vector3 end = age < HitAt && target?.Spawned == true ? target.DrawPos : impact;
            DrawProjection(portal, end, width, reach, aperture);
        }

        internal static void DrawProjection(Vector3 portal, Vector3 end, float width, float reach, float aperture)
        {
            shaft ??= MaterialPool.MatFrom("YuranLivingCoat/Rift/Shaft", ShaderDatabase.Transparent);
            rim ??= MaterialPool.MatFrom("YuranLivingCoat/Rift/Portal", ShaderDatabase.Transparent);
            end.y = portal.y;
            Vector3 tip = Vector3.Lerp(portal, end, reach);
            var rotation = Quaternion.Euler(0f, (end - portal).AngleFlat(), 0f);
            if (reach > 0f)
                DrawQuad((portal + tip) * .5f, rotation, width, (tip - portal).magnitude, shaft);
            var rimPosition = portal + Vector3.up * .01f;
            DrawQuad(rimPosition, rotation, width * 2.9f * aperture, width * .9f * aperture, rim);
        }

        internal static void DrawQuad(Vector3 point, Quaternion rotation, float x, float z, Material material) =>
            GenDraw.DrawMeshNowOrLater(MeshPool.plane10, Matrix4x4.TRS(point, rotation, new Vector3(x, 1f, z)),
                material, false);
    }

    sealed class RiftRescue
    {
        const int ContactAt = 28, SuctionAt = 40, EndAt = 76;
        internal readonly FixedModeComp Mode;
        internal readonly Pawn Pawn;
        readonly int started;
        readonly Vector3 portal;

        internal RiftRescue(FixedModeComp mode, Pawn pawn)
        {
            Mode = mode;
            Pawn = pawn;
            started = Find.TickManager.TicksGame;
            Vector3 center = pawn.DrawPos;
            Vector3 direction = center - mode.Wearer.DrawPos;
            portal = RiftStrike.FindPortal(pawn.Map, center, Mathf.Atan2(direction.z, direction.x), 2f);
        }

        int Age => Find.TickManager.TicksGame - started;
        internal bool Ready => Age >= EndAt;
        static float Ease(float value) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(value));

        internal Vector3 PullOffset()
        {
            Vector3 offset = portal - Pawn.DrawPos;
            offset.y = 0f;
            float progress = Mathf.Clamp01((Age - SuctionAt) / (float)(EndAt - SuctionAt));
            return offset * (progress * progress);
        }

        internal void Draw()
        {
            if (!Pawn.Spawned) return;
            int age = Age;
            var opening = portal;
            opening.y = AltitudeLayer.MoteOverhead.AltitudeFor();
            Vector3 target = Pawn.DrawPos;
            float aperture = Ease(age / 13f) * (1f - .45f * Ease((age - SuctionAt) / (float)(EndAt - SuctionAt)));
            // The strand retracts before suction moves the pawn.
            float reach = Ease((age - 8f) / (ContactAt - 8f)) *
                (1f - Ease((age - ContactAt) / (float)(SuctionAt - ContactAt)));
            RiftStrike.DrawProjection(opening, target, .36f, reach, aperture);
        }
    }
}

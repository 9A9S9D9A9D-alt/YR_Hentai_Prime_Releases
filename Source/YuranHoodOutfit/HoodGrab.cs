using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace YuranHoodOutfit
{
    public sealed class HoodGrab : GameComponent
    {
        internal const int HoldTick = 29, FinishTick = 51, EndTick = 82;
        private const float Range = 3f;
        internal static HoodGrab Current => Verse.Current.Game?.GetComponent<HoodGrab>();
        private List<Grab> active = new List<Grab>();
        private Dictionary<Pawn, int> cooldown = new Dictionary<Pawn, int>();
        private List<Pawn> cooldownPawns;
        private List<int> cooldownTicks;
        private static HediffDef Pinned => DefDatabase<HediffDef>.GetNamed("YR_HoodGrabPinned");
        private static HediffDef FullyPinned => DefDatabase<HediffDef>.GetNamed("YR_HoodGrabFullyPinned");
        public HoodGrab(Game game) { }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref active, "hoodGrabs", LookMode.Deep);
            Scribe_Collections.Look(ref cooldown, "hoodGrabCooldown", LookMode.Reference, LookMode.Value,
                ref cooldownPawns, ref cooldownTicks);
            if (Scribe.mode != LoadSaveMode.PostLoadInit) return;
            active ??= new List<Grab>();
            cooldown ??= new Dictionary<Pawn, int>();
        }

        internal Grab Attacker(Pawn pawn) => active.FirstOrDefault(grab => grab.Source == pawn);
        internal Grab Victim(Pawn pawn) => active.FirstOrDefault(grab => grab.Target == pawn && !grab.Resolved);
        private bool Busy(Pawn pawn) => active.Any(grab => grab.Source == pawn || grab.Target == pawn);
        internal bool HasFullPin => active.Any(grab => grab.FullPin);

        internal bool ProtectedByFullPin(Pawn pawn)
        {
            foreach (Grab grab in active)
            {
                if (grab.FullPin && (grab.Source == pawn || grab.Target == pawn) &&
                    grab.Source?.Spawned == true && !grab.Source.Dead && !grab.Source.Downed &&
                    grab.Source.Position == grab.Origin && OutfitRendering.SettingsFor(grab.Source) != null &&
                    grab.Target?.Spawned == true && !grab.Target.Dead && grab.Source.Map == grab.Target.Map &&
                    grab.Target.Position == grab.TargetCell && grab.Target.health.hediffSet.HasHediff(FullyPinned))
                    return true;
            }
            return false;
        }

        internal static IEnumerable<Gizmo> Commands(OutfitLayers outfit)
        {
            if (outfit.parent is not Apparel { Wearer: Pawn pawn } || !pawn.IsColonistPlayerControlled) yield break;
            yield return Command(pawn, outfit, true);
            yield return Command(pawn, outfit, false);
            yield return PinCommand(pawn, outfit, false);
            yield return PinCommand(pawn, outfit, true);
        }

        internal static Command_Action PinCommand(Pawn source, OutfitLayers outfit, bool release)
        {
            var command = new Command_Action
            {
                defaultLabel = release ? "완전제압 해제" : "완전제압 착취",
                defaultDesc = release ? "완전제압 중인 대상의 제압을 풉니다."
                    : "쓰러진 대상을 지정해 해제할 때까지 제압합니다.",
                icon = outfit.parent.def.uiIcon,
                groupable = false,
                action = () => Current?.TargetFullPin(source, release)
            };
            if (Current == null || !source.Spawned) command.Disable("현재 맵에서 사용할 수 없습니다.");
            return command;
        }

        private void TargetFullPin(Pawn source, bool release)
        {
            if (!source.Spawned) return;
            if (release && Attacker(source) is Grab { FullPin: true, Target: Pawn pinned } &&
                pinned.Spawned && !pinned.Dead && pinned.Map == source.Map &&
                pinned.health.hediffSet.GetFirstHediffOfDef(FullyPinned) is Hediff activePin)
            {
                pinned.health.RemoveHediff(activePin);
                var grab = Attacker(source);
                End(active.IndexOf(grab), grab);
                return;
            }
            bool Valid(LocalTargetInfo aim) => source.Spawned &&
                aim.Pawn is Pawn target && target != source && target.Spawned && !target.Dead &&
                target.Map == source.Map && (release ? target.health.hediffSet.HasHediff(FullyPinned) : target.Downed);
            Find.Targeter.BeginTargeting(new TargetingParameters
            {
                canTargetPawns = true, canTargetLocations = false, canTargetBuildings = false,
                canTargetItems = false, validator = aim => aim.Thing is Pawn pawn && Valid(new LocalTargetInfo(pawn))
            }, aim =>
            {
                if (!Valid(aim)) return;
                if (release) aim.Pawn.health.RemoveHediff(aim.Pawn.health.hediffSet.GetFirstHediffOfDef(FullyPinned));
                else ApplyFullPin(source, aim.Pawn);
            }, null, Valid, source);
        }

        private void ApplyFullPin(Pawn source, Pawn target)
        {
            var previous = Attacker(source);
            if (previous != null) End(active.IndexOf(previous), previous);
            if (!target.health.hediffSet.HasHediff(FullyPinned)) target.health.AddHediff(FullyPinned);
            target.pather.StopDead();
            if (target.equipment is Pawn_EquipmentTracker equipment)
            {
                var items = equipment.AllEquipmentListForReading;
                for (int i = items.Count - 1; i >= 0; i--)
                {
                    ThingWithComps item = items[i];
                    if (target.inventory == null || !equipment.TryTransferEquipmentToContainer(item, target.inventory.innerContainer))
                        equipment.TryDropEquipment(item, out _, target.Position);
                }
            }
            if (target.apparel is Pawn_ApparelTracker apparel)
            {
                var worn = apparel.WornApparel;
                for (int i = worn.Count - 1; i >= 0; i--)
                {
                    Apparel item = worn[i];
                    if (target.inventory == null ||
                        !apparel.GetDirectlyHeldThings().TryTransferToContainer(item, target.inventory.innerContainer))
                        apparel.TryDrop(item, out _, target.Position);
                }
            }
            if (target.health.hediffSet.GetFirstHediffOfDef(Pinned) is Hediff temporaryPin)
                target.health.RemoveHediff(temporaryPin);
            active.Add(new Grab
            {
                Source = source, Target = target, FullPin = true,
                StartTick = Find.TickManager.TicksGame, Origin = source.Position, TargetCell = target.Position,
                East = target.Position.x >= source.Position.x
            });
            source.pather.StopDead();
            Invalidate(source);
            Invalidate(target);
        }

        private static Command_Action Command(Pawn pawn, OutfitLayers outfit, bool assassinate)
        {
            var command = new Command_Action
            {
                defaultLabel = assassinate ? "목 잡기 · 암살" : "목 잡기 · 제압",
                defaultDesc = assassinate ? "양다리로 목을 감고 케이프 촉수 여섯 가닥으로 즉사시킵니다."
                    : "양다리로 목을 감고 넘어뜨려 8초간 무력화합니다.",
                icon = outfit.parent.def.uiIcon,
                groupable = false,
                action = () => Current?.Target(pawn, assassinate)
            };
            if (Current?.CanStart(pawn) != true) command.Disable("사용할 수 없거나 재사용 대기 중입니다.");
            return command;
        }

        private bool CanStart(Pawn pawn) => pawn.Spawned && !pawn.Dead && !pawn.Downed && !Busy(pawn) &&
            OutfitRendering.SettingsFor(pawn) != null &&
            (!cooldown.TryGetValue(pawn, out int until) || until <= Find.TickManager.TicksGame);

        private void Target(Pawn source, bool assassinate)
        {
            bool Valid(LocalTargetInfo aim) => CanStart(source) && aim.Pawn is Pawn target &&
                target.Spawned && target.Map == source.Map && !target.Dead && !target.Downed &&
                target.RaceProps.Humanlike && target.HostileTo(source) && !Busy(target) &&
                (assassinate || !target.kindDef.forceDeathOnDowned) &&
                source.Position.DistanceToSquared(target.Position) <= Range * Range &&
                GenSight.LineOfSight(source.Position, target.Position, source.Map);

            Find.Targeter.BeginTargeting(new TargetingParameters
            {
                canTargetPawns = true, canTargetLocations = false, canTargetBuildings = false,
                canTargetItems = false, validator = target => target.Thing is Pawn pawn && Valid(new LocalTargetInfo(pawn))
            }, aim =>
            {
                if (!Valid(aim)) return;
                Pawn target = aim.Pawn;
                var grab = new Grab
                {
                    Source = source, Target = target, Assassinate = assassinate,
                    StartTick = Find.TickManager.TicksGame, Origin = source.Position, TargetCell = target.Position,
                    East = target.Position.x >= source.Position.x
                };
                active.Add(grab);
                cooldown[source] = grab.StartTick + 600;
                source.pather.StopDead();
                target.pather.StopDead();
                source.stances.stunner.StunFor(EndTick, target);
                target.stances.stunner.StunFor(FinishTick + 1, source);
                Invalidate(source);
                Invalidate(target);
            }, null, Valid, source, onUpdateAction: _ => GenDraw.DrawRadiusRing(source.Position, Range));
        }

        public override void GameComponentTick()
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                Grab grab = active[i];
                Pawn source = grab.Source;
                Pawn target = grab.Target;
                if (source?.Spawned != true || source.Dead || source.Downed || source.Position != grab.Origin ||
                    OutfitRendering.SettingsFor(source) == null ||
                    (grab.FullPin
                        ? target?.Spawned != true || target.Dead || source.Map != target.Map ||
                          target.Position != grab.TargetCell || !target.health.hediffSet.HasHediff(FullyPinned)
                        : grab.Age >= EndTick || (!grab.Resolved &&
                          (target?.Spawned != true || target.Dead || source.Map != target.Map ||
                           target.Downed || target.Position != grab.TargetCell))))
                {
                    End(i, grab);
                    continue;
                }
                if (grab.Age == 35 || (!grab.FullPin && grab.Age == 67)) Invalidate(source);
                if (grab.FullPin)
                {
                    if (grab.PinLoop) grab.MaintainPinSound();
                    continue;
                }
                if (grab.Resolved || grab.Age < FinishTick) continue;
                grab.Resolved = true;
                if (grab.Assassinate)
                    target.Kill(new DamageInfo(DamageDefOf.Stab, 1f, 0f, -1f, source));
                else
                {
                    target.health.AddHediff(Pinned);
                    target.stances.stunner.StunFor(480, source);
                }
                Invalidate(target);
            }
            if (Find.TickManager.TicksGame % 600 == 0)
                foreach (Pawn pawn in new List<Pawn>(cooldown.Keys))
                    if (pawn.Destroyed || cooldown[pawn] <= Find.TickManager.TicksGame) cooldown.Remove(pawn);
        }

        private void End(int index, Grab grab)
        {
            active.RemoveAt(index);
            grab.PinSound?.End();
            grab.CapeDrawing?.Dispose();
            Invalidate(grab.Source);
            Invalidate(grab.Target);
        }

        private static void Invalidate(Pawn pawn)
        {
            pawn?.Drawer?.renderer?.renderTree.SetDirty();
            if (pawn != null) PortraitsCache.SetDirty(pawn);
        }

        public sealed class Grab : IExposable
        {
            internal Pawn Source, Target;
            internal IntVec3 Origin, TargetCell;
            internal int StartTick;
            internal bool Assassinate, East, Resolved, FullPin;
            internal HoodGrabDrawing.CapeMeshes CapeDrawing;
            internal Sustainer PinSound;

            internal void MaintainPinSound()
            {
                if (PinSound == null || PinSound.Ended)
                    PinSound = DefDatabase<SoundDef>.GetNamed("YR_HoodGrabPinLoop").TrySpawnSustainer(
                        SoundInfo.InMap(Target, MaintenanceType.PerTick));
                PinSound?.Maintain();
            }
            internal int Age => Find.TickManager.TicksGame - StartTick;
            internal bool CapeAction => Age >= 35 && (FullPin || Age < 67);
            internal bool UseYuranPose => !FullPin && IsYuran(Source);
            internal static bool IsYuran(Pawn pawn) =>
                pawn?.def.defName is "Yuran_Race" or "Yuran_Race_Miko_BlackSnake";
            internal float Side => East ? 1f : -1f;
            internal bool PinLoop => FullPin && Age >= HoldTick;
            internal float PinPulse => PinLoop
                ? Mathf.Sin((Age - HoldTick) % 36 * (2f * Mathf.PI / 36f)) : 0f;
            internal float Takedown => Assassinate ? 0f : Mathf.Clamp01((Age - 34f) / 17f);
            internal Vector3 TargetSpot => TargetCell.ToVector3Shifted();
            internal float PoseBlend => Mathf.SmoothStep(0f, 1f, Age / (float)HoldTick);
            internal float Release => FullPin ? 0f : Mathf.Clamp01((Age - FinishTick - (Assassinate ? 0f : 9f)) /
                (EndTick - FinishTick - (Assassinate ? 0f : 9f)));

            public void ExposeData()
            {
                Scribe_References.Look(ref Source, "source");
                Scribe_References.Look(ref Target, "target");
                Scribe_Values.Look(ref Origin, "origin");
                Scribe_Values.Look(ref TargetCell, "targetCell");
                Scribe_Values.Look(ref StartTick, "startTick");
                Scribe_Values.Look(ref Assassinate, "assassinate");
                Scribe_Values.Look(ref East, "east");
                Scribe_Values.Look(ref Resolved, "resolved");
                Scribe_Values.Look(ref FullPin, "fullPin");
            }

            internal Vector3 BodyShift
            {
                get
                {
                    var outfit = OutfitRendering.SettingsFor(Source);
                    var contact = outfit == null ? new Vector3(45, 0, -15) : East ? outfit.GrabEast : outfit.GrabWest;
                    float units = HumanlikeMeshPoolUtility.HumanlikeBodyWidthForPawn(Source) / 256f;
                    var end = TargetSpot - Origin.ToVector3Shifted() +
                        new Vector3(Side * (contact.x - 88f) * units, 0, .63f - contact.z * units);
                    float rise = Mathf.Clamp01(Age / (float)HoldTick);
                    float release = Release;
                    var shift = end * (rise * (2f - rise)) * (1f - release);
                    shift.z += Mathf.Sin(rise * Mathf.PI) * (32f * units);
                    if (FullPin)
                    {
                        var offset = (East ? outfit?.PinOffsetEast : outfit?.PinOffsetWest) ?? Vector2.zero;
                        shift += new Vector3(Side * offset.x, 0f, -offset.y) * (units * rise);
                        shift += new Vector3(Side * PinPulse * 4f, 0f, PinPulse * 3f) * units;
                    }
                    else shift.z -= .48f * Takedown * (1f - release);
                    return shift;
                }
            }
        }
    }

    internal static class HoodGrabDrawing
    {
        private static Material cape, grabTorso, nearLeg, farLeg;

        internal static void Install(Harmony harmony)
        {
            harmony.Patch(AccessTools.Method(typeof(Pawn), nameof(Pawn.GetGizmos)),
                postfix: new HarmonyMethod(typeof(HoodGrabDrawing), nameof(ShowOnlyFullPinRelease)) { priority = Priority.Last });
            harmony.Patch(AccessTools.Method(typeof(Pawn_HealthTracker), "PreApplyDamage"),
                prefix: new HarmonyMethod(typeof(HoodGrabDrawing), nameof(AbsorbFullPinDamage)) { priority = Priority.First });
            harmony.Patch(AccessTools.Method(typeof(Pawn), nameof(Pawn.ThreatDisabled)),
                postfix: new HarmonyMethod(typeof(HoodGrabDrawing), nameof(DisableFullPinThreat)));
            harmony.Patch(AccessTools.Method(typeof(Verse.AI.AttackTargetFinder), nameof(Verse.AI.AttackTargetFinder.BestAttackTarget)),
                prefix: new HarmonyMethod(typeof(HoodGrabDrawing), nameof(FilterFullPinTargets)));
            harmony.Patch(AccessTools.Method(typeof(PawnRenderTree), nameof(PawnRenderTree.ParallelPreDraw)),
                postfix: new HarmonyMethod(typeof(HoodGrabDrawing), nameof(AdjustPose)));
            harmony.Patch(AccessTools.Method(typeof(PawnRenderTree), nameof(PawnRenderTree.Draw)),
                postfix: new HarmonyMethod(typeof(HoodGrabDrawing), nameof(DrawParts)));
            cape = MaterialPool.MatFrom("Yuran/Implants/CoccyxTentacles/ReinforcedShaft", ShaderDatabase.Cutout);
            const string posePath = "Yuran/Apparel/YuranHoodOutfit/GrabPose";
            grabTorso = MaterialPool.MatFrom(posePath + "Torso", ShaderDatabase.Cutout);
            nearLeg = MaterialPool.MatFrom(posePath + "Near", ShaderDatabase.Cutout);
            farLeg = MaterialPool.MatFrom(posePath + "Far", ShaderDatabase.Cutout);
        }

        private static void ShowOnlyFullPinRelease(Pawn __instance, ref IEnumerable<Gizmo> __result)
        {
            var current = HoodGrab.Current;
            if (current?.Attacker(__instance)?.FullPin != true || !current.ProtectedByFullPin(__instance)) return;
            if (OutfitRendering.SettingsFor(__instance) is OutfitLayers outfit)
                __result = new[] { HoodGrab.PinCommand(__instance, outfit, true) };
        }

        private static bool AbsorbFullPinDamage(Pawn ___pawn, ref bool absorbed)
        {
            if (HoodGrab.Current?.ProtectedByFullPin(___pawn) != true) return true;
            absorbed = true;
            return false;
        }

        private static void DisableFullPinThreat(Pawn __instance, ref bool __result)
        {
            if (!__result && HoodGrab.Current?.ProtectedByFullPin(__instance) == true) __result = true;
        }

        private static void FilterFullPinTargets(ref Predicate<Thing> validator)
        {
            var current = HoodGrab.Current;
            if (current == null || !current.HasFullPin) return;
            var previous = validator;
            validator = thing => (thing is not Pawn pawn || !current.ProtectedByFullPin(pawn)) &&
                (previous == null || previous(thing));
        }

        private static void AdjustPose(PawnDrawParms parms, List<PawnGraphicDrawRequest> ___drawRequests)
        {
            if (parms.Portrait || parms.Cache || HoodGrab.Current == null) return;
            var grab = HoodGrab.Current.Attacker(parms.pawn);
            var victim = HoodGrab.Current.Victim(parms.pawn);
            if (grab == null && (victim == null || victim.Resolved)) return;
            Matrix4x4 transform = Matrix4x4.identity;
            if (grab != null)
            {
                transform = Matrix4x4.Translate(grab.BodyShift + Vector3.up * ForegroundLift(parms));
                float lean = grab.UseYuranPose ? 0f : grab.Assassinate ? 12f :
                    grab.FullPin ? 16f + grab.PinPulse * 4f : 10f + grab.Takedown * 19f;
                var pivot = parms.matrix.MultiplyPoint3x4(Vector3.zero);
                transform *= Matrix4x4.Translate(pivot) *
                    Matrix4x4.Rotate(Quaternion.AngleAxis(lean * grab.PoseBlend * (1f - grab.Release) * grab.Side, Vector3.up)) *
                    Matrix4x4.Translate(-pivot);
            }
            else
            {
                var center = victim.FullPin ? parms.matrix.MultiplyPoint3x4(Vector3.zero) : victim.TargetSpot;
                var rotation = victim.FullPin
                    ? Quaternion.AngleAxis(90f * victim.Side, Vector3.up) * Quaternion.Inverse(parms.matrix.rotation)
                    : Quaternion.AngleAxis(victim.Takedown * 82f * victim.Side, Vector3.up);
                transform = Matrix4x4.Translate(center) *
                    Matrix4x4.Rotate(rotation) *
                    Matrix4x4.Translate(-center);
            }
            for (int i = 0; i < ___drawRequests.Count; i++)
            {
                var request = ___drawRequests[i];
                if (grab != null && request.node.GetType().FullName == "YR_RJW.PawnRenderNode_CoccyxTentacles")
                {
                    ___drawRequests.RemoveAt(i--);
                    continue;
                }
                if (grab?.UseYuranPose == true && request.node is PawnRenderNode_Body)
                {
                    float width = HumanlikeMeshPoolUtility.HumanlikeBodyWidthForPawn(parms.pawn);
                    request = new PawnGraphicDrawRequest(request.node, grab.East ? MeshPool.plane10 : MeshPool.plane10Flip, grabTorso)
                    {
                        preDrawnComputedMatrix = request.preDrawnComputedMatrix *
                            Matrix4x4.Scale(new Vector3(width, 1f, width))
                    };
                }
                request.preDrawnComputedMatrix = transform * request.preDrawnComputedMatrix;
                ___drawRequests[i] = request;
            }
        }

        private static void DrawParts(PawnDrawParms parms, List<PawnGraphicDrawRequest> ___drawRequests)
        {
            if (parms.Portrait || parms.Cache || parms.flags.FlagSet(PawnRenderFlags.Invisible)) return;
            var grab = HoodGrab.Current?.Attacker(parms.pawn);
            if (grab == null) return;
            float depth = AltitudeLayer.MoteOverhead.AltitudeFor() + .01f;
            float width = HumanlikeMeshPoolUtility.HumanlikeBodyWidthForPawn(parms.pawn);
            if (grab.UseYuranPose)
            {
                var torso = parms.matrix.MultiplyPoint3x4(Vector3.zero) + grab.BodyShift;
                var outfit = OutfitRendering.SettingsFor(parms.pawn);
                Vector2 angles = (grab.East ? outfit?.GrabAnglesEast : outfit?.GrabAnglesWest) ?? new Vector2(2f, -39f);
                float close = grab.PoseBlend * (1f - grab.Release);
                DrawLeg(grab, torso, width, Mathf.Lerp(74f, angles.y, close), 0f, depth - .004f, farLeg, parms.DrawNow);
                DrawLeg(grab, torso, width, Mathf.Lerp(116f, angles.x, close), -5f,
                    depth + PawnRenderUtility.AltitudeForLayer(48), nearLeg, parms.DrawNow);
            }
            if (!grab.CapeAction) return;
            var contact = grab.TargetSpot + new Vector3(grab.Side * grab.Takedown * .27f, 0, .46f - grab.Takedown * .34f);
            foreach (var request in ___drawRequests)
            {
                if (request.node is not OutfitPartNode { PartName: "Collar" }) continue;
                var collar = request.preDrawnComputedMatrix;
                Vector3 root = collar.MultiplyPoint3x4(Vector3.zero);
                DrawCape(grab, collar, width, contact, -1, grab.Target.DrawPos.y - .006f, parms.DrawNow);
                DrawCape(grab, collar, width, contact, 1, Mathf.Min(depth, root.y) - .002f, parms.DrawNow);
                break;
            }
        }

        private static void DrawLeg(HoodGrab.Grab grab, Vector3 torso, float width, float angle,
            float offsetX, float depth, Material material, bool drawNow)
        {
            float units = width / 256f;
            var pivot = new Vector3(-7f * grab.Side, 0f, -62f) * units;
            torso.y = depth;
            torso.x += offsetX * units * grab.Side;
            var rotation = Quaternion.AngleAxis(angle * grab.Side, Vector3.up);
            var matrix = Matrix4x4.TRS(torso + pivot - rotation * pivot, rotation, new Vector3(width, 1f, width));
            GenDraw.DrawMeshNowOrLater(grab.East ? MeshPool.plane10 : MeshPool.plane10Flip, matrix, material, drawNow);
        }

        private static float ForegroundLift(PawnDrawParms parms)
        {
            float baseDepth = parms.matrix.MultiplyPoint3x4(Vector3.zero).y;
            // Preserve the attacker's local layer order above the target.
            return AltitudeLayer.MoteOverhead.AltitudeFor() + .01f - baseDepth;
        }

        private static void DrawCape(HoodGrab.Grab grab, Matrix4x4 collar, float width,
            Vector3 neck, int side, float depth, bool drawNow)
        {
            for (int i = 0; i < 3; i++)
            {
                float strike = grab.Assassinate
                    ? Mathf.Clamp01((grab.Age - 34f - i) / 12f) * (1f - Mathf.Clamp01((grab.Age - 52f) / 15f))
                    : Mathf.Clamp01((grab.Age - 35f - i) / 8f) * (1f - grab.Release);
                strike = Mathf.SmoothStep(0f, 1f, strike);
                var root = collar.MultiplyPoint3x4(new Vector3(
                    grab.Side * (.005f - i * .013f - side * .012f), 0f, .035f - i * .024f) * width);
                var trailing = root + new Vector3(-grab.Side * (.40f + i * .092f), 0, -.22f - i * .078f + side * .10f);
                var contact = neck + new Vector3(grab.Side * (i - 1) * .064f, 0, side * .049f + (i - 1) * .046f);
                if (!grab.Assassinate)
                    contact += new Vector3(grab.Side * (.08f + i * .10f), 0f, side * (.13f + grab.PinPulse * .025f));
                var tip = Vector3.Lerp(trailing, contact, strike);
                var shoulder = root + new Vector3(-grab.Side * (.15f + i * .025f), 0,
                    -.045f - i * .028f);
                var sweep = Vector3.Lerp(root, tip, .72f) + new Vector3(
                    grab.Side * strike * (grab.Assassinate ? -.12f : .22f), 0,
                    -.10f + side * .045f - i * .031f + grab.PinPulse * .016f);
                float girth = width * (.035f - i * .004f) * (1f + grab.PinPulse * .065f);
                grab.CapeDrawing ??= new CapeMeshes();
                grab.CapeDrawing.Draw((side < 0 ? 0 : 3) + i, root, shoulder, sweep, tip,
                    girth, depth + i * .001f, drawNow);
            }
        }

        internal sealed class CapeMeshes
        {
            private const int Steps = 24;
            private readonly Mesh[] strands = new Mesh[6];
            private readonly Vector3[] vertices = new Vector3[(Steps + 1) * 2];

            private Mesh CreateMesh()
            {
                var uv = new Vector2[vertices.Length];
                var triangles = new int[Steps * 6];
                for (int i = 0; i <= Steps; i++)
                {
                    int edge = i * 2;
                    // The shaft's scales run along U. Leave out the rounded end so the mesh can taper to a point.
                    float u = Mathf.Lerp(.01f, .87f, i / (float)Steps);
                    uv[edge] = new Vector2(u, .02f);
                    uv[edge + 1] = new Vector2(u, .98f);
                    if (i == Steps) break;
                    int triangle = i * 6;
                    triangles[triangle] = edge;
                    triangles[triangle + 1] = edge + 2;
                    triangles[triangle + 2] = edge + 1;
                    triangles[triangle + 3] = edge + 1;
                    triangles[triangle + 4] = edge + 2;
                    triangles[triangle + 5] = edge + 3;
                }
                var mesh = new Mesh { name = "Yuran hood grab cape" };
                mesh.vertices = vertices;
                mesh.uv = uv;
                mesh.triangles = triangles;
                return mesh;
            }

            internal void Draw(int index, Vector3 root, Vector3 shoulder, Vector3 sweep, Vector3 tip,
                float girth, float depth, bool drawNow)
            {
                Mesh mesh = strands[index] ??= CreateMesh();
                for (int i = 0; i <= Steps; i++)
                {
                    float t = i / (float)Steps;
                    Vector3 first = Vector3.Lerp(root, shoulder, t);
                    Vector3 middle = Vector3.Lerp(shoulder, sweep, t);
                    Vector3 last = Vector3.Lerp(sweep, tip, t);
                    Vector3 ab = Vector3.Lerp(first, middle, t), bc = Vector3.Lerp(middle, last, t);
                    Vector3 center = Vector3.Lerp(ab, bc, t);
                    center.y = depth;
                    Vector3 tangent = bc - ab;
                    Vector3 edge = new Vector3(tangent.z, 0f, -tangent.x).normalized *
                        (girth * (1f - Mathf.SmoothStep(0f, 1f, t)));
                    vertices[i * 2] = center - edge;
                    vertices[i * 2 + 1] = center + edge;
                }
                mesh.vertices = vertices;
                mesh.RecalculateBounds();
                GenDraw.DrawMeshNowOrLater(mesh, Matrix4x4.identity, cape, drawNow);
            }

            internal void Dispose()
            {
                foreach (Mesh mesh in strands)
                    if (mesh != null) UnityEngine.Object.Destroy(mesh);
            }
        }
    }
}

using RimWorld;
using RimWorld.Utility;
using System;
using UnityEngine;
using Verse;
using Verse.AI;

namespace YR_Booster
{
    public class Verb_Booster : Verb
    {
        public static IntVec3 BoosterTarget;

        private float cachedEffectiveRange = -1f;
        public CompBooster BoosterCompSource
        {
            get
            {
                return DirectOwner as CompBooster;
            }
        }
        public new ThingWithComps EquipmentSource
        {
            get
            {
                if (EquipmentCompSource != null)
                {
                    return EquipmentCompSource.parent;
                }
                if (BoosterCompSource != null)
                {
                    return BoosterCompSource.parent;
                }
                if (ReloadableCompSource != null)
                {
                    return ReloadableCompSource.parent;
                }
                return null;
            }
        }

        public CompBooster CompBooster
        {
            get
            {
                if (BoosterCompSource != null)
                {
                    return BoosterCompSource;
                }

                return EquipmentSource.TryGetComp<CompBooster>();
            }
        }

        public override float EffectiveRange
        {
            get
            {
                //This part makes the code get the defs for Mincho_SpaceBooster
                if (cachedEffectiveRange < 0.0)
                {

                    if (EquipmentSource.TryGetComp<CompQuality>() != null)
                    {

                        cachedEffectiveRange = EquipmentSource.GetStatValue(CompBooster.Props.statDef);

                    }
                    else
                    {
                        cachedEffectiveRange = CompBooster.Props.nonQualityEffectiveRange;
                    }
                }
                return cachedEffectiveRange;
            }
        }

        public override bool MultiSelect => true;

        protected override bool TryCastShot()
        {
            return Booster(CasterPawn, currentTarget, ReloadableCompSource, CompBooster, verbProps);
        }

        public bool Booster(Pawn pawn, LocalTargetInfo currentTarget, CompApparelReloadable compReloadable, CompBooster compBooster, VerbProperties verbProps)
        {

            //it is changed to fix a bug that if it has 0 fuel it cannot run

            if (pawn == null)
            {
                return false;
            }

            if (compReloadable != null && !compReloadable.CanBeUsed(out _))
            {
                return false;
            }
            if (compReloadable != null)
            {
                compReloadable.UsedOnce();
            }

            IntVec3 position = pawn.Position;
            IntVec3 cell = currentTarget.Cell;
            Map map = pawn.Map;

            if (compBooster != null)
            {
                if (compReloadable == null && compBooster.cooldownTicks > 0)
                {
                    return false;
                }
                compBooster.cooldownTicks = compBooster.Props.cooldownTicks;
                foreach (var hediffDef in compBooster.Props.hediffDefs)
                {
                    pawn.health.AddHediff(hediffDef);
                }
                if (compBooster.faceCell)
                {
                    pawn.rotationTracker.FaceCell(cell);
                }
            }
            else
            {
                return false;
            }
            bool flag = Find.Selector.IsSelected(pawn);
            //This part was changed makes the code idenpendent from Royalty
            PawnFlyer newThing = PawnFlyer.MakeFlyer(CompBooster.Props.flyingDef, pawn, cell, verbProps.flightEffecterDef, verbProps.soundLanding, verbProps.flyWithCarriedThing);

            if (newThing != null)
            {
                GenSpawn.Spawn(newThing, cell, map);
                if (flag)
                {
                    Find.Selector.Select(pawn, false, false);
                }
                return true;
            }
            return false;
        }

        public override void OrderForceTarget(LocalTargetInfo target)
        {
            CasterPawn.stances.CancelBusyStanceHard();

            Map map = CasterPawn.Map;
            IntVec3 intVec3 = RCellFinder.BestOrderedGotoDestNear(target.Cell, CasterPawn, new Predicate<IntVec3>(AcceptableDestination));

            //reloadableCompSource.UsedOnce(); is moved from TryCastShot to here to balance in case player tries to cheat the system by repetedly skip but than cancle the action
            BoosterTarget = intVec3;
            //This part were changed makes the code idenpendent from Royalty

            Job job = JobMaker.MakeJob(JobDefOf.CastJump, intVec3);
            job.verbToUse = this;
            if (!CasterPawn.jobs.TryTakeOrderedJob(job))
            {
                return;
            }
            FleckMaker.Static(intVec3, map, FleckDefOf.FeedbackGoto);

            bool AcceptableDestination(IntVec3 c) => ValidJumpTarget(map, c) && CanHitTargetFrom(caster.Position, (LocalTargetInfo)c);
        }

        public override bool ValidateTarget(LocalTargetInfo target, bool showMessages = true) => caster != null && CanHitTarget(target) && ValidJumpTarget(caster.Map, target.Cell) && ReloadableUtility.CanUseConsideringQueuedJobs(CasterPawn, EquipmentSource);

        public override bool CanHitTargetFrom(IntVec3 root, LocalTargetInfo targ)
        {
            float num = EffectiveRange * EffectiveRange;
            IntVec3 cell = targ.Cell;
            return caster.Position.DistanceToSquared(cell) <= (double)num && GenSight.LineOfSight(root, cell, caster.Map);
        }

        public override void OnGUI(LocalTargetInfo target)
        {
            if (CanHitTarget(target) && ValidJumpTarget(caster.Map, target.Cell))
            {
                base.OnGUI(target);
            }
            else
            {
                GenUI.DrawMouseAttachment(TexCommand.CannotShoot);
            }
        }

        public override void DrawHighlight(LocalTargetInfo target)
        {
            if (target.IsValid && ValidJumpTarget(caster.Map, target.Cell))
            {
                GenDraw.DrawTargetHighlightWithLayer(target.CenterVector3, AltitudeLayer.MetaOverlays);
            }
            GenDraw.DrawRadiusRing(caster.Position, EffectiveRange, Color.white, c => GenSight.LineOfSight(caster.Position, c, caster.Map) && ValidJumpTarget(caster.Map, c));
        }

        public static bool ValidJumpTarget(Map map, IntVec3 cell)
        {
            if (!cell.IsValid || !cell.InBounds(map) || cell.Impassable(map) || !cell.Walkable(map) || cell.Fogged(map))
            {
                return false;
            }
            Building edifice = cell.GetEdifice(map);
            return edifice == null || !(edifice is Building_Door buildingDoor) || buildingDoor.Open;
        }
    }

    //public class Verb_Booster_FaceCell : Verb_Booster
    //{
    //    protected override bool TryCastShot()
    //    {
    //        return Booster(CasterPawn, currentTarget, ReloadableCompSource, BoosterCompSource, verbProps, true);
    //    }
    //}
}

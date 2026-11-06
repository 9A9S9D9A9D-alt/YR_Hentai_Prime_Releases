using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace YR_Booster
{
    public class CompProperties_Booster : CompProperties
    {
        public CompProperties_Booster()
        {
            compClass = typeof(CompBooster);
        }
        public List<HediffDef> hediffDefs = new List<HediffDef>();

        public int cooldownTicks = 3000;

        public KeyBindingDef hotKey;

        public string booster_Cooldown = "Booster_Cooldown";
        public string faceCellCommandLabel = "FaceCellCommandLabel";
        public string faceCellCommandDesc = "FaceCellCommandDesc";
        public StatDef statDef;
        public ThingDef flyingDef;
        public float nonQualityEffectiveRange = 3;
        public List<ThingDef> requireWeaponDefs = new List<ThingDef>();
        public HediffDef hediffDefForCheckKillEnemy;
        public string faceCellIconPath = "Ui/Heroart/ExpansionIcon_Royalty";
        public string faceOriginalIconPath = "Ui/Heroart/NotOwned_Royalty";
        public string description = "Booster_Desc";
    }
    public class CompBooster : ThingComp, IVerbOwner
    {
        public int cooldownTicks = 0;
        private VerbTracker verbTracker;
        public bool faceCell = true;
        public Pawn Wearer
        {
            get
            {
                return WearerOf(this);
            }
        }
        public static Pawn WearerOf(CompBooster comp)
        {
            Pawn_ApparelTracker pawn_ApparelTracker = comp.ParentHolder as Pawn_ApparelTracker;
            if (pawn_ApparelTracker != null)
            {
                return pawn_ApparelTracker.pawn;
            }
            return null;
        }

        public List<VerbProperties> VerbProperties
        {
            get
            {
                return parent.def.Verbs;
            }
        }

        public List<Tool> Tools
        {
            get
            {
                return parent.def.tools;
            }
        }
        public ImplementOwnerTypeDef ImplementOwnerTypeDef
        {
            get
            {
                return ImplementOwnerTypeDefOf.NativeVerb;
            }
        }
        public Thing ConstantCaster
        {
            get
            {
                return Wearer;
            }
        }
        public string UniqueVerbOwnerID()
        {
            return "YR_EggVibrator" + parent.ThingID;
        }

        public bool VerbsStillUsableBy(Pawn p)
        {
            return Wearer == p;
        }

        public VerbTracker VerbTracker
        {
            get
            {
                verbTracker ??= new VerbTracker(this);
                return verbTracker;
            }
        }
        public CompProperties_Booster Props
        {
            get
            {
                return (CompProperties_Booster)props;
            }
        }

        public override void CompTick()
        {
            base.CompTick();
            if (cooldownTicks > 0)
            {
                cooldownTicks--;
            }
        }

        public override void Notify_Equipped(Pawn pawn)
        {
            base.Notify_Equipped(pawn);
            if (Props.hediffDefForCheckKillEnemy != null)
            {
                pawn.health.AddHediff(Props.hediffDefForCheckKillEnemy);
            }
        }

        public override void Notify_Unequipped(Pawn pawn)
        {
            base.Notify_Unequipped(pawn);
            if (Props.hediffDefForCheckKillEnemy != null)
            {
                foreach (var hediff in pawn.health.hediffSet.hediffs.ToList())
                {
                    if (hediff.def == Props.hediffDefForCheckKillEnemy)
                    {
                        pawn.health.hediffSet.hediffs.Remove(hediff);
                    }
                }
            }

        }

        public override IEnumerable<Gizmo> CompGetWornGizmosExtra()
        {

            if (!Wearer.IsColonistPlayerControlled)
            {
                yield break;
            }
            foreach (Gizmo gizmo in base.CompGetWornGizmosExtra())
            {
                yield return gizmo;
            }
            ThingWithComps gear = parent;

            var faceCellIcon = ContentFinder<Texture2D>.Get(Props.faceCellIconPath);
            if (!faceCell)
            {
                faceCellIcon = ContentFinder<Texture2D>.Get(Props.faceOriginalIconPath);
            }

            Command_Action faceCellCommand = new Command_Action
            {
                defaultLabel = Props.faceCellCommandLabel.Translate(),
                defaultDesc = Props.faceCellCommandDesc.Translate(),
                icon = faceCellIcon,
                action = delegate ()
                {
                    if (faceCell)
                    {
                        faceCell = false;
                    }
                    else
                    {
                        faceCell = true;
                    }
                }
            };
            yield return faceCellCommand;

            if (Props.requireWeaponDefs.NullOrEmpty())
            {
                if (parent.TryGetComp<CompApparelReloadable>() == null)
                {
                    foreach (Verb verb in VerbTracker.AllVerbs)
                    {
                        if (verb.verbProps.hasStandardCommand)
                        {
                            if (verb is Verb_Booster)
                            {
                                CompBooster comp = ThingCompUtility.TryGetComp<CompBooster>(parent);
                                yield return CreateVerbTargetCommand(gear, verb, comp);
                            }
                        }
                    }
                }
            }
            else
            {
                foreach (var requireWeaponDef in Props.requireWeaponDefs)
                {
                    if (parent is Apparel ap && ap?.Wearer?.equipment?.PrimaryEq?.parent.def == requireWeaponDef)
                    {
                        foreach (Verb verb in VerbTracker.AllVerbs)
                        {
                            if (verb.verbProps.hasStandardCommand)
                            {
                                if (verb is Verb_Booster)
                                {
                                    CompBooster comp = ThingCompUtility.TryGetComp<CompBooster>(parent);
                                    yield return CreateVerbTargetCommand(gear, verb, comp);
                                }
                            }
                        }
                        break;
                    }
                }
            }
            yield break;
        }

        private Command_Booster CreateVerbTargetCommand(Thing gear, Verb verb, CompBooster compBooster)
        {
            Command_Booster command_Booster = new Command_Booster(compBooster);
            if (!compBooster.Props.description.NullOrEmpty())
            {
                var requireWeaponDefString = "";
                for (int i = 0; i < compBooster.Props.requireWeaponDefs.Count; i++)
                {
                    ThingDef requireWeaponDef = compBooster.Props.requireWeaponDefs[i];

                    requireWeaponDefString += requireWeaponDef.label;

                    if (i != compBooster.Props.requireWeaponDefs.Count-1)
                    {
                        requireWeaponDefString += ", ";
                    }
                }

                command_Booster.defaultDesc = compBooster.Props.description.Translate(compBooster.Props.cooldownTicks.TicksToSeconds(), requireWeaponDefString); 
            }
            else
            {
                command_Booster.defaultDesc = gear.def.description;
            }
            command_Booster.hotKey = compBooster.Props.hotKey;
            command_Booster.defaultLabel = verb.verbProps.label;
            command_Booster.verb = verb;
            if (verb.verbProps.defaultProjectile != null && verb.verbProps.commandIcon == null)
            {
                command_Booster.icon = verb.verbProps.defaultProjectile.uiIcon;
                command_Booster.iconAngle = verb.verbProps.defaultProjectile.uiIconAngle;
                command_Booster.iconOffset = verb.verbProps.defaultProjectile.uiIconOffset;
                command_Booster.overrideColor = new Color?(verb.verbProps.defaultProjectile.graphicData.color);
            }
            else
            {
                command_Booster.icon = ((verb.UIIcon != BaseContent.BadTex) ? verb.UIIcon : gear.def.uiIcon);
                command_Booster.iconAngle = gear.def.uiIconAngle;
                command_Booster.iconOffset = gear.def.uiIconOffset;
                command_Booster.defaultIconColor = gear.DrawColor;
            }

            if (cooldownTicks > 0)
            {
                command_Booster.Disable(Props.booster_Cooldown.Translate(cooldownTicks.TicksToSeconds()));
            }
            if (Wearer.Downed)
            {
                command_Booster.Disable("IsIncapped".Translate(Wearer.LabelCap, Wearer));
            }
            return command_Booster;
        }
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref faceCell, "faceCell");
            Scribe_Values.Look(ref cooldownTicks, "cooldownTicks", 0, false);
        }
    }
}

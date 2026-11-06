using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace YR_RJW
{
    public class CompProperties_EggVibrator : CompProperties
    {
        public CompProperties_EggVibrator()
        {
            compClass = typeof(Comp_EggVibrator);
        }
        public List<HediffDef> hediffDefs = new List<HediffDef>();
        public List<HediffDef> commandHediffDefs = new List<HediffDef>();

        public int ticks = 600;

        public int rand = 50;

        public int cooldownTicks = 3000;

        public KeyBindingDef hotKey;

        public ThingDef filthDef;

        public int filthNum = 1;

        public string description;
    }

    public class Comp_EggVibrator : ThingComp, IVerbOwner
    {
        private int ticks = 0;
        public int cooldownTicks = 0;
        private VerbTracker verbTracker;
        private bool automaticPlayback;
        private List<Hediff> automaticEffects = new List<Hediff>();
        private bool IsSamokApparel => parent.TryGetComp<Comp_Tentacle_Apparel>() != null;
        public bool AutomaticPlayback => automaticPlayback;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref cooldownTicks, "cooldownTicks", 0, false);
            Scribe_Values.Look(ref automaticPlayback, "samokHappyVibration", false);
            if (Scribe.mode == LoadSaveMode.Saving)
                automaticEffects.RemoveAll(h => h == null || h.pawn?.health?.hediffSet.hediffs.Contains(h) != true);
            Scribe_Collections.Look(ref automaticEffects, "samokAutomaticEffects", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) {
                automaticEffects ??= new List<Hediff>();
                automaticEffects.RemoveAll(h => h == null);
            }
        }
        private void ToggleAutomaticPlayback()
        {
            automaticPlayback = !automaticPlayback;
            if (automaticPlayback) { ticks = Props.ticks; return; }
            foreach (var effect in automaticEffects)
                if (effect != null && Wearer?.health.hediffSet.hediffs.Contains(effect) == true)
                    Wearer.health.RemoveHediff(effect);
            automaticEffects.Clear();
        }

        public Pawn Wearer => WearerOf(this);
        public static Pawn WearerOf(Comp_EggVibrator comp)
        {
            return comp.ParentHolder is Pawn_ApparelTracker pawn_ApparelTracker ? pawn_ApparelTracker.pawn : null;
        }

        public List<VerbProperties> VerbProperties => parent.def.Verbs;

        public List<Tool> Tools => parent.def.tools;
        public ImplementOwnerTypeDef ImplementOwnerTypeDef => ImplementOwnerTypeDefOf.NativeVerb;
        public Thing ConstantCaster => Wearer;
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

        public override void PostPostMake()
        {
            base.PostPostMake();
        }
        public override void Notify_Equipped(Pawn pawn)
        {
            base.Notify_Equipped(pawn);
        }
        public override void Notify_Unequipped(Pawn pawn)
        {
            base.Notify_Unequipped(pawn);
        }
        public CompProperties_EggVibrator Props => (CompProperties_EggVibrator)props;
        public override void CompTick()
        {
            base.CompTick();
            if (cooldownTicks > 0)
            {
                cooldownTicks--;
            }
            if (IsSamokApparel && !automaticPlayback) return;
            ticks--;
            if (ticks <= 0)
            {
                ticks = Props.ticks;
                if (Wearer != null)
                {
                    System.Random rand = new System.Random();
                    int num = rand.Next(0, 101);
                    if (num <= Props.rand)
                    {
                        foreach (HediffDef hediffDef in Props.hediffDefs)
                        {
                            if (!IsSamokApparel) { Wearer.health.AddHediff(hediffDef); continue; }
                            automaticEffects.RemoveAll(h => h == null || !Wearer.health.hediffSet.hediffs.Contains(h));
                            var effect = HediffMaker.MakeHediff(hediffDef, Wearer);
                            Wearer.health.AddHediff(effect);
                            if (Wearer.health.hediffSet.hediffs.Contains(effect)) automaticEffects.Add(effect);
                        }
                    }
                }
            }
        }

        public override IEnumerable<Gizmo> CompGetWornGizmosExtra()
        {

            foreach (Gizmo gizmo in base.CompGetWornGizmosExtra())
            {
                yield return gizmo;
            }
            ThingWithComps gear = parent;
            var selected=Find.Selector.SelectedObjectsListForReading.OfType<Pawn>().ToList();
            if(IsSamokApparel&&selected.Count>1){
                var targets=selected.Where(p=>p.apparel!=null).SelectMany(p=>p.apparel.WornApparel)
                    .Select(a=>a.TryGetComp<Comp_EggVibrator>()).Where(c=>c!=null&&c.IsSamokApparel).ToList();
                if(targets.FirstOrDefault()==this){
                    bool allOn=targets.All(c=>c.automaticPlayback),allOff=targets.All(c=>!c.automaticPlayback);
                    yield return new Command_Action {
                        defaultLabel="행복진동 "+(allOn?"ON":allOff?"OFF":"혼합"),
                        defaultDesc="선택한 폰들이 착용한 사목 의상의 자동 진동·소리·연출을 일괄 설정합니다.",
                        icon=ContentFinder<Texture2D>.Get("UI/Commands/SamokVibration"),iconDrawScale=.85f,groupable=false,
                        action=()=>Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption>{
                            new FloatMenuOption("선택 대상 모두 켜기",()=>SetPlayback(targets,true)),
                            new FloatMenuOption("선택 대상 모두 끄기",()=>SetPlayback(targets,false))
                        }))
                    };
                }
            }
            else if (IsSamokApparel)
                yield return new Command_Toggle {
                    defaultLabel = "행복진동 " + (automaticPlayback ? "ON" : "OFF"),
                    defaultDesc = "사목 의상의 자동 진동과 그에 따른 소리·연출을 켜거나 끕니다. 기본값은 OFF이며 이 의상에 저장됩니다.",
                    icon = ContentFinder<Texture2D>.Get("UI/Commands/SamokVibration"),
                    iconDrawScale = .85f,
                    groupable = false,
                    isActive = () => automaticPlayback,
                    toggleAction = ToggleAutomaticPlayback
                };
            foreach (Verb verb in VerbTracker.AllVerbs)
            {
                if (verb.verbProps.hasStandardCommand)
                {
                    if (verb is Verb_EggVibrator)
                    {
                        Comp_EggVibrator comp = ThingCompUtility.TryGetComp<Comp_EggVibrator>(parent);
                        yield return CreateVerbTargetCommand(gear, verb, comp);
                    }
                }
            }
            yield break;
        }
        private static void SetPlayback(IEnumerable<Comp_EggVibrator> targets,bool enabled){
            foreach(var target in targets)if(target.automaticPlayback!=enabled)target.ToggleAutomaticPlayback();
        }

        private Command_EggVibrator CreateVerbTargetCommand(Thing gear, Verb verb, Comp_EggVibrator comp_EggVibrator)
        {
            Command_EggVibrator command_EggVibrator = new Command_EggVibrator(comp_EggVibrator)
            {
                defaultDesc = gear.def.description
            };
            if (!comp_EggVibrator.Props.description.NullOrEmpty())
            {
                command_EggVibrator.defaultDesc = comp_EggVibrator.Props.description.Translate(gear.def.label);
            }
            command_EggVibrator.hotKey = comp_EggVibrator.Props.hotKey;
            command_EggVibrator.defaultLabel = verb.verbProps.label;
            command_EggVibrator.verb = verb;
            if (verb.verbProps.defaultProjectile != null && verb.verbProps.commandIcon == null)
            {
                command_EggVibrator.icon = verb.verbProps.defaultProjectile.uiIcon;
                command_EggVibrator.iconAngle = verb.verbProps.defaultProjectile.uiIconAngle;
                command_EggVibrator.iconOffset = verb.verbProps.defaultProjectile.uiIconOffset;
                command_EggVibrator.overrideColor = new Color?(verb.verbProps.defaultProjectile.graphicData.color);
            }
            else
            {
                command_EggVibrator.icon = (verb.UIIcon != BaseContent.BadTex) ? verb.UIIcon : gear.def.uiIcon;
                command_EggVibrator.iconAngle = gear.def.uiIconAngle;
                command_EggVibrator.iconOffset = gear.def.uiIconOffset;
                command_EggVibrator.defaultIconColor = gear.DrawColor;
            }

            if (cooldownTicks > 0)
            {
                command_EggVibrator.Disable("YR_EggVibrator_Cooldown".Translate(cooldownTicks.TicksToSeconds()));
            }
            return command_EggVibrator;
        }
    }
}

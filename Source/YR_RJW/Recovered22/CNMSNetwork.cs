using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using YR_Hentai_Prime_AnimationBed;

namespace YR_RJW
{
    // Keep the two supplied CNMS implementations' holders distinct. Transfers move ownership.
    public static class CNMSNetwork
    {
        private static readonly FieldInfo ModernMainField = AccessTools.Field(typeof(CompFacility_CNMS), "isMainCNMS");
        public static bool IsMain(Thing thing) => thing.TryGetComp<CompCNMS>()?.mainCNMS == true ||
            (thing.TryGetComp<CompFacility_CNMS>() is CompFacility_CNMS modern && (bool)ModernMainField.GetValue(modern));
        public static ThingOwner Container(Thing thing) =>
            thing.TryGetComp<CompFacility_CNMS>()?.innerContainer ?? thing.TryGetComp<CompCNMS>()?.innerContainer;

        public static void ClearModernMains(CompCNMS source)
        {
            foreach (Thing thing in source.parent.Map.listerThings.AllThings)
                if (source.AnotherCNMS(thing) && thing.TryGetComp<CompFacility_CNMS>() is CompFacility_CNMS modern)
                    ModernMainField.SetValue(modern, false);
        }

        private static Thing Destination(CompCNMS source)
        {
            if (IsMain(source.parent)) return null;
            Thing prisonDestination = null;
            bool prison = source.parent.GetRoom()?.IsPrisonCell == true;
            foreach (Thing thing in source.parent.Map.listerThings.AllThings)
            {
                if (thing == source.parent || !source.AnotherCNMS(thing) || Container(thing) == null) continue;
                if (IsMain(thing)) return thing;
                var other = thing.TryGetComp<CompCNMS>();
                if (prison && prisonDestination == null && thing.GetRoom()?.IsPrisonCell != true && other?.PowerOn == true)
                    prisonDestination = thing;
            }
            return prisonDestination;
        }

        public static bool TryTransfer(CompCNMS source)
        {
            Thing destination = Destination(source);
            // A base CNMS keeps modern products in its original modern holder.
            ThingOwner target = destination != null ? Container(destination) : source.parent.TryGetComp<CompFacility_CNMS>()?.innerContainer;
            if (target == null) return false;
            source.innerContainer.TryTransferAllToContainer(target);
            return true;
        }

        [HarmonyPatch(typeof(CompFacility_CNMS), nameof(CompFacility_CNMS.ToggleMainCNMS))]
        private static class ModernToggle
        {
            private static void Postfix(CompFacility_CNMS __instance)
            {
                if (!(bool)ModernMainField.GetValue(__instance) || __instance.parent.Map == null) return;
                var source = __instance.parent.TryGetComp<CompCNMS>();
                if (source == null) return;
                foreach (Thing thing in __instance.parent.Map.listerThings.AllThings)
                    if (source.AnotherCNMS(thing) && thing.TryGetComp<CompCNMS>() is CompCNMS legacy)
                        legacy.mainCNMS = false;
            }
        }

        [HarmonyPatch(typeof(CompFacility_CNMS), "EjectOrTransfer")]
        private static class ModernTransfer
        {
            private static bool Prefix(CompFacility_CNMS __instance)
            {
                var source = __instance.parent.TryGetComp<CompCNMS>();
                if (source == null || !__instance.parent.Spawned) return true;
                Thing destination = Destination(source);
                if (destination == null) return true;
                __instance.innerContainer.TryTransferAllToContainer(Container(destination));
                return false;
            }
        }
    }
}

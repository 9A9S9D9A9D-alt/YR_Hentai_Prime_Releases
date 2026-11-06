using HarmonyLib;
using RimWorld;
using Verse;

namespace YR_RJW
{
    [StaticConstructorOnStartup]
    public static class Harmony_GetTraderCaravanRole
    {
        static Harmony_GetTraderCaravanRole()
        {
            Log.Message("YR_RJW_patched");
            Harmony harmonyInstance = new Harmony("YR_RJW");
            harmonyInstance.Patch(AccessTools.Method(typeof(TraderCaravanUtility), "GetTraderCaravanRole"), null, new HarmonyMethod(typeof(Harmony_GetTraderCaravanRole), "GetTraderCaravanRolePostFix"));

        }
        [HarmonyPostfix]
        public static void GetTraderCaravanRolePostFix(Pawn p, ref TraderCaravanRole __result)
        {
            if (p.kindDef.defName == "Yuran_Slave")
            {
                __result = TraderCaravanRole.Chattel;
            }
        }
    }
}

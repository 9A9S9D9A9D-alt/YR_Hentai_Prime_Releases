using System.Collections.Generic;
using HarmonyLib;
using Verse;

namespace PawnKnockback;

[StaticConstructorOnStartup]
public static class Harmony_PawnKnockback
{
	public static Explosion SA_Exp;

	static Harmony_PawnKnockback()
	{
		Harmony harmony = new Harmony("Amnabi.Knockback");
		harmony.PatchAll();
		harmony.Patch(AccessTools.Method(typeof(DamageWorker), "ExplosionDamageThing"), new HarmonyMethod(typeof(Harmony_PawnKnockback), "HP_EDT"));
	}

	public static bool HP_EDT(Explosion explosion, Thing t, List<Thing> damagedThings, List<Thing> ignoredThings, IntVec3 cell)
	{
		SA_Exp = explosion;
		return true;
	}
}

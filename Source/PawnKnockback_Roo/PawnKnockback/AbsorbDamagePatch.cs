using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Verse;

namespace PawnKnockback;

[HarmonyPatch(typeof(Thing))]
[HarmonyPatch("TakeDamage")]
public static class AbsorbDamagePatch
{
	public static Bullet_Harpoon SA_2138784;

	public static Map SA_2138785;

	private static readonly MethodInfo CheckInfo = AccessTools.Method(typeof(AbsorbDamagePatch), "Check");

	public static void Check(DamageInfo damageInfo)
	{
		if (SA_2138784 != null)
		{
			SA_2138784.retract(SA_2138785, useExplosionCenter: false);
		}
	}

	private static IEnumerable<CodeInstruction> Transpiler(ILGenerator generator, IEnumerable<CodeInstruction> instructions)
	{
		CodeInstruction[] codeInst = instructions.ToArray();
		for (int i = 0; i < codeInst.Length; i++)
		{
			yield return codeInst[i];
			if (i >= 4 && codeInst[i - 4].opcode == OpCodes.Ldarga_S && codeInst[i - 3].opcode == OpCodes.Ldloca_S && codeInst[i - 2].opcode == OpCodes.Callvirt && codeInst[i - 1].opcode == OpCodes.Ldloc_0 && codeInst[i].opcode == OpCodes.Brfalse_S)
			{
				yield return new CodeInstruction(OpCodes.Ldarg_1);
				yield return new CodeInstruction(OpCodes.Call, CheckInfo);
			}
		}
	}
}

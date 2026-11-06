using RimWorld;
using UnityEngine;
using Object = UnityEngine.Object;
using Verse;

namespace PawnKnockback;

public class DamageWorker_Knockback : DamageWorker_AddInjury
{
	public override DamageResult Apply(DamageInfo dinfo, Thing thing)
	{
		Pawn pawn = thing as Pawn;
		bool num = pawn?.Dead ?? true;
		DamageResult damageResult = base.Apply(dinfo, thing);
		DamageKnockbackDef damageKnockbackDef = def as DamageKnockbackDef;
		if (!num && (damageKnockbackDef.useExplosionCenter || !damageKnockbackDef.noKnockbackIfDeflected || damageResult.totalDamageDealt == 0f))
		{
			KnockbackFlyer(dinfo, pawn);
		}
		else if (dinfo.Instigator != null && dinfo.Instigator.Spawned && dinfo.Instigator.Map == thing.Map)
		{
			Bullet_Harpoon.retract(damageKnockbackDef, damageKnockbackDef.bulletTipDef, damageKnockbackDef.useExplosionCenter ? null : dinfo.Instigator, thing.Map, thing.Position.ToVector3Shifted(), damageKnockbackDef.useExplosionCenter ? Harmony_PawnKnockback.SA_Exp.DrawPos : dinfo.Instigator.Position.ToVector3Shifted(), damageKnockbackDef.useExplosionCenter);
		}
		return damageResult;
	}

	private void KnockbackFlyer(DamageInfo dinfo, Pawn pawn)
	{
		if (!pawn.Spawned)
		{
			return;
		}
		Thing instigator = dinfo.Instigator;
		DamageKnockbackDef damageKnockbackDef = def as DamageKnockbackDef;
		if (!damageKnockbackDef.useExplosionCenter && (instigator == null || !instigator.Spawned || instigator.Map != pawn.Map))
		{
			return;
		}
		Vector3 val = (damageKnockbackDef.useExplosionCenter ? Harmony_PawnKnockback.SA_Exp.DrawPos : instigator.DrawPos);
		float num = Rand.Value * (damageKnockbackDef.knockbackRadiusMax - damageKnockbackDef.knockbackRadiusMin) + damageKnockbackDef.knockbackRadiusMin;
		Vector3 val2 = (damageKnockbackDef.pullInstead ? (val - pawn.DrawPos) : (pawn.DrawPos - val));
		Map map = pawn.Map;
		Vector3 val3 = pawn.DrawPos;
		if (damageKnockbackDef.pullInstead && damageKnockbackDef.pullToNearbySpotInstead)
		{
			IntVec3 intVec = (damageKnockbackDef.useExplosionCenter ? Harmony_PawnKnockback.SA_Exp.Position : instigator.Position);
			IntVec3 intVec2 = intVec;
			intVec2.x++;
			if (intVec2.InBounds(map) && !intVec2.Impassable(map) && Vector3.Distance(intVec2.ToVector3Shifted(), val3) < Vector3.Distance(intVec.ToVector3Shifted(), val3))
			{
				intVec = intVec2;
			}
			intVec2 = intVec;
			intVec2.x--;
			if (intVec2.InBounds(map) && !intVec2.Impassable(map) && Vector3.Distance(intVec2.ToVector3Shifted(), val3) < Vector3.Distance(intVec.ToVector3Shifted(), val3))
			{
				intVec = intVec2;
			}
			intVec2 = intVec;
			intVec2.z++;
			if (intVec2.InBounds(map) && !intVec2.Impassable(map) && Vector3.Distance(intVec2.ToVector3Shifted(), val3) < Vector3.Distance(intVec.ToVector3Shifted(), val3))
			{
				intVec = intVec2;
			}
			intVec2 = intVec;
			intVec2.z--;
			if (intVec2.InBounds(map) && !intVec2.Impassable(map) && Vector3.Distance(intVec2.ToVector3Shifted(), val3) < Vector3.Distance(intVec.ToVector3Shifted(), val3))
			{
				intVec = intVec2;
			}
			val3 = intVec.ToVector3Shifted();
		}
		else
		{
			val2.y = 0f;
			val2 = val2.normalized;
			if (val2.x == 0f && val2.z == 0f)
			{
				return;
			}
			Vector3 val4 = val2;
			Vector3 drawPos = pawn.DrawPos;
			int num2 = 0;
			while (Vector3.Distance(drawPos, val3) < num - (float)num2)
			{
				float num3 = Mathf.Abs(val4.x);
				float num4 = Mathf.Abs(val4.y);
				float num5 = Mathf.Abs(val4.z);
				while (num3 >= 1f || num4 >= 1f || num5 >= 1f)
				{
					bool flag = false;
					if (num3 >= 1f && num3 >= num4 && num3 >= num5)
					{
						float x = val3.x;
						val3.x += (int)Mathf.Sign(val2.x);
						val4.x -= Mathf.Sign(val2.x);
						if (val3.ToIntVec3().InBounds(map) && !val3.ToIntVec3().Impassable(map))
						{
							flag = true;
						}
						else
						{
							val3.x = x;
							num2++;
						}
					}
					else if (num4 >= 1f && num4 >= num3 && num4 >= num5)
					{
						float y = val3.y;
						val3.y += (int)Mathf.Sign(val2.y);
						val4.y -= Mathf.Sign(val2.y);
						if (val3.ToIntVec3().InBounds(map) && !val3.ToIntVec3().Impassable(map))
						{
							flag = true;
						}
						else
						{
							val3.y = y;
							num2++;
						}
					}
					else if (num5 >= 1f && num5 >= num4 && num5 >= num3)
					{
						float z = val3.z;
						val3.z += (int)Mathf.Sign(val2.z);
						val4.z -= Mathf.Sign(val2.z);
						if (val3.ToIntVec3().InBounds(map) && !val3.ToIntVec3().Impassable(map))
						{
							flag = true;
						}
						else
						{
							val3.z = z;
							num2++;
						}
					}
					num3 = Mathf.Abs(val4.x);
					num4 = Mathf.Abs(val4.y);
					num5 = Mathf.Abs(val4.z);
					if (!flag)
					{
						break;
					}
				}
				val4 += val2;
			}
		}
		PawnFlyer pawnFlyer = PawnFlyer.MakeFlyer(damageKnockbackDef.flyerToApply, pawn, val3.ToIntVec3(), (EffecterDef)null, (SoundDef)null, false);
		if (pawnFlyer != null)
		{
			if (pawnFlyer is PawnKnockback pawnKnockback)
			{
				pawnKnockback.speedMultiplication = damageKnockbackDef.knockbackSpeedMultiplier;
				pawnKnockback.heightMultiplication = damageKnockbackDef.knockbackHeightMultiplier;
				pawnKnockback.damageDefRef = damageKnockbackDef;
				pawnKnockback.instigator = (damageKnockbackDef.useExplosionCenter ? null : instigator);
				pawnKnockback.centerIfNoinstigator = (damageKnockbackDef.useExplosionCenter ? Harmony_PawnKnockback.SA_Exp.Position.ToVector3() : Vector3.zero);
				pawnKnockback.instigatorEquipmentDef = dinfo.Weapon;
				pawnKnockback.adjustFlightTimeMax();
			}
			GenSpawn.Spawn(pawnFlyer, val3.ToIntVec3(), map);
		}
	}
}

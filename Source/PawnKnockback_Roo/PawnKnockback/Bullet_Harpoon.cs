using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Object = UnityEngine.Object;
using Verse;

namespace PawnKnockback;

public class Bullet_Harpoon : Bullet
{
	private static Dictionary<float, Mesh> planes = new Dictionary<float, Mesh>();

	public static void retract(DamageKnockbackDef toc, ThingDef bulletDef, Thing initiator, Map map, Vector3 startVec, Vector3 endVec, bool useExplosionCenter)
	{
		PawnEmptyFlyer pawnEmptyFlyer = PawnEmptyFlyer.MakeFlyer(toc.emptyFlyerToApply, startVec, endVec.ToIntVec3());
		if (pawnEmptyFlyer != null)
		{
			PawnEmptyFlyer pawnEmptyFlyer2 = null;
			bool flag;
			if (pawnEmptyFlyer != null)
			{
				pawnEmptyFlyer2 = pawnEmptyFlyer;
				flag = true;
			}
			else
			{
				flag = false;
			}
			if (flag)
			{
				pawnEmptyFlyer2.speedMultiplication = toc.knockbackSpeedMultiplier;
				pawnEmptyFlyer2.heightMultiplication = toc.knockbackHeightMultiplier;
				pawnEmptyFlyer2.damageDefRef = toc;
				pawnEmptyFlyer2.centerIfNoinstigator = endVec;
				pawnEmptyFlyer2.instigator = initiator;
				pawnEmptyFlyer2.instigatorEquipmentDef = null;
				pawnEmptyFlyer2.adjustFlightTimeMax();
				pawnEmptyFlyer2.bulletDef = bulletDef;
			}
			GenSpawn.Spawn(pawnEmptyFlyer, startVec.ToIntVec3(), map);
		}
	}

	public void retract(Map map, bool useExplosionCenter)
	{
		if (this.TryGetComp<Comp_HarpoonAttributes>() == null)
		{
			return;
		}
		DamageKnockbackDef damageKnockbackDef = this.TryGetComp<Comp_HarpoonAttributes>().Props.damageKnockbackDef;
		PawnEmptyFlyer pawnEmptyFlyer = PawnEmptyFlyer.MakeFlyer(damageKnockbackDef.emptyFlyerToApply, base.Position.ToVector3Shifted(), base.Launcher.Position);
		if (pawnEmptyFlyer != null)
		{
			PawnEmptyFlyer pawnEmptyFlyer2 = null;
			bool flag;
			if (pawnEmptyFlyer != null)
			{
				pawnEmptyFlyer2 = pawnEmptyFlyer;
				flag = true;
			}
			else
			{
				flag = false;
			}
			if (flag)
			{
				pawnEmptyFlyer2.speedMultiplication = damageKnockbackDef.knockbackSpeedMultiplier;
				pawnEmptyFlyer2.heightMultiplication = damageKnockbackDef.knockbackHeightMultiplier;
				pawnEmptyFlyer2.damageDefRef = damageKnockbackDef;
				pawnEmptyFlyer2.instigator = base.Launcher;
				pawnEmptyFlyer2.instigatorEquipmentDef = null;
				pawnEmptyFlyer2.adjustFlightTimeMax();
				pawnEmptyFlyer2.bulletDef = def;
			}
			GenSpawn.Spawn(pawnEmptyFlyer, destination.ToIntVec3(), map);
		}
	}

	protected override void Impact(Thing hitThing, bool blockedByShield = false)
	{
		if (hitThing == null || !(hitThing is Pawn))
		{
			retract(base.Map, useExplosionCenter: false);
		}
		try
		{
			AbsorbDamagePatch.SA_2138784 = this;
			AbsorbDamagePatch.SA_2138785 = base.Map;
			base.Impact(hitThing, blockedByShield: false);
		}
		catch (Exception ex)
		{
			Log.Error(ex.StackTrace);
		}
		finally
		{
			AbsorbDamagePatch.SA_2138784 = null;
			AbsorbDamagePatch.SA_2138785 = null;
		}
	}

	public static float round(float f)
	{
		return Mathf.Round(f * 100f) / 100f;
	}

	public static Mesh meshOfSize(float length)
	{
		length = round(length);
		if (!planes.TryGetValue(length, out var value))
		{
			value = MeshMakerPlanes.NewPlaneMesh(new Vector2(length, 1f), flipped: false, backLift: false, twist: false);
			planes.Add(length, value);
		}
		return value;
	}

	public static Vector3 vec3MLC(float x, float xAmp, int level, float yAmp)
	{
		Vector3 result = new Vector3
		{
			x = x * xAmp
		};
		float num = 2f;
		for (int i = 0; i < level; i++)
		{
			result.z += yAmp * Mathf.Sin((x - 0.5f) * 2f * (float)Math.PI * num) / num;
			num *= 2f;
		}
		return result;
	}

	protected override void DrawAt(Vector3 drawLoc, bool flip = false)
	{
		base.DrawAt(drawLoc, flip);
		if (launcher != null && def.projectile.damageDef is DamageKnockbackDef { useHarpoonGraphic: not false } damageKnockbackDef)
		{
			Vector3 drawPos = launcher.DrawPos;
			Vector3 drawPos2 = DrawPos;
			float y = drawPos2.y;
			drawPos2.y = 0f;
			drawPos.y = 0f;
			float num = Vector3.Distance(drawPos2, drawPos);
			float num2 = (damageKnockbackDef.harpoonUseHarpoonUnitInstead ? (num / damageKnockbackDef.harpoonUnit) : ((float)damageKnockbackDef.harpoonSegements));
			float num3 = Mathf.Cos((float)(Find.TickManager.TicksAbs - thingIDNumber) * damageKnockbackDef.harpoonSineSpeed) * Mathf.Sqrt(num) / 5f;
			Quaternion val = Quaternion.AngleAxis((0f - Mathf.Atan2((drawPos2 - drawPos).z, (drawPos2 - drawPos).x)) * 180f / (float)Math.PI, Vector3.up);
			float num4 = 1f - (num2 - Mathf.Floor(num2));
			if (num4 == 1f)
			{
				num4 = 0f;
			}
			Vector3 val2 = vec3MLC((0f - num4) / num2, num, 5, num3 * damageKnockbackDef.harpoonSineZAmp);
			for (float num5 = 1f - num4; num5 <= num2; num5 += 1f)
			{
				Vector3 val3 = vec3MLC(num5 / num2, num, 5, num3 * damageKnockbackDef.harpoonSineZAmp);
				float length = Vector3.Distance(val3, val2);
				Vector3 val4 = (val3 + val2) / 2f;
				val4.y = 0f;
				Quaternion val5 = Quaternion.AngleAxis((0f - Mathf.Atan2((val3 - val2).z, (val3 - val2).x)) * 180f / (float)Math.PI, Vector3.up);
				Graphics.DrawMesh(meshOfSize(length), val * val4 + drawPos + new Vector3(0f, y, 0f), val * val5, damageKnockbackDef.graphicDataHarpoonRope.Graphic.MatSingle, 0);
				val2 = val3;
			}
		}
	}

	public override void ExposeData()
	{
		base.ExposeData();
	}
}

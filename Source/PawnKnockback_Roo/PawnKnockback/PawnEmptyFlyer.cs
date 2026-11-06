using System;
using RimWorld;
using UnityEngine;
using Object = UnityEngine.Object;
using Verse;

namespace PawnKnockback;

public class PawnEmptyFlyer : Thing
{
	public EffecterDef flightEffecterDef_Anty;

	public SoundDef soundLanding_Anty;

	private static readonly Func<float, float> FlightSpeed;

	private static readonly Func<float, float> FlightCurveHeight = GenMath.InverseParabola;

	public Thing instigator;

	public Vector3 centerIfNoinstigator;

	public ThingDef bulletDef;

	public ThingDef instigatorEquipmentDef;

	public DamageKnockbackDef damageDefRef;

	public float speedMultiplication = 1f;

	public float heightMultiplication = 1f;

	public Vector3 vectorPosition;

	private Material cachedShadowMaterial;

	private Effecter flightEffecter;

	private int positionLastComputedTick = -1;

	private Vector3 groundPos;

	private Vector3 effectivePos;

	private float effectiveHeight;

	protected Vector3 endVec;

	protected Vector3 startVec;

	private float flightDistance;

	protected int ticksFlightTime = 120;

	protected int ticksFlying;

	public override Vector3 DrawPos
	{
		get
		{
			RecomputePosition();
			return effectivePos;
		}
	}

	public PawnEmptyFlyer()
	{
		AnimationCurve val = new AnimationCurve();
		val.AddKey(0f, 0f);
		val.AddKey(0.1f, 0.15f);
		val.AddKey(1f, 1f);
		PawnKnockback.FlightSpeed = val.Evaluate;
	}

	public override void SpawnSetup(Map map, bool respawningAfterLoad)
	{
		base.SpawnSetup(map, respawningAfterLoad);
		if (!respawningAfterLoad)
		{
			float num = Mathf.Max(flightDistance, 1f) / def.pawnFlyer.flightSpeed;
			num = Mathf.Max(num, def.pawnFlyer.flightDurationMin);
			ticksFlightTime = num.SecondsToTicks();
			ticksFlying = 0;
		}
	}

	private void CheckDestination()
	{
		if (JumpUtility.ValidJumpTarget(this, base.Map, base.Position))
		{
			return;
		}
		int num = GenRadial.NumCellsInRadius(3.9f);
		for (int i = 0; i < num; i++)
		{
			IntVec3 intVec = base.Position + GenRadial.RadialPattern[i];
			if (JumpUtility.ValidJumpTarget(this, base.Map, intVec))
			{
				base.Position = intVec;
				break;
			}
		}
	}

	public static PawnEmptyFlyer MakeFlyer(ThingDef flyingDef, Vector3 startCell, IntVec3 destCell)
	{
		PawnEmptyFlyer obj = (PawnEmptyFlyer)ThingMaker.MakeThing(flyingDef);
		obj.startVec = startCell;
		obj.endVec = destCell.ToVector3Shifted();
		obj.flightDistance = Vector3.Distance(startCell, destCell.ToVector3());
		return obj;
	}

	protected virtual bool ValidateFlyer()
	{
		return true;
	}

	public override void ExposeData()
	{
		base.ExposeData();
		Scribe_Values.Look(ref endVec, "endVec");
		Scribe_Values.Look(ref startVec, "startVec");
		Scribe_Values.Look(ref flightDistance, "flightDistance", 0f);
		Scribe_Values.Look(ref ticksFlightTime, "ticksFlightTime", 0);
		Scribe_Values.Look(ref ticksFlying, "ticksFlying", 0);
		Scribe_Values.Look(ref speedMultiplication, "PK_SpeedMult", 0f);
		Scribe_Values.Look(ref heightMultiplication, "PK_HeightMult", 0f);
		Scribe_Defs.Look(ref damageDefRef, "PK_DamageRef");
		Scribe_Defs.Look(ref instigatorEquipmentDef, "PK_EquipmentRef");
		Scribe_Defs.Look(ref bulletDef, "PK_BulletRef");
		Scribe_References.Look(ref instigator, "PK_Instigator");
		Scribe_Values.Look(ref centerIfNoinstigator, "PK_centerIfNoinstigator");
	}

	public void adjustFlightTimeMax()
	{
		ticksFlightTime = (int)((float)ticksFlightTime / speedMultiplication);
	}

	private void RecomputePosition()
	{
		if (positionLastComputedTick != ticksFlying)
		{
			positionLastComputedTick = ticksFlying;
			float arg = (float)ticksFlying / (float)ticksFlightTime;
			float num = PawnKnockback.FlightSpeed(arg);
			effectiveHeight = PawnKnockback.FlightCurveHeight(num) * heightMultiplication;
			groundPos = Vector3.Lerp(startVec, endVec, num);
			Vector3 val = new Vector3(0f, 0f, 2f);
			Vector3 val2 = Altitudes.AltIncVect * effectiveHeight;
			Vector3 val3 = val * effectiveHeight;
			effectivePos = groundPos + val2 + val3;
		}
	}

	protected override void DrawAt(Vector3 drawLoc, bool flip = false)
	{
		RecomputePosition();
		Vector3 val = ((instigator == null) ? centerIfNoinstigator : instigator.DrawPos);
		DamageKnockbackDef damageKnockbackDef = damageDefRef;
		if (damageKnockbackDef != null && damageKnockbackDef.pullInstead && damageKnockbackDef.useHarpoonGraphic)
		{
			float num = (float)ticksFlying / (float)ticksFlightTime;
			float num2 = 0f;
			float num3 = 0f;
			if (num < 0.1f)
			{
				num2 = (0.1f - num) / 0.1f;
			}
			if (num > 0.8f)
			{
				num3 = (num - 0.8f) / 0.19999999f;
			}
			Vector3 val2 = val;
			Vector3 val3 = effectivePos;
			val3 = val3 * (1f - num3) + val3 * num3;
			float y = val3.y;
			val3.y = 0f;
			val2.y = 0f;
			float num4 = Vector3.Distance(val3, val2);
			float num5 = (damageKnockbackDef.harpoonUseHarpoonUnitInstead ? (num4 / damageKnockbackDef.harpoonUnit) : ((float)damageKnockbackDef.harpoonSegements));
			float num6 = Mathf.Cos((float)(Find.TickManager.TicksAbs - thingIDNumber) * damageKnockbackDef.harpoonSineSpeed) * Mathf.Sqrt(num4) / 5f;
			Quaternion val4 = Quaternion.AngleAxis((0f - Mathf.Atan2((val3 - val2).z, (val3 - val2).x)) * 180f / (float)Math.PI, Vector3.up);
			float num7 = 1f - (num5 - Mathf.Floor(num5));
			if (num7 == 1f)
			{
				num7 = 0f;
			}
			Vector3 val5 = Bullet_Harpoon.vec3MLC((0f - num7) / num5, num4, 5, num6 * damageKnockbackDef.harpoonSineZAmp * num2);
			for (float num8 = 1f - num7; num8 <= num5; num8 += 1f)
			{
				Vector3 val6 = Bullet_Harpoon.vec3MLC(num8 / num5, num4, 5, num6 * damageKnockbackDef.harpoonSineZAmp * num2);
				float length = Vector3.Distance(val6, val5);
				Vector3 val7 = (val6 + val5) / 2f;
				val7.y = 0f;
				Quaternion val8 = Quaternion.AngleAxis((0f - Mathf.Atan2((val6 - val5).z, (val6 - val5).x)) * 180f / (float)Math.PI, Vector3.up);
				Graphics.DrawMesh(Bullet_Harpoon.meshOfSize(length), val4 * val7 + val2 + new Vector3(0f, y, 0f), val4 * val8, damageKnockbackDef.graphicDataHarpoonRope.Graphic.MatSingle, 0);
				val5 = val6;
			}
		}
		Graphics.DrawMesh(MeshPool.GridPlane(bulletDef.graphicData.drawSize), effectivePos, Quaternion.LookRotation((effectivePos - val).Yto0()), bulletDef.DrawMatSingle, 0);
	}

	protected override void Tick()
	{
		if (flightEffecter == null && flightEffecterDef_Anty != null)
		{
			flightEffecter = flightEffecterDef_Anty.Spawn();
			flightEffecter.Trigger((TargetInfo)this, TargetInfo.Invalid, -1);
		}
		else
		{
			flightEffecter?.EffectTick(this, TargetInfo.Invalid);
		}
		if (ticksFlying >= ticksFlightTime)
		{
			Destroy();
		}
		else if (ticksFlying % 5 == 0)
		{
			CheckDestination();
		}
		ticksFlying++;
	}

	public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
	{
		flightEffecter?.Cleanup();
		base.Destroy(mode);
	}
}

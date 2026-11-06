using System;
using RimWorld;
using UnityEngine;
using Object = UnityEngine.Object;
using Verse;
using Verse.Sound;

namespace PawnKnockback;

public class PawnKnockback : PawnFlyer
{
	public EffecterDef flightEffecterDef_Anty;

	public SoundDef soundLanding_Anty;

	public static Func<float, float> FlightSpeed;

	public static Func<float, float> FlightCurveHeight;

	public Thing instigator;

	public Vector3 centerIfNoinstigator;

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

	private Material ShadowMaterial => def.pawnFlyer.ShadowMaterial;

	public override Vector3 DrawPos
	{
		get
		{
			RecomputePosition();
			return effectivePos;
		}
	}

	public void adjustFlightTimeMax()
	{
		ticksFlightTime = (int)((float)ticksFlightTime / speedMultiplication);
	}

	public override void ExposeData()
	{
		base.ExposeData();
		Scribe_Values.Look(ref speedMultiplication, "PK_SpeedMult", 0f);
		Scribe_Values.Look(ref heightMultiplication, "PK_HeightMult", 0f);
		Scribe_Defs.Look(ref damageDefRef, "PK_DamageRef");
		Scribe_Defs.Look(ref instigatorEquipmentDef, "PK_EquipmentRef");
		Scribe_References.Look(ref instigator, "PK_Instigator");
		Scribe_Values.Look(ref centerIfNoinstigator, "PK_centerIfNoinstigator");
	}

	static PawnKnockback()
	{
		FlightCurveHeight = GenMath.InverseParabola;
		AnimationCurve val = new AnimationCurve();
		val.AddKey(0f, 0f);
		val.AddKey(0.1f, 0.15f);
		val.AddKey(1f, 1f);
		FlightSpeed = val.Evaluate;
	}

	protected virtual bool ValidateFlyer()
	{
		return true;
	}

	private void RecomputePosition()
	{
		if (positionLastComputedTick != ticksFlying && base.FlyingPawn != null)
		{
			positionLastComputedTick = ticksFlying;
			float arg = (float)ticksFlying / (float)ticksFlightTime;
			float num = FlightSpeed(arg);
			effectiveHeight = FlightCurveHeight(num) * heightMultiplication;
			groundPos = Vector3.Lerp(startVec, base.DestinationPos, num);
			Vector3 val = new Vector3(0f, 0f, 2f);
			Vector3 val2 = Altitudes.AltIncVect * effectiveHeight;
			Vector3 val3 = val * effectiveHeight;
			effectivePos = groundPos + val2 + val3;
		}
	}

	protected override void DrawAt(Vector3 drawLoc, bool flip = false)
	{
		if (base.FlyingPawn == null)
		{
			return;
		}
		RecomputePosition();
		DrawShadow(groundPos, effectiveHeight);
		Vector3 val = instigator?.DrawPos ?? centerIfNoinstigator;
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
			Vector3 val2 = effectivePos;
			val2 = val2 * (1f - num3) + val2 * num3;
			float y = val2.y;
			val2.y = 0f;
			val.y = 0f;
			float num4 = Vector3.Distance(val2, val);
			float num5 = (damageKnockbackDef.harpoonUseHarpoonUnitInstead ? (num4 / damageKnockbackDef.harpoonUnit) : ((float)damageKnockbackDef.harpoonSegements));
			float num6 = Mathf.Cos((float)(Find.TickManager.TicksAbs - thingIDNumber) * damageKnockbackDef.harpoonSineSpeed) * Mathf.Sqrt(num4) / 5f;
			Quaternion val3 = Quaternion.AngleAxis((0f - Mathf.Atan2((val2 - val).z, (val2 - val).x)) * 180f / (float)Math.PI, Vector3.up);
			float num7 = 1f - (num5 - Mathf.Floor(num5));
			if (num7 == 1f)
			{
				num7 = 0f;
			}
			Vector3 val4 = Bullet_Harpoon.vec3MLC((0f - num7) / num5, num4, 5, num6 * damageKnockbackDef.harpoonSineZAmp * num2);
			for (float num8 = 1f - num7; num8 <= num5; num8 += 1f)
			{
				Vector3 val5 = Bullet_Harpoon.vec3MLC(num8 / num5, num4, 5, num6 * damageKnockbackDef.harpoonSineZAmp * num2);
				float length = Vector3.Distance(val5, val4);
				Vector3 val6 = (val5 + val4) / 2f;
				val6.y = 0f;
				Quaternion val7 = Quaternion.AngleAxis((0f - Mathf.Atan2((val5 - val4).z, (val5 - val4).x)) * 180f / (float)Math.PI, Vector3.up);
				Graphics.DrawMesh(Bullet_Harpoon.meshOfSize(length), val3 * val6 + val + new Vector3(0f, y, 0f), val3 * val7, damageKnockbackDef.graphicDataHarpoonRope.Graphic.MatSingle, 0);
				val4 = val5;
			}
		}
		base.FlyingPawn.Drawer.renderer.RenderPawnAt(effectivePos);
	}

	private void DrawShadow(Vector3 drawLoc, float height)
	{
		Material shadowMaterial = ShadowMaterial;
		if (!((Object)(object)shadowMaterial == (Object)null))
		{
			float num = Mathf.Lerp(1f, 0.6f, height);
			Vector3 val = default(Vector3);
			val = new Vector3(num, 1f, num);
			Matrix4x4 val2 = default(Matrix4x4);
			val2.SetTRS(drawLoc, Quaternion.identity, val);
			Graphics.DrawMesh(MeshPool.plane10, val2, shadowMaterial, 0);
		}
	}

	protected override void RespawnPawn()
	{
		LandingEffects();
		Pawn flyingPawn = base.FlyingPawn;
		base.RespawnPawn();
		if (flyingPawn != null && damageDefRef != null && damageDefRef.landingDamage != null)
		{
			DamageDef landingDamage = damageDefRef.landingDamage;
			flyingPawn.TakeDamage(new DamageInfo(landingDamage, damageDefRef.landingDamageAmount, 0f, 0f, instigator, (BodyPartRecord)null, instigatorEquipmentDef, DamageInfo.SourceCategory.ThingOrUnknown, (Thing)flyingPawn, true, true));
		}
	}

	protected override void TickInterval(int delta)
	{
		if (base.FlyingPawn == null)
		{
			GetDirectlyHeldThings().TryDrop((GetDirectlyHeldThings() as ThingOwner<Thing>).InnerListForReading[0], base.Position, base.MapHeld, ThingPlaceMode.Direct, out var _, null, null, playDropSound: false);
			Destroy();
			return;
		}
		if (flightEffecter == null && flightEffecterDef_Anty != null)
		{
			flightEffecter = flightEffecterDef_Anty.Spawn();
			flightEffecter.Trigger((TargetInfo)this, TargetInfo.Invalid, -1);
		}
		else
		{
			flightEffecter?.EffectTick(this, TargetInfo.Invalid);
		}
		base.TickInterval(delta);
	}

	private void LandingEffects()
	{
		if (soundLanding_Anty != null)
		{
			soundLanding_Anty.PlayOneShot(new TargetInfo(base.Position, base.Map));
		}
		FleckMaker.ThrowDustPuff(base.DestinationPos + Gen.RandomHorizontalVector(0.5f), base.Map, 2f);
	}

	public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
	{
		flightEffecter?.Cleanup();
		base.Destroy(mode);
	}
}


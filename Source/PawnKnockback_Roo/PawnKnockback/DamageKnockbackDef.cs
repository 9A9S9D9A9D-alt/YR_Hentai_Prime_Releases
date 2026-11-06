using Verse;

namespace PawnKnockback;

public class DamageKnockbackDef : DamageDef
{
	public ThingDef bulletTipDef;

	public bool noKnockbackIfDeflected;

	public bool useExplosionCenter;

	public bool pullInstead;

	public bool pullToNearbySpotInstead;

	public float knockbackSpeedMultiplier = 0.3f;

	public float knockbackHeightMultiplier = 0.3f;

	public float knockbackRadiusMin = 3f;

	public float knockbackRadiusMax = 10f;

	public ThingDef flyerToApply;

	public ThingDef emptyFlyerToApply;

	public bool useHarpoonGraphic;

	public float harpoonSineSpeed = 0.25f;

	public int harpoonSegements = 20;

	public bool harpoonUseHarpoonUnitInstead;

	public float harpoonUnit = 1f;

	public float harpoonSineZAmp = 1f;

	public float landingDamageAmount = 1f;

	public DamageDef landingDamage;

	public GraphicData graphicDataHarpoonRope;
}

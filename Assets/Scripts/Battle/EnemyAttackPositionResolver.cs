using UnityEngine;
using UnityEngine.UI;

public readonly struct EnemyAttackPositionPlan
{
	public readonly EnemyAttackRangeType rangeType;
	public readonly Vector3 homeLocalPosition;
	public readonly Vector3 standWorldPosition;
	public readonly Vector3 impactWorldPosition;
	public readonly Vector3 projectileStartWorldPosition;
	public readonly Vector3 projectileEndWorldPosition;

	public EnemyAttackPositionPlan(
		EnemyAttackRangeType rangeType,
		Vector3 homeLocalPosition,
		Vector3 standWorldPosition,
		Vector3 impactWorldPosition,
		Vector3 projectileStartWorldPosition,
		Vector3 projectileEndWorldPosition)
	{
		this.rangeType = rangeType;
		this.homeLocalPosition = homeLocalPosition;
		this.standWorldPosition = standWorldPosition;
		this.impactWorldPosition = impactWorldPosition;
		this.projectileStartWorldPosition = projectileStartWorldPosition;
		this.projectileEndWorldPosition = projectileEndWorldPosition;
	}
}

public static class EnemyAttackPositionResolver
{
	const float DefaultMeleeGapRatio = 0.05f;
	const float MinMeleeGapPixels = 8f;
	const float MaxMeleeGapPixels = 18f;
	const float ImpactOverlapRatio = 0.02f;
	const float MinImpactOverlapPixels = 4f;
	const float MaxImpactOverlapPixels = 10f;
	const float DefaultMidRangeProgress = 0.55f;

	public static EnemyAttackRangeType ResolveRangeType(MobDef def)
	{
		if (def == null)
			return EnemyAttackRangeType.Melee;
		if (def.attackRangeType != EnemyAttackRangeType.Default)
			return def.attackRangeType;
		return string.IsNullOrEmpty(def.projectileSpritePath)
			? EnemyAttackRangeType.Melee
			: EnemyAttackRangeType.Ranged;
	}

	public static EnemyAttackPositionPlan Resolve(
		RectTransform enemySlot,
		RectTransform enemyBody,
		RectTransform playerBody,
		MobDef def)
	{
		var rangeType = ResolveRangeType(def);
		Vector3 slotWorld = enemySlot != null ? enemySlot.position : Vector3.zero;
		Vector3 playerWorld = playerBody != null ? playerBody.position : slotWorld;
		Vector3 homeLocal = enemySlot != null ? enemySlot.localPosition : Vector3.zero;
		Vector3 meleeStand = ResolveMeleeStandWorld(enemySlot, enemyBody, playerBody, def);
		Vector3 meleeImpact = ResolveMeleeImpactWorld(enemySlot, enemyBody, playerBody, def, meleeStand);

		Vector3 standWorld;
		switch (rangeType)
		{
			case EnemyAttackRangeType.Ranged:
			case EnemyAttackRangeType.Unique:
				standWorld = slotWorld;
				break;
			case EnemyAttackRangeType.MidRange:
				standWorld = Vector3.Lerp(slotWorld, meleeStand, DefaultMidRangeProgress);
				break;
			case EnemyAttackRangeType.Melee:
			default:
				standWorld = meleeStand;
				break;
		}

		Vector3 impactWorld = rangeType == EnemyAttackRangeType.Melee
			? meleeImpact
			: new Vector3(playerWorld.x, standWorld.y, standWorld.z);
		return new EnemyAttackPositionPlan(
			rangeType,
			homeLocal,
			standWorld,
			impactWorld,
			RectPointWorld(enemyBody, 0.08f, 0.55f, slotWorld),
			RectPointWorld(playerBody, 0.62f, 0.58f, playerWorld));
	}

	static Vector3 ResolveMeleeStandWorld(
		RectTransform enemySlot,
		RectTransform enemyBody,
		RectTransform playerBody,
		MobDef def)
	{
		Vector3 slotWorld = enemySlot != null ? enemySlot.position : Vector3.zero;
		var enemyBounds = VisualHorizontalBounds(enemyBody, slotWorld.x);
		var playerBounds = VisualHorizontalBounds(playerBody, slotWorld.x);
		float enemyLeftOffset = enemyBounds.min - slotWorld.x;
		float gap = ResolveMeleeGap(def, enemyBounds.Width, playerBounds.Width);
		float targetX = playerBounds.max + gap - enemyLeftOffset;
		return new Vector3(targetX, slotWorld.y, slotWorld.z);
	}

	static Vector3 ResolveMeleeImpactWorld(
		RectTransform enemySlot,
		RectTransform enemyBody,
		RectTransform playerBody,
		MobDef def,
		Vector3 fallbackStandWorld)
	{
		Vector3 slotWorld = enemySlot != null ? enemySlot.position : fallbackStandWorld;
		var enemyBounds = VisualHorizontalBounds(enemyBody, slotWorld.x);
		var playerBounds = VisualHorizontalBounds(playerBody, fallbackStandWorld.x);
		float enemyLeftOffset = enemyBounds.min - slotWorld.x;
		float overlap = ResolveImpactOverlap(enemyBounds.Width, playerBounds.Width);
		float targetX = playerBounds.max - overlap - enemyLeftOffset;
		return new Vector3(targetX, fallbackStandWorld.y, fallbackStandWorld.z);
	}

	static float ResolveMeleeGap(MobDef def, float enemyWidth, float playerWidth)
	{
		if (def != null && def.attackStopGap > 0f)
			return def.attackStopGap;

		float referenceWidth = Mathf.Max(1f, Mathf.Min(enemyWidth, playerWidth));
		return Mathf.Clamp(referenceWidth * DefaultMeleeGapRatio, MinMeleeGapPixels, MaxMeleeGapPixels);
	}

	static float ResolveImpactOverlap(float enemyWidth, float playerWidth)
	{
		float referenceWidth = Mathf.Max(1f, Mathf.Min(enemyWidth, playerWidth));
		return Mathf.Clamp(referenceWidth * ImpactOverlapRatio, MinImpactOverlapPixels, MaxImpactOverlapPixels);
	}

	static HorizontalBounds VisualHorizontalBounds(RectTransform rt, float fallbackX)
	{
		if (rt == null)
			return new HorizontalBounds(fallbackX, fallbackX);

		var rect = rt.rect;
		float xMin = rect.xMin;
		float xMax = rect.xMax;

		var image = rt.GetComponent<Image>();
		if (image != null && image.preserveAspect && image.sprite != null && rect.height > 0f)
		{
			var spriteRect = image.sprite.rect;
			if (spriteRect.width > 0f && spriteRect.height > 0f)
			{
				float spriteAspect = spriteRect.width / spriteRect.height;
				float rectAspect = rect.width / rect.height;
				if (spriteAspect < rectAspect)
				{
					float visibleWidth = rect.height * spriteAspect;
					float centerX = rect.center.x;
					xMin = centerX - visibleWidth * 0.5f;
					xMax = centerX + visibleWidth * 0.5f;
				}
			}
		}

		ApplyKnownVisibleSpriteInsets(rt, ref xMin, ref xMax);

		float worldMin = rt.TransformPoint(new Vector3(xMin, rect.center.y, 0f)).x;
		float worldMax = rt.TransformPoint(new Vector3(xMax, rect.center.y, 0f)).x;
		return new HorizontalBounds(Mathf.Min(worldMin, worldMax), Mathf.Max(worldMin, worldMax));
	}

	static void ApplyKnownVisibleSpriteInsets(RectTransform rt, ref float xMin, ref float xMax)
	{
		if (rt == null || rt.name != "PlayerBody")
			return;

		const float PlayerVisibleLeftRatio = 0.179f;
		const float PlayerVisibleRightRatio = 0.823f;

		float fullMin = xMin;
		float width = xMax - xMin;
		xMin = fullMin + width * PlayerVisibleLeftRatio;
		xMax = fullMin + width * PlayerVisibleRightRatio;
	}

	static Vector3 RectPointWorld(RectTransform rt, float normalizedX, float normalizedY, Vector3 fallback)
	{
		if (rt == null)
			return fallback;

		var rect = rt.rect;
		float x = Mathf.Lerp(rect.xMin, rect.xMax, normalizedX);
		float y = Mathf.Lerp(rect.yMin, rect.yMax, normalizedY);
		return rt.TransformPoint(new Vector3(x, y, 0f));
	}

	readonly struct HorizontalBounds
	{
		public readonly float min;
		public readonly float max;
		public float Width => Mathf.Max(0f, max - min);

		public HorizontalBounds(float min, float max)
		{
			this.min = min;
			this.max = max;
		}
	}
}

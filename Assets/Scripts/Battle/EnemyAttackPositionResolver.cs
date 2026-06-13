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
		if (!EnemyVisualBoundsResolver.TryResolveWorldBounds(rt, out Rect bounds))
			return new HorizontalBounds(fallbackX, fallbackX);

		return new HorizontalBounds(bounds.xMin, bounds.xMax);
	}

	static Vector3 RectPointWorld(RectTransform rt, float normalizedX, float normalizedY, Vector3 fallback)
	{
		return EnemyVisualBoundsResolver.ResolveWorldPoint(rt, normalizedX, normalizedY, fallback);
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

public readonly struct EnemyVisualBounds
{
	public readonly Rect rect;

	public EnemyVisualBounds(Rect rect)
	{
		this.rect = rect;
	}

	public Vector2 center => rect.center;
	public float xMin => rect.xMin;
	public float xMax => rect.xMax;
	public float yMin => rect.yMin;
	public float yMax => rect.yMax;
	public float width => Mathf.Max(0f, rect.width);
	public float height => Mathf.Max(0f, rect.height);

	public EnemyVisualBounds Padded(float padding)
	{
		float p = Mathf.Max(0f, padding);
		return new EnemyVisualBounds(Rect.MinMaxRect(
			rect.xMin - p,
			rect.yMin - p,
			rect.xMax + p,
			rect.yMax + p));
	}
}

public static class EnemyVisualBoundsResolver
{
	const float PlayerVisibleLeftRatio = 0.179f;
	const float PlayerVisibleRightRatio = 0.823f;

	public static bool TryResolveBoundsIn(Image image, RectTransform reference, out EnemyVisualBounds bounds)
	{
		RectTransform source = image != null ? image.rectTransform : null;
		return TryResolveBoundsIn(source, reference, out bounds);
	}

	public static bool TryResolveBoundsIn(RectTransform source, RectTransform reference, out EnemyVisualBounds bounds)
	{
		return TryResolveBoundsIn(source, reference, useImageVisualRect: true, out bounds);
	}

	public static bool TryResolveRectTransformBoundsIn(RectTransform source, RectTransform reference,
		out EnemyVisualBounds bounds)
	{
		return TryResolveBoundsIn(source, reference, useImageVisualRect: false, out bounds);
	}

	static bool TryResolveBoundsIn(RectTransform source, RectTransform reference, bool useImageVisualRect,
		out EnemyVisualBounds bounds)
	{
		bounds = default;
		if (source == null || reference == null)
			return false;

		Rect localRect = useImageVisualRect
			? ResolveRenderedLocalRect(source)
			: source.rect;
		bounds = new EnemyVisualBounds(LocalRectToReferenceRect(source, reference, localRect));
		return true;
	}

	public static bool TryResolveWorldBounds(RectTransform source, out Rect bounds)
	{
		bounds = default;
		if (source == null)
			return false;

		Rect localRect = ResolveRenderedLocalRect(source);
		Vector3 p0 = source.TransformPoint(new Vector3(localRect.xMin, localRect.yMin, 0f));
		Vector3 p1 = source.TransformPoint(new Vector3(localRect.xMin, localRect.yMax, 0f));
		Vector3 p2 = source.TransformPoint(new Vector3(localRect.xMax, localRect.yMax, 0f));
		Vector3 p3 = source.TransformPoint(new Vector3(localRect.xMax, localRect.yMin, 0f));
		bounds = RectFromPoints(p0, p1, p2, p3);
		return true;
	}

	public static Vector3 ResolveWorldPoint(RectTransform source, float normalizedX, float normalizedY,
		Vector3 fallback)
	{
		if (source == null)
			return fallback;

		Rect localRect = ResolveRenderedLocalRect(source);
		float x = Mathf.Lerp(localRect.xMin, localRect.xMax, Mathf.Clamp01(normalizedX));
		float y = Mathf.Lerp(localRect.yMin, localRect.yMax, Mathf.Clamp01(normalizedY));
		return source.TransformPoint(new Vector3(x, y, 0f));
	}

	public static Rect ResolveRenderedLocalRect(RectTransform source)
	{
		if (source == null)
			return Rect.zero;

		var image = source.GetComponent<Image>();
		return ResolveRenderedLocalRect(image, source.rect);
	}

	public static Rect ResolveRenderedLocalRect(Image image)
	{
		return image != null ? ResolveRenderedLocalRect(image, image.rectTransform.rect) : Rect.zero;
	}

	static Rect ResolveRenderedLocalRect(Image image, Rect rect)
	{
		if (image == null)
			return rect;

		Sprite sprite = image.sprite;
		if (image.preserveAspect && sprite != null)
			rect = ApplyPreserveAspect(rect, sprite);

		if (image.useSpriteMesh && sprite != null)
			rect = ApplySpriteMeshBounds(rect, sprite);

		ApplyKnownVisibleSpriteInsets(image.rectTransform, ref rect);
		return rect;
	}

	static Rect ApplyPreserveAspect(Rect rect, Sprite sprite)
	{
		if (sprite == null || rect.width <= 0f || rect.height <= 0f)
			return rect;

		Rect spriteRect = sprite.rect;
		if (spriteRect.width <= 0f || spriteRect.height <= 0f)
			return rect;

		float spriteAspect = spriteRect.width / spriteRect.height;
		float rectAspect = rect.width / rect.height;
		if (Mathf.Approximately(spriteAspect, rectAspect))
			return rect;

		if (spriteAspect > rectAspect)
		{
			float visibleHeight = rect.width / spriteAspect;
			float centerY = rect.center.y;
			return Rect.MinMaxRect(rect.xMin, centerY - visibleHeight * 0.5f,
				rect.xMax, centerY + visibleHeight * 0.5f);
		}

		float visibleWidth = rect.height * spriteAspect;
		float centerX = rect.center.x;
		return Rect.MinMaxRect(centerX - visibleWidth * 0.5f, rect.yMin,
			centerX + visibleWidth * 0.5f, rect.yMax);
	}

	static Rect ApplySpriteMeshBounds(Rect rect, Sprite sprite)
	{
		if (sprite == null)
			return rect;

		Vector2[] vertices = sprite.vertices;
		if (vertices == null || vertices.Length == 0)
			return rect;

		float pixelsPerUnit = Mathf.Max(0.0001f, sprite.pixelsPerUnit);
		Rect spriteRect = sprite.rect;
		if (spriteRect.width <= 0f || spriteRect.height <= 0f)
			return rect;

		Vector2 pivot = sprite.pivot;
		float fullMinX = -pivot.x / pixelsPerUnit;
		float fullMaxX = (spriteRect.width - pivot.x) / pixelsPerUnit;
		float fullMinY = -pivot.y / pixelsPerUnit;
		float fullMaxY = (spriteRect.height - pivot.y) / pixelsPerUnit;
		if (Mathf.Abs(fullMaxX - fullMinX) <= 0.0001f || Mathf.Abs(fullMaxY - fullMinY) <= 0.0001f)
			return rect;

		float minX = float.PositiveInfinity;
		float minY = float.PositiveInfinity;
		float maxX = float.NegativeInfinity;
		float maxY = float.NegativeInfinity;
		for (int i = 0; i < vertices.Length; i++)
		{
			Vector2 vertex = vertices[i];
			minX = Mathf.Min(minX, vertex.x);
			minY = Mathf.Min(minY, vertex.y);
			maxX = Mathf.Max(maxX, vertex.x);
			maxY = Mathf.Max(maxY, vertex.y);
		}

		if (float.IsNaN(minX) || float.IsNaN(maxX) || float.IsNaN(minY) || float.IsNaN(maxY)
			|| float.IsInfinity(minX) || float.IsInfinity(maxX)
			|| float.IsInfinity(minY) || float.IsInfinity(maxY)
			|| maxX <= minX || maxY <= minY)
			return rect;

		float nxMin = Mathf.Clamp01(Mathf.InverseLerp(fullMinX, fullMaxX, minX));
		float nxMax = Mathf.Clamp01(Mathf.InverseLerp(fullMinX, fullMaxX, maxX));
		float nyMin = Mathf.Clamp01(Mathf.InverseLerp(fullMinY, fullMaxY, minY));
		float nyMax = Mathf.Clamp01(Mathf.InverseLerp(fullMinY, fullMaxY, maxY));
		return Rect.MinMaxRect(
			Mathf.Lerp(rect.xMin, rect.xMax, nxMin),
			Mathf.Lerp(rect.yMin, rect.yMax, nyMin),
			Mathf.Lerp(rect.xMin, rect.xMax, nxMax),
			Mathf.Lerp(rect.yMin, rect.yMax, nyMax));
	}

	static void ApplyKnownVisibleSpriteInsets(RectTransform source, ref Rect rect)
	{
		if (source == null || source.name != "PlayerBody")
			return;

		float xMin = rect.xMin;
		float width = rect.width;
		rect.xMin = xMin + width * PlayerVisibleLeftRatio;
		rect.xMax = xMin + width * PlayerVisibleRightRatio;
	}

	static Rect LocalRectToReferenceRect(RectTransform source, RectTransform reference, Rect localRect)
	{
		Vector3 p0 = reference.InverseTransformPoint(
			source.TransformPoint(new Vector3(localRect.xMin, localRect.yMin, 0f)));
		Vector3 p1 = reference.InverseTransformPoint(
			source.TransformPoint(new Vector3(localRect.xMin, localRect.yMax, 0f)));
		Vector3 p2 = reference.InverseTransformPoint(
			source.TransformPoint(new Vector3(localRect.xMax, localRect.yMax, 0f)));
		Vector3 p3 = reference.InverseTransformPoint(
			source.TransformPoint(new Vector3(localRect.xMax, localRect.yMin, 0f)));
		return RectFromPoints(p0, p1, p2, p3);
	}

	static Rect RectFromPoints(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
	{
		float xMin = Mathf.Min(Mathf.Min(p0.x, p1.x), Mathf.Min(p2.x, p3.x));
		float xMax = Mathf.Max(Mathf.Max(p0.x, p1.x), Mathf.Max(p2.x, p3.x));
		float yMin = Mathf.Min(Mathf.Min(p0.y, p1.y), Mathf.Min(p2.y, p3.y));
		float yMax = Mathf.Max(Mathf.Max(p0.y, p1.y), Mathf.Max(p2.y, p3.y));
		return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
	}
}

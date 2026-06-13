using System;
using UnityEngine;

/// <summary>
/// 적 주사위 물리/카메라/임시 비주얼 프로필.
/// </summary>
[Serializable]
public sealed class EnemyDiceProfile
{
	public const string DefaultId = "default_d6";
	public const string BatId = "enemy_d6_bat_tmp";
	public const string SkeletonId = "enemy_d6_skeleton_tmp";
	public const string GoblinId = "enemy_d6_goblin_tmp";
	public const string SlimeId = "enemy_d6_slime_tmp";
	public const string DraculaId = "dracula_d6";
	public const int MaxEnemyDiceCount = 5;
	public const float DiceWorldSizeAtScaleOne = 1.36f; // DicePrefabBuilder.HalfSize * 2.
	public const float MinimumDiceSpacingMultiplier = 1.25f;
	public const float MinimumWallMarginMultiplier = 0.75f;
	public const float MaxSpawnJitterMultiplier = 0.10f;
	public const float DefaultDiceScale = 0.90f;
	public const float DefaultDiceSpacing = 1.55f;
	public const float DefaultCameraOrthographicSize = 3.05f;
	public const float DefaultSettleTimeoutSeconds = 10f;
	public const float CameraAspect = 16f / 9f;
	public const float CameraFramePadding = 1.05f;
	public const float DefaultOverlayHeight = 168f;
	public static readonly Vector3 DefaultArenaSize = new Vector3(10.2f, 8f, 3.8f);

	public string id = DefaultId;
	public GameObject prefab;
	public float diceScale = DefaultDiceScale;
	public float diceSpacing = DefaultDiceSpacing;
	public Vector3 arenaSize = DefaultArenaSize;
	public float launchHeight = 1.35f;
	public float positionJitter = 0.10f;
	public float forceMin = 3.8f;
	public float forceMax = 6.0f;
	public float torqueMin = 10f;
	public float torqueMax = 18f;
	public float bounce = 0.65f;
	public float staticFriction = 0.55f;
	public float dynamicFriction = 0.45f;
	public PhysicsMaterialCombine bounceCombine = PhysicsMaterialCombine.Maximum;
	public PhysicsMaterialCombine frictionCombine = PhysicsMaterialCombine.Average;
	public PhysicsMaterial colliderMaterial;
	public float rigidbodyMass = 1f;
	public float rigidbodyLinearDamping = 0f;
	public float rigidbodyAngularDamping = 0.05f;
	public Color visualBaseColor = Color.white;
	public Color visualDetailColor = new Color(0.08f, 0.08f, 0.09f, 1f);
	public float materialSmoothness = 0.28f;
	public float materialMetallic = 0f;
	public bool materialTransparent;
	public Texture2D faceAtlasTexture;
	public bool jellyEnabled;
	public float jellyCompressionMax = 0.14f;
	public float jellyShearMax = 0.08f;
	public float jellyImpulseScale = 0.012f;
	public float jellyStiffness = 42f;
	public float jellyDamping = 12f;
	public float jellySettleEpsilon = 0.0015f;
	public float jellyDentRadius = 0.42f;
	public float jellyBulgeScale = 0.36f;
	public float jellyWobbleMax = 0.08f;
	public bool cameraOrthographic = true;
	public float cameraOrthographicSize = DefaultCameraOrthographicSize;
	public Vector3 cameraOffset = new Vector3(0f, 7f, 0f);
	public Vector3 cameraEulerAngles = new Vector3(90f, 0f, 0f);
	public float cameraFieldOfView = 34f;
	public float overlayAspect = 16f / 9f;
	public float overlayMinHeight = DefaultOverlayHeight;
	public float overlayMaxHeight = DefaultOverlayHeight;
	public float overlayHeadGap = 8f;
	public float settleTimeoutSeconds = DefaultSettleTimeoutSeconds;

	public float DiceWorldSize => ComputeDiceWorldSize(diceScale);
	public float SafeDiceSpacing => ComputeSafeDiceSpacing(DiceWorldSize, positionJitter);
	public float SafeWallMargin => ComputeWallMargin(DiceWorldSize);

	public static EnemyDiceProfile CreateDefault(GameObject prefab = null)
	{
		var profile = new EnemyDiceProfile
		{
			id = DefaultId,
			prefab = prefab
		};
		profile.NormalizeSafetySizing();
		return profile;
	}

	public static EnemyDiceProfile CreatePrototype(EnemyDiceStyleKind style, GameObject prefab = null)
	{
		switch (style)
		{
			case EnemyDiceStyleKind.Bat:
				return CreateSafeProfile(new EnemyDiceProfile
				{
					id = BatId,
					prefab = prefab,
					diceScale = 0.78f,
					diceSpacing = 0.86f,
					launchHeight = 1.48f,
					positionJitter = 0.08f,
					forceMin = 4.2f,
					forceMax = 6.4f,
					torqueMin = 16f,
					torqueMax = 26f,
					bounce = 0.82f,
					staticFriction = 0.18f,
					dynamicFriction = 0.12f,
					bounceCombine = PhysicsMaterialCombine.Maximum,
					frictionCombine = PhysicsMaterialCombine.Minimum,
					rigidbodyMass = 0.65f,
					rigidbodyLinearDamping = 0.01f,
					rigidbodyAngularDamping = 0.02f,
					visualBaseColor = new Color(0.55f, 0.25f, 0.92f, 1f),
					visualDetailColor = new Color(0.95f, 0.82f, 1f, 1f),
					materialSmoothness = 0.42f,
					cameraOrthographicSize = 1.65f,
					overlayHeadGap = 14f,
				});
			case EnemyDiceStyleKind.Skeleton:
				return CreateSafeProfile(new EnemyDiceProfile
				{
					id = SkeletonId,
					prefab = prefab,
					diceScale = 0.88f,
					diceSpacing = 0.98f,
					launchHeight = 1.28f,
					positionJitter = 0.08f,
					forceMin = 3.6f,
					forceMax = 5.4f,
					torqueMin = 9f,
					torqueMax = 15f,
					bounce = 0.36f,
					staticFriction = 0.62f,
					dynamicFriction = 0.48f,
					bounceCombine = PhysicsMaterialCombine.Average,
					frictionCombine = PhysicsMaterialCombine.Average,
					rigidbodyMass = 0.90f,
					rigidbodyLinearDamping = 0.03f,
					rigidbodyAngularDamping = 0.08f,
					visualBaseColor = new Color(0.86f, 0.78f, 0.58f, 1f),
					visualDetailColor = new Color(0.16f, 0.13f, 0.10f, 1f),
					materialSmoothness = 0.18f,
				});
			case EnemyDiceStyleKind.Goblin:
				return CreateSafeProfile(new EnemyDiceProfile
				{
					id = GoblinId,
					prefab = prefab,
					diceScale = DefaultDiceScale,
					diceSpacing = DefaultDiceSpacing,
					launchHeight = 1.12f,
					positionJitter = 0.08f,
					forceMin = 3.3f,
					forceMax = 4.9f,
					torqueMin = 7f,
					torqueMax = 12f,
					bounce = 0.32f,
					staticFriction = 0.58f,
					dynamicFriction = 0.44f,
					bounceCombine = PhysicsMaterialCombine.Average,
					frictionCombine = PhysicsMaterialCombine.Average,
					rigidbodyMass = 1.10f,
					rigidbodyLinearDamping = 0.05f,
					rigidbodyAngularDamping = 0.10f,
					visualBaseColor = new Color(0.07f, 0.35f, 0.16f, 1f),
					visualDetailColor = new Color(0.78f, 0.95f, 0.48f, 1f),
					materialSmoothness = 0.22f,
					cameraOrthographicSize = DefaultCameraOrthographicSize,
				});
			case EnemyDiceStyleKind.Slime:
				return CreateSafeProfile(new EnemyDiceProfile
				{
					id = SlimeId,
					prefab = prefab,
					diceScale = 0.95f,
					diceSpacing = 1.02f,
					launchHeight = 1.38f,
					positionJitter = 0.10f,
					forceMin = 4.0f,
					forceMax = 6.3f,
					torqueMin = 11f,
					torqueMax = 19f,
					bounce = 0.08f,
					staticFriction = 0.48f,
					dynamicFriction = 0.36f,
					bounceCombine = PhysicsMaterialCombine.Minimum,
					frictionCombine = PhysicsMaterialCombine.Average,
					rigidbodyMass = 0.80f,
					rigidbodyLinearDamping = 0.16f,
					rigidbodyAngularDamping = 0.28f,
					visualBaseColor = new Color(0.48f, 1.00f, 0.18f, 0.78f),
					visualDetailColor = new Color(0.001f, 0.018f, 0.003f, 1f),
					materialSmoothness = 0.82f,
					materialTransparent = true,
					jellyEnabled = true,
					jellyCompressionMax = 0.30f,
					jellyShearMax = 0.16f,
					jellyImpulseScale = 0.028f,
					jellyStiffness = 54f,
					jellyDamping = 18f,
					jellySettleEpsilon = 0.0015f,
					jellyDentRadius = 0.46f,
					jellyBulgeScale = 0.48f,
					jellyWobbleMax = 0.12f,
					overlayHeadGap = 10f,
				});
			case EnemyDiceStyleKind.Dracula:
				var profile = CreateDefault(prefab);
				profile.id = DraculaId;
				return profile;
			default:
				return CreateDefault(prefab);
		}
	}

	static EnemyDiceProfile CreateSafeProfile(EnemyDiceProfile profile)
	{
		profile.NormalizeSafetySizing();
		return profile;
	}

	public static EnemyDiceProfile[] CreatePrototypeSet(GameObject prefab = null)
	{
		var profiles = new[]
		{
			CreateDefault(prefab),
			CreatePrototype(EnemyDiceStyleKind.Bat, prefab),
			CreatePrototype(EnemyDiceStyleKind.Skeleton, prefab),
			CreatePrototype(EnemyDiceStyleKind.Goblin, prefab),
			CreatePrototype(EnemyDiceStyleKind.Slime, prefab),
			CreatePrototype(EnemyDiceStyleKind.Dracula, prefab),
		};
		for (int i = 0; i < profiles.Length; i++)
			profiles[i]?.NormalizeSafetySizing();
		return profiles;
	}

	public void NormalizeSafetySizing(int diceCount = MaxEnemyDiceCount)
	{
		diceScale = Mathf.Max(0.1f, diceScale);
		float diceWorldSize = DiceWorldSize;
		positionJitter = Mathf.Clamp(positionJitter, 0f, diceWorldSize * MaxSpawnJitterMultiplier);
		diceSpacing = Mathf.Max(diceSpacing, ComputeSafeDiceSpacing(diceWorldSize, positionJitter));
		arenaSize = ComputeSafeArenaSize(diceCount, diceWorldSize, diceSpacing, arenaSize, positionJitter);
		cameraOrthographicSize = Mathf.Max(cameraOrthographicSize, ComputeCameraOrthographicSize(arenaSize));
		settleTimeoutSeconds = settleTimeoutSeconds > 0f
			? settleTimeoutSeconds
			: DefaultSettleTimeoutSeconds;
	}

	public Vector3[] ComputeSpawnPositions(Vector3 center, int diceCount)
	{
		int count = Mathf.Clamp(diceCount, 1, MaxEnemyDiceCount);
		NormalizeSafetySizing(count);

		var positions = new Vector3[count];
		float diceWorldSize = DiceWorldSize;
		float halfDice = diceWorldSize * 0.5f;
		float edgeMargin = SafeWallMargin + positionJitter + halfDice;
		float halfWidth = arenaSize.x * 0.5f;
		float halfDepth = arenaSize.z * 0.5f;
		float minX = center.x - halfWidth + edgeMargin;
		float maxX = center.x + halfWidth - edgeMargin;
		float minZ = center.z - halfDepth + edgeMargin;
		float maxZ = center.z + halfDepth - edgeMargin;
		float startX = center.x - (count - 1) * diceSpacing * 0.5f;
		float z = Mathf.Clamp(center.z, minZ, maxZ);

		for (int i = 0; i < count; i++)
		{
			float x = Mathf.Clamp(startX + i * diceSpacing, minX, maxX);
			positions[i] = new Vector3(x, center.y, z);
		}
		return positions;
	}

	public static float ComputeDiceWorldSize(float diceScale)
	{
		return DiceWorldSizeAtScaleOne * Mathf.Max(0.1f, diceScale);
	}

	public static float ComputeSafeDiceSpacing(float diceWorldSize, float positionJitter = 0f)
	{
		float size = Mathf.Max(0.01f, diceWorldSize);
		float jitter = Mathf.Max(0f, positionJitter);
		return Mathf.Max(size * MinimumDiceSpacingMultiplier, size + jitter * 2f);
	}

	public static float ComputeWallMargin(float diceWorldSize)
	{
		return Mathf.Max(0.01f, diceWorldSize) * MinimumWallMarginMultiplier;
	}

	public static float ComputeRequiredArenaWidth(int diceCount, float diceWorldSize,
		float diceSpacing, float positionJitter = 0f)
	{
		int count = Mathf.Clamp(diceCount, 1, MaxEnemyDiceCount);
		float size = Mathf.Max(0.01f, diceWorldSize);
		float spacing = Mathf.Max(0f, diceSpacing);
		float jitter = Mathf.Max(0f, positionJitter);
		return size + (count - 1) * spacing + (ComputeWallMargin(size) + jitter) * 2f;
	}

	public static float ComputeRequiredArenaDepth(float diceWorldSize, float positionJitter = 0f)
	{
		float size = Mathf.Max(0.01f, diceWorldSize);
		float jitter = Mathf.Max(0f, positionJitter);
		return size + (ComputeWallMargin(size) + jitter) * 2f;
	}

	public static Vector3 ComputeSafeArenaSize(int diceCount, float diceWorldSize,
		float diceSpacing, Vector3 currentArenaSize, float positionJitter = 0f)
	{
		float width = ComputeRequiredArenaWidth(diceCount, diceWorldSize, diceSpacing, positionJitter);
		float depth = ComputeRequiredArenaDepth(diceWorldSize, positionJitter);
		return new Vector3(
			Mathf.Max(currentArenaSize.x, width),
			Mathf.Max(currentArenaSize.y, diceWorldSize * 3f),
			Mathf.Max(currentArenaSize.z, depth));
	}

	public static float ComputeCameraOrthographicSize(Vector3 arenaSize)
	{
		float halfDepth = Mathf.Max(0.1f, arenaSize.z * 0.5f);
		float halfWidth = Mathf.Max(0.1f, arenaSize.x / (CameraAspect * 2f));
		return Mathf.Max(halfDepth, halfWidth) * CameraFramePadding;
	}

	public static float ComputeMaxSafeDiceScale(Vector3 arenaSize, int diceCount = MaxEnemyDiceCount)
	{
		int count = Mathf.Clamp(diceCount, 1, MaxEnemyDiceCount);
		float widthFactor = 1f
			+ (count - 1) * MinimumDiceSpacingMultiplier
			+ MinimumWallMarginMultiplier * 2f
			+ MaxSpawnJitterMultiplier * 2f;
		float depthFactor = 1f
			+ MinimumWallMarginMultiplier * 2f
			+ MaxSpawnJitterMultiplier * 2f;
		float maxWorldSizeByWidth = arenaSize.x / widthFactor;
		float maxWorldSizeByDepth = arenaSize.z / depthFactor;
		return Mathf.Min(maxWorldSizeByWidth, maxWorldSizeByDepth) / DiceWorldSizeAtScaleOne;
	}

	public void NormalizeDefaultDisplaySize()
	{
		if (id != DefaultId)
			return;

		diceScale = DefaultDiceScale;
		diceSpacing = DefaultDiceSpacing;
		cameraOrthographicSize = DefaultCameraOrthographicSize;
		arenaSize = DefaultArenaSize;
		positionJitter = Mathf.Min(positionJitter, DiceWorldSize * MaxSpawnJitterMultiplier);
		settleTimeoutSeconds = Mathf.Max(settleTimeoutSeconds, DefaultSettleTimeoutSeconds);
		overlayMinHeight = Mathf.Max(overlayMinHeight, DefaultOverlayHeight);
		overlayMaxHeight = Mathf.Max(overlayMaxHeight, overlayMinHeight);
		staticFriction = staticFriction > 0f ? staticFriction : 0.55f;
		dynamicFriction = dynamicFriction > 0f ? dynamicFriction : 0.45f;
		bounceCombine = PhysicsMaterialCombine.Maximum;
		frictionCombine = PhysicsMaterialCombine.Average;
		rigidbodyMass = rigidbodyMass > 0f ? rigidbodyMass : 1f;
		rigidbodyLinearDamping = Mathf.Max(0f, rigidbodyLinearDamping);
		rigidbodyAngularDamping = rigidbodyAngularDamping > 0f ? rigidbodyAngularDamping : 0.05f;
		if (visualBaseColor.a <= 0f)
			visualBaseColor = Color.white;
		if (visualDetailColor.a <= 0f)
			visualDetailColor = new Color(0.08f, 0.08f, 0.09f, 1f);
		materialSmoothness = materialSmoothness > 0f ? materialSmoothness : 0.28f;
		jellyEnabled = false;
		NormalizeSafetySizing();
	}
}

[Serializable]
public sealed class EnemyDiceProfileCatalog
{
	[SerializeField] EnemyDiceProfile[] profiles = { EnemyDiceProfile.CreateDefault() };

	public EnemyDiceProfile[] Profiles => profiles;

	public EnemyDiceProfile Resolve(string profileId)
	{
		string normalizedId = string.IsNullOrWhiteSpace(profileId)
			? EnemyDiceProfile.DefaultId
			: profileId;

		var profile = Find(normalizedId);
		if (profile != null)
		{
			profile.NormalizeDefaultDisplaySize();
			profile.NormalizeSafetySizing();
			return profile;
		}

		if (EnemyDiceStyleResolver.TryResolveStyleFromProfileId(normalizedId, out var style))
		{
			profile = EnemyDiceProfile.CreatePrototype(style, Find(EnemyDiceProfile.DefaultId)?.prefab);
			profile.NormalizeSafetySizing();
			return profile;
		}

		profile = Find(EnemyDiceProfile.DefaultId);
		if (profile != null)
		{
			profile.NormalizeDefaultDisplaySize();
			profile.NormalizeSafetySizing();
			return profile;
		}

		profile = EnemyDiceProfile.CreateDefault();
		profile.NormalizeSafetySizing();
		return profile;
	}

	EnemyDiceProfile Find(string profileId)
	{
		if (profiles == null)
			return null;

		for (int i = 0; i < profiles.Length; i++)
		{
			var profile = profiles[i];
			if (profile != null && profile.id == profileId)
				return profile;
		}
		return null;
	}

	public static EnemyDiceProfileCatalog CreateDefault(GameObject prefab = null)
	{
		return new EnemyDiceProfileCatalog
		{
			profiles = EnemyDiceProfile.CreatePrototypeSet(prefab)
		};
	}
}

public enum EnemyDiceStyleKind
{
	Default,
	Bat,
	Skeleton,
	Goblin,
	Slime,
	Dracula,
}

/// <summary>
/// 프로토타입용 임시 이름 기반 적 주사위 스타일 resolver.
/// 안정적인 몹 id가 생기면 이 tolerant name matching은 데이터 기반 매핑으로 교체한다.
/// </summary>
public static class EnemyDiceStyleResolver
{
	public static EnemyDiceStyleKind ResolveStyle(EnemyInfo enemy)
	{
		return ResolveStyle(enemy != null ? enemy.name : null);
	}

	public static EnemyDiceStyleKind ResolveStyle(string enemyIdentifier)
	{
		string normalized = Normalize(enemyIdentifier);
		if (string.IsNullOrEmpty(normalized))
			return EnemyDiceStyleKind.Default;

		if (normalized.Contains("bat") || normalized.Contains("박쥐"))
			return EnemyDiceStyleKind.Bat;
		if (normalized.Contains("skeleton") || normalized.Contains("스켈레톤") || normalized.Contains("해골"))
			return EnemyDiceStyleKind.Skeleton;
		if (normalized.Contains("goblin") || normalized.Contains("고블린"))
			return EnemyDiceStyleKind.Goblin;
		if (normalized.Contains("slime") || normalized.Contains("슬라임"))
			return EnemyDiceStyleKind.Slime;
		if (normalized.Contains("dracula") || normalized.Contains("드라큘라"))
			return EnemyDiceStyleKind.Dracula;

		return EnemyDiceStyleKind.Default;
	}

	public static string ResolveProfileId(EnemyInfo enemy)
	{
		return ToProfileId(ResolveStyle(enemy));
	}

	public static string ResolveProfileId(string enemyIdentifier)
	{
		return ToProfileId(ResolveStyle(enemyIdentifier));
	}

	public static bool TryResolveStyleFromProfileId(string profileId, out EnemyDiceStyleKind style)
	{
		switch (Normalize(profileId))
		{
			case EnemyDiceProfile.BatId:
			case "bat":
			case "박쥐":
				style = EnemyDiceStyleKind.Bat;
				return true;
			case EnemyDiceProfile.SkeletonId:
			case "skeleton":
			case "해골":
			case "스켈레톤":
				style = EnemyDiceStyleKind.Skeleton;
				return true;
			case EnemyDiceProfile.GoblinId:
			case "goblin":
			case "고블린":
				style = EnemyDiceStyleKind.Goblin;
				return true;
			case EnemyDiceProfile.SlimeId:
			case "slime":
			case "슬라임":
				style = EnemyDiceStyleKind.Slime;
				return true;
			case EnemyDiceProfile.DraculaId:
			case "dracula":
			case "드라큘라":
				style = EnemyDiceStyleKind.Dracula;
				return true;
			default:
				style = EnemyDiceStyleKind.Default;
				return false;
		}
	}

	public static string ToProfileId(EnemyDiceStyleKind style)
	{
		switch (style)
		{
			case EnemyDiceStyleKind.Bat:
				return EnemyDiceProfile.BatId;
			case EnemyDiceStyleKind.Skeleton:
				return EnemyDiceProfile.SkeletonId;
			case EnemyDiceStyleKind.Goblin:
				return EnemyDiceProfile.GoblinId;
			case EnemyDiceStyleKind.Slime:
				return EnemyDiceProfile.SlimeId;
			case EnemyDiceStyleKind.Dracula:
				return EnemyDiceProfile.DraculaId;
			default:
				return EnemyDiceProfile.DefaultId;
		}
	}

	static string Normalize(string value)
	{
		return string.IsNullOrWhiteSpace(value)
			? ""
			: value.Trim().ToLowerInvariant();
	}
}

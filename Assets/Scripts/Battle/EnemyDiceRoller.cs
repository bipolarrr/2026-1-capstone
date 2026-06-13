using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 적 주사위 — 별도 3D 아레나에서 rank 개수만큼 회전시키고 결과를 반환한다.
/// 렌더링은 EnemyDiceCamera가 RenderTexture로 뽑아 UI 오버레이(RawImage)에 보여줌.
/// </summary>
public class EnemyDiceRoller : MonoBehaviour
{
	[SerializeField] Dice[] enemyDice;
	[SerializeField] Vector3 vaultCenter;
	[SerializeField] Camera diceCamera;
	[SerializeField] EnemyDiceProfileCatalog profileCatalog = EnemyDiceProfileCatalog.CreateDefault();
	[SerializeField] Material slimeJellyMaterial;

	[SerializeField] float diceSpacing = 1f;

	sealed class RendererMaterialState
	{
		public Material[] originalMaterials;
		public Material[] runtimeMaterials;
	}

	readonly Dictionary<Renderer, RendererMaterialState> rendererMaterialStates = new Dictionary<Renderer, RendererMaterialState>();
	readonly Dictionary<string, Texture2D> styleTextures = new Dictionary<string, Texture2D>();

	/// <summary>
	/// count개 주사위를 활성화하고 중앙 정렬 배치. 굴리지 않고 정적인 면만 보여줌.
	/// "날아오는" 연출 중 UI 오버레이에 미리 보일 상태를 만든다.
	/// 각 주사위의 home position도 이 자리로 재설정해, 이후 물리 굴림이 같은 자리에서 시작하게 함.
	/// </summary>
	public void PlaceForCount(int count)
	{
		PlaceForCount(count, EnemyDiceProfile.DefaultId);
	}

	public void PlaceForCount(int count, string profileId)
	{
		if (enemyDice == null || enemyDice.Length == 0)
			return;
		var profile = ResolveProfile(profileId);
		profile?.NormalizeSafetySizing(count);
		ApplyCameraProfile(profile);
		count = Mathf.Clamp(count, 1, enemyDice.Length);
		Vector3[] spawnPositions = profile != null
			? profile.ComputeSpawnPositions(vaultCenter, count)
			: ComputeFallbackSpawnPositions(count);

		for (int i = 0; i < enemyDice.Length; i++)
		{
			if (enemyDice[i] == null) continue;

			if (i < count)
			{
				enemyDice[i].gameObject.SetActive(true);
				var pos = spawnPositions[i];
				ApplyDieProfile(enemyDice[i], profile);
				BeginJellyRoll(enemyDice[i]);
				enemyDice[i].SetHome(pos);
				enemyDice[i].ForceResult(DiceRandomizer.Next()); // 굴리기 전 표시용 정적 면
			}
			else
			{
				enemyDice[i].gameObject.SetActive(false);
			}
		}
	}

	public Coroutine RollForEnemy(int diceCount, Action<EnemyDiceResult> onComplete)
	{
		return RollForEnemy(diceCount, EnemyDiceProfile.DefaultId, onComplete);
	}

	public Coroutine RollForEnemy(int diceCount, string profileId, Action<EnemyDiceResult> onComplete)
	{
		return StartCoroutine(RollRoutine(diceCount, profileId, onComplete));
	}

	public EnemyDiceProfile ResolveProfile(string profileId)
	{
		if (profileCatalog == null)
			profileCatalog = EnemyDiceProfileCatalog.CreateDefault();
		return profileCatalog.Resolve(profileId);
	}

	void OnDestroy()
	{
		foreach (var state in rendererMaterialStates.Values)
		{
			if (state?.runtimeMaterials == null)
				continue;

			for (int i = 0; i < state.runtimeMaterials.Length; i++)
				DestroyRuntimeObject(state.runtimeMaterials[i]);
		}
		rendererMaterialStates.Clear();

		foreach (var texture in styleTextures.Values)
			DestroyRuntimeObject(texture);
		styleTextures.Clear();
	}

	IEnumerator RollRoutine(int diceCount, string profileId, Action<EnemyDiceResult> onComplete)
	{
		if (enemyDice == null || enemyDice.Length == 0)
		{
			Debug.LogWarning("[EnemyDice] enemyDice 미할당 — 물리 롤러 씬 wiring 확인 필요");
			onComplete?.Invoke(null);
			yield break;
		}

		var profile = ResolveProfile(profileId);
		int count = Mathf.Clamp(diceCount, 1, enemyDice.Length);
		profile?.NormalizeSafetySizing(count);
		PlaceForCount(count, profile != null ? profile.id : profileId);

		// 족보가 존재 가능한 경우(rank ≥ 4)만 드럼롤 — 그 외는 무음 (향후 던지는 SE 자리).
		bool playBgm = count >= 4;
		if (playBgm) DiceDrumRollAudio.Play();

		int completed = 0;
		int[] values = new int[count];
		bool[] completedMask = new bool[count];
		Vector3[] anchors = CaptureActiveAnchors(count);
		Coroutine[] rollCoroutines = new Coroutine[count];
		float settleTimeout = profile != null
			? profile.settleTimeoutSeconds
			: EnemyDiceProfile.DefaultSettleTimeoutSeconds;
		float timeoutAt = Time.realtimeSinceStartup + settleTimeout;
		for (int i = 0; i < count; i++)
			rollCoroutines[i] = StartCoroutine(RollPhysicalDieRoutine(
				i, values, completedMask, timeoutAt, () => completed++));

		while (completed < count && Time.realtimeSinceStartup < timeoutAt)
			yield return null;

		if (completed < count)
		{
			for (int i = 0; i < count; i++)
			{
				if (completedMask[i])
					continue;

				if (rollCoroutines[i] != null)
					StopCoroutine(rollCoroutines[i]);

				CompleteTimedOutDie(i, count, profile, profileId, anchors[i], values, completedMask);
				completed++;
			}
		}

		if (playBgm) DiceDrumRollAudio.Stop();

		var (_, comboName, _, _) = DamageCalculator.Calculate(
			PadToFive(values), new List<PowerUpType>());

		bool hasCombo = count >= 4 && !string.IsNullOrEmpty(comboName);
		if (!hasCombo) comboName = "";

		float multiplier = EnemyDiceResult.GetMultiplier(comboName);

		var result = new EnemyDiceResult
		{
			values           = values,
			comboName        = comboName,
			damageMultiplier = multiplier,
			hasCombo         = hasCombo
		};

		Debug.Log($"[EnemyDice] roll complete: [{string.Join(",", values)}] combo=\"{comboName}\"");
		onComplete?.Invoke(result);
	}

	void ApplyDieProfile(Dice die, EnemyDiceProfile profile)
	{
		if (die == null || profile == null)
			return;

		die.transform.localScale = Vector3.one * Mathf.Max(0.1f, profile.diceScale);
		die.ConfigurePhysicalRoll(
			profile.launchHeight,
			profile.positionJitter,
			profile.forceMin,
			profile.forceMax,
			profile.torqueMin,
			profile.torqueMax);

		ApplyVisualProfile(die, profile);
		ApplyJellyProfile(die, profile);
		ApplyRigidbodyProfile(die, profile);
		ApplyColliderProfile(die, profile);
	}

	void ApplyVisualProfile(Dice die, EnemyDiceProfile profile)
	{
		foreach (var renderer in die.GetComponentsInChildren<Renderer>(true))
		{
			if (renderer == null)
				continue;
			if (renderer.GetComponent<SlimeDiceJellyRenderProxy>() != null)
				continue;

			var state = EnsureRuntimeMaterials(renderer);
			if (state == null || state.runtimeMaterials == null)
				continue;

			for (int i = 0; i < state.runtimeMaterials.Length; i++)
			{
				var runtimeMaterial = state.runtimeMaterials[i];
				if (runtimeMaterial == null)
					continue;

				var originalMaterial = state.originalMaterials != null && i < state.originalMaterials.Length
					? state.originalMaterials[i]
					: null;
				ApplyMaterialProfile(runtimeMaterial, originalMaterial, profile);
			}
		}
	}

	void ApplyJellyProfile(Dice die, EnemyDiceProfile profile)
	{
		var deformer = die.GetComponent<SlimeDiceJellyDeformer>();
		if (profile.jellyEnabled)
		{
			if (deformer == null)
				deformer = die.gameObject.AddComponent<SlimeDiceJellyDeformer>();
			deformer.Configure(profile, slimeJellyMaterial);
			return;
		}

		if (deformer != null)
			deformer.DisableJelly();
	}

	RendererMaterialState EnsureRuntimeMaterials(Renderer renderer)
	{
		if (rendererMaterialStates.TryGetValue(renderer, out var state))
			return state;

		var originalMaterials = renderer.sharedMaterials;
		if (originalMaterials == null || originalMaterials.Length == 0)
			return null;

		var runtimeMaterials = new Material[originalMaterials.Length];
		for (int i = 0; i < originalMaterials.Length; i++)
		{
			if (originalMaterials[i] == null)
				continue;

			runtimeMaterials[i] = new Material(originalMaterials[i])
			{
				name = $"{originalMaterials[i].name}_EnemyDiceRuntime"
			};
		}

		renderer.sharedMaterials = runtimeMaterials;
		state = new RendererMaterialState
		{
			originalMaterials = originalMaterials,
			runtimeMaterials = runtimeMaterials
		};
		rendererMaterialStates[renderer] = state;
		return state;
	}

	void ApplyMaterialProfile(Material material, Material originalMaterial, EnemyDiceProfile profile)
	{
		var originalTexture = GetMainTexture(originalMaterial);
		var atlasTexture = profile.faceAtlasTexture;
		var styledTexture = atlasTexture == null && profile.id != EnemyDiceProfile.DefaultId
			? TryGetStyledTexture(originalMaterial, profile)
			: null;
		var appliedTexture = atlasTexture != null
			? atlasTexture
			: styledTexture;

		if (appliedTexture != null)
			SetMainTexture(material, appliedTexture);
		else if (originalTexture != null)
			SetMainTexture(material, originalTexture);

		Color materialColor = appliedTexture != null
			? new Color(1f, 1f, 1f, Mathf.Clamp01(profile.visualBaseColor.a))
			: profile.visualBaseColor;
		SetMaterialColor(material, materialColor);

		if (material.HasProperty("_Smoothness"))
			material.SetFloat("_Smoothness", Mathf.Clamp01(profile.materialSmoothness));
		if (material.HasProperty("_Metallic"))
			material.SetFloat("_Metallic", Mathf.Clamp01(profile.materialMetallic));

		ApplyMaterialTransparency(material, profile.materialTransparent || materialColor.a < 0.99f);
	}

	Texture2D TryGetStyledTexture(Material originalMaterial, EnemyDiceProfile profile)
	{
		if (originalMaterial == null)
			return null;

		var source = GetMainTexture(originalMaterial) as Texture2D;
		if (source == null)
			return null;

		Color32 baseColor = profile.visualBaseColor;
		Color32 detailColor = profile.visualDetailColor;
		string key = $"{source.GetInstanceID()}:{profile.id}:"
			+ $"{baseColor.r:X2}{baseColor.g:X2}{baseColor.b:X2}{baseColor.a:X2}:"
			+ $"{detailColor.r:X2}{detailColor.g:X2}{detailColor.b:X2}{detailColor.a:X2}";
		if (styleTextures.TryGetValue(key, out var cachedTexture))
			return cachedTexture;

		Color32[] pixels;
		try
		{
			pixels = source.GetPixels32();
		}
		catch (UnityException)
		{
			return null;
		}

		for (int i = 0; i < pixels.Length; i++)
			pixels[i] = ColorizeDicePixel(pixels[i], profile);

		var texture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false)
		{
			name = $"{source.name}_{profile.id}_runtime",
			filterMode = source.filterMode,
			wrapMode = source.wrapMode,
			hideFlags = HideFlags.DontSave
		};
		texture.SetPixels32(pixels);
		texture.Apply(false, false);
		styleTextures[key] = texture;
		return texture;
	}

	static Color32 ColorizeDicePixel(Color32 pixel, EnemyDiceProfile profile)
	{
		Color source = pixel;
		if (source.a <= 0.01f)
			return pixel;

		float max = Mathf.Max(source.r, Mathf.Max(source.g, source.b));
		float min = Mathf.Min(source.r, Mathf.Min(source.g, source.b));
		float brightness = source.grayscale;
		float saturation = max > 0.0001f ? (max - min) / max : 0f;
		bool basePixel = brightness >= 0.52f && saturation <= 0.35f;
		Color target = basePixel ? profile.visualBaseColor : profile.visualDetailColor;
		float shade = basePixel
			? Mathf.Lerp(0.72f, 1.12f, brightness)
			: Mathf.Lerp(0.68f, 1.18f, brightness);
		Color styled = target * shade;
		styled.a = source.a * Mathf.Clamp01(target.a);
		return styled;
	}

	static Texture GetMainTexture(Material material)
	{
		if (material == null)
			return null;
		if (material.HasProperty("_BaseMap"))
			return material.GetTexture("_BaseMap");
		if (material.HasProperty("_MainTex"))
			return material.GetTexture("_MainTex");
		return null;
	}

	static void SetMainTexture(Material material, Texture texture)
	{
		if (material == null || texture == null)
			return;
		if (material.HasProperty("_BaseMap"))
			material.SetTexture("_BaseMap", texture);
		if (material.HasProperty("_MainTex"))
			material.SetTexture("_MainTex", texture);
	}

	static void SetMaterialColor(Material material, Color color)
	{
		if (material == null)
			return;
		if (material.HasProperty("_BaseColor"))
			material.SetColor("_BaseColor", color);
		if (material.HasProperty("_Color"))
			material.SetColor("_Color", color);
	}

	static void ApplyMaterialTransparency(Material material, bool transparent)
	{
		if (material == null)
			return;

		if (material.HasProperty("_Surface"))
			material.SetFloat("_Surface", transparent ? 1f : 0f);
		if (material.HasProperty("_SrcBlend"))
			material.SetInt("_SrcBlend", transparent
				? (int)UnityEngine.Rendering.BlendMode.SrcAlpha
				: (int)UnityEngine.Rendering.BlendMode.One);
		if (material.HasProperty("_DstBlend"))
			material.SetInt("_DstBlend", transparent
				? (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha
				: (int)UnityEngine.Rendering.BlendMode.Zero);
		if (material.HasProperty("_ZWrite"))
			material.SetInt("_ZWrite", transparent ? 0 : 1);

		if (transparent)
		{
			material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
			material.SetOverrideTag("RenderType", "Transparent");
			material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
		}
		else
		{
			material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
			material.SetOverrideTag("RenderType", "Opaque");
			material.renderQueue = -1;
		}
	}

	void ApplyRigidbodyProfile(Dice die, EnemyDiceProfile profile)
	{
		var body = die.GetComponent<Rigidbody>();
		if (body == null)
			return;

		body.mass = Mathf.Max(0.01f, profile.rigidbodyMass);
		body.linearDamping = Mathf.Max(0f, profile.rigidbodyLinearDamping);
		body.angularDamping = Mathf.Max(0f, profile.rigidbodyAngularDamping);
		body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
		body.interpolation = RigidbodyInterpolation.Interpolate;
		body.solverIterations = Mathf.Max(body.solverIterations, 12);
		body.solverVelocityIterations = Mathf.Max(body.solverVelocityIterations, 4);
	}

	void ApplyColliderProfile(Dice die, EnemyDiceProfile profile)
	{
		foreach (var col in die.GetComponentsInChildren<Collider>())
		{
			if (profile.colliderMaterial != null)
				col.material = profile.colliderMaterial;

			var material = col.material;
			if (material == null)
				continue;

			material.bounciness = Mathf.Clamp01(profile.bounce);
			material.staticFriction = Mathf.Clamp01(profile.staticFriction);
			material.dynamicFriction = Mathf.Clamp01(profile.dynamicFriction);
			material.bounceCombine = profile.bounceCombine;
			material.frictionCombine = profile.frictionCombine;
		}
	}

	static void DestroyRuntimeObject(UnityEngine.Object obj)
	{
		if (obj == null)
			return;
		if (Application.isPlaying)
			UnityEngine.Object.Destroy(obj);
		else
			UnityEngine.Object.DestroyImmediate(obj);
	}

	void ApplyCameraProfile(EnemyDiceProfile profile)
	{
		if (diceCamera == null || profile == null)
			return;

		profile.NormalizeSafetySizing();
		diceCamera.orthographic = profile.cameraOrthographic;
		if (profile.cameraOrthographic)
		{
			diceCamera.orthographicSize = Mathf.Max(0.1f, profile.cameraOrthographicSize);
		}
		else
		{
			diceCamera.fieldOfView = Mathf.Max(1f, profile.cameraFieldOfView);
		}
		diceCamera.transform.position = vaultCenter + profile.cameraOffset;
		diceCamera.transform.rotation = Quaternion.Euler(profile.cameraEulerAngles);
	}

	IEnumerator RollPhysicalDieRoutine(int idx, int[] values, bool[] completedMask,
		float timeoutAt, Action onCompleted)
	{
		if (idx < 0 || idx >= enemyDice.Length || enemyDice[idx] == null)
		{
			onCompleted?.Invoke();
			yield break;
		}

		Vector3 anchor = enemyDice[idx].transform.position;
		enemyDice[idx].SetSpinAnchor(anchor);
		BeginJellyRoll(enemyDice[idx]);
		enemyDice[idx].BeginPhysicalRoll(UnityEngine.Random.rotationUniform);

		while (!completedMask[idx] && Time.realtimeSinceStartup < timeoutAt)
		{
			bool valid = false;
			int face = 0;
			yield return enemyDice[idx].WaitForValidSettle((ok, value) =>
			{
				valid = ok;
				face = value;
			});

			if (completedMask[idx] || Time.realtimeSinceStartup >= timeoutAt)
				yield break;

			if (valid)
			{
				SnapJellyToRest(enemyDice[idx]);
				enemyDice[idx].FinalizePhysicalRoll(face);
				values[idx] = face;
				completedMask[idx] = true;
				onCompleted?.Invoke();
				yield break;
			}

			Debug.Log($"[EnemyDice] invalid settle/nudge idx={idx}");
			enemyDice[idx].NudgeInvalidSettle(anchor);
			yield return null;
		}
	}

	Vector3[] ComputeFallbackSpawnPositions(int count)
	{
		count = Mathf.Clamp(count, 1, enemyDice != null && enemyDice.Length > 0
			? enemyDice.Length
			: EnemyDiceProfile.MaxEnemyDiceCount);
		float spacing = Mathf.Max(diceSpacing,
			EnemyDiceProfile.ComputeSafeDiceSpacing(
				EnemyDiceProfile.ComputeDiceWorldSize(EnemyDiceProfile.DefaultDiceScale)));
		float startX = -((count - 1) * spacing * 0.5f);
		var positions = new Vector3[count];
		for (int i = 0; i < count; i++)
			positions[i] = new Vector3(vaultCenter.x + startX + i * spacing, vaultCenter.y, vaultCenter.z);
		return positions;
	}

	Vector3[] CaptureActiveAnchors(int count)
	{
		var anchors = new Vector3[count];
		for (int i = 0; i < count; i++)
			anchors[i] = enemyDice[i] != null ? enemyDice[i].transform.position : vaultCenter;
		return anchors;
	}

	void CompleteTimedOutDie(int idx, int count, EnemyDiceProfile profile, string profileId,
		Vector3 anchor, int[] values, bool[] completedMask)
	{
		int face = ComputeDeterministicFallbackFace(
			profile != null ? profile.id : profileId, idx, count);
		var die = idx >= 0 && idx < enemyDice.Length ? enemyDice[idx] : null;
		if (die != null)
		{
			SnapJellyToRest(die);
			die.ForcePhysicalFallback(face, anchor);
		}

		values[idx] = face;
		completedMask[idx] = true;
		Debug.LogWarning(
			$"[EnemyDice] settle timeout idx={idx} profile={profileId} fallback={face} timeout={(profile != null ? profile.settleTimeoutSeconds : EnemyDiceProfile.DefaultSettleTimeoutSeconds):0.##}s");
	}

	public static int ComputeDeterministicFallbackFace(string profileId, int dieIndex, int diceCount)
	{
		unchecked
		{
			int hash = 17;
			if (!string.IsNullOrEmpty(profileId))
			{
				for (int i = 0; i < profileId.Length; i++)
					hash = hash * 31 + profileId[i];
			}
			hash = hash * 31 + dieIndex;
			hash = hash * 31 + diceCount;
			return Mathf.Abs(hash % 6) + 1;
		}
	}

	static int[] PadToFive(int[] values)
	{
		if (values.Length >= 5) return values;
		int[] padded = new int[5];
		for (int i = 0; i < values.Length; i++) padded[i] = values[i];
		return padded;
	}

	static void BeginJellyRoll(Dice die)
	{
		var deformer = die != null ? die.GetComponent<SlimeDiceJellyDeformer>() : null;
		if (deformer != null)
			deformer.BeginRoll();
	}

	static void SnapJellyToRest(Dice die)
	{
		var deformer = die != null ? die.GetComponent<SlimeDiceJellyDeformer>() : null;
		if (deformer != null)
			deformer.SnapToRest();
	}
}

public sealed class SlimeDiceJellyDeformer : MonoBehaviour
{
	const string RenderChildName = "SlimeDiceJellyRender";
	const int MeshSubdivisions = 10;

	static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
	static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
	static readonly int[] DentCenterIds =
	{
		Shader.PropertyToID("_DentCenter0"),
		Shader.PropertyToID("_DentCenter1"),
		Shader.PropertyToID("_DentCenter2"),
		Shader.PropertyToID("_DentCenter3"),
	};
	static readonly int[] DentNormalRadiusIds =
	{
		Shader.PropertyToID("_DentNormalRadius0"),
		Shader.PropertyToID("_DentNormalRadius1"),
		Shader.PropertyToID("_DentNormalRadius2"),
		Shader.PropertyToID("_DentNormalRadius3"),
	};
	static readonly int[] DentShearIds =
	{
		Shader.PropertyToID("_DentShear0"),
		Shader.PropertyToID("_DentShear1"),
		Shader.PropertyToID("_DentShear2"),
		Shader.PropertyToID("_DentShear3"),
	};
	static readonly int[] DentWobbleIds =
	{
		Shader.PropertyToID("_DentWobble0"),
		Shader.PropertyToID("_DentWobble1"),
		Shader.PropertyToID("_DentWobble2"),
		Shader.PropertyToID("_DentWobble3"),
	};

	readonly SlimeDiceJellySolver solver = new SlimeDiceJellySolver();
	MaterialPropertyBlock propertyBlock;
	MeshRenderer jellyRenderer;
	MeshFilter jellyFilter;
	MeshRenderer sourceRenderer;
	MeshFilter sourceFilter;
	Texture baseTexture;
	Color baseTextureColor = Color.white;
	EnemyDiceProfile profile;
	bool sourceRendererHidden;
	bool sourceRendererWasEnabled = true;
	bool updateActive;
	bool warnedUnsupportedMesh;

	public SlimeDiceJellySolver Solver => solver;

	public void Configure(EnemyDiceProfile profile, Material jellyMaterial)
	{
		this.profile = profile;
		if (profile == null || !profile.jellyEnabled || jellyMaterial == null)
		{
			DisableJelly();
			return;
		}

		solver.Configure(
			profile.jellyCompressionMax,
			profile.jellyShearMax,
			profile.jellyImpulseScale,
			profile.jellyStiffness,
			profile.jellyDamping,
			profile.jellySettleEpsilon,
			profile.jellyDentRadius,
			profile.jellyBulgeScale,
			profile.jellyWobbleMax);

		if (!TryEnsureRenderer(jellyMaterial))
		{
			DisableJelly();
			return;
		}

		BeginRoll();
	}

	public void BeginRoll()
	{
		updateActive = false;
		solver.Reset();
		ApplyPropertyBlock();
	}

	public void SnapToRest()
	{
		updateActive = false;
		solver.Reset();
		ApplyPropertyBlock();
	}

	public void DisableJelly()
	{
		profile = null;
		updateActive = false;
		solver.Reset();
		ApplyPropertyBlock();
		if (jellyRenderer != null)
			jellyRenderer.enabled = false;
		RestoreSourceRenderer();
	}

	bool TryEnsureRenderer(Material jellyMaterial)
	{
		if (propertyBlock == null)
			propertyBlock = new MaterialPropertyBlock();

		if (!TryResolveSourceRenderer(out var resolvedRenderer, out var resolvedFilter))
		{
			Debug.LogWarning($"[SlimeDiceJelly] {name} 원본 MeshRenderer/MeshFilter를 찾을 수 없어 젤리 렌더를 비활성화합니다.");
			return false;
		}

		if (!SlimeDiceJellyRenderMeshBuilder.TryBuildSubdividedRenderMesh(
			resolvedFilter.sharedMesh, MeshSubdivisions, out var renderMesh))
		{
			if (!warnedUnsupportedMesh)
			{
				Debug.LogWarning($"[SlimeDiceJelly] {name} 원본 mesh가 6면 quad 구조가 아니어서 젤리 렌더를 비활성화합니다.");
				warnedUnsupportedMesh = true;
			}
			return false;
		}

		if (sourceRenderer != resolvedRenderer)
		{
			RestoreSourceRenderer();
			sourceRenderer = resolvedRenderer;
			sourceFilter = resolvedFilter;
		}

		EnsureJellyChild(sourceFilter.transform);

		baseTexture = ResolveMainTexture(sourceRenderer);
		baseTextureColor = ResolveMaterialColor(sourceRenderer);
		jellyFilter.sharedMesh = renderMesh;
		jellyRenderer.sharedMaterial = jellyMaterial;
		jellyRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
		jellyRenderer.receiveShadows = false;
		jellyRenderer.enabled = true;
		HideSourceRenderer();
		return true;
	}

	bool TryResolveSourceRenderer(out MeshRenderer resolvedRenderer, out MeshFilter resolvedFilter)
	{
		foreach (var renderer in GetComponentsInChildren<MeshRenderer>(true))
		{
			resolvedRenderer = renderer;
			resolvedFilter = renderer != null ? renderer.GetComponent<MeshFilter>() : null;
			if (resolvedRenderer == null || resolvedFilter == null || resolvedFilter.sharedMesh == null)
				continue;
			if (resolvedRenderer.GetComponent<SlimeDiceJellyRenderProxy>() != null)
				continue;
			return true;
		}

		resolvedRenderer = null;
		resolvedFilter = null;
		return false;
	}

	void EnsureJellyChild(Transform parent)
	{
		if (jellyRenderer == null || jellyFilter == null)
		{
			var renderTransform = transform.Find(RenderChildName);
			GameObject renderGo = renderTransform != null
				? renderTransform.gameObject
				: new GameObject(RenderChildName);

			jellyFilter = renderGo.GetComponent<MeshFilter>();
			if (jellyFilter == null)
				jellyFilter = renderGo.AddComponent<MeshFilter>();
			jellyRenderer = renderGo.GetComponent<MeshRenderer>();
			if (jellyRenderer == null)
				jellyRenderer = renderGo.AddComponent<MeshRenderer>();
			if (renderGo.GetComponent<SlimeDiceJellyRenderProxy>() == null)
				renderGo.AddComponent<SlimeDiceJellyRenderProxy>();
		}

		jellyRenderer.gameObject.transform.SetParent(parent, false);
		jellyRenderer.gameObject.transform.localPosition = Vector3.zero;
		jellyRenderer.gameObject.transform.localRotation = Quaternion.identity;
		jellyRenderer.gameObject.transform.localScale = Vector3.one;
		jellyRenderer.gameObject.layer = parent.gameObject.layer;
	}

	void HideSourceRenderer()
	{
		if (sourceRenderer == null)
			return;

		if (!sourceRendererHidden)
		{
			sourceRendererWasEnabled = sourceRenderer.enabled;
			sourceRendererHidden = true;
		}
		sourceRenderer.enabled = false;
	}

	void RestoreSourceRenderer()
	{
		if (sourceRenderer != null && sourceRendererHidden)
			sourceRenderer.enabled = sourceRendererWasEnabled;

		sourceRendererHidden = false;
	}

	void OnCollisionEnter(Collision collision)
	{
		ApplyCollision(collision);
	}

	void OnCollisionStay(Collision collision)
	{
		ApplyCollision(collision);
	}

	void ApplyCollision(Collision collision)
	{
		if (profile == null || !profile.jellyEnabled || jellyRenderer == null || !jellyRenderer.enabled)
			return;
		if (collision == null || collision.contactCount <= 0)
			return;

		var contact = collision.GetContact(0);
		Vector3 localPoint = transform.InverseTransformPoint(contact.point);
		Vector3 localNormal = transform.InverseTransformDirection(contact.normal);
		Vector3 localVelocity = transform.InverseTransformDirection(collision.relativeVelocity);
		solver.ApplyImpulse(localPoint, localNormal, localVelocity, collision.impulse.magnitude);
		updateActive = true;
		ApplyPropertyBlock();
	}

	void FixedUpdate()
	{
		if (!updateActive || profile == null || jellyRenderer == null || !jellyRenderer.enabled)
			return;

		solver.Step(Time.fixedDeltaTime);
		ApplyPropertyBlock();
		if (solver.IsSettled)
			updateActive = false;
	}

	void OnDisable()
	{
		updateActive = false;
		solver.Reset();
		ApplyPropertyBlock();
	}

	void ApplyPropertyBlock()
	{
		if (jellyRenderer == null)
			return;
		if (propertyBlock == null)
			propertyBlock = new MaterialPropertyBlock();

		propertyBlock.Clear();
		propertyBlock.SetColor(BaseColorId, ResolveShaderBaseColor());
		if (baseTexture != null)
			propertyBlock.SetTexture(BaseMapId, baseTexture);

		for (int i = 0; i < SlimeDiceJellySolver.MaxDentSlots; i++)
		{
			var dent = solver.GetDent(i);
			propertyBlock.SetVector(DentCenterIds[i],
				new Vector4(dent.localPoint.x, dent.localPoint.y, dent.localPoint.z, dent.depth));
			propertyBlock.SetVector(DentNormalRadiusIds[i],
				new Vector4(dent.outwardNormal.x, dent.outwardNormal.y, dent.outwardNormal.z, dent.radius));
			propertyBlock.SetVector(DentShearIds[i],
				new Vector4(dent.shear.x, dent.shear.y, dent.shear.z, 0f));
			propertyBlock.SetVector(DentWobbleIds[i],
				new Vector4(dent.wobble, dent.bulgeScale, 0f, 0f));
		}

		jellyRenderer.SetPropertyBlock(propertyBlock);
	}

	Color ResolveShaderBaseColor()
	{
		if (profile == null)
			return Color.clear;
		if (baseTexture == null)
			return profile.visualBaseColor;

		var color = baseTextureColor;
		color.a = Mathf.Clamp01(profile.visualBaseColor.a);
		return color;
	}

	static Texture ResolveMainTexture(Renderer renderer)
	{
		if (renderer == null)
			return null;

		var materials = renderer.sharedMaterials;
		if (materials == null)
			return null;

		for (int i = 0; i < materials.Length; i++)
		{
			var texture = ResolveMainTexture(materials[i]);
			if (texture != null)
				return texture;
		}
		return null;
	}

	static Texture ResolveMainTexture(Material material)
	{
		if (material == null)
			return null;
		if (material.HasProperty("_BaseMap"))
			return material.GetTexture("_BaseMap");
		if (material.HasProperty("_MainTex"))
			return material.GetTexture("_MainTex");
		return null;
	}

	static Color ResolveMaterialColor(Renderer renderer)
	{
		if (renderer == null)
			return Color.white;

		var materials = renderer.sharedMaterials;
		if (materials == null)
			return Color.white;

		for (int i = 0; i < materials.Length; i++)
		{
			var material = materials[i];
			if (material == null)
				continue;
			if (material.HasProperty("_BaseColor"))
				return material.GetColor("_BaseColor");
			if (material.HasProperty("_Color"))
				return material.GetColor("_Color");
		}
		return Color.white;
	}
}

public struct SlimeDiceJellyDentSnapshot
{
	public Vector3 localPoint;
	public Vector3 outwardNormal;
	public float depth;
	public float velocity;
	public Vector3 shear;
	public float wobble;
	public float radius;
	public float bulgeScale;
}

public sealed class SlimeDiceJellySolver
{
	public const int MaxDentSlots = 4;

	struct DentState
	{
		public Vector3 localPoint;
		public Vector3 outwardNormal;
		public float depth;
		public float velocity;
		public Vector3 shear;
		public Vector3 shearVelocity;
		public float wobble;
		public float wobbleVelocity;
	}

	readonly DentState[] dents = new DentState[MaxDentSlots];
	float compressionMax = 0.14f;
	float shearMax = 0.08f;
	float impulseScale = 0.012f;
	float stiffness = 42f;
	float damping = 12f;
	float settleEpsilon = 0.0015f;
	float dentRadius = 0.42f;
	float bulgeScale = 0.36f;
	float wobbleMax = 0.08f;

	public Vector3 ContactNormal => GetDominantDent().outwardNormal;
	public float Compression => GetDominantDent().depth;
	public Vector3 Shear => GetDominantDent().shear;
	public float Wobble => GetDominantDent().wobble;
	public bool IsSettled
	{
		get
		{
			for (int i = 0; i < dents.Length; i++)
				if (IsDentActive(dents[i]))
					return false;
			return true;
		}
	}

	public void Configure(float compressionMax, float shearMax, float impulseScale,
		float stiffness, float damping, float settleEpsilon,
		float dentRadius, float bulgeScale, float wobbleMax)
	{
		this.compressionMax = Mathf.Max(0f, compressionMax);
		this.shearMax = Mathf.Max(0f, shearMax);
		this.impulseScale = Mathf.Max(0f, impulseScale);
		this.stiffness = Mathf.Max(0.01f, stiffness);
		this.damping = Mathf.Max(0f, damping);
		this.settleEpsilon = Mathf.Max(0.00001f, settleEpsilon);
		this.dentRadius = Mathf.Max(0.01f, dentRadius);
		this.bulgeScale = Mathf.Max(0f, bulgeScale);
		this.wobbleMax = Mathf.Max(0f, wobbleMax);
	}

	public void Configure(float compressionMax, float shearMax, float impulseScale,
		float stiffness, float damping, float settleEpsilon)
	{
		Configure(compressionMax, shearMax, impulseScale, stiffness, damping, settleEpsilon,
			dentRadius, bulgeScale, wobbleMax);
	}

	public void Reset()
	{
		for (int i = 0; i < dents.Length; i++)
			ResetDent(i);
	}

	public void ApplyImpulse(Vector3 localPoint, Vector3 localNormal,
		Vector3 relativeVelocity, float impulseMagnitude)
	{
		Vector3 normal = CorrectOutwardNormal(localPoint, localNormal);
		if (normal.sqrMagnitude <= 0.000001f)
			return;

		int index = SelectDentSlot(localPoint);
		var dent = dents[index];
		if (!IsDentActive(dent))
		{
			dent.localPoint = localPoint;
			dent.outwardNormal = normal;
		}
		else
		{
			dent.localPoint = Vector3.Lerp(dent.localPoint, localPoint, 0.35f);
			dent.outwardNormal = (dent.outwardNormal + normal).normalized;
		}

		float normalSpeed = Mathf.Abs(Vector3.Dot(relativeVelocity, normal));
		float intensity = Mathf.Max(0f, impulseMagnitude) + normalSpeed * 0.10f;
		float depthAdd = Mathf.Clamp(intensity * impulseScale, 0f, compressionMax);
		dent.depth = Mathf.Clamp(dent.depth + depthAdd, 0f, compressionMax);
		dent.velocity += depthAdd * stiffness * 0.30f;

		Vector3 tangentVelocity = relativeVelocity - Vector3.Project(relativeVelocity, normal);
		Vector3 shearAdd = Vector3.ClampMagnitude(-tangentVelocity * impulseScale * 0.18f, shearMax);
		dent.shear = Vector3.ClampMagnitude(dent.shear + shearAdd, shearMax);
		dent.shearVelocity += shearAdd * stiffness * 0.24f;

		float wobbleAdd = Mathf.Clamp(depthAdd + shearAdd.magnitude * 0.65f, 0f, wobbleMax);
		dent.wobble = Mathf.Clamp(dent.wobble + wobbleAdd, -wobbleMax, wobbleMax);
		dent.wobbleVelocity += wobbleAdd * stiffness * 0.26f;
		dents[index] = dent;
	}

	public void ApplyImpulse(Vector3 localNormal, Vector3 relativeVelocity, float impulseMagnitude)
	{
		Vector3 normal = localNormal.sqrMagnitude > 0.000001f ? localNormal.normalized : Vector3.up;
		ApplyImpulse(normal * 0.5f, normal, relativeVelocity, impulseMagnitude);
	}

	public void Step(float deltaTime)
	{
		if (deltaTime <= 0f)
			return;

		for (int i = 0; i < dents.Length; i++)
		{
			var dent = dents[i];
			StepPositiveScalar(ref dent.depth, ref dent.velocity, compressionMax, deltaTime);
			StepVector(ref dent.shear, ref dent.shearVelocity, shearMax, deltaTime);
			StepSignedScalar(ref dent.wobble, ref dent.wobbleVelocity, wobbleMax, deltaTime);
			dents[i] = dent;
		}
	}

	public SlimeDiceJellyDentSnapshot GetDent(int index)
	{
		if (index < 0 || index >= dents.Length)
			return CreateRestSnapshot();

		var dent = dents[index];
		return new SlimeDiceJellyDentSnapshot
		{
			localPoint = dent.localPoint,
			outwardNormal = dent.outwardNormal.sqrMagnitude > 0.000001f ? dent.outwardNormal : Vector3.up,
			depth = dent.depth,
			velocity = dent.velocity,
			shear = dent.shear,
			wobble = dent.wobble,
			radius = IsDentActive(dent) ? dentRadius : 0f,
			bulgeScale = bulgeScale
		};
	}

	public static Vector3 CorrectOutwardNormal(Vector3 localPoint, Vector3 localNormal)
	{
		Vector3 normal = localNormal.sqrMagnitude > 0.000001f
			? localNormal.normalized
			: Vector3.zero;

		if (normal == Vector3.zero && localPoint.sqrMagnitude > 0.000001f)
			normal = localPoint.normalized;
		if (normal == Vector3.zero)
			return Vector3.zero;

		if (localPoint.sqrMagnitude > 0.000001f && Vector3.Dot(localPoint, normal) < 0f)
			normal = -normal;
		return normal;
	}

	int SelectDentSlot(Vector3 localPoint)
	{
		int emptyIndex = -1;
		int weakestIndex = 0;
		float weakestDepth = float.PositiveInfinity;
		int closestIndex = -1;
		float closestSqrDistance = dentRadius * dentRadius;

		for (int i = 0; i < dents.Length; i++)
		{
			var dent = dents[i];
			if (!IsDentActive(dent))
			{
				if (emptyIndex < 0)
					emptyIndex = i;
				continue;
			}

			float sqrDistance = (dent.localPoint - localPoint).sqrMagnitude;
			if (sqrDistance < closestSqrDistance)
			{
				closestSqrDistance = sqrDistance;
				closestIndex = i;
			}

			if (dent.depth < weakestDepth)
			{
				weakestDepth = dent.depth;
				weakestIndex = i;
			}
		}

		if (closestIndex >= 0)
			return closestIndex;
		if (emptyIndex >= 0)
			return emptyIndex;
		return weakestIndex;
	}

	DentState GetDominantDent()
	{
		int bestIndex = 0;
		float bestWeight = -1f;
		for (int i = 0; i < dents.Length; i++)
		{
			var dent = dents[i];
			float weight = dent.depth + dent.shear.magnitude + Mathf.Abs(dent.wobble);
			if (weight <= bestWeight)
				continue;

			bestWeight = weight;
			bestIndex = i;
		}

		var best = dents[bestIndex];
		if (best.outwardNormal.sqrMagnitude <= 0.000001f)
			best.outwardNormal = Vector3.up;
		return best;
	}

	bool IsDentActive(DentState dent)
	{
		return dent.depth > settleEpsilon ||
			Mathf.Abs(dent.velocity) > settleEpsilon ||
			dent.shear.magnitude > settleEpsilon ||
			dent.shearVelocity.magnitude > settleEpsilon ||
			Mathf.Abs(dent.wobble) > settleEpsilon ||
			Mathf.Abs(dent.wobbleVelocity) > settleEpsilon;
	}

	void StepPositiveScalar(ref float value, ref float velocity, float maxValue, float deltaTime)
	{
		velocity += (-value * stiffness - velocity * damping) * deltaTime;
		value = Mathf.Clamp(value + velocity * deltaTime, 0f, maxValue);
		if ((value <= 0f && velocity < 0f) || (value >= maxValue && velocity > 0f))
			velocity = 0f;

		if (value <= settleEpsilon && Mathf.Abs(velocity) <= settleEpsilon)
		{
			value = 0f;
			velocity = 0f;
		}
	}

	void StepSignedScalar(ref float value, ref float velocity, float maxValue, float deltaTime)
	{
		velocity += (-value * stiffness - velocity * damping) * deltaTime;
		value = Mathf.Clamp(value + velocity * deltaTime, -maxValue, maxValue);
		if (Mathf.Abs(value) <= settleEpsilon && Mathf.Abs(velocity) <= settleEpsilon)
		{
			value = 0f;
			velocity = 0f;
		}
	}

	void StepVector(ref Vector3 value, ref Vector3 velocity, float maxMagnitude, float deltaTime)
	{
		velocity += (-value * stiffness - velocity * damping) * deltaTime;
		value = Vector3.ClampMagnitude(value + velocity * deltaTime, maxMagnitude);
		if (value.magnitude <= settleEpsilon && velocity.magnitude <= settleEpsilon)
		{
			value = Vector3.zero;
			velocity = Vector3.zero;
		}
	}

	void ResetDent(int index)
	{
		dents[index] = new DentState
		{
			outwardNormal = Vector3.up
		};
	}

	SlimeDiceJellyDentSnapshot CreateRestSnapshot()
	{
		return new SlimeDiceJellyDentSnapshot
		{
			outwardNormal = Vector3.up,
			radius = 0f,
			bulgeScale = bulgeScale
		};
	}
}

public static class SlimeDiceJellyRenderMeshBuilder
{
	const int CubeFaceCount = 6;
	const int QuadTriangleIndexCount = 6;
	static readonly Dictionary<int, Mesh> MeshCache = new Dictionary<int, Mesh>();

	struct BoundaryEdge
	{
		public int a;
		public int b;

		public BoundaryEdge(int a, int b)
		{
			this.a = a;
			this.b = b;
		}
	}

	struct QuadFace
	{
		public Vector3 p0;
		public Vector3 p1;
		public Vector3 p2;
		public Vector3 p3;
		public Vector2 uv0;
		public Vector2 uv1;
		public Vector2 uv2;
		public Vector2 uv3;
		public Vector3 normal;
	}

	public static bool TryBuildSubdividedRenderMesh(Mesh sourceMesh, int subdivisions, out Mesh mesh)
	{
		mesh = null;
		if (sourceMesh == null)
			return false;

		int cells = Mathf.Max(1, subdivisions);
		int cacheKey = sourceMesh.GetInstanceID() * 397 ^ cells;
		if (MeshCache.TryGetValue(cacheKey, out mesh) && mesh != null)
			return true;

		var sourceVertices = sourceMesh.vertices;
		var sourceUvs = sourceMesh.uv;
		var sourceNormals = sourceMesh.normals;
		var sourceTriangles = sourceMesh.triangles;
		if (sourceVertices == null || sourceUvs == null || sourceTriangles == null)
			return false;
		if (sourceUvs.Length < sourceVertices.Length || sourceTriangles.Length != CubeFaceCount * QuadTriangleIndexCount)
			return false;

		var vertices = new List<Vector3>(CubeFaceCount * (cells + 1) * (cells + 1));
		var uvs = new List<Vector2>(vertices.Capacity);
		var normals = new List<Vector3>(vertices.Capacity);
		var triangles = new List<int>(CubeFaceCount * cells * cells * 6);

		for (int face = 0; face < CubeFaceCount; face++)
		{
			if (!TryReadQuadFace(sourceVertices, sourceUvs, sourceNormals, sourceTriangles,
				face * QuadTriangleIndexCount, out var quad))
			{
				return false;
			}

			AddSubdividedQuad(quad, cells, vertices, uvs, normals, triangles);
		}

		mesh = new Mesh
		{
			name = $"{sourceMesh.name}_SlimeJellyRender",
			hideFlags = HideFlags.DontSave
		};
		mesh.SetVertices(vertices);
		mesh.SetUVs(0, uvs);
		mesh.SetNormals(normals);
		mesh.SetTriangles(triangles, 0);
		mesh.RecalculateBounds();
		MeshCache[cacheKey] = mesh;
		return true;
	}

	static bool TryReadQuadFace(Vector3[] vertices, Vector2[] uvs, Vector3[] normals,
		int[] triangles, int triangleStart, out QuadFace quad)
	{
		quad = default;
		var faceIndices = new int[QuadTriangleIndexCount];
		for (int i = 0; i < QuadTriangleIndexCount; i++)
		{
			int index = triangles[triangleStart + i];
			if (index < 0 || index >= vertices.Length || index >= uvs.Length)
				return false;
			faceIndices[i] = index;
		}

		var unique = new List<int>(4);
		for (int i = 0; i < faceIndices.Length; i++)
		{
			if (!unique.Contains(faceIndices[i]))
				unique.Add(faceIndices[i]);
		}
		if (unique.Count != 4)
			return false;

		var boundaryEdges = BuildBoundaryEdges(faceIndices);
		if (boundaryEdges.Count != 4)
			return false;

		int[] loop = new int[4];
		if (!TryBuildBoundaryLoop(unique[0], boundaryEdges, loop))
			return false;

		Vector3 faceNormal = ResolveFaceNormal(vertices, normals, faceIndices, unique);
		Vector3 loopNormal = Vector3.Cross(vertices[loop[1]] - vertices[loop[0]], vertices[loop[2]] - vertices[loop[0]]);
		if (Vector3.Dot(loopNormal, faceNormal) < 0f)
		{
			int swap = loop[1];
			loop[1] = loop[3];
			loop[3] = swap;
		}

		quad = new QuadFace
		{
			p0 = vertices[loop[0]],
			p1 = vertices[loop[1]],
			p2 = vertices[loop[2]],
			p3 = vertices[loop[3]],
			uv0 = uvs[loop[0]],
			uv1 = uvs[loop[1]],
			uv2 = uvs[loop[2]],
			uv3 = uvs[loop[3]],
			normal = faceNormal
		};
		return true;
	}

	static List<BoundaryEdge> BuildBoundaryEdges(int[] faceIndices)
	{
		var candidates = new[]
		{
			new BoundaryEdge(faceIndices[0], faceIndices[1]),
			new BoundaryEdge(faceIndices[1], faceIndices[2]),
			new BoundaryEdge(faceIndices[2], faceIndices[0]),
			new BoundaryEdge(faceIndices[3], faceIndices[4]),
			new BoundaryEdge(faceIndices[4], faceIndices[5]),
			new BoundaryEdge(faceIndices[5], faceIndices[3]),
		};

		var boundaryEdges = new List<BoundaryEdge>(4);
		for (int i = 0; i < candidates.Length; i++)
		{
			int count = 0;
			for (int j = 0; j < candidates.Length; j++)
			{
				if (SameUndirectedEdge(candidates[i], candidates[j]))
					count++;
			}
			if (count == 1)
				boundaryEdges.Add(candidates[i]);
		}
		return boundaryEdges;
	}

	static bool TryBuildBoundaryLoop(int start, List<BoundaryEdge> boundaryEdges, int[] loop)
	{
		int[] neighbors = new int[2];
		if (GetBoundaryNeighbors(start, boundaryEdges, neighbors) != 2)
			return false;

		return TryBuildBoundaryLoop(start, neighbors[0], boundaryEdges, loop) ||
			TryBuildBoundaryLoop(start, neighbors[1], boundaryEdges, loop);
	}

	static bool TryBuildBoundaryLoop(int start, int next, List<BoundaryEdge> boundaryEdges, int[] loop)
	{
		loop[0] = start;
		loop[1] = next;
		for (int i = 2; i < 4; i++)
		{
			if (!TryGetNextBoundaryVertex(loop[i - 1], loop[i - 2], boundaryEdges, out loop[i]))
				return false;
			for (int j = 0; j < i; j++)
				if (loop[j] == loop[i])
					return false;
		}

		return TryGetNextBoundaryVertex(loop[3], loop[2], boundaryEdges, out int closing) && closing == loop[0];
	}

	static int GetBoundaryNeighbors(int vertex, List<BoundaryEdge> boundaryEdges, int[] neighbors)
	{
		int count = 0;
		for (int i = 0; i < boundaryEdges.Count; i++)
		{
			var edge = boundaryEdges[i];
			if (edge.a == vertex)
			{
				if (count < neighbors.Length)
					neighbors[count] = edge.b;
				count++;
			}
			else if (edge.b == vertex)
			{
				if (count < neighbors.Length)
					neighbors[count] = edge.a;
				count++;
			}
		}
		return count;
	}

	static bool TryGetNextBoundaryVertex(int current, int previous,
		List<BoundaryEdge> boundaryEdges, out int next)
	{
		for (int i = 0; i < boundaryEdges.Count; i++)
		{
			var edge = boundaryEdges[i];
			if (edge.a == current && edge.b != previous)
			{
				next = edge.b;
				return true;
			}
			if (edge.b == current && edge.a != previous)
			{
				next = edge.a;
				return true;
			}
		}

		next = -1;
		return false;
	}

	static Vector3 ResolveFaceNormal(Vector3[] vertices, Vector3[] normals, int[] faceIndices, List<int> unique)
	{
		Vector3 normal = Vector3.zero;
		for (int i = 0; i < faceIndices.Length; i += 3)
		{
			Vector3 a = vertices[faceIndices[i]];
			Vector3 b = vertices[faceIndices[i + 1]];
			Vector3 c = vertices[faceIndices[i + 2]];
			normal += Vector3.Cross(b - a, c - a);
		}

		if (normal.sqrMagnitude <= 0.000001f && normals != null && normals.Length >= vertices.Length)
		{
			for (int i = 0; i < unique.Count; i++)
				normal += normals[unique[i]];
		}

		return normal.sqrMagnitude > 0.000001f ? normal.normalized : Vector3.up;
	}

	static bool SameUndirectedEdge(BoundaryEdge a, BoundaryEdge b)
	{
		return (a.a == b.a && a.b == b.b) || (a.a == b.b && a.b == b.a);
	}

	static void AddSubdividedQuad(QuadFace quad, int cells,
		List<Vector3> vertices, List<Vector2> uvs, List<Vector3> normals, List<int> triangles)
	{
		int start = vertices.Count;
		for (int y = 0; y <= cells; y++)
		{
			float v = y / (float)cells;
			for (int x = 0; x <= cells; x++)
			{
				float u = x / (float)cells;
				vertices.Add(Bilinear(quad.p0, quad.p1, quad.p2, quad.p3, u, v));
				uvs.Add(Bilinear(quad.uv0, quad.uv1, quad.uv2, quad.uv3, u, v));
				normals.Add(quad.normal);
			}
		}

		for (int y = 0; y < cells; y++)
		for (int x = 0; x < cells; x++)
		{
			int i00 = start + y * (cells + 1) + x;
			int i10 = i00 + 1;
			int i01 = i00 + cells + 1;
			int i11 = i01 + 1;
			triangles.Add(i00);
			triangles.Add(i10);
			triangles.Add(i11);
			triangles.Add(i00);
			triangles.Add(i11);
			triangles.Add(i01);
		}
	}

	static Vector3 Bilinear(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float u, float v)
	{
		return Vector3.Lerp(Vector3.Lerp(p0, p1, u), Vector3.Lerp(p3, p2, u), v);
	}

	static Vector2 Bilinear(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float u, float v)
	{
		return Vector2.Lerp(Vector2.Lerp(p0, p1, u), Vector2.Lerp(p3, p2, u), v);
	}
}

sealed class SlimeDiceJellyRenderProxy : MonoBehaviour
{
}

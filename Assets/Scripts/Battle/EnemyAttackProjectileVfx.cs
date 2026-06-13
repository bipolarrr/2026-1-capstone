using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class EnemyAttackProjectileVfx : MonoBehaviour
{
	const float ChromaKeyDistance = 58f;
	const float ChromaKeySoftness = 28f;
	const float DraculaLaserChargeDuration = 0.08f;
	const float DraculaLaserBeamDuration = 0.32f;
	const float DraculaLaserFadeStartRatio = 0.68f;
	const float DraculaLaserGlowThickness = 28f;
	const float DraculaLaserCoreThickness = 8f;

	static readonly Dictionary<Sprite, Sprite> displaySpriteCache = new Dictionary<Sprite, Sprite>();

	[SerializeField] Transform vfxParent;
	[SerializeField] Image projectileImage;
	[SerializeField] Vector2 size = new Vector2(220f, 98f);
	[SerializeField] Vector2 startOffset = new Vector2(-26f, 10f);
	[SerializeField] float travelDistance = 176f;
	[SerializeField] float arcHeight = 8f;
	[SerializeField] float duration = 0.38f;
	[SerializeField] float fadeInRatio = 0.12f;
	[SerializeField] float fadeOutStartRatio = 0.62f;
	[SerializeField] bool flipX;

	Coroutine routine;
	Coroutine laserRoutine;
	Image laserGlowImage;
	Image laserCoreImage;
	Image laserChargeImage;

	public bool IsPlaying => routine != null;

	void OnDisable()
	{
		StopAndHide();
	}

	public Coroutine Play(Sprite sprite, RectTransform shooterBody)
	{
		if (!isActiveAndEnabled)
			return null;
		if (sprite == null)
		{
			Debug.LogWarning("[EnemyAttackProjectileVfx] 공격 VFX 스프라이트가 없습니다.");
			return null;
		}
		if (shooterBody == null)
		{
			Debug.LogWarning("[EnemyAttackProjectileVfx] shooterBody가 없어 공격 VFX를 생략합니다.");
			return null;
		}

		EnsureProjectileImage();
		if (projectileImage == null)
		{
			Debug.LogWarning("[EnemyAttackProjectileVfx] projectileImage를 만들 수 없어 공격 VFX를 생략합니다.");
			return null;
		}

		if (routine != null)
			StopCoroutine(routine);
		routine = StartCoroutine(PlayRoutine(sprite, shooterBody));
		return routine;
	}

	public Coroutine PlayDraculaLaser(RectTransform shooterBody, RectTransform targetBody, System.Action onImpact = null)
	{
		if (!isActiveAndEnabled)
			return null;
		if (shooterBody == null || targetBody == null)
		{
			Debug.LogWarning("[DraculaLaserAttackVfx] shooterBody 또는 targetBody가 없어 레이저 VFX를 생략합니다.");
			return null;
		}

		EnsureLaserImages();
		if (laserGlowImage == null || laserCoreImage == null || laserChargeImage == null)
		{
			Debug.LogWarning("[DraculaLaserAttackVfx] 레이저 Image를 만들 수 없어 VFX를 생략합니다.");
			return null;
		}

		if (laserRoutine != null)
			StopCoroutine(laserRoutine);
		laserRoutine = StartCoroutine(PlayDraculaLaserRoutine(shooterBody, targetBody, onImpact));
		return laserRoutine;
	}

	public void StopAndHide()
	{
		if (routine != null)
			StopCoroutine(routine);
		routine = null;
		if (laserRoutine != null)
			StopCoroutine(laserRoutine);
		laserRoutine = null;
		HideLaserImages();
		if (projectileImage == null)
			return;
		projectileImage.gameObject.SetActive(false);
		projectileImage.sprite = null;
	}

	void EnsureProjectileImage()
	{
		if (projectileImage != null)
			return;

		Transform parent = ResolveVfxParent();
		if (parent == null)
			return;

		var go = new GameObject("EnemyAttackProjectileVfxImage");
		var rt = go.AddComponent<RectTransform>();
		rt.SetParent(parent, false);
		rt.anchorMin = new Vector2(0.5f, 0.5f);
		rt.anchorMax = new Vector2(0.5f, 0.5f);
		rt.pivot = new Vector2(0.5f, 0.5f);
		rt.sizeDelta = size;

		projectileImage = go.AddComponent<Image>();
		projectileImage.preserveAspect = true;
		projectileImage.raycastTarget = false;
		projectileImage.useSpriteMesh = false;
		go.SetActive(false);
		rt.SetAsLastSibling();
	}

	Transform ResolveVfxParent()
	{
		if (vfxParent != null)
			return vfxParent;

		var parentCanvas = GetComponentInParent<Canvas>();
		if (parentCanvas != null)
			return parentCanvas.transform;

		var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
		return canvases != null && canvases.Length > 0 ? canvases[0].transform : null;
	}

	void EnsureLaserImages()
	{
		if (laserGlowImage != null && laserCoreImage != null && laserChargeImage != null)
			return;

		Transform parent = ResolveVfxParent();
		if (parent == null)
			return;

		if (laserGlowImage == null)
			laserGlowImage = CreateLaserImage(parent, "DraculaLaserGlow");
		if (laserCoreImage == null)
			laserCoreImage = CreateLaserImage(parent, "DraculaLaserCore");
		if (laserChargeImage == null)
			laserChargeImage = CreateLaserImage(parent, "DraculaLaserChargeFlash");
	}

	Image CreateLaserImage(Transform parent, string objectName)
	{
		var go = new GameObject(objectName);
		var rt = go.AddComponent<RectTransform>();
		rt.SetParent(parent, false);
		rt.anchorMin = new Vector2(0.5f, 0.5f);
		rt.anchorMax = new Vector2(0.5f, 0.5f);
		rt.pivot = new Vector2(0f, 0.5f);

		var image = go.AddComponent<Image>();
		image.raycastTarget = false;
		image.color = Color.clear;
		go.SetActive(false);
		return image;
	}

	IEnumerator PlayDraculaLaserRoutine(RectTransform shooterBody, RectTransform targetBody, System.Action onImpact)
	{
		laserGlowImage.gameObject.SetActive(true);
		laserCoreImage.gameObject.SetActive(true);
		laserChargeImage.gameObject.SetActive(true);
		laserGlowImage.rectTransform.SetAsLastSibling();
		laserCoreImage.rectTransform.SetAsLastSibling();
		laserChargeImage.rectTransform.SetAsLastSibling();

		SetLaserBeamAlpha(0f);
		bool impactApplied = false;
		float elapsed = 0f;
		while (elapsed < DraculaLaserChargeDuration)
		{
			elapsed += Time.deltaTime;
			UpdateLaserPose(shooterBody, targetBody);
			float t = Mathf.Clamp01(elapsed / DraculaLaserChargeDuration);
			UpdateChargeFlash(shooterBody, t);
			yield return null;
		}

		if (!impactApplied)
		{
			impactApplied = true;
			onImpact?.Invoke();
		}
		laserChargeImage.gameObject.SetActive(false);

		elapsed = 0f;
		while (elapsed < DraculaLaserBeamDuration)
		{
			elapsed += Time.deltaTime;
			UpdateLaserPose(shooterBody, targetBody);
			float t = Mathf.Clamp01(elapsed / DraculaLaserBeamDuration);
			float alpha = t > DraculaLaserFadeStartRatio
				? 1f - Mathf.InverseLerp(DraculaLaserFadeStartRatio, 1f, t)
				: 1f;
			float pulse = 1f + Mathf.Sin(t * Mathf.PI * 8f) * 0.08f;
			SetLaserBeamAlpha(alpha);
			laserGlowImage.rectTransform.localScale = new Vector3(1f, pulse, 1f);
			laserCoreImage.rectTransform.localScale = new Vector3(1f, 1f + (pulse - 1f) * 0.35f, 1f);
			yield return null;
		}

		HideLaserImages();
		laserRoutine = null;
	}

	void UpdateLaserPose(RectTransform shooterBody, RectTransform targetBody)
	{
		Vector3 start = RectPointWorld(shooterBody, 0.44f, 0.56f);
		Vector3 end = RectPointWorld(targetBody, 0.54f, 0.58f);
		ConfigureLaserBeam(laserGlowImage.rectTransform, start, end, DraculaLaserGlowThickness);
		ConfigureLaserBeam(laserCoreImage.rectTransform, start, end, DraculaLaserCoreThickness);
	}

	void UpdateChargeFlash(RectTransform shooterBody, float t)
	{
		Vector3 start = RectPointWorld(shooterBody, 0.44f, 0.56f);
		var rt = laserChargeImage.rectTransform;
		rt.position = start;
		rt.pivot = new Vector2(0.5f, 0.5f);
		rt.sizeDelta = new Vector2(58f, 58f);
		rt.localRotation = Quaternion.Euler(0f, 0f, 45f);
		float scale = Mathf.Lerp(0.35f, 1.2f, t);
		rt.localScale = new Vector3(scale, scale, 1f);
		float alpha = 1f - t;
		laserChargeImage.color = new Color(1f, 0.04f, 0.02f, 0.70f * alpha);
	}

	void ConfigureLaserBeam(RectTransform rt, Vector3 start, Vector3 end, float thickness)
	{
		Vector3 delta = end - start;
		float length = Mathf.Max(1f, delta.magnitude);
		float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
		rt.pivot = new Vector2(0f, 0.5f);
		rt.position = start;
		rt.sizeDelta = new Vector2(length, thickness);
		rt.localRotation = Quaternion.Euler(0f, 0f, angle);
	}

	void SetLaserBeamAlpha(float alpha)
	{
		if (laserGlowImage != null)
			laserGlowImage.color = new Color(1f, 0f, 0.03f, 0.32f * alpha);
		if (laserCoreImage != null)
			laserCoreImage.color = new Color(1f, 0.22f, 0.16f, 0.95f * alpha);
	}

	void HideLaserImages()
	{
		if (laserGlowImage != null)
		{
			laserGlowImage.gameObject.SetActive(false);
			laserGlowImage.color = Color.clear;
			laserGlowImage.rectTransform.localScale = Vector3.one;
		}
		if (laserCoreImage != null)
		{
			laserCoreImage.gameObject.SetActive(false);
			laserCoreImage.color = Color.clear;
			laserCoreImage.rectTransform.localScale = Vector3.one;
		}
		if (laserChargeImage != null)
		{
			laserChargeImage.gameObject.SetActive(false);
			laserChargeImage.color = Color.clear;
			laserChargeImage.rectTransform.localScale = Vector3.one;
			laserChargeImage.rectTransform.localRotation = Quaternion.identity;
		}
	}

	IEnumerator PlayRoutine(Sprite sourceSprite, RectTransform shooterBody)
	{
		Sprite displaySprite = ResolveDisplaySprite(sourceSprite);
		var rt = projectileImage.rectTransform;
		rt.anchorMin = new Vector2(0.5f, 0.5f);
		rt.anchorMax = new Vector2(0.5f, 0.5f);
		rt.pivot = new Vector2(0.5f, 0.5f);
		rt.sizeDelta = size;
		rt.localRotation = Quaternion.identity;

		projectileImage.sprite = displaySprite;
		projectileImage.color = Color.clear;
		projectileImage.preserveAspect = true;
		projectileImage.raycastTarget = false;
		projectileImage.gameObject.SetActive(true);
		rt.SetAsLastSibling();

		Vector3 start = RectPointWorld(shooterBody, 0.14f, 0.58f) + new Vector3(startOffset.x, startOffset.y, 0f);
		Vector3 end = start + Vector3.left * Mathf.Max(1f, travelDistance);

		float elapsed = 0f;
		while (elapsed < duration)
		{
			elapsed += Time.deltaTime;
			float t = Mathf.Clamp01(elapsed / Mathf.Max(0.001f, duration));
			float eased = t * t * (3f - 2f * t);
			Vector3 position = Vector3.Lerp(start, end, eased);
			position.y += Mathf.Sin(t * Mathf.PI) * arcHeight;
			rt.position = position;

			float alpha = ResolveAlpha(t);
			float scale = 0.84f + Mathf.Sin(t * Mathf.PI) * 0.18f;
			rt.localScale = new Vector3((flipX ? -1f : 1f) * scale, scale, 1f);
			projectileImage.color = new Color(1f, 1f, 1f, alpha);
			yield return null;
		}

		projectileImage.gameObject.SetActive(false);
		projectileImage.sprite = null;
		projectileImage.color = Color.white;
		rt.localScale = Vector3.one;
		routine = null;
	}

	float ResolveAlpha(float t)
	{
		if (t < fadeInRatio)
			return Mathf.InverseLerp(0f, Mathf.Max(0.001f, fadeInRatio), t);
		if (t > fadeOutStartRatio)
			return 1f - Mathf.InverseLerp(fadeOutStartRatio, 1f, t);
		return 1f;
	}

	static Vector3 RectPointWorld(RectTransform rt, float normalizedX, float normalizedY)
	{
		return EnemyVisualBoundsResolver.ResolveWorldPoint(rt, normalizedX, normalizedY,
			rt != null ? rt.position : Vector3.zero);
	}

	static Sprite ResolveDisplaySprite(Sprite source)
	{
		if (source == null)
			return null;
		if (displaySpriteCache.TryGetValue(source, out var cached) && cached != null)
			return cached;
		if (TryCreateChromaKeySprite(source, out var keyedSprite))
		{
			displaySpriteCache[source] = keyedSprite;
			return keyedSprite;
		}

		displaySpriteCache[source] = source;
		return source;
	}

	static bool TryCreateChromaKeySprite(Sprite source, out Sprite keyedSprite)
	{
		keyedSprite = null;
		if (source == null || source.texture == null)
			return false;

		Texture2D copiedTexture = null;
		try
		{
			copiedTexture = CopyTextureReadable(source.texture);
		}
		catch (System.Exception ex)
		{
			Debug.LogWarning($"[EnemyAttackProjectileVfx] 크로마키 텍스처 생성 실패: {ex.Message}");
			return false;
		}

		if (copiedTexture == null)
			return false;

		var pixels = copiedTexture.GetPixels32();
		if (pixels == null || pixels.Length == 0)
		{
			Object.Destroy(copiedTexture);
			return false;
		}

		Color32 keyColor = AverageCornerColor(pixels, copiedTexture.width, copiedTexture.height);
		if (!LooksLikeMagentaKey(keyColor))
		{
			Object.Destroy(copiedTexture);
			return false;
		}

		for (int i = 0; i < pixels.Length; i++)
		{
			float distance = ColorDistance(pixels[i], keyColor);
			if (distance <= ChromaKeyDistance)
			{
				pixels[i].a = 0;
			}
			else if (distance <= ChromaKeyDistance + ChromaKeySoftness)
			{
				float keep = Mathf.InverseLerp(ChromaKeyDistance, ChromaKeyDistance + ChromaKeySoftness, distance);
				pixels[i].a = (byte)Mathf.RoundToInt(pixels[i].a * keep);
			}
		}

		copiedTexture.SetPixels32(pixels);
		copiedTexture.Apply(false, false);
		copiedTexture.filterMode = FilterMode.Point;
		copiedTexture.wrapMode = TextureWrapMode.Clamp;

		Rect rect = source.rect;
		Vector2 pivot = rect.width > 0f && rect.height > 0f
			? new Vector2(source.pivot.x / rect.width, source.pivot.y / rect.height)
			: new Vector2(0.5f, 0.5f);
		try
		{
			keyedSprite = Sprite.Create(
				copiedTexture,
				rect,
				pivot,
				source.pixelsPerUnit,
				0,
				SpriteMeshType.FullRect,
				source.border);
		}
		catch (System.Exception ex)
		{
			Debug.LogWarning($"[EnemyAttackProjectileVfx] 크로마키 스프라이트 생성 실패: {ex.Message}");
			Object.Destroy(copiedTexture);
			keyedSprite = null;
			return false;
		}
		keyedSprite.name = source.name + "_ChromaKey";
		return true;
	}

	static Texture2D CopyTextureReadable(Texture sourceTexture)
	{
		var rt = RenderTexture.GetTemporary(
			sourceTexture.width,
			sourceTexture.height,
			0,
			RenderTextureFormat.ARGB32,
			RenderTextureReadWrite.Default);
		var previous = RenderTexture.active;
		try
		{
			Graphics.Blit(sourceTexture, rt);
			RenderTexture.active = rt;
			var copy = new Texture2D(sourceTexture.width, sourceTexture.height, TextureFormat.RGBA32, false);
			copy.ReadPixels(new Rect(0f, 0f, sourceTexture.width, sourceTexture.height), 0, 0);
			copy.Apply(false, false);
			return copy;
		}
		finally
		{
			RenderTexture.active = previous;
			RenderTexture.ReleaseTemporary(rt);
		}
	}

	static Color32 AverageCornerColor(Color32[] pixels, int width, int height)
	{
		int lastX = Mathf.Max(0, width - 1);
		int lastY = Mathf.Max(0, height - 1);
		Color32 a = pixels[0];
		Color32 b = pixels[lastX];
		Color32 c = pixels[lastY * width];
		Color32 d = pixels[lastY * width + lastX];
		return new Color32(
			(byte)((a.r + b.r + c.r + d.r) / 4),
			(byte)((a.g + b.g + c.g + d.g) / 4),
			(byte)((a.b + b.b + c.b + d.b) / 4),
			(byte)((a.a + b.a + c.a + d.a) / 4));
	}

	static bool LooksLikeMagentaKey(Color32 color)
	{
		return color.a > 240 && color.r > 180 && color.b > 180 && color.g < 80;
	}

	static float ColorDistance(Color32 a, Color32 b)
	{
		float dr = a.r - b.r;
		float dg = a.g - b.g;
		float db = a.b - b.b;
		return Mathf.Sqrt(dr * dr + dg * dg + db * db);
	}
}

public static class DraculaLaserAttackVfx
{
	// EnemyInfo에는 원본 asset path/id가 없으므로 Stage1 Dracula boss를 안정적으로 묶기 위해
	// 현재 보스 데이터의 이름, dice profile id, 그리고 기존 보스 스프라이트 경로를 한곳에서만 대조한다.
	public const string Stage1DraculaBossSpritePath = "Assets/Mobs/Boss_Dracula_example.png";

	public static bool ShouldPlayForCurrentAttack(
		bool isBossBattle,
		string currentStageId,
		EnemyInfo enemy,
		BossDef stageBoss,
		string enemyDiceProfileId)
	{
		if (!isBossBattle)
			return false;
		if (enemy == null || stageBoss == null)
			return false;
		if (currentStageId != Stage1Forest.Id)
			return false;
		if (enemy.name != stageBoss.name)
			return false;

		bool spriteMatches = PathsEqual(stageBoss.spritePath, Stage1DraculaBossSpritePath);
		bool profileMatches = enemyDiceProfileId == EnemyDiceProfile.DraculaId
			|| stageBoss.enemyDiceProfileId == EnemyDiceProfile.DraculaId;
		return spriteMatches && profileMatches;
	}

	static bool PathsEqual(string a, string b)
	{
		if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
			return false;
		return string.Equals(
			a.Replace('\\', '/').Trim(),
			b.Replace('\\', '/').Trim(),
			System.StringComparison.OrdinalIgnoreCase);
	}
}

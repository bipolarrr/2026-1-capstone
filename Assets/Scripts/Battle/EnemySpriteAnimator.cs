using System.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.UI;

public class EnemySpriteAnimator : MonoBehaviour
{
	[SerializeField] Image targetImage;
	[SerializeField] float idleFrameRate = 12f;
	[SerializeField] float actionFrameRate = 18f;
	[SerializeField] float deathFrameRate = 30f;
	[SerializeField] Sprite[] idleSprites;
	[SerializeField] Sprite[] attackSprites;
	[SerializeField] Sprite[] hitSprites;
	[SerializeField] Sprite[] deathSprites;
	[SerializeField] Vector2[] deathSpriteCenterOffsets;
	[SerializeField] AnimationClip deathAnimationClip;
	[SerializeField] bool attackPingPong;
	[SerializeField] bool hitPingPong;
	[SerializeField] float attackVisualScaleMultiplier = 1f;
	[SerializeField] Vector2 attackVisualOffset;
	[SerializeField] bool attackUseFullTextureFrames;

	Coroutine idleRoutine;
	Coroutine actionRoutine;
	Coroutine deathRoutine;
	Sprite[] sourceAttackSprites;
	Sprite[] generatedFullTextureAttackSprites;
	bool idlePaused;
	bool deathLocked;
	float configuredActionFrameRate;
	float configuredDeathFrameRate;
	int idleIndex;
	int idleDirection = 1;
	int currentFrameIndex;
	int currentFrameCount;
	Animator deathClipAnimator;
	SpriteRenderer deathClipSpriteProbe;
	PlayableGraph deathClipGraph;
	bool defaultUseSpriteMesh;
	bool hasDefaultUseSpriteMesh;
	Vector2 deathBaseAnchoredPosition;
	bool deathAlignmentActive;
	bool actionVisualOverrideActive;
	Vector3 actionVisualBaseScale;
	Vector2 actionVisualBaseAnchoredPosition;

	public bool HasHitSprites => hitSprites != null && hitSprites.Length > 0;
	public bool HasAttackSprites => attackSprites != null && attackSprites.Length > 0;
	public bool HasDeathSprites => deathSprites != null && deathSprites.Length > 0;
	public bool HasDeathAnimationClip => deathAnimationClip != null;
	public bool HasDeathAnimation => HasDeathSprites || HasDeathAnimationClip;
	public bool IsDeathAnimationPlaying => deathRoutine != null;
	public bool IsDeathLocked => deathLocked;
	public float ActionFrameRate => actionFrameRate;
	public int AttackFrameCount => attackSprites != null ? attackSprites.Length : 0;
	public float AttackDurationSeconds => AttackFrameCount > 0
		? AttackFrameCount / Mathf.Max(1f, actionFrameRate)
		: 0f;
	public bool IsActionPlaying => actionRoutine != null;
	public int CurrentFrameIndex => currentFrameIndex;
	public int CurrentFrameCount => currentFrameCount;
	public float CurrentFrameNormalized => currentFrameCount > 1
		? Mathf.Clamp01((float)currentFrameIndex / (currentFrameCount - 1))
		: 0f;
	public bool HasAttackVisualOverride => !Mathf.Approximately(AttackVisualScaleMultiplier, 1f)
		|| attackVisualOffset.sqrMagnitude > 0.0001f;
	public float AttackVisualScaleMultiplier => attackVisualScaleMultiplier > 0f
		? Mathf.Max(0.01f, attackVisualScaleMultiplier)
		: 1f;
	public Vector2 AttackVisualOffset => attackVisualOffset;
	public bool AttackUseFullTextureFrames => attackUseFullTextureFrames;

	void Awake()
	{
		if (targetImage == null)
			targetImage = GetComponent<Image>();
		CaptureDefaultSpriteMeshMode();
		configuredActionFrameRate = actionFrameRate;
		configuredDeathFrameRate = deathFrameRate;
	}

	void OnEnable()
	{
		if (!deathLocked)
			StartIdle();
	}

	void OnDisable()
	{
		StopAllAnimation(clearDeathLock: false);
	}

	void OnDestroy()
	{
		ReleaseGeneratedFullTextureAttackSprites();
	}

	public void Bind(EnemySpriteAnimationSet set, Sprite fallbackSprite)
	{
		StopAllAnimation();
		idleSprites = set != null ? set.idleSprites : null;
		sourceAttackSprites = set != null ? set.attackSprites : null;
		hitSprites = set != null ? set.hitSprites : null;
		deathSprites = set != null ? set.deathSprites : null;
		deathSpriteCenterOffsets = set != null ? set.deathSpriteCenterOffsets : null;
		deathAnimationClip = set != null ? set.deathAnimationClip : null;
		attackVisualScaleMultiplier = set != null ? set.attackVisualScaleMultiplier : 1f;
		attackVisualOffset = set != null ? set.attackVisualOffset : Vector2.zero;
		attackUseFullTextureFrames = set != null && set.attackUseFullTextureFrames;
		ApplyAttackSpriteCanvasMode();
		if (configuredActionFrameRate <= 0f)
			configuredActionFrameRate = actionFrameRate;
		actionFrameRate = set != null && set.attackFrameRate > 0f
			? set.attackFrameRate
			: configuredActionFrameRate;
		if (configuredDeathFrameRate <= 0f)
			configuredDeathFrameRate = deathFrameRate;
		float baseDeathFrameRate = set != null && set.deathFrameRate > 0f
			? set.deathFrameRate
			: configuredDeathFrameRate;
		deathFrameRate = baseDeathFrameRate * (set != null
			? DeathFrameRateMultiplier(set.deathFrameRateMultiplier)
			: 1f);
		attackPingPong = set != null && set.attackPingPong;
		hitPingPong = true;
		idleIndex = 0;
		idleDirection = 1;
		SetCurrentFrame(0, idleSprites != null && idleSprites.Length > 0 ? idleSprites.Length : 1);

		if (targetImage != null)
		{
			CaptureDefaultSpriteMeshMode();
			targetImage.useSpriteMesh = defaultUseSpriteMesh;
			Sprite first = FirstSprite(idleSprites);
			targetImage.sprite = first != null ? first : fallbackSprite;
		}

		StartIdle();
	}

	public void SetAttackVisualOverride(float scaleMultiplier, Vector2 offset, bool useFullTextureFrames = false)
	{
		if (actionVisualOverrideActive)
			RestoreActionVisualOverride();

		attackVisualScaleMultiplier = scaleMultiplier > 0f ? scaleMultiplier : 1f;
		attackVisualOffset = offset;
		attackUseFullTextureFrames = useFullTextureFrames;
		ApplyAttackSpriteCanvasMode();
	}

	public Coroutine PlayAttack()
	{
		return PlayOneShot(attackSprites, attackPingPong, useAttackVisualOverride: true);
	}

	public Coroutine PlayHit()
	{
		return PlayOneShot(hitSprites, hitPingPong, useAttackVisualOverride: false);
	}

	public bool TryPlayDebugSpriteKind(string spriteKind, Sprite fallbackSprite, out string message)
	{
		message = null;
		if (string.IsNullOrWhiteSpace(spriteKind))
		{
			message = "spriteKind가 비어 있습니다.";
			return false;
		}

		switch (NormalizeSpriteKind(spriteKind))
		{
			case "idle":
				StopOnFirstIdle(fallbackSprite);
				StartIdle();
				message = "mob idle 자동 루프 재개";
				return true;
			case "attack":
				return PlayAttack() != null
					? Succeed("mob attack 재생", out message)
					: Fail("Attack 스프라이트가 없습니다.", out message);
			case "hit":
				return PlayHit() != null
					? Succeed("mob hit 재생", out message)
					: Fail("Hit 스프라이트가 없습니다.", out message);
			case "death":
				return PlayDebugDeathAndHold(fallbackSprite) != null
					? Succeed("mob death 재생", out message)
					: Fail("Death 애니메이션이 없습니다.", out message);
			default:
				message = $"알 수 없는 mob spriteKind: {spriteKind}. 사용 가능: idle, attack, hit, death";
				return false;
		}
	}

	public void StopOnFirstIdle(Sprite fallbackSprite)
	{
		StopAllAnimation();
		idleIndex = 0;
		idleDirection = 1;
		if (targetImage == null)
			return;

		CaptureDefaultSpriteMeshMode();
		targetImage.useSpriteMesh = defaultUseSpriteMesh;
		Sprite first = idleSprites != null && idleSprites.Length > 0 ? idleSprites[0] : null;
		targetImage.sprite = first != null ? first : fallbackSprite;
		SetCurrentFrame(0, idleSprites != null && idleSprites.Length > 0 ? idleSprites.Length : 1);
	}

	public void ReturnToIdle(Sprite fallbackSprite)
	{
		StopOnFirstIdle(fallbackSprite);
		StartIdle();
	}

	public Coroutine PlayDeathAndHold(Sprite fallbackSprite, bool fallToGround = false,
		Vector2 fallTargetAnchorMin = default, Vector2 fallTargetAnchorMax = default)
	{
		if (deathLocked || deathRoutine != null)
			return deathRoutine;
		if (targetImage == null || !isActiveAndEnabled)
			return null;
		if (!HasDeathAnimation)
		{
			StopOnFirstIdle(fallbackSprite);
			return null;
		}

		StopIdleAndAction();
		idlePaused = true;
		deathLocked = true;
		CaptureDefaultSpriteMeshMode();
		targetImage.useSpriteMesh = false;
		var rt = targetImage.rectTransform;
		CaptureDeathAlignmentBase(rt);
		deathRoutine = HasDeathAnimationClip
			? StartCoroutine(PlayDeathClipAndHoldRoutine(
				fallToGround,
				rt.anchorMin,
				rt.anchorMax,
				fallTargetAnchorMin,
				fallTargetAnchorMax))
			: StartCoroutine(PlayDeathAndHoldRoutine(
				fallToGround,
				rt.anchorMin,
				rt.anchorMax,
				fallTargetAnchorMin,
				fallTargetAnchorMax));
		return deathRoutine;
	}

	public Coroutine PlayDebugDeathAndHold(Sprite fallbackSprite)
	{
		StopAllAnimation(clearDeathLock: true);
		return PlayDeathAndHold(fallbackSprite);
	}

	public Coroutine PlayDebugDeathAndHold(Sprite fallbackSprite, bool fallToGround,
		Vector2 fallTargetAnchorMin, Vector2 fallTargetAnchorMax)
	{
		StopAllAnimation(clearDeathLock: true);
		return PlayDeathAndHold(fallbackSprite, fallToGround, fallTargetAnchorMin, fallTargetAnchorMax);
	}

	void StartIdle()
	{
		if (!isActiveAndEnabled)
			return;
		if (deathLocked || deathRoutine != null)
			return;
		if (idleRoutine != null)
			StopCoroutine(idleRoutine);
		idlePaused = false;
		if (idleSprites != null && idleSprites.Length > 0 && targetImage != null)
			idleRoutine = StartCoroutine(IdleLoop());
		else
			idleRoutine = null;
	}

	Coroutine PlayOneShot(Sprite[] sprites, bool pingPong)
	{
		return PlayOneShot(sprites, pingPong, useAttackVisualOverride: false);
	}

	Coroutine PlayOneShot(Sprite[] sprites, bool pingPong, bool useAttackVisualOverride)
	{
		if (deathLocked || deathRoutine != null)
			return null;
		if (sprites == null || sprites.Length == 0 || targetImage == null || !isActiveAndEnabled)
			return null;
		if (actionRoutine != null)
			StopCoroutine(actionRoutine);
		RestoreActionVisualOverride();
		actionRoutine = StartCoroutine(PlayOneShotRoutine(sprites, pingPong, useAttackVisualOverride));
		return actionRoutine;
	}

	IEnumerator IdleLoop()
	{
		float delay = 1f / Mathf.Max(1f, idleFrameRate);
		while (true)
		{
			if (!idlePaused && idleSprites != null && idleSprites.Length > 0)
			{
				int frameIndex = idleIndex % idleSprites.Length;
				var sprite = idleSprites[frameIndex];
				if (sprite != null)
					targetImage.sprite = sprite;
				SetCurrentFrame(frameIndex, idleSprites.Length);
				AdvancePingPongIdleIndex();
			}
			yield return new WaitForSeconds(delay);
		}
	}

	void AdvancePingPongIdleIndex()
	{
		if (idleSprites == null || idleSprites.Length <= 1)
		{
			idleIndex = 0;
			idleDirection = 1;
			return;
		}

		idleIndex += idleDirection;
		if (idleIndex >= idleSprites.Length)
		{
			idleDirection = -1;
			idleIndex = idleSprites.Length - 2;
		}
		else if (idleIndex < 0)
		{
			idleDirection = 1;
			idleIndex = 1;
		}
	}

	IEnumerator PlayOneShotRoutine(Sprite[] sprites, bool pingPong, bool useAttackVisualOverride)
	{
		idlePaused = true;
		if (useAttackVisualOverride)
			ApplyAttackVisualOverride();
		float delay = 1f / Mathf.Max(1f, actionFrameRate);
		for (int i = 0; i < sprites.Length; i++)
		{
			if (sprites[i] != null)
				targetImage.sprite = sprites[i];
			SetCurrentFrame(i, sprites.Length);
			yield return new WaitForSeconds(delay);
		}
		if (pingPong)
		{
			for (int i = sprites.Length - 2; i >= 0; i--)
			{
				if (sprites[i] != null)
					targetImage.sprite = sprites[i];
				SetCurrentFrame(i, sprites.Length);
				yield return new WaitForSeconds(delay);
			}
		}
		actionRoutine = null;
		RestartIdleFromFirstFrame();
	}

	void RestartIdleFromFirstFrame()
	{
		RestoreActionVisualOverride();
		idleIndex = 0;
		idleDirection = 1;
		if (idleSprites != null && idleSprites.Length > 0 && targetImage != null)
		{
			CaptureDefaultSpriteMeshMode();
			targetImage.useSpriteMesh = defaultUseSpriteMesh;
			Sprite first = idleSprites[0];
			if (first != null)
				targetImage.sprite = first;
			SetCurrentFrame(0, idleSprites.Length);
			AdvancePingPongIdleIndex();
		}
		idlePaused = false;
	}

	IEnumerator PlayDeathAndHoldRoutine(bool fallToGround, Vector2 startAnchorMin, Vector2 startAnchorMax,
		Vector2 targetAnchorMin, Vector2 targetAnchorMax)
	{
		float delay = 1f / Mathf.Max(1f, deathFrameRate);
		for (int i = 0; i < deathSprites.Length; i++)
		{
			if (fallToGround)
				ApplyDeathFall(startAnchorMin, startAnchorMax, targetAnchorMin, targetAnchorMax,
					deathSprites.Length > 1 ? (float)i / (deathSprites.Length - 1) : 1f);
			if (deathSprites[i] != null)
				targetImage.sprite = deathSprites[i];
			ApplyDeathAlignmentOffset(i);
			SetCurrentFrame(i, deathSprites.Length);
			yield return new WaitForSeconds(delay);
		}

		if (fallToGround)
			ApplyDeathFall(startAnchorMin, startAnchorMax, targetAnchorMin, targetAnchorMax, 1f);

		Sprite last = LastSprite(deathSprites);
		if (last != null)
			targetImage.sprite = last;
		int lastFrame = Mathf.Max(0, deathSprites.Length - 1);
		ApplyDeathAlignmentOffset(lastFrame);
		SetCurrentFrame(lastFrame, deathSprites.Length);
		deathRoutine = null;
		idlePaused = true;
	}

	IEnumerator PlayDeathClipAndHoldRoutine(bool fallToGround, Vector2 startAnchorMin, Vector2 startAnchorMax,
		Vector2 targetAnchorMin, Vector2 targetAnchorMax)
	{
		if (!TryCreateDeathClipGraph(deathAnimationClip, out AnimationClipPlayable playable))
		{
			deathRoutine = null;
			idlePaused = true;
			yield break;
		}

		float duration = Mathf.Max(0.001f, deathAnimationClip.length);
		int frameCount = EstimateClipFrameCount(deathAnimationClip);
		float elapsed = 0f;
		while (elapsed < duration)
		{
			playable.SetTime(elapsed);
			if (deathClipGraph.IsValid())
				deathClipGraph.Evaluate(0f);
			ApplyDeathClipSprite();

			float t = Mathf.Clamp01(elapsed / duration);
			if (fallToGround)
				ApplyDeathFall(startAnchorMin, startAnchorMax, targetAnchorMin, targetAnchorMax, t);
			SetCurrentFrame(Mathf.RoundToInt(t * Mathf.Max(0, frameCount - 1)), frameCount);
			yield return null;
			elapsed += Time.deltaTime;
		}

		if (fallToGround)
			ApplyDeathFall(startAnchorMin, startAnchorMax, targetAnchorMin, targetAnchorMax, 1f);

		playable.SetTime(duration);
		playable.SetSpeed(0f);
		if (deathClipGraph.IsValid())
			deathClipGraph.Evaluate(0f);
		ApplyDeathClipSprite();

		SetCurrentFrame(Mathf.Max(0, frameCount - 1), frameCount);
		deathRoutine = null;
		idlePaused = true;
	}

	void SetCurrentFrame(int index, int count)
	{
		currentFrameCount = Mathf.Max(0, count);
		currentFrameIndex = currentFrameCount > 0 ? Mathf.Clamp(index, 0, currentFrameCount - 1) : 0;
	}

	void StopIdleAndAction()
	{
		if (idleRoutine != null)
			StopCoroutine(idleRoutine);
		if (actionRoutine != null)
			StopCoroutine(actionRoutine);
		idleRoutine = null;
		actionRoutine = null;
		RestoreActionVisualOverride();
	}

	void StopAllAnimation(bool clearDeathLock = true)
	{
		StopIdleAndAction();
		if (deathRoutine != null)
			StopCoroutine(deathRoutine);
		DestroyDeathClipGraph();
		deathRoutine = null;
		idlePaused = false;
		if (clearDeathLock)
		{
			deathLocked = false;
			RestoreDeathAlignmentBase();
			if (targetImage != null)
			{
				CaptureDefaultSpriteMeshMode();
				targetImage.useSpriteMesh = defaultUseSpriteMesh;
			}
		}
	}

	void CaptureDefaultSpriteMeshMode()
	{
		if (hasDefaultUseSpriteMesh || targetImage == null)
			return;

		defaultUseSpriteMesh = targetImage.useSpriteMesh;
		hasDefaultUseSpriteMesh = true;
	}

	void CaptureDeathAlignmentBase(RectTransform rt)
	{
		if (rt == null)
			return;

		deathBaseAnchoredPosition = rt.anchoredPosition;
		deathAlignmentActive = true;
	}

	void RestoreDeathAlignmentBase()
	{
		if (!deathAlignmentActive || targetImage == null)
			return;

		targetImage.rectTransform.anchoredPosition = deathBaseAnchoredPosition;
		deathAlignmentActive = false;
	}

	void ApplyAttackVisualOverride()
	{
		if (!HasAttackVisualOverride || targetImage == null)
			return;

		var rt = targetImage.rectTransform;
		if (!actionVisualOverrideActive)
		{
			actionVisualBaseScale = rt.localScale;
			actionVisualBaseAnchoredPosition = rt.anchoredPosition;
		}

		actionVisualOverrideActive = true;
		rt.localScale = actionVisualBaseScale * AttackVisualScaleMultiplier;
		rt.anchoredPosition = actionVisualBaseAnchoredPosition + attackVisualOffset;
	}

	void RestoreActionVisualOverride()
	{
		if (!actionVisualOverrideActive)
			return;

		if (targetImage != null)
		{
			var rt = targetImage.rectTransform;
			rt.localScale = actionVisualBaseScale;
			rt.anchoredPosition = actionVisualBaseAnchoredPosition;
		}
		actionVisualOverrideActive = false;
	}

	void ApplyDeathAlignmentOffset(int frameIndex)
	{
		if (!deathAlignmentActive || targetImage == null || targetImage.sprite == null)
			return;
		if (deathSpriteCenterOffsets == null || frameIndex < 0 || frameIndex >= deathSpriteCenterOffsets.Length)
			return;

		targetImage.rectTransform.anchoredPosition =
			deathBaseAnchoredPosition + SpritePixelOffsetToAnchoredOffset(
				targetImage,
				deathSpriteCenterOffsets[frameIndex]);
	}

	static Vector2 SpritePixelOffsetToAnchoredOffset(Image image, Vector2 spritePixelOffset)
	{
		if (image == null || image.sprite == null)
			return Vector2.zero;

		Rect rect = image.rectTransform.rect;
		Rect spriteRect = image.sprite.rect;
		if (rect.width <= 0f || rect.height <= 0f || spriteRect.width <= 0f || spriteRect.height <= 0f)
			return Vector2.zero;

		if (image.preserveAspect)
		{
			float uniformScale = Mathf.Min(rect.width / spriteRect.width, rect.height / spriteRect.height);
			return spritePixelOffset * uniformScale;
		}

		return new Vector2(
			spritePixelOffset.x * rect.width / spriteRect.width,
			spritePixelOffset.y * rect.height / spriteRect.height);
	}

	bool TryCreateDeathClipGraph(AnimationClip clip, out AnimationClipPlayable playable)
	{
		playable = default;
		DestroyDeathClipGraph();
		if (clip == null || targetImage == null)
			return false;

		deathClipAnimator = targetImage.GetComponent<Animator>();
		if (deathClipAnimator == null)
			deathClipAnimator = targetImage.gameObject.AddComponent<Animator>();
		deathClipAnimator.applyRootMotion = false;
		deathClipAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
		deathClipSpriteProbe = targetImage.GetComponent<SpriteRenderer>();
		if (deathClipSpriteProbe == null)
			deathClipSpriteProbe = targetImage.gameObject.AddComponent<SpriteRenderer>();
		deathClipSpriteProbe.enabled = false;
		deathClipSpriteProbe.sprite = null;
		deathClipAnimator.Rebind();
		deathClipAnimator.Update(0f);

		deathClipGraph = PlayableGraph.Create($"{name}_DeathClip");
		deathClipGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
		playable = AnimationClipPlayable.Create(deathClipGraph, clip);
		playable.SetApplyFootIK(false);
		playable.SetDuration(clip.length);
		playable.SetTime(0f);
		playable.SetSpeed(1f);

		var output = AnimationPlayableOutput.Create(deathClipGraph, "DeathClip", deathClipAnimator);
		output.SetSourcePlayable(playable);
		deathClipGraph.Play();
		return deathClipGraph.IsValid();
	}

	void DestroyDeathClipGraph()
	{
		if (deathClipGraph.IsValid())
			deathClipGraph.Destroy();
	}

	void ApplyDeathClipSprite()
	{
		if (targetImage == null || deathClipSpriteProbe == null || deathClipSpriteProbe.sprite == null)
			return;
		targetImage.sprite = deathClipSpriteProbe.sprite;
	}

	static Sprite FirstSprite(Sprite[] sprites)
	{
		if (sprites == null) return null;
		for (int i = 0; i < sprites.Length; i++)
			if (sprites[i] != null) return sprites[i];
		return null;
	}

	static Sprite LastSprite(Sprite[] sprites)
	{
		if (sprites == null) return null;
		for (int i = sprites.Length - 1; i >= 0; i--)
			if (sprites[i] != null) return sprites[i];
		return null;
	}

	void ApplyAttackSpriteCanvasMode()
	{
		ReleaseGeneratedFullTextureAttackSprites();
		if (!attackUseFullTextureFrames || sourceAttackSprites == null)
		{
			attackSprites = sourceAttackSprites;
			return;
		}

		attackSprites = BuildFullTextureSprites(sourceAttackSprites, out generatedFullTextureAttackSprites);
	}

	static Sprite[] BuildFullTextureSprites(Sprite[] source, out Sprite[] generated)
	{
		generated = null;
		if (source == null)
			return null;

		var result = new Sprite[source.Length];
		var created = new System.Collections.Generic.List<Sprite>();
		for (int i = 0; i < source.Length; i++)
		{
			var sprite = source[i];
			if (sprite == null || sprite.texture == null)
			{
				result[i] = sprite;
				continue;
			}

			if (IsFullTextureSprite(sprite))
			{
				result[i] = sprite;
				continue;
			}

			var texture = sprite.texture;
			var full = Sprite.Create(
				texture,
				new Rect(0f, 0f, texture.width, texture.height),
				new Vector2(0.5f, 0.5f),
				sprite.pixelsPerUnit,
				0u,
				SpriteMeshType.FullRect);
			full.name = $"{sprite.name}_FullTexture";
			result[i] = full;
			created.Add(full);
		}

		generated = created.Count > 0 ? created.ToArray() : null;
		return result;
	}

	static bool IsFullTextureSprite(Sprite sprite)
	{
		if (sprite == null || sprite.texture == null)
			return false;

		var rect = sprite.rect;
		return Mathf.Approximately(rect.x, 0f)
			&& Mathf.Approximately(rect.y, 0f)
			&& Mathf.Approximately(rect.width, sprite.texture.width)
			&& Mathf.Approximately(rect.height, sprite.texture.height);
	}

	void ReleaseGeneratedFullTextureAttackSprites()
	{
		if (generatedFullTextureAttackSprites == null)
			return;

		if (targetImage != null && System.Array.IndexOf(generatedFullTextureAttackSprites, targetImage.sprite) >= 0)
			targetImage.sprite = null;

		for (int i = 0; i < generatedFullTextureAttackSprites.Length; i++)
		{
			if (generatedFullTextureAttackSprites[i] == null)
				continue;
			if (Application.isPlaying)
				Destroy(generatedFullTextureAttackSprites[i]);
			else
				DestroyImmediate(generatedFullTextureAttackSprites[i]);
		}
		generatedFullTextureAttackSprites = null;
	}

	static string NormalizeSpriteKind(string spriteKind)
	{
		return spriteKind.Trim().Replace("_", "").Replace("-", "").ToLowerInvariant();
	}

	static bool Succeed(string value, out string message)
	{
		message = value;
		return true;
	}

	static bool Fail(string value, out string message)
	{
		message = value;
		return false;
	}

	static float DeathFrameRateMultiplier(float value)
	{
		return value > 0f ? Mathf.Max(0.01f, value) : 1f;
	}

	static int EstimateClipFrameCount(AnimationClip clip)
	{
		if (clip == null)
			return 0;
		return Mathf.Max(1, Mathf.RoundToInt(clip.length * Mathf.Max(1f, clip.frameRate)) + 1);
	}

	void ApplyDeathFall(Vector2 startMin, Vector2 startMax, Vector2 targetMin, Vector2 targetMax, float t)
	{
		if (targetImage == null)
			return;

		t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
		var rt = targetImage.rectTransform;
		rt.anchorMin = Vector2.LerpUnclamped(startMin, targetMin, t);
		rt.anchorMax = Vector2.LerpUnclamped(startMax, targetMax, t);
		rt.offsetMin = Vector2.zero;
		rt.offsetMax = Vector2.zero;
	}
}

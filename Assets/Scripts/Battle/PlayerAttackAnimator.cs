using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 플레이어 공격 시 PlayerBody 스프라이트 시퀀스와 무기 발사 연출을 재생한다.
/// </summary>
public class PlayerAttackAnimator : MonoBehaviour
{
	[SerializeField] Image playerBody;
	[SerializeField] Image weaponProjectile;
	[SerializeField] Sprite[] attackSprites;
	[SerializeField] PlayerBodyAnimator bodyAnimator;
	[SerializeField] float frameRate = 45f;
	[SerializeField] int frameStep = 2;
	[SerializeField] float attackBodyScaleMultiplier = 0.75f;
	[SerializeField] float weaponVisibleRatio = 0.50f;
	[SerializeField] float weaponLaunchRatio = 0.60f;
	[SerializeField] float projectileEndRatio = 0.84f;
	[SerializeField, Range(0f, 1f)] float impactNormalizedTime = 0.80f;
	[SerializeField] float projectileArcHeight = 120f;
	[SerializeField] Vector2 handAttachStartOffset = new Vector2(70f, 155f);
	[SerializeField] Vector2 handAttachEndOffset = new Vector2(120f, 170f);
	[SerializeField] float[] handAttachNormalizedTimes =
	{
		0.507f,
		0.534f,
		0.562f,
		0.589f
	};
	[SerializeField] Vector2[] handAttachOffsets =
	{
		new Vector2(50f, 94f),
		new Vector2(20f, 84f),
		new Vector2(-32f, 110f),
		new Vector2(-28f, 132f)
	};
	[SerializeField] Vector2 projectileTargetOffset = new Vector2(0f, 95f);

	Coroutine currentAnim;
	RectTransform currentTarget;
	System.Action currentImpact;
	Vector3 originalBodyScale;
	bool hasOriginalBodyScale;
	bool attackBodyScaleApplied;

	public Coroutine Play(RectTransform target)
	{
		return Play(target, null);
	}

	public Coroutine Play(RectTransform target, System.Action onImpact)
	{
		if (attackSprites == null || attackSprites.Length == 0 || playerBody == null)
		{
			CleanupAnimationState();
			onImpact?.Invoke();
			return null;
		}

		if (currentAnim != null)
		{
			StopCoroutine(currentAnim);
			currentAnim = null;
		}
		CleanupAnimationState();

		currentTarget = target;
		currentImpact = onImpact;
		currentAnim = StartCoroutine(PlaySequence());
		return currentAnim;
	}

	void OnDisable()
	{
		if (currentAnim != null)
		{
			StopCoroutine(currentAnim);
			currentAnim = null;
		}
		currentTarget = null;
		currentImpact = null;
		CleanupAnimationState();
	}

	IEnumerator PlaySequence()
	{
		if (bodyAnimator != null)
			bodyAnimator.PauseAuto();

		ApplyAttackBodyScale();

		RectTransform projectileRt = weaponProjectile != null ? weaponProjectile.rectTransform : null;
		bool canLaunchWeapon = projectileRt != null && currentTarget != null;
		HideWeapon();

		try
		{
			float frameDuration = 1f / Mathf.Max(1f, frameRate);
			int step = Mathf.Max(1, frameStep);
			int frameCount = Mathf.CeilToInt((float)attackSprites.Length / step);
			float duration = Mathf.Max(frameDuration, frameCount * frameDuration);
			float impactT = canLaunchWeapon
				? ResolveEffectiveImpactNormalizedTime(impactNormalizedTime,
					weaponVisibleRatio,
					weaponLaunchRatio,
					projectileEndRatio,
					handAttachNormalizedTimes,
					handAttachOffsets)
				: Mathf.Clamp01(impactNormalizedTime);
			float impactTime = impactT * duration;

			float elapsed = 0f;
			int lastFrame = -1;
			bool impactTriggered = false;
			while (elapsed < duration)
			{
				int sequenceFrame = Mathf.Clamp(Mathf.FloorToInt(elapsed / frameDuration), 0, frameCount - 1);
				int spriteIndex = Mathf.Clamp(sequenceFrame * step, 0, attackSprites.Length - 1);
				if (spriteIndex != lastFrame && attackSprites[spriteIndex] != null)
				{
					SetPlayerSprite(attackSprites[spriteIndex]);
					lastFrame = spriteIndex;
				}

				if (canLaunchWeapon)
					UpdateWeapon(projectileRt, elapsed / duration);

				if (!impactTriggered && elapsed >= impactTime)
				{
					impactTriggered = true;
					currentImpact?.Invoke();
				}

				elapsed += Time.deltaTime;
				yield return null;
			}

			if (!impactTriggered)
				currentImpact?.Invoke();
		}
		finally
		{
			CleanupAnimationState();
			currentTarget = null;
			currentImpact = null;
			currentAnim = null;
		}
	}

	void UpdateWeapon(RectTransform projectileRt, float sequenceT)
	{
		if (currentTarget == null)
		{
			HideWeapon();
			return;
		}

		float visibleT = ResolveEffectiveVisibleRatio(weaponVisibleRatio, handAttachNormalizedTimes, handAttachOffsets);
		float launchT = ResolveEffectiveLaunchRatio(visibleT, weaponLaunchRatio, handAttachNormalizedTimes, handAttachOffsets);
		float endT = ResolveEffectiveEndRatio(launchT, projectileEndRatio);
		if (sequenceT < visibleT || sequenceT > endT)
		{
			HideWeapon();
			return;
		}

		if (!weaponProjectile.gameObject.activeSelf)
			weaponProjectile.gameObject.SetActive(true);

		if (sequenceT < launchT)
		{
			float attachT = Mathf.InverseLerp(visibleT, launchT, sequenceT);
			float easedAttach = Smooth01(attachT);
			Vector2 attachOffset = ResolveHandAttachOffset(sequenceT,
				easedAttach,
				handAttachNormalizedTimes,
				handAttachOffsets,
				handAttachStartOffset,
				handAttachEndOffset);
			projectileRt.position = HandAttachPosition(attachOffset);
			projectileRt.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(-18f, 8f, easedAttach));
			return;
		}

		float t = Mathf.InverseLerp(launchT, endT, sequenceT);
		float eased = t * t * (3f - 2f * t);
		Vector2 launchOffset = ResolveHandAttachOffset(launchT,
			1f,
			handAttachNormalizedTimes,
			handAttachOffsets,
			handAttachStartOffset,
			handAttachEndOffset);
		Vector3 start = HandAttachPosition(launchOffset);
		Vector3 fallbackEnd = currentTarget.position + (Vector3)projectileTargetOffset;
		Vector3 end = EnemyVisualBoundsResolver.ResolveWorldPoint(currentTarget, 0.5f, 0.78f, fallbackEnd);
		Vector3 pos = Vector3.Lerp(start, end, eased);
		pos.y += Mathf.Sin(t * Mathf.PI) * projectileArcHeight;
		projectileRt.position = pos;
		projectileRt.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(8f, 390f, t));
	}

	Vector3 HandAttachPosition(Vector2 offset)
	{
		var bodyRt = playerBody != null ? playerBody.rectTransform : null;
		Vector3 basePosition = bodyRt != null ? bodyRt.position : Vector3.zero;
		return basePosition + (Vector3)(offset * CurrentAttackBodyScaleMultiplier());
	}

	static Vector2 ResolveHandAttachOffset(float sequenceT,
		float fallbackAttachT,
		float[] normalizedTimes,
		Vector2[] offsets,
		Vector2 fallbackStart,
		Vector2 fallbackEnd)
	{
		if (!HasUsableHandAttachKeys(normalizedTimes, offsets))
			return Vector2.Lerp(fallbackStart, fallbackEnd, Mathf.Clamp01(fallbackAttachT));

		float t = Mathf.Clamp01(sequenceT);
		if (t <= normalizedTimes[0])
			return offsets[0];

		int last = normalizedTimes.Length - 1;
		if (t >= normalizedTimes[last])
			return offsets[last];

		for (int i = 1; i < normalizedTimes.Length; i++)
		{
			float nextT = normalizedTimes[i];
			if (t > nextT)
				continue;

			float prevT = normalizedTimes[i - 1];
			float segmentT = Mathf.InverseLerp(prevT, nextT, t);
			return Vector2.Lerp(offsets[i - 1], offsets[i], segmentT);
		}

		return offsets[last];
	}

	static float ResolveEffectiveVisibleRatio(float configuredVisible,
		float[] normalizedTimes,
		Vector2[] offsets)
	{
		float visibleT = Mathf.Clamp01(configuredVisible);
		if (HasUsableHandAttachKeys(normalizedTimes, offsets))
			visibleT = Mathf.Max(visibleT, Mathf.Clamp01(normalizedTimes[0]));
		return visibleT;
	}

	static float ResolveEffectiveLaunchRatio(float visibleT,
		float configuredLaunch,
		float[] normalizedTimes,
		Vector2[] offsets)
	{
		float minLaunchT = Mathf.Min(visibleT + 0.01f, 1f);
		float launchT = Mathf.Clamp(configuredLaunch, minLaunchT, 1f);
		if (HasUsableHandAttachKeys(normalizedTimes, offsets))
		{
			int last = normalizedTimes.Length - 1;
			launchT = Mathf.Max(launchT, Mathf.Clamp01(normalizedTimes[last]) + 0.01f);
		}
		return Mathf.Clamp(launchT, minLaunchT, 1f);
	}

	static float ResolveEffectiveEndRatio(float launchT, float configuredEnd)
	{
		float minEndT = Mathf.Min(launchT + 0.01f, 1f);
		return Mathf.Clamp(configuredEnd, minEndT, 1f);
	}

	static float ResolveEffectiveImpactNormalizedTime(float configuredImpact,
		float configuredVisible,
		float configuredLaunch,
		float configuredEnd,
		float[] normalizedTimes,
		Vector2[] offsets)
	{
		float impactT = Mathf.Clamp01(configuredImpact);
		if (!HasUsableHandAttachKeys(normalizedTimes, offsets))
			return impactT;

		float visibleT = ResolveEffectiveVisibleRatio(configuredVisible, normalizedTimes, offsets);
		float launchT = ResolveEffectiveLaunchRatio(visibleT, configuredLaunch, normalizedTimes, offsets);
		float endT = ResolveEffectiveEndRatio(launchT, configuredEnd);
		return Mathf.Clamp(Mathf.Max(impactT, endT - 0.04f), launchT, endT);
	}

	static bool HasUsableHandAttachKeys(float[] normalizedTimes, Vector2[] offsets)
	{
		if (normalizedTimes == null || offsets == null)
			return false;
		if (normalizedTimes.Length < 2 || normalizedTimes.Length != offsets.Length)
			return false;

		float previous = Mathf.Clamp01(normalizedTimes[0]);
		for (int i = 1; i < normalizedTimes.Length; i++)
		{
			float current = Mathf.Clamp01(normalizedTimes[i]);
			if (current <= previous)
				return false;
			previous = current;
		}
		return true;
	}

	static float Smooth01(float t)
	{
		t = Mathf.Clamp01(t);
		return t * t * (3f - 2f * t);
	}

	void SetPlayerSprite(Sprite sprite)
	{
		if (bodyAnimator != null)
			bodyAnimator.SetSprite(sprite);
		else if (playerBody != null && sprite != null)
			playerBody.sprite = sprite;
	}

	void CleanupAnimationState()
	{
		HideWeapon();
		RestoreAttackBodyScale();
		if (bodyAnimator != null)
			bodyAnimator.ResumeAuto();
	}

	void HideWeapon()
	{
		if (weaponProjectile != null)
			weaponProjectile.gameObject.SetActive(false);
	}

	void ApplyAttackBodyScale()
	{
		if (playerBody == null)
			return;

		var bodyRt = playerBody.rectTransform;
		if (!hasOriginalBodyScale)
		{
			originalBodyScale = bodyRt.localScale;
			hasOriginalBodyScale = true;
		}

		attackBodyScaleApplied = true;
		bodyRt.localScale = originalBodyScale * CurrentAttackBodyScaleMultiplier();
	}

	void RestoreAttackBodyScale()
	{
		if (!hasOriginalBodyScale || playerBody == null)
			return;

		var bodyRt = playerBody.rectTransform;
		bodyRt.localScale = originalBodyScale;
		hasOriginalBodyScale = false;
		attackBodyScaleApplied = false;
	}

	float CurrentAttackBodyScaleMultiplier()
	{
		return attackBodyScaleApplied ? Mathf.Max(0.01f, attackBodyScaleMultiplier) : 1f;
	}
}
